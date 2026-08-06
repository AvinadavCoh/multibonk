using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Lobby;

namespace Multibonk.Game.Handlers.NetworkNotify
{
    /// <summary>
    /// Broadcasts the host's periodic state digest to all clients (see SyncDigestPatches).
    /// </summary>
    public class StateDigestEventHandler : GameEventHandler
    {
        public StateDigestEventHandler(LobbyContext lobbyContext)
        {
            GameEvents.StateDigestEvent += digest =>
            {
                if (!LobbyPatchFlags.IsHosting)
                    return;

                var packet = new SendStateDigestPacket(
                    digest.Sequence,
                    digest.StageTime,
                    digest.RunTime,
                    digest.Gold,
                    digest.Level,
                    digest.LiveEnemies,
                    digest.LivePickups,
                    digest.SentCounters,
                    digest.EngineEnemyCount);

                foreach (var player in lobbyContext.GetPlayers())
                {
                    player.Connection?.EnqueuePacket(packet);
                }
            };
        }
    }
}
