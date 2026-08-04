using MelonLoader;
using Multibonk.Game.Diagnostics;
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

        private void OnEnemySpawned(int enemyId, int enemyType, Vector3 position, int level, bool isBoss, int flag)
        {
            DebugLogger.Log($"[Host] OnEnemySpawned called: ID={enemyId}, Type={enemyType}, Pos=({position.x}, {position.y}, {position.z}), Level={level}, IsBoss={isBoss}, Flag={flag}");

            if (!LobbyPatchFlags.IsHosting)
            {
                DebugLogger.Warning("[Host] Not hosting, skipping enemy spawn broadcast");
                return;
            }

            var players = lobbyContext.GetPlayers().ToList();

            var packet = new SendEnemySpawnPacket(
                enemyId,
                enemyType,
                position,
                level,
                isBoss,
                flag
            );

            int sentCount = 0;
            // Broadcast to all connected clients
            foreach (var player in players)
            {
                if (player.Connection != null)
                {
                    DebugLogger.Log($"[Host] Sending enemy spawn to player {player.Name} (UUID: {player.UUID})");
                    player.Connection.EnqueuePacket(packet);
                    sentCount++;
                }
                else
                {
                    DebugLogger.Warning($"[Host] Player {player.Name} has no connection");
                }
            }
            
            SyncTelemetry.RecordSent(SyncChannel.EnemySpawn);
            DebugLogger.Log($"[Host] Enemy spawn packet sent to {sentCount} clients");
        }
    }
}
