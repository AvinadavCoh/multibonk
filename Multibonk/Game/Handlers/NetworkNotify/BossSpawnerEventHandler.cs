using MelonLoader;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Lobby;
using UnityEngine;

namespace Multibonk.Game.Handlers.NetworkNotify
{
    /// <summary>
    /// Handles broadcasting boss spawner activation to all connected clients
    /// When host activates a bush boss spawner, all clients activate their local spawner
    /// </summary>
    public class BossSpawnerEventHandler : GameEventHandler
    {
        public BossSpawnerEventHandler(LobbyContext lobbyContext)
        {
            GameEvents.BossSpawnerActivateEvent += (spawnerPosition) =>
            {
                MelonLogger.Msg($"[Host] Broadcasting boss spawner activation at ({spawnerPosition.x:F2}, {spawnerPosition.y:F2}, {spawnerPosition.z:F2})");

                var packet = new SendBossSpawnerActivatePacket(spawnerPosition);
                foreach (var player in lobbyContext.GetPlayers())
                {
                    player.Connection?.EnqueuePacket(packet);
                }
            };
        }
    }
}
