using Il2Cpp;
using MelonLoader;
using Multibonk.Game.Handlers;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Lobby;

namespace Multibonk.Game
{
    /// <summary>
    /// Coordinates when the run ends in multiplayer.
    ///
    /// The host decides when everyone is dead and sets AllowGameOver.
    /// Clients suppress their local game-over screen via GameOverPatch until
    /// they receive a RUN_OVER packet, which also sets AllowGameOver before
    /// calling GameManager.OnDied() explicitly.
    /// </summary>
    public static class RunCoordinator
    {
        // DEFECT 4: explicit volatile backing field so writes from a network thread are
        // immediately visible to the Unity main thread reading AllowGameOver in GameOverPatch.
        private static volatile bool _allowGameOver = false;

        // Guards TryEndRun against concurrent calls (e.g. host death event racing with
        // an incoming PLAYER_DIED_PACKET on a network thread).
        private static readonly object _runOverLock = new object();
        private static bool _runOverBroadcast = false;

        /// <summary>
        /// When true GameOverPatch lets GameManager.OnDied() run.
        /// Remains false until either the host (all dead) or the RUN_OVER
        /// handler sets it.
        /// </summary>
        public static bool AllowGameOver => _allowGameOver;

        /// <summary>
        /// Fired by Reset() so that listeners (e.g. LobbyContext) can clear
        /// their per-run state without needing a direct reference to this class.
        /// </summary>
        public static event System.Action RunReset;

        /// <summary>
        /// Opens the game-over gate so the next GameManager.OnDied() call
        /// is let through by GameOverPatch.
        /// </summary>
        public static void SetAllowGameOver()
        {
            _allowGameOver = true;
        }

        /// <summary>
        /// Closes the gate and fires RunReset for all per-run state subscribers.
        /// Call on game restart / retry / new scene load.
        /// </summary>
        public static void Reset()
        {
            _allowGameOver = false;
            lock (_runOverLock) { _runOverBroadcast = false; }
            RunReset?.Invoke();
        }

        /// <summary>
        /// The single shared "is everyone dead? if so, end the run" method.
        ///
        /// Safe to call from any thread. Idempotent: a second call after the
        /// broadcast has already been sent is a no-op. Only acts while hosting.
        ///
        /// Call this from every path that could change the alive/dead state
        /// of a LobbyPlayer (death packets, disconnect handler, etc.).
        /// </summary>
        public static void TryEndRun(LobbyContext lobbyContext)
        {
            if (!LobbyPatchFlags.IsHosting) return;
            if (!lobbyContext.AreAllPlayersDead()) return;

            // Claim the "end the run" token exactly once, even under concurrent calls.
            lock (_runOverLock)
            {
                if (_runOverBroadcast) return;
                _runOverBroadcast = true;
            }

            MelonLogger.Msg("[Host] All players confirmed dead - broadcasting RUN_OVER and ending run.");

            var runOverPacket = new SendRunOverPacket();
            foreach (var player in lobbyContext.GetPlayers())
            {
                if (player.Connection != null)
                    player.Connection.EnqueuePacket(runOverPacket);
            }

            SetAllowGameOver();
            GameDispatcher.Enqueue(() =>
            {
                try
                {
                    GameManager.Instance?.OnDied();
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"[Host] Failed to trigger GameManager.OnDied: {ex.Message}");
                }
            });
        }
    }
}
