using Multibonk.Networking.Lobby;

namespace Multibonk.Game.Handlers.Logic
{
    /// <summary>
    /// Drives LevelUpCoordinator's timeout ticks via MelonMod.OnUpdate()
    /// (executor.Update()), which the MelonLoader runtime calls every frame
    /// regardless of whether the game is paused.
    ///
    /// This is the AUTHORITATIVE tick source for level-up timeouts.  The game's
    /// MyTime.Update patch in LevelUpScreenPatches may or may not fire while
    /// MyTime.paused == true (unverified); this handler is the guaranteed path
    /// that ensures the escape hatches advance even during a paused state.
    ///
    /// Both tick methods are idempotent elapsed-time checks against
    /// UnityEngine.Time.unscaledTime (not scaled time), so calling them from
    /// both this handler and the MyTime.Update patch is harmless.
    /// </summary>
    public class LevelUpTimeoutTicker : GameEventHandler
    {
        public override void Update()
        {
            if (!LobbyPatchFlags.InMultiplayer) return;

            try
            {
                if (LobbyPatchFlags.IsHosting)
                    LevelUpCoordinator.TickHostTimeout();
                else
                    LevelUpCoordinator.TickClientTimeout();
            }
            catch (System.Exception ex)
            {
                MelonLoader.MelonLogger.Error(
                    $"[LevelUp] LevelUpTimeoutTicker.Update failed: {ex.Message}");
            }
        }
    }
}
