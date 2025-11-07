using System.Collections.Generic;

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

        /// <summary>
        /// Register a mapping when client spawns an enemy from network packet
        /// </summary>
        /// <param name="clientEnemyInstance">The enemy object spawned on client</param>
        /// <param name="hostEnemyId">The enemy ID from the host's spawn packet</param>
        public static void RegisterMapping(object clientEnemyInstance, int hostEnemyId)
        {
            if (clientEnemyInstance == null) return;
            
            int clientId = clientEnemyInstance.GetHashCode();
            
            _clientToHostId[clientId] = hostEnemyId;
            _hostToClientId[hostEnemyId] = clientId;
            
            MelonLoader.MelonLogger.Msg($"[EnemyIdMapper] Mapped client enemy {clientId} → host ID {hostEnemyId}");
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
        }

        /// <summary>
        /// Remove a specific mapping (when enemy dies)
        /// </summary>
        public static void RemoveMapping(object clientEnemyInstance)
        {
            if (clientEnemyInstance == null) return;
            
            int clientId = clientEnemyInstance.GetHashCode();
            if (_clientToHostId.TryGetValue(clientId, out int hostId))
            {
                _clientToHostId.Remove(clientId);
                _hostToClientId.Remove(hostId);
            }
        }
    }
}
