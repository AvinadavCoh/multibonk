using MelonLoader;
using Multibonk.Game.Patches;
using System.Linq;
using System.Collections;

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
            GameEvents.GameLoadedEvent += () => PreloadEnemyTypes();
        }

        /// <summary>
        /// Loads every EnemyData asset into the cache. Safe to call repeatedly
        /// (also used as an on-demand retry by EnemySpawnPacketHandler).
        /// Must run on the main Unity thread.
        /// </summary>
        public static void PreloadEnemyTypes()
        {
            try
            {
                DebugLogger.Log("[EnemyCachePreloader] Pre-loading all enemy types into cache...");

                // Find Assembly-CSharp
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

                if (assembly == null)
                {
                    MelonLogger.Warning("[EnemyCachePreloader] Could not find Assembly-CSharp");
                    return;
                }

                // Get EnemyData type (moved to Il2Cpp root namespace in game v1.0.69)
                var enemyDataType = assembly.GetType("Il2Cpp.EnemyData")
                    ?? assembly.GetType("Il2CppAssets.Scripts.ScriptableObjects.EnemyData")
                    ?? assembly.GetType("EnemyData");

                if (enemyDataType == null)
                {
                    MelonLogger.Warning("[EnemyCachePreloader] Could not find EnemyData type");
                    return;
                }

                // 1. Use Resources.FindObjectsOfTypeAll
                LoadFromResources(enemyDataType);

                // 2. Scrape EnemyManager/SummonerController for referenced enemies
                ScrapeEnemyManager(enemyDataType);

                MelonLogger.Msg($"[EnemyCachePreloader] Total cached enemy types: {EnemyDataCache.Count}");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[EnemyCachePreloader] Error pre-loading enemy types: {ex.Message}");
                MelonLogger.Error($"Stack: {ex.StackTrace}");
            }
        }

        private static void LoadFromResources(System.Type enemyDataType)
        {
            // Preferred: direct typed call. Returns an Il2CppReferenceArray (NOT a System.Array),
            // which is why the old reflection + (System.Array) cast failed.
            try
            {
                var enemyDataObjects = UnityEngine.Resources.FindObjectsOfTypeAll<Il2Cpp.EnemyData>();
                if (enemyDataObjects != null)
                {
                    int count = 0;
                    foreach (var enemyData in enemyDataObjects)
                    {
                        CacheEnemyData(enemyData);
                        count++;
                    }

                    DebugLogger.Log($"[EnemyCachePreloader] Found {count} EnemyData objects in Resources");
                    if (count > 0)
                        return;
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[EnemyCachePreloader] Typed Resources load failed: {ex.Message}");
            }

            // Fallback: reflection (in case the type moves again in a future game update).
            try
            {
                var resourcesType = typeof(UnityEngine.Resources);
                var findObjectsMethod = resourcesType.GetMethods()
                    .Where(m => m.Name == "FindObjectsOfTypeAll" && m.IsGenericMethod && m.GetParameters().Length == 0)
                    .FirstOrDefault();

                if (findObjectsMethod == null) return;

                var genericMethod = findObjectsMethod.MakeGenericMethod(enemyDataType);
                var result = genericMethod.Invoke(null, null);

                // Il2Cpp arrays are not System.Array - enumerate instead of casting
                if (result is System.Collections.IEnumerable enumerable)
                {
                    int count = 0;
                    foreach (var enemyData in enumerable)
                    {
                        CacheEnemyData(enemyData);
                        count++;
                    }
                    DebugLogger.Log($"[EnemyCachePreloader] Found {count} EnemyData objects via reflection");
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[EnemyCachePreloader] Resources load failed: {ex.Message}");
            }
        }

        private static void ScrapeEnemyManager(System.Type enemyDataType)
        {
            try
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                
                if (assembly == null) return;

                var enemyManagerType = assembly.GetType("Il2CppAssets.Scripts.Managers.EnemyManager");
                if (enemyManagerType == null) return;

                var instanceProp = enemyManagerType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (instanceProp == null) return;

                var enemyManager = instanceProp.GetValue(null);
                if (enemyManager == null) 
                {
                    MelonLogger.Warning("[EnemyCachePreloader] EnemyManager.Instance is null, skipping scrape");
                    return;
                }

                MelonLogger.Msg("[EnemyCachePreloader] Scraping EnemyManager for EnemyData...");
                ScrapeObjectForEnemyData(enemyManager, enemyDataType);

                // Check SummonerController
                var summonerField = enemyManagerType.GetField("summonerController");
                if (summonerField != null)
                {
                    var summonerController = summonerField.GetValue(enemyManager);
                    if (summonerController != null)
                    {
                        MelonLogger.Msg("[EnemyCachePreloader] Scraping SummonerController for EnemyData...");
                        ScrapeObjectForEnemyData(summonerController, enemyDataType);
                    }
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[EnemyCachePreloader] Error scraping EnemyManager: {ex.Message}");
            }
        }

        private static void ScrapeObjectForEnemyData(object obj, System.Type enemyDataType)
        {
            if (obj == null) return;
            var type = obj.GetType();
            
            // Check all fields (public and private)
            foreach (var field in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
            {
                try
                {
                    // Skip primitive types to avoid errors
                    if (field.FieldType.IsPrimitive || field.FieldType == typeof(string)) continue;

                    var value = field.GetValue(obj);
                    if (value == null) continue;

                    // Check if it is EnemyData
                    if (enemyDataType.IsAssignableFrom(value.GetType()))
                    {
                        CacheEnemyData(value);
                    }
                    // Check if it is a collection (List or Array)
                    else if (value is IEnumerable enumerable)
                    {
                        foreach (var item in enumerable)
                        {
                            if (item != null && enemyDataType.IsAssignableFrom(item.GetType()))
                            {
                                CacheEnemyData(item);
                            }
                        }
                    }
                }
                catch {}
            }
        }

        private static void CacheEnemyData(object enemyData)
        {
            if (enemyData == null) return;

            try
            {
                // Get enemyName property
                var enemyNameProp = enemyData.GetType().GetProperty("enemyName");
                if (enemyNameProp == null) return;

                var enemyEnum = enemyNameProp.GetValue(enemyData);
                if (enemyEnum == null) return;

                int enemyType = (int)enemyEnum;

                // Only log if new
                if (!EnemyDataCache.HasEnemyData(enemyType))
                {
                    EnemyDataCache.CacheEnemyData(enemyType, enemyData);
                    DebugLogger.Log($"[EnemyCachePreloader] Cached enemy type {enemyType}");
                }
                else
                {
                    // Refresh the existing entry
                    EnemyDataCache.CacheEnemyData(enemyType, enemyData);
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[EnemyCachePreloader] Failed to cache EnemyData: {ex.Message}");
            }
        }
    }
}