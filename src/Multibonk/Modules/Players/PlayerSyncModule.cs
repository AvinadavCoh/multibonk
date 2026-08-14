using System;
using System.Collections.Generic;
using Il2CppAssets.Scripts.Actors.Player;
using Multibonk.Net;
using UnityEngine;
using NetFacade = Multibonk.Net.Net;

namespace Multibonk.Modules.Players
{
    /// <summary>
    /// Player-position-sync: makes the local player's movement visible to every other
    /// connected player, and every other connected player's movement visible locally.
    ///
    /// REMOTE-PLAYER REPRESENTATION: a remote player is a mod-created, primitive capsule
    /// GameObject (UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Capsule)),
    /// deliberately decoupled from the game's own player/character spawn system - it is
    /// driven purely by network position/rotation, nothing else. One capsule per remote
    /// player uuid, created lazily on first update for that uuid. A real character model
    /// is a later slice (see STATE.md).
    ///
    /// OWNERSHIP MODEL (relayed by the host, peer-ish):
    ///   - Every machine reads ITS OWN local player's transform every frame (Tick) and
    ///     sends it: a CLIENT sends PLAYER_MOVE/PLAYER_ROTATE to the host; the HOST, for
    ///     its own player, sends PLAYER_MOVED/PLAYER_ROTATED (tagged with the host's own
    ///     uuid) directly to all clients via Broadcast.
    ///   - On the HOST, a PLAYER_MOVE/ROTATE arriving from a client updates that client's
    ///     remote capsule locally AND is relayed as PLAYER_MOVED/ROTATED (tagged with
    ///     that client's uuid) to every OTHER client via BroadcastExcept, so 3+ players
    ///     converge.
    ///   - On ANY machine, a PLAYER_MOVED/ROTATED for a uuid that is not the local player
    ///     creates-if-absent and updates that uuid's capsule.
    ///
    /// WIRE: reuses the legacy ids/layouts (see PacketIds.cs / Packets.cs headers) so
    /// this is compatible with tools/MultibonkTestKit out of the box.
    ///
    /// THREADING: Tick() runs from Mod.OnUpdate (main thread, see ModuleHost.TickAll).
    /// Packet handlers run on the main thread via the PacketRegistry dispatch (see the
    /// Net facade's threading model). All GameObject creation/mutation below therefore
    /// happens on the main thread only.
    /// </summary>
    public sealed class PlayerSyncModule : IModule
    {
        // How often Tick() is allowed to actually send, regardless of movement - caps
        // the channel to roughly 18Hz instead of flooding a send per frame.
        private const float SendIntervalSeconds = 1f / 18f;

        // Only send a move update once the local player has actually moved more than
        // this many units since the last send (squared, to avoid a sqrt per frame).
        private const float MoveThresholdSqr = 0.05f * 0.05f;

        // Only send a rotate update once the local player has turned more than this many
        // degrees since the last send.
        private const float RotateThresholdDegrees = 2f;

        private readonly Dictionary<ushort, GameObject> _remotePlayers = new Dictionary<ushort, GameObject>();

        private bool _subscribedToNet;
        private bool _hasSentOnce;
        private float _nextSendAllowedTime;
        private Vector3 _lastSentPosition;
        private Quaternion _lastSentRotation;

        public void Install(PacketRegistry registry)
        {
            // See the WIRE ID COLLISION NOTE in PacketIds.cs - each id below is shared
            // with an unrelated packet sent by the OTHER role, so every handler branches
            // on NetFacade.IsHost at dispatch time and no-ops for the role that doesn't
            // apply (matching the exact pattern SessionModule already establishes).
            registry.Register((byte)ClientSentPacketId.PLAYER_MOVE_PACKET, (reader, from) =>
            {
                if (NetFacade.IsHost) HandlePlayerMove(reader, from);
            });

            registry.Register((byte)ClientSentPacketId.PLAYER_ROTATE_PACKET, (reader, from) =>
            {
                if (NetFacade.IsHost) HandlePlayerRotate(reader, from);
            });

            registry.Register((byte)ServerSentPacketId.PLAYER_MOVED_PACKET, (reader, from) =>
            {
                if (!NetFacade.IsHost) HandlePlayerMoved(reader, from);
            });

            registry.Register((byte)ServerSentPacketId.PLAYER_ROTATED_PACKET, (reader, from) =>
            {
                if (!NetFacade.IsHost) HandlePlayerRotated(reader, from);
            });

            if (!_subscribedToNet)
            {
                _subscribedToNet = true;
                // Host-side only in practice: when a client's connection drops, remove
                // its remote capsule immediately rather than leaving a stale body behind.
                // (Client-side losing the host connection ends the whole session instead
                // - handled by OnSessionEnd below.)
                NetFacade.OnClientDisconnected += HandleClientDisconnected;
            }

            Log.Info("[PlayerSync] Module installed - move/rotate packet handlers registered.");
        }

        public void OnSessionStart(bool isHost)
        {
            ClearAllRemotes();
            ResetSendThrottle();
            Log.Info($"[PlayerSync] Session started (isHost={isHost}). Remote-player state reset.");
        }

        public void OnSessionEnd()
        {
            ClearAllRemotes();
            ResetSendThrottle();
            Log.Info("[PlayerSync] Session ended - destroyed all remote-player capsules.");
        }

        public void Tick()
        {
            try
            {
                if (!NetFacade.InSession) return;

                var player = MyPlayer.Instance;
                if (player == null) return; // not in a run yet - skip silently, per spec.

                Transform t = player.transform;
                if (t == null) return;

                float now = Time.time;
                if (now < _nextSendAllowedTime) return;

                Vector3 pos = t.position;
                Quaternion rot = t.rotation;

                bool moved = !_hasSentOnce || (pos - _lastSentPosition).sqrMagnitude > MoveThresholdSqr;
                bool rotated = !_hasSentOnce || Quaternion.Angle(rot, _lastSentRotation) > RotateThresholdDegrees;

                if (!moved && !rotated) return;

                _nextSendAllowedTime = now + SendIntervalSeconds;
                _hasSentOnce = true;

                ushort localUuid = ModuleHost.Session.LocalUuid;

                if (moved)
                {
                    _lastSentPosition = pos;
                    if (NetFacade.IsHost) NetFacade.Broadcast(new PlayerMovedPacket(localUuid, pos));
                    else NetFacade.SendToHost(new PlayerMovePacket(pos));
                }

                if (rotated)
                {
                    _lastSentRotation = rot;
                    Vector3 euler = rot.eulerAngles;
                    if (NetFacade.IsHost) NetFacade.Broadcast(new PlayerRotatedPacket(localUuid, euler));
                    else NetFacade.SendToHost(new PlayerRotatePacket(euler));
                }
            }
            catch (Exception e)
            {
                Log.Error($"[PlayerSync] Tick threw: {e}");
            }
        }

        // ---------------- host-side handlers (receiving from a client) ----------------

        private void HandlePlayerMove(NetReader reader, Connection from)
        {
            float x = reader.ReadFloat(), y = reader.ReadFloat(), z = reader.ReadFloat();
            var pos = new Vector3(x, y, z);

            ushort uuid = from.PlayerUuid;
            if (uuid == 0)
            {
                Log.Warn($"[PlayerSync] PLAYER_MOVE_PACKET from connId={from.Id} before it has a player uuid (JOIN_LOBBY_PACKET not processed yet?) - ignoring.");
                return;
            }

            UpdateRemotePosition(uuid, pos);
            NetFacade.BroadcastExcept(from, new PlayerMovedPacket(uuid, pos));
        }

        private void HandlePlayerRotate(NetReader reader, Connection from)
        {
            float ex = reader.ReadFloat(), ey = reader.ReadFloat(), ez = reader.ReadFloat();
            var euler = new Vector3(ex, ey, ez);

            ushort uuid = from.PlayerUuid;
            if (uuid == 0)
            {
                Log.Warn($"[PlayerSync] PLAYER_ROTATE_PACKET from connId={from.Id} before it has a player uuid - ignoring.");
                return;
            }

            UpdateRemoteRotation(uuid, euler);
            NetFacade.BroadcastExcept(from, new PlayerRotatedPacket(uuid, euler));
        }

        private void HandleClientDisconnected(Connection conn)
        {
            if (conn.PlayerUuid == 0) return;
            RemoveRemote(conn.PlayerUuid);
        }

        // ---------------- client-side (and host, for 3+ players) handlers ----------------

        private void HandlePlayerMoved(NetReader reader, Connection from)
        {
            ushort uuid = reader.ReadUShort();
            float x = reader.ReadFloat(), y = reader.ReadFloat(), z = reader.ReadFloat();

            if (uuid == 0 || uuid == ModuleHost.Session.LocalUuid) return; // unassigned, or it's us - never draw a capsule for ourselves.
            UpdateRemotePosition(uuid, new Vector3(x, y, z));
        }

        private void HandlePlayerRotated(NetReader reader, Connection from)
        {
            ushort uuid = reader.ReadUShort();
            float ex = reader.ReadByte() / 255f * 360f;
            float ey = reader.ReadByte() / 255f * 360f;
            float ez = reader.ReadByte() / 255f * 360f;

            if (uuid == 0 || uuid == ModuleHost.Session.LocalUuid) return;
            UpdateRemoteRotation(uuid, new Vector3(ex, ey, ez));
        }

        // ---------------- remote-capsule management ----------------

        private GameObject GetOrCreateRemote(ushort uuid)
        {
            if (_remotePlayers.TryGetValue(uuid, out var existing) && existing != null)
                return existing;

            GameObject go;
            try
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = $"Multibonk_RemotePlayer_{uuid}";

                // Purely a network-position marker, deliberately decoupled from the
                // game's own player/physics systems - strip the auto-added collider so
                // it can never nudge real players, enemies, or projectiles.
                var collider = go.GetComponent<CapsuleCollider>();
                if (collider != null) UnityEngine.Object.Destroy(collider);
            }
            catch (Exception e)
            {
                Log.Error($"[PlayerSync] Failed to create remote-player capsule for uuid={uuid}: {e}");
                return null;
            }

            _remotePlayers[uuid] = go;
            Log.Info($"[PlayerSync] Created remote-player capsule for uuid={uuid}.");
            return go;
        }

        private void UpdateRemotePosition(ushort uuid, Vector3 position)
        {
            try
            {
                var go = GetOrCreateRemote(uuid);
                if (go != null) go.transform.position = position;
            }
            catch (Exception e)
            {
                Log.Error($"[PlayerSync] UpdateRemotePosition(uuid={uuid}) threw: {e}");
            }
        }

        private void UpdateRemoteRotation(ushort uuid, Vector3 eulerAngles)
        {
            try
            {
                var go = GetOrCreateRemote(uuid);
                if (go != null) go.transform.eulerAngles = eulerAngles;
            }
            catch (Exception e)
            {
                Log.Error($"[PlayerSync] UpdateRemoteRotation(uuid={uuid}) threw: {e}");
            }
        }

        private void RemoveRemote(ushort uuid)
        {
            if (!_remotePlayers.TryGetValue(uuid, out var go)) return;
            _remotePlayers.Remove(uuid);

            try { if (go != null) UnityEngine.Object.Destroy(go); }
            catch (Exception e) { Log.Error($"[PlayerSync] Destroying remote-player capsule for uuid={uuid} threw: {e}"); }

            Log.Info($"[PlayerSync] Removed remote-player capsule for uuid={uuid} (disconnected).");
        }

        private void ClearAllRemotes()
        {
            foreach (var kvp in _remotePlayers)
            {
                try { if (kvp.Value != null) UnityEngine.Object.Destroy(kvp.Value); }
                catch (Exception e) { Log.Error($"[PlayerSync] Destroying remote-player capsule for uuid={kvp.Key} threw: {e}"); }
            }
            _remotePlayers.Clear();
        }

        private void ResetSendThrottle()
        {
            _hasSentOnce = false;
            _nextSendAllowedTime = 0f;
        }
    }
}
