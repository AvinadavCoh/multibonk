using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Lobby;

namespace Multibonk.Game.Handlers.NetworkNotify
{
    /// <summary>
    /// Broadcasts the host's clock to all clients (see TimeSyncPatches).
    /// </summary>
    public class TimeSyncEventHandler : GameEventHandler
    {
        public TimeSyncEventHandler(LobbyContext lobbyContext)
        {
            GameEvents.TimeSyncEvent += (stageTime, runTime, paused) =>
            {
                if (!LobbyPatchFlags.IsHosting)
                    return;

                var packet = new SendTimeSyncPacket(stageTime, runTime, paused);
                foreach (var player in lobbyContext.GetPlayers())
                {
                    player.Connection?.EnqueuePacket(packet);
                }
            };
        }
    }
}
