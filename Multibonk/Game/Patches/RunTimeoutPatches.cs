using HarmonyLib;
using Il2Cpp;
using Il2CppAssets.Scripts.Utility;
using MelonLoader;
using Multibonk.Game.Handlers;
using Multibonk.Networking.Lobby;
using UnityEngine;

namespace Multibonk.Game.Patches
{
    /// <summary>
    /// DEFECT 3 — Client-side escape hatch for host crash after all players are dead.
    ///
    /// Problem: clients block their own game-over screen (GameOverPatch) waiting for a
    /// RUN_OVER packet from the host.  If the host crashes after everyone has died that
    /// packet never arrives and the client is permanently soft-locked.
    ///
    /// Solution: when the local client player dies, StartTimeout() is called.  Every
    /// frame (via MyTime.Update) we check whether AllowGameOver was set by the arriving
    /// RUN_OVER packet.  If 15 seconds pass without it we open the gate locally, log a
    /// clearly visible error, and trigger GameManager.OnDied() ourselves.
    ///
    /// The timer is started by PlayerDiedReportEventHandler and reset on
    /// RunCoordinator.RunReset (also wired in PlayerDiedReportEventHandler's ctor).
    ///
    /// The patch is a no-op in single-player (InMultiplayer == false) and on the host
    /// (IsHosting == true), so it cannot interfere with normal gameplay.
    /// </summary>
    public static class RunTimeoutPatches
    {
        private const float TIMEOUT_SECONDS = 15f;

        private static bool _timeoutActive = false;
        private static float _timeoutStartTime = 0f;

        /// <summary>
        /// Called by PlayerDiedReportEventHandler when the client's local player dies.
        /// Starts the 15-second countdown.
        /// </summary>
        public static void StartTimeout()
        {
            _timeoutActive = true;
            _timeoutStartTime = Time.unscaledTime;
            MelonLogger.Msg(
                $"[Client] Run-over timeout started. If RUN_OVER is not received within " +
                $"{TIMEOUT_SECONDS:F0} s the escape hatch will open the game-over gate locally.");
        }

        /// <summary>
        /// Called via RunCoordinator.RunReset to cancel any pending timeout at run start/restart.
        /// </summary>
        public static void Reset()
        {
            _timeoutActive = false;
            _timeoutStartTime = 0f;
        }

        /// <summary>
        /// Ticks every frame on the Unity main thread (same hook used by TimeSyncPatches
        /// and SyncDigestPatches).  Fires the escape hatch when the timer elapses.
        /// </summary>
        [HarmonyPatch(typeof(MyTime), nameof(MyTime.Update))]
        class ClientRunOverTimeoutTick
        {
            static void Postfix()
            {
                if (!_timeoutActive)
                    return;

                // Only applies to multiplayer clients; hosts coordinate the run-end themselves.
                if (!LobbyPatchFlags.InMultiplayer || LobbyPatchFlags.IsHosting)
                    return;

                // RUN_OVER arrived normally — cancel the timer.
                if (RunCoordinator.AllowGameOver)
                {
                    _timeoutActive = false;
                    return;
                }

                float elapsed = Time.unscaledTime - _timeoutStartTime;
                if (elapsed < TIMEOUT_SECONDS)
                    return;

                _timeoutActive = false;
                MelonLogger.Error(
                    $"[Client] RUN_OVER was not received within {TIMEOUT_SECONDS:F0} s after local player death. " +
                    "Triggering local game-over escape hatch — the host most likely crashed. " +
                    "This is abnormal; investigate the host-side logs.");

                // Must call Unity API on the main thread — GameDispatcher guarantees that.
                GameDispatcher.Enqueue(() =>
                {
                    try
                    {
                        RunCoordinator.SetAllowGameOver();
                        GameManager.Instance?.OnDied();
                    }
                    catch (System.Exception ex)
                    {
                        MelonLogger.Error(
                            $"[Client] Escape hatch failed to trigger GameManager.OnDied: {ex.Message}");
                    }
                });
            }
        }
    }
}
