using MelonLoader;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Lobby;

namespace Multibonk.Game.Handlers.NetworkNotify
{
    /// <summary>
    /// Handles broadcasting shrine usage from host to all clients
    /// Only runs on the host
    /// </summary>
    public class ShrineUseEventHandler : GameEventHandler
    {
        public ShrineUseEventHandler(LobbyContext lobbyContext)
        {
            GameEvents.UseShrineEvent += () =>
            {
                if (!LobbyPatchFlags.IsHosting)
                    return;

                // TODO: Get actual shrine ID and type from the event
                // For now using placeholder values
                string shrineId = "shrine_" + UnityEngine.Random.Range(0, 1000);
                int shrineType = 0; // Will need to determine type from game

                MelonLogger.Msg($"[Host] Broadcasting shrine use: {shrineId} (type {shrineType})");

                // Get the host player UUID
                var myUuid = lobbyContext.GetMyself().UUID;

                var packet = new SendShrineUsePacket(shrineId, myUuid, shrineType);
                
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
