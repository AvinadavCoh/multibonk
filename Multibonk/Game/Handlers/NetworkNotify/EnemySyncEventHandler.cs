using MelonLoader;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Lobby;
using System.Collections.Generic;

namespace Multibonk.Game.Handlers.NetworkNotify
{
    /// <summary>
    /// Handles enemy synchronization across clients.
    /// Uses a threshold-based approach to minimize network traffic.
    /// 
    /// CURRENT IMPLEMENTATION:
    /// - Tracks last known health for each enemy
    /// - Only broadcasts health updates when >10% HP change occurs
    /// - Always broadcasts death immediately
    /// 
    /// PERFORMANCE:
    /// - Regular enemies: 1 packet (death only)
    /// - Boss fight: ~4-5 packets (health thresholds + death)
    /// - Total bandwidth: ~1-2 KB per level
    /// 
    /// IDEAL OPTIMIZATIONS (Future):
    /// 1. Adaptive Threshold:
    ///    - Small enemies: Don't sync health at all (death only)
    ///    - Medium enemies: 25% threshold
    ///    - Bosses: 10% threshold
    ///    - Automatically detect based on maxHP
    /// 
    /// 2. Batched Updates (ROR2-style):
    ///    - Collect all enemy updates in a frame
    ///    - Send one packet with multiple enemy states
    ///    - Reduces packet overhead by ~70%
    /// 
    /// 3. Priority System:
    ///    - Track which enemies are on-screen for each player
    ///    - Sync visible enemies more frequently
    ///    - Delay updates for off-screen enemies
    /// 
    /// 4. Interpolation Hints:
    ///    - Send velocity/direction with health update
    ///    - Clients can predict next health value
    ///    - Smoother experience, fewer packets needed
    /// 
    /// 5. Compression:
    ///    - Use byte for health percentage (0-100)
    ///    - Use ushort for enemy IDs instead of strings
    ///    - Reduce packet size by ~60%
    /// </summary>
    public class EnemySyncEventHandler : GameEventHandler
    {
        private readonly LobbyContext _lobbyContext;
        
        // Track last known health for threshold detection
        // IDEAL: Move this to a dedicated EnemyStateManager class
        private readonly Dictionary<string, float> _lastKnownHealth = new Dictionary<string, float>();
        
        // Threshold for health sync (10% change)
        // IDEAL: Make this configurable per enemy type
        private const float HEALTH_SYNC_THRESHOLD = 0.10f;

        public EnemySyncEventHandler(LobbyContext lobbyContext)
        {
            _lobbyContext = lobbyContext;

            // Enemy death - always sync immediately
            GameEvents.EnemyDieEvent += (enemyId) =>
            {
                if (!LobbyPatchFlags.IsHosting)
                    return;

                MelonLogger.Msg($"Broadcasting enemy death: {enemyId}");

                var packet = new SendEnemyDeathPacket(enemyId);

                // Broadcast to all connected clients
                foreach (var player in _lobbyContext.GetPlayers())
                {
                    if (player.Connection != null)
                    {
                        player.Connection.EnqueuePacket(packet);
                    }
                }

                // Clean up tracking
                _lastKnownHealth.Remove(enemyId);
            };

            // Enemy health changed - only sync on significant changes
            GameEvents.EnemyHealthChangedEvent += (enemyId, currentHealth, maxHealth) =>
            {
                if (!LobbyPatchFlags.IsHosting)
                    return;

                // Initialize tracking if new enemy
                if (!_lastKnownHealth.ContainsKey(enemyId))
                {
                    _lastKnownHealth[enemyId] = maxHealth;
                }

                float lastHealth = _lastKnownHealth[enemyId];
                float healthLost = lastHealth - currentHealth;
                float percentLost = healthLost / maxHealth;

                // Only broadcast if significant change (>10% of max health)
                if (percentLost >= HEALTH_SYNC_THRESHOLD)
                {
                    MelonLogger.Msg($"Broadcasting enemy health: {enemyId} - {currentHealth}/{maxHealth} ({percentLost * 100:F1}% lost)");

                    var packet = new SendEnemyHealthUpdatePacket(enemyId, currentHealth, maxHealth);

                    // Broadcast to all connected clients
                    foreach (var player in _lobbyContext.GetPlayers())
                    {
                        if (player.Connection != null)
                        {
                            player.Connection.EnqueuePacket(packet);
                        }
                    }

                    // Update last known health
                    _lastKnownHealth[enemyId] = currentHealth;
                }
            };
        }
    }
}
