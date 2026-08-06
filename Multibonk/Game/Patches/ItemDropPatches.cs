using HarmonyLib;
using Il2Cpp;
using Il2CppAssets.Scripts.Inventory__Items__Pickups.Pickups;
using MelonLoader;
using Multibonk.Networking.Lobby;
using UnityEngine;

namespace Multibonk.Game.Patches
{
    /// <summary>
    /// Synchronizes world pickups (XP orbs, gold, powerups) between players.
    ///
    /// Megabonk spawns all world drops through Il2Cpp.PickupManager:
    /// - SpawnPickup(EPickup, Vector3, int, bool, float) creates/stacks a pooled Pickup.
    /// - DespawnPickup(Pickup) removes it (consumption or expiry).
    ///
    /// Sync model (host-authoritative):
    ///   Host → clients:
    ///     SpawnPickup postfix broadcasts the drop (id, type, final position, value).
    ///     DespawnPickup postfix broadcasts the removal.
    ///   Client → host:
    ///     When the local player consumes a network pickup DespawnPickupPatch fires on the
    ///     client, looks up the host id via the reverse map and fires
    ///     GameEvents.TriggerClientPickupConsumed → ItemPickedUpEventHandler sends
    ///     PICKUP_CONSUMED_PACKET → PickupConsumedServerPacketHandler despawns the host copy
    ///     and relays ITEM_PICKED_UP to the other clients.
    ///   Local RNG drops are blocked on clients so the host is the sole spawn authority.
    ///   XP/gold values are handled by the separate XP/gold sync handlers — untouched here.
    ///
    /// Loop suppression (pickup consumed):
    ///   TryTakeHostPickup removes the entry from livePickupObjects *before* the server
    ///   handler calls DespawnPickup.  DespawnPickupPatch.Postfix (host side) checks that
    ///   dict first; finding the id already absent it returns without broadcasting, so
    ///   ItemPickedUpEventHandler is never invoked and no echo packet is sent.
    /// </summary>
    public static class ItemDropPatches
    {
        /// <summary>
        /// When true, allows a client to spawn a pickup from a network packet,
        /// bypassing the normal client spawn block.
        /// </summary>
        public static bool AllowNetworkSpawn = false;

        // Host: live pickups by hash-code id (spawn path stores the object so the server
        // handler can retrieve it for DespawnPickup without scanning the scene).
        private static readonly Dictionary<int, Pickup> livePickupObjects = new Dictionary<int, Pickup>();

        // Client: host pickup id → local Pickup instance (forward map)
        private static readonly Dictionary<string, Pickup> networkPickups = new Dictionary<string, Pickup>();

        // Client: local Pickup hash-code → host pickup id (reverse map, for DespawnPickupPatch)
        private static readonly Dictionary<int, string> pickupHashToHostId = new Dictionary<int, string>();

        [HarmonyPatch(typeof(PickupManager), nameof(PickupManager.SpawnPickup))]
        class SpawnPickupPatch
        {
            static bool Prefix()
            {
                if (AllowNetworkSpawn)
                    return true; // network-initiated spawn on a client

                if (LobbyPatchFlags.InMultiplayer && !LobbyPatchFlags.IsHosting)
                {
                    DebugLogger.Log("[Client] Blocked local pickup spawn (waiting for host packet)");
                    return false; // host decides all drops
                }

                return true;
            }

            static void Postfix(Pickup __result, EPickup ePickup, int value)
            {
                if (!LobbyPatchFlags.InMultiplayer || !LobbyPatchFlags.IsHosting || __result == null)
                    return;

                try
                {
                    int id = __result.GetHashCode();
                    livePickupObjects[id] = __result;

                    // Use the pickup's actual position (random offset is applied inside SpawnPickup)
                    Vector3 finalPos = __result.transform.position;

                    GameEvents.TriggerSpawnDrop(id.ToString(), finalPos, (int)ePickup, value);
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"Error broadcasting pickup spawn: {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(PickupManager), nameof(PickupManager.DespawnPickup))]
        class DespawnPickupPatch
        {
            static void Postfix(Pickup pickup)
            {
                if (!LobbyPatchFlags.InMultiplayer || pickup == null)
                    return;

                try
                {
                    if (LobbyPatchFlags.IsHosting)
                    {
                        // Host path: broadcast removal to all clients.
                        // TryTakeHostPickup (called by PickupConsumedServerPacketHandler) removes
                        // the entry before calling DespawnPickup, so this Remove returns false for
                        // client-initiated despawns and exits silently — no echo.
                        int id = pickup.GetHashCode();
                        if (!livePickupObjects.Remove(id))
                            return; // never broadcast, or already removed by TryTakeHostPickup

                        GameEvents.TriggerItemPickedUp(id.ToString(), 0);
                    }
                    else
                    {
                        // Client path: if this pickup was spawned by a host packet, notify the
                        // host that we consumed it.
                        // ItemPickedUpPacketHandler cleans up both maps before calling DespawnPickup,
                        // so the reverse-map lookup is empty for host-initiated despawns — no echo.
                        int hash = pickup.GetHashCode();
                        if (!pickupHashToHostId.TryGetValue(hash, out var hostId))
                            return; // not a network pickup, or already cleaned up by host packet

                        pickupHashToHostId.Remove(hash);
                        networkPickups.Remove(hostId);

                        DebugLogger.Log($"[Client] Local player consumed network pickup {hostId} — notifying host");
                        GameEvents.TriggerClientPickupConsumed(hostId);
                    }
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"Error in DespawnPickupPatch: {ex.Message}");
                }
            }
        }

        // ─── Host-side helpers ───────────────────────────────────────────────────────

        /// <summary>
        /// Host: live pickup count (for the desync detector).
        /// </summary>
        public static int HostLivePickupCount => livePickupObjects.Count;

        /// <summary>
        /// Host: look up and remove a pickup by its wire id.
        /// Called by PickupConsumedServerPacketHandler before DespawnPickup; the removal
        /// is what prevents DespawnPickupPatch from re-broadcasting the despawn.
        /// </summary>
        public static bool TryTakeHostPickup(string hostId, out Pickup pickup)
        {
            pickup = null;
            if (!int.TryParse(hostId, out int id)) return false;
            if (!livePickupObjects.TryGetValue(id, out pickup)) return false;
            livePickupObjects.Remove(id);
            return pickup != null;
        }

        // ─── Client-side helpers ─────────────────────────────────────────────────────

        /// <summary>
        /// Client: live network pickup count (for the desync detector).
        /// </summary>
        public static int ClientLivePickupCount => networkPickups.Count;

        /// <summary>
        /// Client: remember which local pickup corresponds to a host pickup id.
        /// Maintains both the forward map (hostId → pickup) and the reverse map
        /// (pickup hash → hostId) used by DespawnPickupPatch to detect local consumption.
        /// </summary>
        public static void RegisterNetworkPickup(string hostId, Pickup pickup)
        {
            if (pickup == null) return;
            networkPickups[hostId] = pickup;
            pickupHashToHostId[pickup.GetHashCode()] = hostId;
        }

        /// <summary>
        /// Client: resolve and forget a host pickup id (called when the host sends an
        /// ITEM_PICKED_UP packet).  Clears both maps so DespawnPickupPatch finds nothing
        /// and does not send a redundant PICKUP_CONSUMED_PACKET to the host.
        /// </summary>
        public static bool TryTakeNetworkPickup(string hostId, out Pickup pickup)
        {
            if (networkPickups.TryGetValue(hostId, out pickup))
            {
                networkPickups.Remove(hostId);
                if (pickup != null)
                    pickupHashToHostId.Remove(pickup.GetHashCode());
                return pickup != null;
            }
            return false;
        }

        /// <summary>
        /// Clears all tracked pickups (call on scene change/restart).
        /// </summary>
        public static void Clear()
        {
            livePickupObjects.Clear();
            networkPickups.Clear();
            pickupHashToHostId.Clear();
        }
    }
}
