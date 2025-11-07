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
                // Only host broadcasts health changes
                if (!LobbyPatchFlags.IsHosting || !LobbyPatchFlags.InMultiplayer)
                    return;

                try
                {
                    var enemyType = __instance.GetType();
                    
                    // Get enemy ID (use instance hash code)
                    string enemyId = __instance.GetHashCode().ToString();
                    
                    // Get current HP
                    var hpProp = enemyType.GetProperty("hp");
                    if (hpProp == null) return;
                    float currentHp = (float)hpProp.GetValue(__instance);
                    
                    // Get max HP
                    var maxHpField = enemyType.GetField("maxHp");
                    if (maxHpField == null) return;
                    float maxHp = (float)maxHpField.GetValue(__instance);

                    // Only broadcast if HP changed significantly (10% threshold to reduce spam)
                    float hpPercent = currentHp / maxHp;
                    
                    // Broadcast health changes
                    GameEvents.TriggerEnemyHealthChanged(enemyId, currentHp, maxHp);
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
                // Check if we can find the target method
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                    
                if (assembly == null)
                {
                    MelonLogger.Warning("Could not find Assembly-CSharp for EnemyDiedPatch - skipping patch");
                    return false;
                }

                var enemyType = assembly.GetType("Il2CppAssets.Scripts.Actors.Enemies.Enemy");
                if (enemyType == null)
                {
                    MelonLogger.Warning("Could not find Enemy type for EnemyDiedPatch - skipping patch");
                    return false;
                }

                var enemyDiedMethods = enemyType.GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Where(m => m.Name == "EnemyDied")
                    .ToList();

                var enemyDiedMethod = enemyDiedMethods.FirstOrDefault(m => m.GetParameters().Length == 0);
                
                if (enemyDiedMethod == null)
                {
                    MelonLogger.Warning("Could not find EnemyDied method - skipping patch");
                    return false;
                }

                return true;
            }

            static System.Reflection.MethodBase TargetMethod()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                    
                if (assembly == null)
                {
                    return null;
                }

                var enemyType = assembly.GetType("Il2CppAssets.Scripts.Actors.Enemies.Enemy");
                if (enemyType == null)
                {
                    return null;
                }

                // Find the EnemyDied method - there are two overloads, get the one without parameters
                var enemyDiedMethods = enemyType.GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Where(m => m.Name == "EnemyDied")
                    .ToList();

                var enemyDiedMethod = enemyDiedMethods.FirstOrDefault(m => m.GetParameters().Length == 0);
                
                if (enemyDiedMethod != null)
                {
                    MelonLogger.Msg("Found Enemy.EnemyDied for patching");
                }

                return enemyDiedMethod;
            }

            static void Postfix(object __instance)
            {
                // Only host broadcasts deaths
                if (!LobbyPatchFlags.IsHosting || !LobbyPatchFlags.InMultiplayer)
                    return;

                try
                {
                    // Get enemy ID
                    string enemyId = __instance.GetHashCode().ToString();
                    
                    MelonLogger.Msg($"[Host] Enemy died: {enemyId}");
                    
                    // Trigger death event
                    GameEvents.TriggerEnemyDie(enemyId);
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"Error in EnemyDiedPatch: {ex.Message}");
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
        class EnemyManagerSpawnEnemyPatch
        {
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

            static bool Prefix(object __instance, object enemyData, UnityEngine.Vector3 pos, int waveNumber, bool forceSpawn)
            {
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
                        MelonLogger.Msg($"[Host] Spawning enemy: {enemyName} at ({pos.x}, {pos.y}, {pos.z}), wave: {waveNumber}, forced: {forceSpawn}");
                    }
                    catch { }
                }
                
                return true; // Allow spawn
            }

            static void Postfix(object __instance, object enemyData, UnityEngine.Vector3 pos, int waveNumber, bool forceSpawn, object __result)
            {
                // Only host broadcasts spawns
                if (!LobbyPatchFlags.IsHosting)
                    return;

                if (__result == null)
                {
                    DebugLogger.Log($"[Host] Enemy spawn failed, not broadcasting");
                    return;
                }

                try
                {
                    // Get enemy instance ID for tracking
                    int enemyId = __result != null ? __result.GetHashCode() : 0;
                    
                    // Get enemy type from EnemyData
                    var typeField = enemyData?.GetType().GetProperty("Type");
                    int enemyType = typeField != null ? (int)typeField.GetValue(enemyData) : 0;

                    DebugLogger.Log($"[Host] Broadcasting enemy spawn: ID={enemyId}, Type={enemyType}, Pos=({pos.x}, {pos.y}, {pos.z})");
                    
                    // Trigger event to broadcast spawn to clients
                    GameEvents.TriggerEnemySpawned(enemyId, enemyType, pos, waveNumber, forceSpawn);
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"[Host] Failed to broadcast enemy spawn: {ex.Message}");
                }
            }
        }
    }
}
