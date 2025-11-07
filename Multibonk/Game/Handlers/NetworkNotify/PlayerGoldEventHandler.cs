using MelonLoader;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Lobby;

namespace Multibonk.Game.Handlers.NetworkNotify
{
    /// <summary>
    /// Handles broadcasting gold collection events to all connected clients
    /// Works with PlayerGoldPatches to sync gold pickups in "Shared" mode
    /// </summary>
    public class PlayerGoldEventHandler : GameEventHandler
    {
        public PlayerGoldEventHandler(LobbyContext lobbyContext)
        {
            GameEvents.PlayerGoldGainedEvent += (goldAmount) =>
            {
                MelonLogger.Msg($"[Host] Broadcasting gold gain: {goldAmount}");

                var packet = new SendPlayerGoldGainedPacket(goldAmount);
                foreach (var player in lobbyContext.GetPlayers())
                {
                    player.Connection?.EnqueuePacket(packet);
                }
            };
        }
    }
}
