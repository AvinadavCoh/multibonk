using MelonLoader;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Lobby;

namespace Multibonk.Game.Handlers.NetworkNotify
{
    /// <summary>
    /// Handles broadcasting stage transition (portal activation) to all connected clients
    /// When host activates the portal, all clients trigger their portal's DoLoadNextStage()
    /// </summary>
    public class StageTransitionEventHandler : GameEventHandler
    {
        public StageTransitionEventHandler(LobbyContext lobbyContext)
        {
            GameEvents.StageTransitionEvent += () =>
            {
                MelonLogger.Msg("[Host] Broadcasting stage transition (portal activation)");

                var packet = new SendStageTransitionPacket();
                foreach (var player in lobbyContext.GetPlayers())
                {
                    player.Connection?.EnqueuePacket(packet);
                }
            };
        }
    }
}
