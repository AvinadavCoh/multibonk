using MelonLoader;
using Multibonk.Game.Handlers;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Lobby;
using UnityEngine;

namespace Multibonk.Game.Handlers.NetworkNotify
{
    /// <summary>
    /// Handles host-side enemy spawn events and broadcasts to clients
    /// </summary>
    public class EnemySpawnedEventHandler : GameEventHandler
    {
        private readonly LobbyContext lobbyContext;

        public EnemySpawnedEventHandler(LobbyContext lobbyContext)
        {
            this.lobbyContext = lobbyContext;
            
            GameEvents.EnemySpawnedEvent += OnEnemySpawned;
        }

        private void OnEnemySpawned(int enemyId, int enemyType, Vector3 position, int level, bool isBoss)
        {
            if (!LobbyPatchFlags.IsHosting)
                return;

            DebugLogger.Log($"[Host] Sending enemy spawn packet: ID={enemyId}, Type={enemyType}, Level={level}");

            var packet = new SendEnemySpawnPacket(
                enemyId,
                enemyType,
                position,
                level,
                isBoss
            );

            // Broadcast to all connected clients
            foreach (var player in lobbyContext.GetPlayers())
            {
                if (player.Connection != null)
                {
                    player.Connection.EnqueuePacket(packet);
                }
            }
        }
    }
}
