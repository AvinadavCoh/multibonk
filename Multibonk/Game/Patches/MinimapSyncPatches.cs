using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using Multibonk.Networking.Lobby;
using UnityEngine;

namespace Multibonk.Game.Patches
{
    /// <summary>
    /// Synchronizes map/fog-of-war reveals between players — bidirectionally.
    ///
    /// Megabonk's fog of war lives on Il2Cpp.FullMap: the local player's position is fed
    /// into QueueRevealFog(Vector3 worldPos) from FixedUpdate, and RevealFog() then clears
    /// pixels of the fog texture around it (used by both the full map and the minimap).
    ///
    /// Sync model:
    ///   Host → clients: QueueRevealFogPatch fires on the host, throttles to ~2 units of
    ///     movement (MIN_BROADCAST_DISTANCE_SQR), packs world X/Z into TileX/TileY and
    ///     fires MapTileRevealedEvent → MapRevealEventHandler → SendMapRevealPacket.
    ///
    ///   Client → host: QueueRevealFogPatch fires on the client with the same throttle,
    ///     fires MapTileRevealedEvent → MapRevealEventHandler → SendClientMapRevealPacket.
    ///     The host handler (MapRevealServerPacketHandler) applies the reveal locally and
    ///     relays it to the other clients.
    ///
    /// Loop suppression:
    ///   ApplyingNetworkReveal is set inside RevealAt() before calling QueueRevealFog and
    ///   cleared in a finally block.  The patch checks this flag first and exits without
    ///   triggering any event, so a reveal applied from the network is never re-sent.
    ///   This applies on both sides — the host applying a client's reveal and a client
    ///   applying a relayed reveal both go through RevealAt(), so neither fires an event.
    /// </summary>
    public static class MinimapSyncPatches
    {
        // Host: last world position we broadcast to clients.
        private static Vector3 lastBroadcastPos = new Vector3(float.MinValue, 0f, float.MinValue);
        // Client: last world position we sent to the host.
        private static Vector3 lastClientBroadcastPos = new Vector3(float.MinValue, 0f, float.MinValue);

        // Re-broadcast after ~2 units of movement (same threshold for both directions).
        private const float MIN_BROADCAST_DISTANCE_SQR = 4f;

        // Cached FullMap instance (refreshed after scene changes)
        private static FullMap cachedFullMap;

        /// <summary>
        /// True while a fog reveal received from the network is being applied locally.
        /// Prevents QueueRevealFogPatch from re-firing the event and sending the reveal
        /// back onto the wire in either direction.
        /// </summary>
        public static bool ApplyingNetworkReveal = false;

        /// <summary>
        /// Host: broadcast fog reveals to clients.
        /// Client: send fog reveals to the host (same throttle, same event).
        /// </summary>
        [HarmonyPatch(typeof(FullMap), nameof(FullMap.QueueRevealFog))]
        class QueueRevealFogPatch
        {
            static void Postfix(FullMap __instance, Vector3 worldPos)
            {
                if (!LobbyPatchFlags.InMultiplayer)
                    return;

                // A reveal applied from the network must never be re-sent.
                if (ApplyingNetworkReveal)
                    return;

                try
                {
                    // Cache the instance while we're here (also refreshes after scene reloads)
                    cachedFullMap = __instance;

                    if (LobbyPatchFlags.IsHosting)
                    {
                        // Host path: throttle and broadcast to all clients.
                        if ((worldPos - lastBroadcastPos).sqrMagnitude < MIN_BROADCAST_DISTANCE_SQR)
                            return;

                        lastBroadcastPos = worldPos;

                        // TileX = world X, TileY = world Z
                        GameEvents.TriggerMapTileRevealed(
                            Mathf.RoundToInt(worldPos.x),
                            Mathf.RoundToInt(worldPos.z));
                    }
                    else
                    {
                        // Client path: same throttle, sends to host via MapRevealEventHandler.
                        if ((worldPos - lastClientBroadcastPos).sqrMagnitude < MIN_BROADCAST_DISTANCE_SQR)
                            return;

                        lastClientBroadcastPos = worldPos;

                        GameEvents.TriggerMapTileRevealed(
                            Mathf.RoundToInt(worldPos.x),
                            Mathf.RoundToInt(worldPos.z));
                    }
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"Error in QueueRevealFogPatch: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Apply a fog reveal received from the network.
        /// Sets ApplyingNetworkReveal so the patch does not re-fire the event.
        /// Must be called on the main Unity thread.
        /// </summary>
        public static void RevealAt(int worldX, int worldZ)
        {
            try
            {
                var map = GetFullMap();
                if (map == null)
                {
                    DebugLogger.Warning($"[MinimapSync] FullMap not found — cannot reveal ({worldX}, {worldZ})");
                    return;
                }

                ApplyingNetworkReveal = true;
                try
                {
                    map.QueueRevealFog(new Vector3(worldX, 0f, worldZ));
                }
                finally
                {
                    ApplyingNetworkReveal = false;
                }
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
                // Il2Cpp UnityEngine.Object == handles the 'destroyed' case,
                // so this also refreshes after scene changes.
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
            lastClientBroadcastPos = new Vector3(float.MinValue, 0f, float.MinValue);
        }
    }
}
