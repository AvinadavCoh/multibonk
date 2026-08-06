using MelonLoader;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Multibonk.Networking.Comms;
using Multibonk.Networking.Lobby;

namespace Multibonk.Game.Handlers.NetworkNotify
{
    /// <summary>
    /// Sends LEVELUP_DONE_PACKET to the host when the local CLIENT player closes its
    /// upgrade screen.  Also resets LevelUpCoordinator when the run ends.
    ///
    /// Only acts on clients (IsHosting == false); on the host the coordination is handled
    /// directly inside LevelUpCoordinator.OnHostPlayerDone().
    /// </summary>
    public class LevelUpEventHandler : GameEventHandler
    {
        public LevelUpEventHandler(NetworkService network)
        {
            // Reset coordination state on every run restart.
            RunCoordinator.RunReset += LevelUpCoordinator.Reset;

            GameEvents.LevelupScreenClosedEvent += () =>
            {
                // Only clients report to the host; the host coordinates directly.
                if (!LobbyPatchFlags.InMultiplayer || LobbyPatchFlags.IsHosting)
                    return;

                try
                {
                    MelonLogger.Msg("[Client] Sending LEVELUP_DONE to host");
                    network.GetClientService().Enqueue(new SendLevelupDonePacket());
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[Client] Failed to send LEVELUP_DONE: {ex.Message}");
                }
            };
        }
    }
}
