using System;
using System.Collections.Generic;
using System.Linq;
using Multibonk.Net;
using NetFacade = Multibonk.Net.Net;

namespace Multibonk.Modules.Session
{
    /// <summary>
    /// First feature module: the lobby/session handshake, wire-compatible with the
    /// legacy mod's protocol (Multibonk/Networking/Comms/...) and with
    /// MultibonkTestKit's `host` and `join` modes. This module implements BOTH roles -
    /// a given mod instance is host XOR client for any one session (see Net.IsHost /
    /// Net.InSession - StartHost/JoinHost each tear down any prior session first), so
    /// the host-side and client-side handler methods below never run concurrently
    /// against each other.
    ///
    /// WIRE ID COLLISION NOTE: in the legacy protocol, ServerSentPacketId and
    /// ClientSentPacketId are separate byte spaces (e.g. id 0 means JOIN_LOBBY_PACKET
    /// when a HOST receives it, but LOBBY_PLAYER_LIST_PACKET when a CLIENT receives it) -
    /// legacy keeps them apart with two separate dispatch dictionaries (ServerProtocol
    /// vs ClientProtocol). Multibonk.Net.PacketRegistry is a single shared id->handler
    /// table, registered once at mod load - before the role for any given session is
    /// known - so each id below is registered exactly once, with a handler that
    /// branches on NetFacade.IsHost at DISPATCH time to pick the correct
    /// interpretation. This is safe because a given side only ever receives packets
    /// sent by the *other* role.
    /// </summary>
    public sealed class SessionModule : IModule
    {
        // Matches the wire byte written by both ServerSentPacketId and ClientSentPacketId
        // at the same value - see the WIRE ID COLLISION NOTE above for why one constant
        // covers both directions.
        private const byte IdJoinLobbyOrPlayerList = 0;   // host receives: JOIN_LOBBY_PACKET | client receives: LOBBY_PLAYER_LIST_PACKET
        private const byte IdCharacterSelectOrSelected = 1; // host receives: CHARACTER_SELECTION | client receives: PLAYER_SELECTED_CHARACTER
        private const byte IdGameLoadedOrStartGame = 2;   // host receives: GAME_LOADED_PACKET | client receives: START_GAME
        private const byte IdSpawnPlayer = 6;             // host never receives this - only the host sends it.

        private const int ModVersion = 100; // matches legacy LobbyService.JoinLobby / MultibonkTestKit's join mode.
        private const string LocalCharacterPlaceholder = "Warrior"; // TODO: no character-select UI yet.

        private readonly LobbyPlayerRegistry _registry = new LobbyPlayerRegistry();
        private ushort _nextUuid = 1;
        private ushort _localUuid;
        private bool _subscribedToNet;

        public void Install(PacketRegistry registry)
        {
            registry.Register(IdJoinLobbyOrPlayerList, (reader, from) =>
            {
                if (NetFacade.IsHost) HandleJoinLobby(reader, from);
                else HandleLobbyPlayerList(reader, from);
            });

            registry.Register(IdCharacterSelectOrSelected, (reader, from) =>
            {
                if (NetFacade.IsHost) HandleCharacterSelection(reader, from);
                else HandlePlayerSelectedCharacter(reader, from);
            });

            registry.Register(IdGameLoadedOrStartGame, (reader, from) =>
            {
                if (NetFacade.IsHost) HandleGameLoaded(reader, from);
                else HandleStartGame(reader, from);
            });

            registry.Register(IdSpawnPlayer, HandleSpawnPlayer);

            if (!_subscribedToNet)
            {
                _subscribedToNet = true;
                NetFacade.OnClientConnected += HandleNetConnected;
            }

            Log.Info("[Session] Module installed - lobby/handshake packet handlers registered.");
        }

        public void OnSessionStart(bool isHost)
        {
            _registry.Clear();
            _nextUuid = 1;
            _localUuid = 0;

            if (isHost)
            {
                var self = new LobbyPlayer
                {
                    Uuid = AllocateUuid(),
                    Name = LocalPlayerName(),
                    Character = string.Empty,
                    Connection = null,
                    IsLocal = true,
                };
                _registry.Add(self);
                _localUuid = self.Uuid;
                Log.Info($"[Session] Session started as HOST. Local player uuid={_localUuid} name=\"{self.Name}\".");
            }
            else
            {
                Log.Info("[Session] Session started as CLIENT. Waiting for the connection to finish before sending JOIN_LOBBY_PACKET.");
            }
        }

        public void OnSessionEnd()
        {
            Log.Info("[Session] Session ended - clearing lobby roster.");
            _registry.Clear();
            _localUuid = 0;
            _nextUuid = 1;
        }

        // ---------------- connection lifecycle ----------------

        private void HandleNetConnected(Connection conn)
        {
            if (!NetFacade.IsHost)
            {
                // We are the client and just finished connecting to the host - kick off the handshake.
                string name = LocalPlayerName();
                NetFacade.SendToHost(new JoinLobbyPacket(ModVersion, name));
                Log.Info($"[Session] Connected to host (connId={conn.Id}). Sent JOIN_LOBBY_PACKET (version={ModVersion}, name=\"{name}\").");
            }
            else
            {
                Log.Info($"[Session] Client connected (connId={conn.Id}). Waiting for JOIN_LOBBY_PACKET...");
            }
        }

        // ---------------- host-side handlers ----------------

        private void HandleJoinLobby(NetReader reader, Connection from)
        {
            int version = reader.ReadInt();
            string name = reader.ReadString();

            ushort uuid = AllocateUuid();
            var player = new LobbyPlayer { Uuid = uuid, Name = name, Character = string.Empty, Connection = from, IsLocal = false };
            _registry.Add(player);
            Log.Info($"[Session] JOIN_LOBBY_PACKET from connId={from.Id}: assigned uuid={uuid} name=\"{name}\" modVersion={version}.");

            // Recipient always identifies itself as roster[0] - matches legacy
            // JoinLobbyPacketHandler's GetPlayers().Prepend(newPlayer) and the test
            // kit's join-mode TryCaptureOwnUuid convention.
            var roster = new List<LobbyPlayer> { player };
            roster.AddRange(_registry.All().Where(p => p.Uuid != uuid));

            NetFacade.Send(from, new LobbyPlayerListPacket(roster));
            Log.Info($"[Session] Sent LOBBY_PLAYER_LIST_PACKET to connId={from.Id}: {roster.Count} player(s), [0]=uuid{uuid} (their own identity).");
        }

        private void HandleCharacterSelection(NetReader reader, Connection from)
        {
            string character = reader.ReadString();
            var player = _registry.GetByConnection(from);
            if (player == null)
            {
                Log.Warn($"[Session] CHARACTER_SELECTION from unregistered connId={from.Id} - ignoring.");
                return;
            }

            player.Character = character;
            Log.Info($"[Session] {player.Name} (uuid={player.Uuid}) selected character \"{character}\".");

            var packet = new PlayerSelectedCharacterPacket(player.Uuid, character);
            foreach (var p in _registry.All())
                NetFacade.Send(p.Connection, packet); // null Connection (the local host entry) is a no-op, matching legacy's `player.Connection?.EnqueuePacket`.
        }

        private void HandleGameLoaded(NetReader reader, Connection from)
        {
            float px = reader.ReadFloat(), py = reader.ReadFloat(), pz = reader.ReadFloat();
            float rx = reader.ReadFloat(), ry = reader.ReadFloat(), rz = reader.ReadFloat(), rw = reader.ReadFloat();

            var player = _registry.GetByConnection(from);
            string who = player != null ? $"{player.Name} (uuid={player.Uuid})" : $"connId={from.Id}";
            Log.Info($"[Session] GAME_LOADED_PACKET from {who}: pos=({px:0.##},{py:0.##},{pz:0.##}) rot=({rx:0.##},{ry:0.##},{rz:0.##},{rw:0.##}).");

            // TODO: characterByte=0 is a placeholder - there is no player module / typed
            // ECharacter lookup yet to translate the selected character NAME into the
            // game's enum, so this can't match legacy's Enum.Parse<ECharacter>(...) exactly.
            byte characterByte = 0;
            ushort playerId = player?.Uuid ?? 0;
            NetFacade.Send(from, new SpawnPlayerPacket(characterByte, playerId, px, py, pz, rx, ry, rz, rw));
            Log.Info($"[Session] Sent SPAWN_PLAYER_PACKET to connId={from.Id} for uuid={playerId} (characterByte=0 placeholder - no player module yet).");
        }

        // ---------------- client-side handlers ----------------

        private void HandleLobbyPlayerList(NetReader reader, Connection from)
        {
            int count = reader.ReadByte();
            var roster = new List<LobbyPlayer>(count);
            for (int i = 0; i < count; i++)
            {
                ushort uuid = reader.ReadUShort();
                string name = reader.ReadString();
                string character = reader.ReadString();
                roster.Add(new LobbyPlayer { Uuid = uuid, Name = name, Character = character, IsLocal = i == 0 });
            }

            _registry.Clear();
            foreach (var p in roster) _registry.Add(p);
            _localUuid = roster.Count > 0 ? roster[0].Uuid : (ushort)0;

            Log.Info($"[Session] LOBBY_PLAYER_LIST_PACKET received: {roster.Count} player(s). My uuid={_localUuid} (roster[0], per convention).");

            NetFacade.SendToHost(new SelectCharacterPacket(LocalCharacterPlaceholder));
            Log.Info($"[Session] Sent CHARACTER_SELECTION \"{LocalCharacterPlaceholder}\".");
        }

        private void HandlePlayerSelectedCharacter(NetReader reader, Connection from)
        {
            ushort playerId = reader.ReadUShort();
            string character = reader.ReadString();
            var player = _registry.Get(playerId);
            if (player != null) player.Character = character;
            Log.Info($"[Session] PLAYER_SELECTED_CHARACTER: uuid={playerId} character=\"{character}\".");
        }

        private void HandleStartGame(NetReader reader, Connection from)
        {
            int seed = reader.ReadInt();
            Log.Info($"[Session] START_GAME received. seed={seed}. No real game world/player module yet - simulating map load and sending GAME_LOADED_PACKET immediately.");

            NetFacade.SendToHost(new GameLoadedPacket(0f, 0f, 0f, 0f, 0f, 0f, 1f));
            Log.Info("[Session] Sent GAME_LOADED_PACKET (dummy pos=(0,0,0) rot=(0,0,0,1)).");
        }

        private void HandleSpawnPlayer(NetReader reader, Connection from)
        {
            byte characterByte = reader.ReadByte();
            ushort playerId = reader.ReadUShort();
            float px = reader.ReadFloat(), py = reader.ReadFloat(), pz = reader.ReadFloat();
            float rx = reader.ReadFloat(), ry = reader.ReadFloat(), rz = reader.ReadFloat(), rw = reader.ReadFloat();
            Log.Info($"[Session] SPAWN_PLAYER_PACKET received: uuid={playerId} characterByte={characterByte} pos=({px:0.##},{py:0.##},{pz:0.##}) " +
                     "- no player module yet, not spawning a game object.");
        }

        // ---------------- dev-trigger support (see Mod.cs) ----------------

        /// <summary>Host-only. Broadcasts START_GAME with the given seed to every connected client. Logs and no-ops if not hosting.</summary>
        public void SendStartGameToAll(int seed)
        {
            if (!NetFacade.IsHost)
            {
                Log.Warn("[Session] SendStartGameToAll called while not hosting - ignoring.");
                return;
            }

            NetFacade.Broadcast(new StartGamePacket(seed));
            Log.Info($"[Session] Sent START_GAME (seed={seed}) to all connected clients.");
        }

        // ---------------- helpers ----------------

        private ushort AllocateUuid() => _nextUuid++;

        private static string LocalPlayerName()
        {
            try { return string.IsNullOrWhiteSpace(Environment.UserName) ? "Player" : Environment.UserName; }
            catch { return "Player"; }
        }
    }
}
