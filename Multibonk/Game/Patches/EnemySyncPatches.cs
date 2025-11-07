using HarmonyLib;
using MelonLoader;
using Multibonk.Networking.Lobby;
using System.Linq;

namespace Multibonk.Game.Patches
{
    /// <summary>
    /// Patches to intercept enemy health changes and death events.
    /// 
    /// IMPLEMENTATION STRATEGY:
    /// 1. Find enemy health/damage system in game code
    /// 2. Patch the methods that modify enemy health
    /// 3. Trigger our sync events
    /// 
    /// IDEAL ARCHITECTURE:
    /// - Patch at the lowest level (health modification)
    /// - Don't patch visual/UI updates (too frequent)
    /// - Use Postfix patches to ensure game logic runs first
    /// 
    /// COMMON PATTERNS TO LOOK FOR (in dnSpy):
    /// - Enemy.TakeDamage(float amount)
    /// - Enemy.Die() or Enemy.Kill()
    /// - EnemyHealth.ModifyHealth(float delta)
    /// - HealthComponent.Damage(DamageInfo info)
    /// 
    /// OPTIMIZATION NOTES:
    /// - Only host should broadcast (check LobbyPatchFlags.IsHosting)
    /// - Cache enemy references to avoid repeated lookups
    /// - Use object pooling for frequent packet creation
    /// </summary>
    public static class EnemySyncPatches
    {
        /// <summary>
        /// Patches Enemy.set_hp to broadcast health changes
        /// This is called whenever enemy HP is modified
        /// </summary>
        [HarmonyPatch]
        class EnemySetHpPatch
        {
            static bool Prepare()
            {
                return true;
            }

            static System.Reflection.MethodBase TargetMethod()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                    
                if (assembly == null)
                {
                    MelonLogger.Warning("Could not find Assembly-CSharp for EnemySetHpPatch");
                    return null;
                }

                var enemyType = assembly.GetType("Il2CppAssets.Scripts.Actors.Enemies.Enemy");
                if (enemyType == null)
                {
                    MelonLogger.Warning("Could not find Enemy type for EnemySetHpPatch");
                    return null;
                }

                // Find set_hp method
                var setHpMethod = enemyType.GetMethod("set_hp", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (setHpMethod == null)
                {
                    MelonLogger.Warning("Could not find set_hp method");
                    return null;
                }

                MelonLogger.Msg("Found Enemy.set_hp for patching");
                return setHpMethod;
            }

            static void Postfix(object __instance)
            {
                // Skip if not in multiplayer
                if (!LobbyPatchFlags.InMultiplayer)
                    return;

                try
                {
                    var enemyType = __instance.GetType();
                    
                    // Get enemy ID
                    // For clients: use mapped host ID if available
                    // For host: use instance hash code
                    int enemyId;
                    if (LobbyPatchFlags.IsHosting)
                    {
                        enemyId = __instance.GetHashCode();
                    }
                    else
                    {
                        // Client: get mapped host ID
                        enemyId = EnemyIdMapper.GetHostId(__instance);
                    }
                    
                    // Get current HP
                    var hpProp = enemyType.GetProperty("hp");
                    if (hpProp == null) return;
                    float currentHp = (float)hpProp.GetValue(__instance);
                    
                    // Get max HP
                    var maxHpField = enemyType.GetField("maxHp");
                    if (maxHpField == null) return;
                    float maxHp = (float)maxHpField.GetValue(__instance);

                    // Broadcast health changes (both host and client)
                    GameEvents.TriggerEnemyHealthChanged(enemyId.ToString(), currentHp, maxHp);
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"Error in EnemySetHpPatch: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Patches Enemy.EnemyDied to broadcast death events
        /// This is the main death method called when an enemy dies
        /// </summary>
        [HarmonyPatch]
        class EnemyDiedPatch
        {
            static bool Prepare()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                    
                if (assembly == null)
                    return false;

                var enemyType = assembly.GetType("Il2CppAssets.Scripts.Actors.Enemies.Enemy");
                if (enemyType == null)
                    return false;

                // Try EnemyDied (private, no parameters)
                var enemyDiedMethod = enemyType.GetMethod("EnemyDied", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (enemyDiedMethod != null && enemyDiedMethod.GetParameters().Length == 0)
                {
                    MelonLogger.Msg("Found Enemy.EnemyDied for patching");
                    return true;
                }

                // Try Kill (public, no parameters)
                var killMethod = enemyType.GetMethod("Kill", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                
                if (killMethod != null && killMethod.GetParameters().Length == 0)
                {
                    MelonLogger.Msg("Found Enemy.Kill for patching");
                    return true;
                }

                return false;
            }

            static System.Reflection.MethodBase TargetMethod()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                    
                if (assembly == null)
                    return null;

                var enemyType = assembly.GetType("Il2CppAssets.Scripts.Actors.Enemies.Enemy");
                if (enemyType == null)
                    return null;

                // Try EnemyDied first (private, no parameters)
                var enemyDiedMethod = enemyType.GetMethod("EnemyDied", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (enemyDiedMethod != null && enemyDiedMethod.GetParameters().Length == 0)
                {
                    return enemyDiedMethod;
                }

                // Fall back to Kill (public, no parameters)
                var killMethod = enemyType.GetMethod("Kill", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                
                if (killMethod != null && killMethod.GetParameters().Length == 0)
                {
                    return killMethod;
                }

                return null;
            }

            static void Postfix(object __instance)
            {
                // Skip if not in multiplayer
                if (!LobbyPatchFlags.InMultiplayer)
                    return;

                try
                {
                    // Get enemy ID
                    // For clients: use mapped host ID if available
                    // For host: use instance hash code
                    string enemyId;
                    if (LobbyPatchFlags.IsHosting)
                    {
                        enemyId = __instance.GetHashCode().ToString();
                    }
                    else
                    {
                        // Client: get mapped host ID and cleanup mapping
                        int hostId = EnemyIdMapper.GetHostId(__instance);
                        enemyId = hostId.ToString();
                        EnemyIdMapper.RemoveMapping(__instance);
                    }
                    
                    MelonLogger.Msg($"[{(LobbyPatchFlags.IsHosting ? "Host" : "Client")}] Enemy died: {enemyId}");
                    
                    // Trigger death event (both host and client broadcast)
                    GameEvents.TriggerEnemyDie(enemyId);
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"Error in EnemyKillPatch: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Patches EnemyManager.SpawnEnemy to implement host-only spawning
        /// Clients receive spawn packets from host instead of spawning independently
        /// 
        /// NOTE: This patch uses string-based patching since we don't have direct access to game types
        /// The method signature is: SpawnEnemy(EnemyData, Vector3, int, bool, EEnemyFlag, bool)
        /// </summary>
        [HarmonyPatch]
        public class EnemyManagerSpawnEnemyPatch
        {
            /// <summary>
            /// When true, allows client to spawn enemies from network packets
            /// This bypasses the normal client spawn blocking
            /// </summary>
            public static bool AllowNetworkSpawn = false;
            
            /// <summary>
            /// Track recently broadcast enemies to prevent duplicates
            /// Key: enemy instance hash code, Value: timestamp
            /// </summary>
            private static Dictionary<int, float> recentlyBroadcast = new Dictionary<int, float>();
            private const float BROADCAST_COOLDOWN = 0.1f; // 100ms cooldown between broadcasts of same enemy

            static bool Prepare()
            {
                // Check if we can find the target method
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                    
                if (assembly == null)
                {
                    MelonLogger.Warning("Could not find Assembly-CSharp for EnemyManagerSpawnEnemyPatch - skipping patch");
                    return false;
                }

                var enemyManagerType = assembly.GetType("Il2CppAssets.Scripts.Managers.EnemyManager");
                if (enemyManagerType == null)
                {
                    MelonLogger.Warning("Could not find EnemyManager type for patching - skipping patch");
                    return false;
                }

                var methods = enemyManagerType.GetMethods()
                    .Where(m => m.Name == "SpawnEnemy" && m.GetParameters().Length == 6)
                    .ToList();

                if (methods.Count == 0)
                {
                    MelonLogger.Warning("Could not find SpawnEnemy method with 6 parameters - skipping patch");
                    return false;
                }

                return true;
            }

            static System.Reflection.MethodBase TargetMethod()
            {
                // Find the EnemyManager type using Assembly.GetType
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                    
                if (assembly == null)
                {
                    return null;
                }

                var enemyManagerType = assembly.GetType("Il2CppAssets.Scripts.Managers.EnemyManager");
                if (enemyManagerType == null)
                {
                    return null;
                }

                // Find the SpawnEnemy method with specific parameter types
                // SpawnEnemy(EnemyData enemyData, Vector3 pos, int waveNumber, bool forceSpawn, EEnemyFlag flag, bool canBeElite)
                var methods = enemyManagerType.GetMethods()
                    .Where(m => m.Name == "SpawnEnemy" && m.GetParameters().Length == 6)
                    .ToList();

                if (methods.Count == 0)
                {
                    return null;
                }

                MelonLogger.Msg($"Found {methods.Count} SpawnEnemy methods with 6 parameters");
                return methods[0]; // Take the first matching method
            }

            static bool Prefix(object __instance, object enemyData, UnityEngine.Vector3 pos, int waveNumber, bool forceSpawn, object flag, bool canBeElite)
            {
                // Allow network-initiated spawns to bypass blocking
                if (AllowNetworkSpawn)
                {
                    DebugLogger.Log($"[Client] Allowing network-initiated enemy spawn");
                    return true; // Allow this spawn (from network packet)
                }

                // If in multiplayer as a client, block the spawn (will receive from host)
                if (LobbyPatchFlags.InMultiplayer && !LobbyPatchFlags.IsHosting)
                {
                    DebugLogger.Log($"[Client] Blocked local enemy spawn (waiting for host packet)");
                    return false; // Skip original method - enemy will be spawned when packet arrives
                }
                
                // Host or single-player - allow spawn
                if (LobbyPatchFlags.IsHosting)
                {
                    try
                    {
                        var nameField = enemyData?.GetType().GetProperty("Name");
                        string enemyName = nameField?.GetValue(enemyData)?.ToString() ?? "Unknown";
                        MelonLogger.Msg($"[Host] Spawning enemy: {enemyName} at ({pos.x}, {pos.y}, {pos.z}), wave: {waveNumber}, forced: {forceSpawn}, flag: {flag}");
                    }
                    catch { }
                }
                
                return true; // Allow spawn
            }

            static void Postfix(object __instance, object enemyData, UnityEngine.Vector3 pos, int waveNumber, bool forceSpawn, object flag, bool canBeElite, object __result)
            {
                DebugLogger.Log($"[EnemySpawnPatch] Postfix called: IsHosting={LobbyPatchFlags.IsHosting}, InMultiplayer={LobbyPatchFlags.InMultiplayer}, Result={__result != null}");
                
                try
                {
                    // Cache EnemyData for client spawning (works on both host and client)
                    if (enemyData != null)
                    {
                        var enemyNameField = enemyData.GetType().GetProperty("enemyName");
                        if (enemyNameField != null)
                        {
                            var enemyEnumValue = enemyNameField.GetValue(enemyData);
                            if (enemyEnumValue != null)
                            {
                                int enemyType = (int)enemyEnumValue;
                                EnemyDataCache.CacheEnemyData(enemyType, enemyData);
                                DebugLogger.Log($"[EnemySpawnPatch] Cached EnemyData for type {enemyType}");
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    DebugLogger.Warning($"[EnemySpawnPatch] Failed to cache EnemyData: {ex.Message}");
                }
                
                // Only host broadcasts spawns
                if (!LobbyPatchFlags.IsHosting)
                {
                    DebugLogger.Log($"[EnemySpawnPatch] Not hosting, skipping broadcast");
                    return;
                }

                if (__result == null)
                {
                    DebugLogger.Warning($"[EnemySpawnPatch] Enemy spawn returned null, not broadcasting");
                    return;
                }

                try
                {
                    // Get enemy instance ID for tracking
                    int enemyId = __result != null ? __result.GetHashCode() : 0;
                    
                    // Check if we recently broadcast this enemy (deduplication)
                    float currentTime = UnityEngine.Time.time;
                    if (recentlyBroadcast.TryGetValue(enemyId, out float lastBroadcast))
                    {
                        if (currentTime - lastBroadcast < BROADCAST_COOLDOWN)
                        {
                            DebugLogger.Log($"[EnemySpawnPatch] Skipping duplicate broadcast for enemy ID {enemyId}");
                            return;
                        }
                    }
                    
                    // Mark as broadcast
                    recentlyBroadcast[enemyId] = currentTime;
                    
                    // Clean up old entries (older than 1 second)
                    var keysToRemove = recentlyBroadcast.Where(kvp => currentTime - kvp.Value > 1f).Select(kvp => kvp.Key).ToList();
                    foreach (var key in keysToRemove)
                    {
                        recentlyBroadcast.Remove(key);
                    }
                    
                    // Get enemy type from EnemyData.enemyName (EEnemy enum)
                    var enemyNameField = enemyData?.GetType().GetProperty("enemyName");
                    int enemyType = 0;
                    
                    if (enemyNameField != null)
                    {
                        var enemyEnumValue = enemyNameField.GetValue(enemyData);
                        enemyType = enemyEnumValue != null ? (int)enemyEnumValue : 0;
                        DebugLogger.Log($"[EnemySpawnPatch] Found enemyName enum: {enemyEnumValue} (int value: {enemyType})");
                    }
                    else
                    {
                        DebugLogger.Warning($"[EnemySpawnPatch] Could not find enemyName property on EnemyData");
                    }
                    
                    // Determine if this is a boss spawn
                    // Check the flag parameter - if it's "Boss" enum value, it's a boss
                    bool isBoss = false;
                    try
                    {
                        if (flag != null)
                        {
                            string flagString = flag.ToString();
                            isBoss = flagString.Contains("Boss") || flagString.Contains("BOSS");
                            DebugLogger.Log($"[EnemySpawnPatch] Enemy flag: {flagString}, IsBoss: {isBoss}");
                        }
                    }
                    catch { }

                    DebugLogger.Log($"[EnemySpawnPatch] Broadcasting enemy spawn: ID={enemyId}, Type={enemyType}, Pos=({pos.x}, {pos.y}, {pos.z}), Wave={waveNumber}, IsBoss={isBoss}");
                    
                    // Trigger event to broadcast spawn to clients
                    GameEvents.TriggerEnemySpawned(enemyId, enemyType, pos, waveNumber, isBoss);
                    
                    DebugLogger.Log($"[EnemySpawnPatch] TriggerEnemySpawned called successfully");
                }
                catch (System.Exception ex)
                {
                    DebugLogger.Error($"[EnemySpawnPatch] Failed to broadcast enemy spawn: {ex.Message}");
                    DebugLogger.Error($"Stack: {ex.StackTrace}");
                }
            }
        }
    }
}
