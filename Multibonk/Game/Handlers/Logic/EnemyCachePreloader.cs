using MelonLoader;
using Multibonk.Game.Patches;
using System.Linq;

namespace Multibonk.Game.Handlers.Logic
{
    /// <summary>
    /// Pre-loads all enemy types into the cache at game start
    /// This ensures the client can spawn any enemy type the host broadcasts
    /// </summary>
    public class EnemyCachePreloader : GameEventHandler
    {
        public EnemyCachePreloader()
        {
            GameEvents.GameLoadedEvent += PreloadEnemyTypes;
        }

        private void PreloadEnemyTypes()
        {
            try
            {
                MelonLogger.Msg("[EnemyCachePreloader] Pre-loading all enemy types into cache...");

                // Find Assembly-CSharp
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                    
                if (assembly == null)
                {
                    MelonLogger.Warning("[EnemyCachePreloader] Could not find Assembly-CSharp");
                    return;
                }

                // Get EnemyData type
                var enemyDataType = assembly.GetType("Il2CppAssets.Scripts.ScriptableObjects.EnemyData");
                if (enemyDataType == null)
                {
                    // Try without namespace
                    enemyDataType = assembly.GetType("EnemyData");
                }

                if (enemyDataType == null)
                {
                    MelonLogger.Warning("[EnemyCachePreloader] Could not find EnemyData type");
                    return;
                }

                // Use Resources.FindObjectsOfTypeAll to get ALL EnemyData assets
                var resourcesType = typeof(UnityEngine.Resources);
                var findObjectsMethod = resourcesType.GetMethods()
                    .Where(m => m.Name == "FindObjectsOfTypeAll" && m.IsGenericMethod && m.GetParameters().Length == 0)
                    .FirstOrDefault();

                if (findObjectsMethod == null)
                {
                    MelonLogger.Warning("[EnemyCachePreloader] Could not find FindObjectsOfTypeAll method");
                    return;
                }

                var genericMethod = findObjectsMethod.MakeGenericMethod(enemyDataType);
                var enemyDataObjects = (System.Array)genericMethod.Invoke(null, null);

                if (enemyDataObjects == null || enemyDataObjects.Length == 0)
                {
                    MelonLogger.Warning("[EnemyCachePreloader] No EnemyData objects found in Resources");
                    return;
                }

                MelonLogger.Msg($"[EnemyCachePreloader] Found {enemyDataObjects.Length} EnemyData objects");

                // Cache each enemy type
                int cachedCount = 0;
                foreach (var enemyData in enemyDataObjects)
                {
                    if (enemyData == null) continue;

                    try
                    {
                        // Get enemyName property
                        var enemyNameProp = enemyData.GetType().GetProperty("enemyName");
                        if (enemyNameProp == null) continue;

                        var enemyEnum = enemyNameProp.GetValue(enemyData);
                        if (enemyEnum == null) continue;

                        int enemyType = (int)enemyEnum;

                        // Cache the enemy data
                        EnemyDataCache.CacheEnemyData(enemyType, enemyData);
                        cachedCount++;

                        MelonLogger.Msg($"[EnemyCachePreloader] Cached enemy type {enemyType} ({enemyEnum})");
                    }
                    catch (System.Exception ex)
                    {
                        MelonLogger.Warning($"[EnemyCachePreloader] Failed to cache enemy data: {ex.Message}");
                    }
                }

                MelonLogger.Msg($"[EnemyCachePreloader] ✓ Pre-loaded {cachedCount} enemy types into cache");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[EnemyCachePreloader] Error pre-loading enemy types: {ex.Message}");
                MelonLogger.Error($"Stack: {ex.StackTrace}");
            }
        }
    }
}
