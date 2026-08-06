using Il2Cpp;
using MelonLoader;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Lobby;
using UnityEngine;

namespace Multibonk.Game.Handlers.NetworkNotify
{
    /// <summary>
    /// Host-side handler for the local player's death.
    ///
    /// When the host player dies:
    ///   1. The host's own LobbyPlayer is marked dead.
    ///   2. A PLAYER_DEATH broadcast is sent to all connected clients (HUD update).
    ///   3. RunCoordinator.TryEndRun is called — the shared, idempotent end-run check.
    ///      If every lobby player is now dead, TryEndRun broadcasts RUN_OVER, opens the
    ///      game-over gate, and triggers GameManager.OnDied() on the main thread.
    ///
    /// Only active while hosting (clients use PlayerDiedReportEventHandler).
    /// </summary>
    public class PlayerDeathEventHandler : GameEventHandler
    {
        public PlayerDeathEventHandler(LobbyContext lobbyContext)
        {
            GameEvents.PlayerDieEvent += () =>
            {
                if (!LobbyPatchFlags.IsHosting)
                    return;

                try
                {
                    var myself = lobbyContext.GetMyself();

                    // DEFECT 5: Log a visible error when myself is null so this failure is
                    // never silently swallowed.  A null here means the host's own death will
                    // not be recorded — AreAllPlayersDead() can never return true — causing
                    // a permanent soft-lock.
                    if (myself == null)
                    {
                        MelonLogger.Error(
                            "[Host] GetMyself() returned null — host death cannot be recorded. " +
                            "The run will soft-lock (game-over screen will never appear).");
                        return;
                    }

                    myself.IsDead = true;
                    MelonLogger.Msg($"[Host] Local player '{myself.Name}' marked dead.");

                    // Notify every client that the host died (for the Players HUD).
                    // Vector3.zero is a placeholder — no corpse VFX is triggered.
                    var deathPacket = new SendPlayerDeathPacket(myself.UUID, Vector3.zero);
                    foreach (var player in lobbyContext.GetPlayers())
                    {
                        if (player.Connection != null)
                            player.Connection.EnqueuePacket(deathPacket);
                    }

                    // Shared, idempotent end-run evaluation (DEFECT 2).
                    // TryEndRun broadcasts RUN_OVER and ends the run only if every
                    // lobby player is now dead; otherwise it is a no-op.
                    if (!lobbyContext.AreAllPlayersDead())
                        MelonLogger.Msg("[Host] Host died but other players are still alive - run continues.");

                    RunCoordinator.TryEndRun(lobbyContext);
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"[Host] Failed to handle player death: {ex.Message}");
                }
            };
        }
    }
}
