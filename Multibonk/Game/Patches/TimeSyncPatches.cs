using HarmonyLib;
using Il2CppAssets.Scripts.Utility;
using MelonLoader;
using Multibonk.Networking.Lobby;

namespace Multibonk.Game.Patches
{
    /// <summary>
    /// Keeps everyone on the host's clock.
    ///
    /// The game clock lives in the static class Il2CppAssets.Scripts.Utility.MyTime
    /// (stageTimer / runTimer / paused, advanced by MyTime.Update).
    ///
    /// Host: every SYNC_INTERVAL seconds a TimeSyncPacket broadcasts the timers.
    /// Client: applies them when drift exceeds DRIFT_TOLERANCE (see TimeSyncPacketHandler).
    /// </summary>
    public static class TimeSyncPatches
    {
        private const float SYNC_INTERVAL = 2f;
        public const float DRIFT_TOLERANCE = 0.3f;

        private static float lastSyncSent = float.MinValue;

        /// <summary>
        /// Client: apply the host's timers. Must run on the main Unity thread.
        /// </summary>
        public static void ApplyHostTime(float stageTime, float runTime, bool hostPaused)
        {
            try
            {
                // Don't fight the client's own pause (their upgrade screen is open)
                if (MyTime.paused)
                    return;

                bool drifted =
                    System.Math.Abs(MyTime.stageTimer - stageTime) > DRIFT_TOLERANCE ||
                    System.Math.Abs(MyTime.runTimer - runTime) > DRIFT_TOLERANCE;

                if (!drifted)
                    return;

                DebugLogger.Log($"[TimeSync] Adjusting clock: stage {MyTime.stageTimer:F2} -> {stageTime:F2}, run {MyTime.runTimer:F2} -> {runTime:F2}");
                MyTime.stageTimer = stageTime;
                MyTime.runTimer = runTime;
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[TimeSync] Failed to apply host time: {ex.Message}");
            }
        }

        // ------------------------------------------------------------------
        // Pause sync: when the host pauses (ESC / level-up screen), everyone pauses;
        // when the host unpauses, everyone resumes.
        // A client's own local pause is left alone - we only auto-unpause a pause
        // that the network initiated.
        // ------------------------------------------------------------------

        /// <summary>True while the local pause was initiated by a network packet.</summary>
        private static bool networkPauseActive = false;

        /// <summary>Client: apply the host's pause. Must run on the main Unity thread.</summary>
        public static void ApplyNetworkPause()
        {
            try
            {
                if (!MyTime.paused)
                {
                    MyTime.Pause();
                    networkPauseActive = true;
                    DebugLogger.Log("[TimeSync] Paused by host");
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[TimeSync] Failed to apply pause: {ex.Message}");
            }
        }

        /// <summary>Client: apply the host's unpause (only if we paused for the network).</summary>
        public static void ApplyNetworkUnpause()
        {
            try
            {
                if (networkPauseActive && MyTime.paused)
                {
                    MyTime.Unpause();
                    DebugLogger.Log("[TimeSync] Unpaused by host");
                }
                networkPauseActive = false;
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[TimeSync] Failed to apply unpause: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(MyTime), nameof(MyTime.Pause))]
        class MyTimePausePatch
        {
            static void Postfix()
            {
                // During level-up coordination the coordinator owns pause signalling.
                // Suppress the generic broadcast so an intermediate re-pause inside
                // CloseLevelupScreenPatch does not send a spurious PAUSE_GAME to clients.
                if (LobbyPatchFlags.InMultiplayer && LobbyPatchFlags.IsHosting
                    && !LevelUpCoordinator.IsActive)
                    GameEvents.TriggerInGamePause();
            }
        }

        [HarmonyPatch(typeof(MyTime), nameof(MyTime.Unpause))]
        class MyTimeUnpausePatch
        {
            static void Postfix()
            {
                // During level-up coordination the coordinator owns pause signalling.
                // Suppress the generic broadcast so CloseLevelupScreen's internal
                // MyTime.Unpause does not prematurely unpause clients via UNPAUSE_GAME
                // before all players have finished picking.
                // When the coordinator calls MyTime.Unpause() in DoResume(), IsActive is
                // already false, so the final UNPAUSE_GAME broadcast IS sent (intentional
                // belt-and-suspenders alongside the LEVELUP_RESUME packet).
                if (LobbyPatchFlags.InMultiplayer && LobbyPatchFlags.IsHosting
                    && !LevelUpCoordinator.IsActive)
                    GameEvents.TriggerInGameUnpause();
            }
        }

        /// <summary>
        /// Host: broadcast the clock periodically (MyTime.Update runs every frame).
        /// </summary>
        [HarmonyPatch(typeof(MyTime), nameof(MyTime.Update))]
        class MyTimeUpdatePatch
        {
            static void Postfix()
            {
                if (!LobbyPatchFlags.InMultiplayer || !LobbyPatchFlags.IsHosting)
                    return;

                try
                {
                    float now = UnityEngine.Time.unscaledTime;
                    if (now - lastSyncSent < SYNC_INTERVAL)
                        return;

                    lastSyncSent = now;
                    GameEvents.TriggerTimeSync(MyTime.stageTimer, MyTime.runTimer, MyTime.paused);
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"[TimeSync] Failed to broadcast time: {ex.Message}");
                }
            }
        }
    }
}
