using Il2Cpp;
using MelonLoader;
using Multibonk.Game;
using Multibonk.Game.Handlers;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    /// <summary>
    /// Client-side handler for RUN_OVER (ServerSentPacketId = 29).
    ///
    /// The host sends this when every lobby player is confirmed dead.
    /// This handler opens the game-over gate and then calls GameManager.OnDied()
    /// on the main Unity thread so the client sees the game-over screen.
    /// </summary>
    public class RunOverPacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.RUN_OVER;

        public void Handle(IncomingMessage msg, Connection conn)
        {
            MelonLogger.Msg("[Client] Received RUN_OVER - all players dead. Triggering game over.");

            GameDispatcher.Enqueue(() =>
            {
                try
                {
                    // Open the gate before calling OnDied so GameOverPatch lets it through
                    RunCoordinator.SetAllowGameOver();
                    GameManager.Instance?.OnDied();
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"[Client] Failed to trigger game over via GameManager.OnDied: {ex.Message}");
                }
            });
        }
    }
}
