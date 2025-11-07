using System.Collections.Generic;

namespace Multibonk.Game.Patches
{
    /// <summary>
    /// Caches EnemyData objects by type for client-side spawning
    /// The Prefix/Postfix patches populate this cache when enemies spawn
    /// </summary>
    public static class EnemyDataCache
    {
        private static readonly Dictionary<int, object> _cache = new Dictionary<int, object>();

        /// <summary>
        /// Cache an EnemyData object by its type (EEnemy enum value)
        /// </summary>
        public static void CacheEnemyData(int enemyType, object enemyData)
        {
            if (enemyData == null) return;
            
            if (!_cache.ContainsKey(enemyType))
            {
                _cache[enemyType] = enemyData;
            }
        }

        /// <summary>
        /// Get cached EnemyData for a specific type
        /// </summary>
        public static object GetEnemyData(int enemyType)
        {
            return _cache.TryGetValue(enemyType, out var data) ? data : null;
        }

        /// <summary>
        /// Check if we have EnemyData cached for a type
        /// </summary>
        public static bool HasEnemyData(int enemyType)
        {
            return _cache.ContainsKey(enemyType);
        }

        /// <summary>
        /// Clear all cached EnemyData (call on game restart)
        /// </summary>
        public static void Clear()
        {
            _cache.Clear();
        }

        /// <summary>
        /// Get count of cached enemy types
        /// </summary>
        public static int Count => _cache.Count;
    }
}
