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
    /// - SpawnPickup(EPickup ePickup, Vector3 pos, int value, bool useRandomOffsetPosition, float pickupDelay)
    ///   creates/stacks a pooled Il2Cpp.Pickup (called e.g. from OnEnemyDied).
    /// - DespawnPickup(Pickup pickup) removes it (consumption or expiry).
    ///
    /// Sync model (host-authoritative):
    /// - Host: SpawnPickup postfix broadcasts the drop (id, type, final position, value);
    ///   DespawnPickup postfix broadcasts the removal.
    /// - Client: local RNG drops are blocked; pickups are spawned/despawned from host packets.
    ///   The actual XP/gold award is still handled by the existing XP/gold sync handlers.
    /// </summary>
    public static class ItemDropPatches
    {
        /// <summary>
        /// When true, allows a client to spawn a pickup from a network packet,
        /// bypassing the normal client spawn block.
        /// </summary>
        public static bool AllowNetworkSpawn = false;

        // Host: pickup ids we've broadcast and not yet despawned (prevents duplicate removals)
        private static readonly HashSet<int> liveBroadcastIds = new HashSet<int>();

        // Client: host pickup id -> local Pickup instance
        private static readonly Dictionary<string, Pickup> networkPickups = new Dictionary<string, Pickup>();

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
                    liveBroadcastIds.Add(id);

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
                if (!LobbyPatchFlags.InMultiplayer || !LobbyPatchFlags.IsHosting || pickup == null)
                    return;

                try
                {
                    int id = pickup.GetHashCode();
                    if (!liveBroadcastIds.Remove(id))
                        return; // never broadcast (or already removed)

                    GameEvents.TriggerItemPickedUp(id.ToString(), 0);
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"Error broadcasting pickup despawn: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Client: remember which local pickup corresponds to a host pickup id.
        /// </summary>
        public static void RegisterNetworkPickup(string hostId, Pickup pickup)
        {
            if (pickup != null)
                networkPickups[hostId] = pickup;
        }

        /// <summary>
        /// Client: resolve and forget a host pickup id.
        /// </summary>
        public static bool TryTakeNetworkPickup(string hostId, out Pickup pickup)
        {
            if (networkPickups.TryGetValue(hostId, out pickup))
            {
                networkPickups.Remove(hostId);
                return pickup != null;
            }
            return false;
        }

        /// <summary>
        /// Clears all tracked pickups (call on scene change/restart).
        /// </summary>
        public static void Clear()
        {
            liveBroadcastIds.Clear();
            networkPickups.Clear();
        }
    }
}
