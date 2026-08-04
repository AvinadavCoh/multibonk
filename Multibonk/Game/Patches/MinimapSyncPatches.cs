using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using Multibonk.Networking.Lobby;
using UnityEngine;

namespace Multibonk.Game.Patches
{
    /// <summary>
    /// Synchronizes map/fog-of-war reveals between players.
    ///
    /// Megabonk's fog of war lives on Il2Cpp.FullMap: the local player's position is fed into
    /// QueueRevealFog(Vector3 worldPos) from FixedUpdate, and RevealFog() then clears pixels of
    /// the fog texture around it (used by both the full map and the minimap material).
    ///
    /// Sync model:
    /// - Host: every fog reveal queued for the host player is broadcast as a MapRevealPacket.
    ///   TileX/TileY carry the world-space X/Z coordinates (rounded to ints).
    /// - Client: receives the packet and calls FullMap.QueueRevealFog at that world position,
    ///   so areas the host explores are revealed for everyone.
    /// </summary>
    public static class MinimapSyncPatches
    {
        // Last world position we broadcast - avoids spamming packets every FixedUpdate.
        private static Vector3 lastBroadcastPos = new Vector3(float.MinValue, 0f, float.MinValue);
        private const float MIN_BROADCAST_DISTANCE_SQR = 4f; // re-broadcast after ~2 units of movement

        // Cached FullMap instance for client-side reveals
        private static FullMap cachedFullMap;

        /// <summary>
        /// Host: broadcast fog reveals to clients.
        /// </summary>
        [HarmonyPatch(typeof(FullMap), nameof(FullMap.QueueRevealFog))]
        class QueueRevealFogPatch
        {
            static void Postfix(FullMap __instance, Vector3 worldPos)
            {
                if (!LobbyPatchFlags.InMultiplayer || !LobbyPatchFlags.IsHosting)
                    return;

                try
                {
                    // Cache the instance while we're here (also useful after scene reloads)
                    cachedFullMap = __instance;

                    if ((worldPos - lastBroadcastPos).sqrMagnitude < MIN_BROADCAST_DISTANCE_SQR)
                        return;

                    lastBroadcastPos = worldPos;

                    // TileX = world X, TileY = world Z
                    GameEvents.TriggerMapTileRevealed(
                        Mathf.RoundToInt(worldPos.x),
                        Mathf.RoundToInt(worldPos.z));
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"Error in QueueRevealFogPatch: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Client: reveal fog at a world position received from the host.
        /// Used by MapRevealPacketHandler / MapRevealBulkPacketHandler.
        /// Must be called on the main Unity thread.
        /// </summary>
        public static void RevealAt(int worldX, int worldZ)
        {
            try
            {
                var map = GetFullMap();
                if (map == null)
                {
                    DebugLogger.Warning($"[Client] FullMap not found - cannot reveal ({worldX}, {worldZ})");
                    return;
                }

                map.QueueRevealFog(new Vector3(worldX, 0f, worldZ));
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Error revealing fog at ({worldX}, {worldZ}): {ex.Message}");
            }
        }

        private static FullMap GetFullMap()
        {
            try
            {
                // Interop UnityEngine.Object == handles the 'destroyed' case,
                // so this also refreshes after scene changes
                if (cachedFullMap == null)
                    cachedFullMap = UnityEngine.Object.FindObjectOfType<FullMap>();
            }
            catch
            {
                cachedFullMap = UnityEngine.Object.FindObjectOfType<FullMap>();
            }

            return cachedFullMap;
        }

        /// <summary>
        /// Clears cached state (call on scene change/restart).
        /// </summary>
        public static void Clear()
        {
            cachedFullMap = null;
            lastBroadcastPos = new Vector3(float.MinValue, 0f, float.MinValue);
        }
    }
}
