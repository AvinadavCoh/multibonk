using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Lobby;

namespace Multibonk.Game.Handlers.NetworkNotify
{
    /// <summary>
    /// Broadcasts the host's pause/unpause to all clients (see TimeSyncPatches).
    /// </summary>
    public class PauseSyncEventHandler : GameEventHandler
    {
        public PauseSyncEventHandler(LobbyContext lobbyContext)
        {
            GameEvents.InGamePauseEvent += () =>
            {
                if (!LobbyPatchFlags.IsHosting) return;

                DebugLogger.Log("[Host] Broadcasting pause");
                var packet = new SendPauseGamePacket();
                foreach (var player in lobbyContext.GetPlayers())
                    player.Connection?.EnqueuePacket(packet);
            };

            GameEvents.InGameUnpauseEvent += () =>
            {
                if (!LobbyPatchFlags.IsHosting) return;

                DebugLogger.Log("[Host] Broadcasting unpause");
                var packet = new SendUnpauseGamePacket();
                foreach (var player in lobbyContext.GetPlayers())
                    player.Connection?.EnqueuePacket(packet);
            };
        }
    }
}
