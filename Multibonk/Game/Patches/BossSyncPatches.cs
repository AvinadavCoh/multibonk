using HarmonyLib;
using MelonLoader;
using Multibonk.Networking.Lobby;
using System.Linq;

namespace Multibonk.Game.Patches
{
    /// <summary>
    /// Patches for boss-specific synchronization
    /// Handles boss spawning, boss health bars, and boss death events
    /// </summary>
    public static class BossSyncPatches
    {
        /// <summary>
        /// Patches EnemyManager.SpawnBoss to broadcast boss spawns
        /// This ensures bosses are properly synchronized with the IsBoss flag
        /// </summary>
        [HarmonyPatch]
        class SpawnBossPatch
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
                    MelonLogger.Warning("Could not find Assembly-CSharp for SpawnBossPatch");
                    return null;
                }

                var enemyManagerType = assembly.GetType("Il2CppAssets.Scripts.Actors.Enemies.EnemyManager");
                if (enemyManagerType == null)
                {
                    MelonLogger.Warning("Could not find EnemyManager type");
                    return null;
                }

                // Find SpawnBoss method
                var spawnBossMethod = enemyManagerType.GetMethod("SpawnBoss", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                
                if (spawnBossMethod == null)
                {
                    MelonLogger.Warning("Could not find SpawnBoss method");
                    return null;
                }

                MelonLogger.Msg("Found EnemyManager.SpawnBoss for patching");
                return spawnBossMethod;
            }

            static void Postfix(object __instance, object __result)
            {
                // Only host broadcasts boss spawns
                if (!LobbyPatchFlags.IsHosting || !LobbyPatchFlags.InMultiplayer)
                    return;

                if (__result == null)
                    return;

                try
                {
                    var enemyType = __result.GetType();
                    
                    // Get position
                    var transform = enemyType.GetProperty("transform")?.GetValue(__result);
                    if (transform == null) return;
                    
                    var positionProp = transform.GetType().GetProperty("position");
                    if (positionProp == null) return;
                    var position = (UnityEngine.Vector3)positionProp.GetValue(transform);
                    
                    // Get enemy data for type
                    var enemyDataProp = enemyType.GetProperty("enemyData");
                    if (enemyDataProp == null) return;
                    var enemyData = enemyDataProp.GetValue(__result);
                    
                    var enemyEnumProp = enemyData.GetType().GetProperty("eEnemy");
                    if (enemyEnumProp == null) return;
                    int enemyTypeValue = (int)enemyEnumProp.GetValue(enemyData);
                    
                    // Get enemy ID
                    int enemyId = __result.GetHashCode();
                    
                    // Get wave number (boss level)
                    var waveNumProp = enemyType.GetProperty("waveNumber");
                    int level = waveNumProp != null ? (int)waveNumProp.GetValue(__result) : 1;
                    
                    MelonLogger.Msg($"[Host] Boss spawned: ID={enemyId}, Type={enemyTypeValue}, Level={level}");
                    
                    // Trigger event with isBoss=true
                    GameEvents.TriggerEnemySpawned(enemyId, enemyTypeValue, position, level, true);
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"Error in SpawnBossPatch: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Patches InteractableBossSpawner.Interact to sync boss spawner activation
        /// When one player activates a boss spawner, all players should see the boss
        /// </summary>
        [HarmonyPatch]
        class BossSpawnerInteractPatch
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
                    MelonLogger.Warning("Could not find Assembly-CSharp for BossSpawnerInteractPatch");
                    return null;
                }

                var bossSpawnerType = assembly.GetType("Il2Cpp.InteractableBossSpawner");
                if (bossSpawnerType == null)
                {
                    MelonLogger.Warning("Could not find InteractableBossSpawner type");
                    return null;
                }

                // Find Interact method
                var interactMethod = bossSpawnerType.GetMethod("Interact", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                
                if (interactMethod == null)
                {
                    MelonLogger.Warning("Could not find Interact method on InteractableBossSpawner");
                    return null;
                }

                MelonLogger.Msg("Found InteractableBossSpawner.Interact for patching");
                return interactMethod;
            }

            static bool Prefix(object __instance)
            {
                // Only host can activate boss spawners in multiplayer
                if (!LobbyPatchFlags.InMultiplayer)
                    return true; // Single player, allow normal behavior

                if (LobbyPatchFlags.IsHosting)
                {
                    // Host can activate - will broadcast via SpawnBoss patch
                    MelonLogger.Msg("[Host] Activating boss spawner");
                    return true;
                }
                else
                {
                    // Client cannot activate boss spawners
                    MelonLogger.Msg("[Client] Boss spawner interaction blocked - only host can activate");
                    return false; // Block the interaction
                }
            }
        }

        // TODO: Add boss health bar sync when we find the HealthBarUi class
        // TODO: Add boss phase transition sync if phases exist
        
        // NOTE: Boss spawning is now fully synchronized:
        // 1. Only host can activate InteractableBossSpawner
        // 2. Host's SpawnBoss call is patched and broadcasts to clients
        // 3. Clients receive EnemySpawnPacket with IsBoss=true
        // 4. Boss health syncs via EnemySyncEventHandler (10% threshold)
        // 5. Boss death syncs immediately to all clients
    }
}
