using HarmonyLib;
using Il2Cpp;
using Il2CppAssets.Scripts.Utility;
using MelonLoader;
using Multibonk.Networking.Lobby;

namespace Multibonk.Game.Patches
{
    /// <summary>
    /// Harmony patches that coordinate the co-op level-up "wait for everyone to pick" gate.
    ///
    /// How the level-up screen pauses the game:
    ///   ShowLevelupScreen() calls MyTime.Pause() internally, and CloseLevelupScreen()
    ///   calls MyTime.Unpause().  Because XP is a shared pool both machines level up at
    ///   roughly the same instant, so each local screen open/close is independent.
    ///   The existing pause-sync (MyTimePausePatch / PauseSyncEventHandler) cannot gate
    ///   on "all players done" — it only mirrors the host's pause state.
    ///
    /// What these patches add:
    ///   ShowLevelupScreenPatch  — notifies LevelUpCoordinator that a level-up is starting.
    ///   CloseLevelupScreenPatch — AFTER the screen closes (auto-unpause), immediately
    ///                             re-pauses the local game, then either:
    ///                               HOST   → OnHostPlayerDone() in LevelUpCoordinator.
    ///                               CLIENT → ClientStartWaiting() arms the escape-hatch
    ///                                        timer, then GameEvents.LevelupScreenClosedEvent
    ///                                        triggers LevelUpEventHandler → LEVELUP_DONE.
    ///   LevelUpTimeoutTick      — each frame (MyTime.Update) ticks the 20 s hard timeouts
    ///                             in LevelUpCoordinator for BOTH host and client:
    ///                               HOST   → TickHostTimeout() (prevents host soft-lock)
    ///                               CLIENT → TickClientTimeout() (client ESCAPE HATCH —
    ///                                        force-unpauses if LEVELUP_RESUME never arrives,
    ///                                        e.g. host crashed or XP desync prevented the
    ///                                        host from ever opening its own level-up screen)
    /// </summary>
    public static class LevelUpScreenPatches
    {
        // ──────────────────────────────────────────────────────────────────────────
        // ShowLevelupScreen – initialise coordination when the screen opens
        // ──────────────────────────────────────────────────────────────────────────

        [HarmonyPatch(typeof(LevelupScreen), nameof(LevelupScreen.ShowLevelupScreen))]
        class ShowLevelupScreenPatch
        {
            [HarmonyPostfix]
            static void Postfix()
            {
                if (!LobbyPatchFlags.InMultiplayer) return;

                try
                {
                    if (LobbyPatchFlags.IsHosting)
                        LevelUpCoordinator.InitForLevelUp();
                    else
                        LevelUpCoordinator.SetActiveOnClient();
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[LevelUp] ShowLevelupScreen patch failed: {ex.Message}");
                }
            }
        }

        // ──────────────────────────────────────────────────────────────────────────
        // CloseLevelupScreen – gate: re-pause, arm escape hatch, then report done
        // ──────────────────────────────────────────────────────────────────────────

        [HarmonyPatch(typeof(LevelupScreen), nameof(LevelupScreen.CloseLevelupScreen))]
        class CloseLevelupScreenPatch
        {
            [HarmonyPostfix]
            static void Postfix()
            {
                if (!LobbyPatchFlags.InMultiplayer) return;
                if (!LevelUpCoordinator.IsActive) return;

                try
                {
                    // CloseLevelupScreen() called MyTime.Unpause() internally.
                    // Cancel that unpause: keep both sides paused until all players are done.
                    MyTime.Pause();

                    if (LobbyPatchFlags.IsHosting)
                    {
                        // Host marks itself done; coordinator resumes everyone when all
                        // connected, alive players have also reported.
                        LevelUpCoordinator.OnHostPlayerDone();
                    }
                    else
                    {
                        // Arm the client-side escape-hatch timer BEFORE firing the event
                        // so the clock starts running immediately.  If LEVELUP_RESUME never
                        // arrives (host crashed or XP desync) the 20 s hatch force-unpauses.
                        LevelUpCoordinator.ClientStartWaiting();

                        // Fire the event; LevelUpEventHandler sends LEVELUP_DONE to host.
                        GameEvents.TriggerLevelupScreenClosed();
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[LevelUp] CloseLevelupScreen patch failed: {ex.Message}");
                }
            }
        }

        // ──────────────────────────────────────────────────────────────────────────
        // MyTime.Update – tick both host and client timeouts every frame
        // ──────────────────────────────────────────────────────────────────────────

        [HarmonyPatch(typeof(MyTime), nameof(MyTime.Update))]
        class LevelUpTimeoutTick
        {
            [HarmonyPostfix]
            static void Postfix()
            {
                if (!LobbyPatchFlags.InMultiplayer) return;

                try
                {
                    if (LobbyPatchFlags.IsHosting)
                        LevelUpCoordinator.TickHostTimeout();
                    else
                        LevelUpCoordinator.TickClientTimeout(); // client escape hatch
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[LevelUp] Timeout tick failed: {ex.Message}");
                }
            }
        }
    }
}
