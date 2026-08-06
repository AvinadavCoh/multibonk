using System.Collections.Generic;
using Il2CppAssets.Scripts.Actors.Enemies;

namespace Multibonk.Game.Patches
{
    /// <summary>
    /// Maps client-spawned enemy instances to host enemy IDs
    /// Needed because GetHashCode() produces different IDs on host vs client for the same conceptual enemy
    /// </summary>
    public static class EnemyIdMapper
    {
        // Maps client enemy instance hash → host enemy ID
        private static readonly Dictionary<int, int> _clientToHostId = new Dictionary<int, int>();

        // Maps host enemy ID → client enemy instance hash
        private static readonly Dictionary<int, int> _hostToClientId = new Dictionary<int, int>();

        // Maps host enemy ID → actual client Enemy instance (for direct game-object calls)
        private static readonly Dictionary<int, Enemy> _hostToEnemy = new Dictionary<int, Enemy>();

        /// <summary>
        /// Number of live host-to-client mappings. Compared against the telemetry
        /// ledger by the desync detector - if these disagree, spawns are being counted
        /// as applied without actually producing a mapped enemy.
        /// </summary>
        public static int MappingCount => _hostToClientId.Count;

        /// <summary>
        /// Register a mapping when client spawns an enemy from network packet.
        /// Also stores the Enemy instance reference for direct game-object calls.
        /// </summary>
        /// <param name="clientEnemyInstance">The enemy object spawned on client</param>
        /// <param name="hostEnemyId">The enemy ID from the host's spawn packet</param>
        public static void RegisterMapping(object clientEnemyInstance, int hostEnemyId)
        {
            if (clientEnemyInstance == null) return;

            int clientId = clientEnemyInstance.GetHashCode();

            _clientToHostId[clientId] = hostEnemyId;
            _hostToClientId[hostEnemyId] = clientId;

            // Store the concrete Enemy reference so death/health handlers can call
            // game methods directly without another round-trip through reflection.
            var enemy = clientEnemyInstance as Enemy;
            if (enemy != null)
            {
                _hostToEnemy[hostEnemyId] = enemy;
            }
            else
            {
                MelonLoader.MelonLogger.Warning($"[EnemyIdMapper] Could not cast instance to Enemy for host ID {hostEnemyId} - TryGetEnemy will not work for this entry");
            }

            MelonLoader.MelonLogger.Msg($"[EnemyIdMapper] Mapped client enemy {clientId} → host ID {hostEnemyId}");
        }

        /// <summary>
        /// Look up the live Enemy instance for a given host ID.
        /// Returns false if not mapped or if the underlying Unity object has been
        /// destroyed/returned to pool (Unity == null check covers both).
        /// Stale entries are cleaned up automatically on a miss.
        /// </summary>
        public static bool TryGetEnemy(int hostId, out Enemy enemy)
        {
            enemy = null;

            if (!_hostToEnemy.TryGetValue(hostId, out var cachedEnemy))
                return false;

            // Unity overrides == for destroyed/pooled objects; this returns true
            // when the native object no longer exists even if the managed wrapper lives on.
            if (cachedEnemy == null)
            {
                // Stale entry - clean up all three maps
                _hostToEnemy.Remove(hostId);
                if (_hostToClientId.TryGetValue(hostId, out int clientId))
                {
                    _clientToHostId.Remove(clientId);
                    _hostToClientId.Remove(hostId);
                }
                return false;
            }

            enemy = cachedEnemy;
            return true;
        }

        /// <summary>
        /// Get the host ID for a client enemy instance
        /// Used when client damages/kills an enemy and needs to broadcast using host's ID
        /// </summary>
        public static int GetHostId(object clientEnemyInstance)
        {
            if (clientEnemyInstance == null) return 0;

            int clientId = clientEnemyInstance.GetHashCode();
            return _clientToHostId.TryGetValue(clientId, out int hostId) ? hostId : clientId;
        }

        /// <summary>
        /// Get the client instance ID for a host enemy ID
        /// Used when receiving damage/death packets from host
        /// </summary>
        public static int GetClientId(int hostEnemyId)
        {
            return _hostToClientId.TryGetValue(hostEnemyId, out int clientId) ? clientId : hostEnemyId;
        }

        /// <summary>
        /// Check if we have a mapping for this client enemy
        /// </summary>
        public static bool HasMapping(object clientEnemyInstance)
        {
            if (clientEnemyInstance == null) return false;
            return _clientToHostId.ContainsKey(clientEnemyInstance.GetHashCode());
        }

        /// <summary>
        /// Clear all mappings (call on game restart)
        /// </summary>
        public static void Clear()
        {
            _clientToHostId.Clear();
            _hostToClientId.Clear();
            _hostToEnemy.Clear();
        }

        /// <summary>
        /// Remove a specific mapping (when enemy dies).
        /// Cleans the hashcode maps and the Enemy instance reference.
        /// </summary>
        public static void RemoveMapping(object clientEnemyInstance)
        {
            if (clientEnemyInstance == null) return;

            int clientId = clientEnemyInstance.GetHashCode();
            if (_clientToHostId.TryGetValue(clientId, out int hostId))
            {
                _clientToHostId.Remove(clientId);
                _hostToClientId.Remove(hostId);
                _hostToEnemy.Remove(hostId);
            }
        }
    }
}
