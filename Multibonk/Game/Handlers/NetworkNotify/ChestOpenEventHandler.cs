using MelonLoader;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Lobby;

namespace Multibonk.Game.Handlers.NetworkNotify
{
    /// <summary>
    /// Handles broadcasting chest openings from host to all clients
    /// Only runs on the host
    /// </summary>
    public class ChestOpenEventHandler : GameEventHandler
    {
        public ChestOpenEventHandler(LobbyContext lobbyContext)
        {
            GameEvents.OpenChestEvent += (chestId) =>
            {
                if (!LobbyPatchFlags.IsHosting)
                    return;

                MelonLogger.Msg($"[Host] Broadcasting chest open: {chestId}");

                // Get the host player UUID
                var myUuid = lobbyContext.GetMyself().UUID;

                var packet = new SendChestOpenPacket(chestId, myUuid);
                
                // Broadcast to all connected clients
                foreach (var player in lobbyContext.GetPlayers())
                {
                    if (player.Connection != null)
                    {
                        player.Connection.EnqueuePacket(packet);
                    }
                }
            };
        }
    }
}
