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
                var found = FindDeathMethod() != null;
                if (!found)
                    MelonLogger.Warning("Could not find Enemy death method (EnemyDied/Kill) - death sync disabled");
                return found;
            }

            static System.Reflection.MethodBase TargetMethod()
            {
                var method = FindDeathMethod();
                if (method != null)
                    MelonLogger.Msg($"Found Enemy.{method.Name}({method.GetParameters().Length} params) for death sync patching");
                return method;
            }

            /// <summary>
            /// Locates the enemy death method across game versions.
            /// NOTE: interop proxies expose ALL methods as public, and v1.0.69 added overloads:
            /// EnemyDied() / EnemyDied(DamageContainer) / Kill(string). GetMethod by name alone
            /// is ambiguous, so enumerate and filter by name + parameter count.
            /// </summary>
            static System.Reflection.MethodBase FindDeathMethod()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

                if (assembly == null)
                    return null;

                var enemyType = assembly.GetType("Il2CppAssets.Scripts.Actors.Enemies.Enemy");
                if (enemyType == null)
                    return null;

                var methods = enemyType.GetMethods(
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

                // Preferred: parameterless EnemyDied (all death paths converge here)
                return methods.FirstOrDefault(m => m.Name == "EnemyDied" && m.GetParameters().Length == 0)
                    ?? methods.FirstOrDefault(m => m.Name == "EnemyDied" && m.GetParameters().Length == 1)
                    ?? methods.FirstOrDefault(m => m.Name == "Kill");
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

                // Game v1.0.69+: SpawnEnemy(EnemyData, Vector3 pos, int waveNumber, bool forceSpawn,
                // EEnemyFlag flag, bool canBeElite, float extraSizeMultiplier) - 7 parameters.
                // (Older versions had 6; the other current overload takes a summonerId instead of pos.)
                var methods = enemyManagerType.GetMethods()
                    .Where(m => m.Name == "SpawnEnemy" && m.GetParameters().Length == 7)
                    .ToList();

                if (methods.Count == 0)
                {
                    MelonLogger.Warning("Could not find SpawnEnemy method with 7 parameters - skipping patch");
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

                // Find the SpawnEnemy method with specific parameter types (v1.0.69+)
                // SpawnEnemy(EnemyData enemyData, Vector3 pos, int waveNumber, bool forceSpawn, EEnemyFlag flag, bool canBeElite, float extraSizeMultiplier)
                var methods = enemyManagerType.GetMethods()
                    .Where(m => m.Name == "SpawnEnemy" && m.GetParameters().Length == 7)
                    .ToList();

                if (methods.Count == 0)
                {
                    return null;
                }

                MelonLogger.Msg($"Found {methods.Count} SpawnEnemy methods with 7 parameters");
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
                BroadcastEnemySpawn(enemyData, __result, pos, waveNumber, flag);
            }

            /// <summary>
            /// Shared spawn-broadcast logic, used by both SpawnEnemy overload patches.
            /// Caches EnemyData (host and client) and broadcasts the spawn (host only).
            /// </summary>
            internal static void BroadcastEnemySpawn(object enemyData, object __result, UnityEngine.Vector3 pos, int waveNumber, object flag)
            {
                DebugLogger.Log($"[EnemySpawnPatch] Broadcast check: IsHosting={LobbyPatchFlags.IsHosting}, InMultiplayer={LobbyPatchFlags.InMultiplayer}, Result={__result != null}");

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
                    
                    // Determine if this is a boss spawn by calling IsBoss() method on the Enemy instance
                    bool isBoss = false;
                    try
                    {
                        if (__result != null)
                        {
                            var enemyType_Class = __result.GetType();
                            var isBossMethod = enemyType_Class.GetMethod("IsBoss");
                            
                            if (isBossMethod != null)
                            {
                                var isBossResult = isBossMethod.Invoke(__result, null);
                                if (isBossResult != null)
                                {
                                    isBoss = (bool)isBossResult;
                                    DebugLogger.Log($"[EnemySpawnPatch] Called IsBoss() method: {isBoss}");
                                }
                            }
                            else
                            {
                                DebugLogger.Warning($"[EnemySpawnPatch] Could not find IsBoss() method on Enemy");
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        DebugLogger.Warning($"[EnemySpawnPatch] Failed to check IsBoss(): {ex.Message}");
                    }

                    // Additional check: Inspect EnemyData for boss properties
                    if (!isBoss && enemyData != null)
                    {
                        try 
                        {
                            // Check for 'isBoss' property
                            var isBossProp = enemyData.GetType().GetProperty("isBoss");
                            if (isBossProp != null)
                            {
                                var val = isBossProp.GetValue(enemyData);
                                if (val != null && val is bool b) 
                                {
                                    isBoss = b;
                                    if (isBoss) DebugLogger.Log("[EnemySpawnPatch] Detected boss via EnemyData.isBoss");
                                }
                            }
                        }
                        catch {}
                    }

                    // Carry the actual EEnemyFlag so clients replay Boss/StageBoss/Elite spawns
                    // correctly (this is what makes the boss HP bar appear on clients)
                    int flagValue = 0;
                    try
                    {
                        if (flag != null)
                            flagValue = System.Convert.ToInt32(flag);
                    }
                    catch { }

                    DebugLogger.Log($"[EnemySpawnPatch] Broadcasting enemy spawn: ID={enemyId}, Type={enemyType}, Pos=({pos.x}, {pos.y}, {pos.z}), Wave={waveNumber}, IsBoss={isBoss}, Flag={flagValue}");

                    // Trigger event to broadcast spawn to clients
                    GameEvents.TriggerEnemySpawned(enemyId, enemyType, pos, waveNumber, isBoss, flagValue);
                    
                    DebugLogger.Log($"[EnemySpawnPatch] TriggerEnemySpawned called successfully");
                }
                catch (System.Exception ex)
                {
                    DebugLogger.Error($"[EnemySpawnPatch] Failed to broadcast enemy spawn: {ex.Message}");
                    DebugLogger.Error($"Stack: {ex.StackTrace}");
                }
            }
        }

        /// <summary>
        /// Patches the second SpawnEnemy overload added in game v1.0.69:
        /// SpawnEnemy(EnemyData enemyData, int summonerId, bool forceSpawn, EEnemyFlag flag, bool useDirectionBias)
        /// This is the path used by summoners (regular stage spawning), so without this patch
        /// most enemies were neither blocked on clients nor broadcast by the host.
        /// </summary>
        [HarmonyPatch]
        public class EnemyManagerSpawnEnemySummonerPatch
        {
            static bool Prepare()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

                if (assembly == null) return false;

                var enemyManagerType = assembly.GetType("Il2CppAssets.Scripts.Managers.EnemyManager");
                if (enemyManagerType == null) return false;

                var found = enemyManagerType.GetMethods()
                    .Any(m => m.Name == "SpawnEnemy" && m.GetParameters().Length == 5);

                if (!found)
                {
                    MelonLogger.Warning("Could not find SpawnEnemy method with 5 parameters (summoner overload) - skipping patch");
                    return false;
                }

                MelonLogger.Msg("Found SpawnEnemy (summoner overload, 5 params) for patching");
                return true;
            }

            static System.Reflection.MethodBase TargetMethod()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

                if (assembly == null) return null;

                var enemyManagerType = assembly.GetType("Il2CppAssets.Scripts.Managers.EnemyManager");
                if (enemyManagerType == null) return null;

                return enemyManagerType.GetMethods()
                    .FirstOrDefault(m => m.Name == "SpawnEnemy" && m.GetParameters().Length == 5);
            }

            static bool Prefix()
            {
                // Network-initiated spawns bypass the block (uses the shared flag)
                if (EnemyManagerSpawnEnemyPatch.AllowNetworkSpawn)
                    return true;

                // Clients don't spawn on their own - the host's packets do it
                if (LobbyPatchFlags.InMultiplayer && !LobbyPatchFlags.IsHosting)
                {
                    DebugLogger.Log("[Client] Blocked local summoner enemy spawn (waiting for host packet)");
                    return false;
                }

                return true;
            }

            static void Postfix(object enemyData, object flag, object __result)
            {
                if (__result == null)
                    return;

                try
                {
                    // This overload has no position parameter - read it from the spawned enemy
                    UnityEngine.Vector3 pos = UnityEngine.Vector3.zero;
                    var mb = __result as UnityEngine.MonoBehaviour;
                    if (mb != null)
                        pos = mb.transform.position;

                    EnemyManagerSpawnEnemyPatch.BroadcastEnemySpawn(enemyData, __result, pos, 0, flag);
                }
                catch (System.Exception ex)
                {
                    DebugLogger.Error($"[EnemySpawnPatch] Summoner overload postfix failed: {ex.Message}");
                }
            }
        }
    }
}
