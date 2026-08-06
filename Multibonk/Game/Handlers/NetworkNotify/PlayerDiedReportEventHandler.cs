using MelonLoader;
using Multibonk.Game.Patches;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Multibonk.Networking.Comms;
using Multibonk.Networking.Lobby;

namespace Multibonk.Game.Handlers.NetworkNotify
{
    /// <summary>
    /// When the local player on a CLIENT machine dies, this handler:
    ///   1. Sends PLAYER_DIED_PACKET to the host so the host can track all-dead state.
    ///   2. Starts the 15-second escape-hatch timer (RunTimeoutPatches) in case the host
    ///      crashes before sending RUN_OVER (DEFECT 3).
    ///
    /// Does nothing when hosting (the host handles its own death inside
    /// PlayerDeathEventHandler which has direct access to LobbyContext).
    /// </summary>
    public class PlayerDiedReportEventHandler : GameEventHandler
    {
        public PlayerDiedReportEventHandler(NetworkService network)
        {
            // DEFECT 3: make sure the escape-hatch timer is cleared whenever the run
            // resets (retry / restart).  Wired here because this handler is the only
            // DI-constructed class dedicated to the client death → report flow.
            RunCoordinator.RunReset += RunTimeoutPatches.Reset;

            GameEvents.PlayerDieEvent += () =>
            {
                // Only clients report; hosts handle their own death inline
                if (!LobbyPatchFlags.InMultiplayer || LobbyPatchFlags.IsHosting)
                    return;

                try
                {
                    MelonLogger.Msg("[Client] Local player died - sending PLAYER_DIED_PACKET to host");
                    network.GetClientService().Enqueue(new SendPlayerDiedPacket());

                    // DEFECT 3: start the 15-second timeout.  If RUN_OVER does not arrive
                    // (host crashed) the RunTimeoutPatches tick will open the gate locally.
                    RunTimeoutPatches.StartTimeout();
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"[Client] Failed to report death to host: {ex.Message}");
                }
            };
        }
    }
}
