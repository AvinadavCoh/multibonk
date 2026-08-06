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
        public static bool AllowNetworkInteract = false;

        /// <summary>
        /// Patches EnemyManager.SpawnBoss to broadcast boss spawns
        /// This ensures bosses are properly synchronized with the IsBoss flag
        /// DISABLED: Boss spawns are already handled by EnemySpawnPatch in EnemySyncPatches.cs
        /// </summary>
        [HarmonyPatch]
        class SpawnBossPatch
        {
            static bool Prepare()
            {
                // Disable this patch - boss spawns already work via main enemy spawn system
                return false;
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

                // Try Il2Cpp namespace first (IL2CPP games)
                var enemyManagerType = assembly.GetType("Il2Cpp.EnemyManager");
                if (enemyManagerType == null)
                {
                    // Try the full namespace
                    enemyManagerType = assembly.GetType("Il2CppAssets.Scripts.Actors.Enemies.EnemyManager");
                }
                
                if (enemyManagerType == null)
                {
                    MelonLogger.Warning("Could not find EnemyManager type - SpawnBoss patch disabled");
                    return null;
                }

                // Find SpawnBoss method
                var spawnBossMethod = enemyManagerType.GetMethod("SpawnBoss", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                
                if (spawnBossMethod == null)
                {
                    MelonLogger.Warning("Could not find SpawnBoss method - patch disabled");
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
                    
                    // Trigger event with isBoss=true and EEnemyFlag.Boss
                    // (EEnemyFlag: None=0, Elite=1, Boss=2, StageBoss=4, Challenge=8,
                    //  SummonerMiniboss=16, FinalBoss=32 - verified against v1.0.69)
                    const int ENEMY_FLAG_BOSS = 2;
                    GameEvents.TriggerEnemySpawned(enemyId, enemyTypeValue, position, level, true, ENEMY_FLAG_BOSS);
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"Error in SpawnBossPatch: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Patches InteractableBossSpawner.Interact to sync boss spawner activation
        /// When host activates a boss spawner (bush), broadcast to clients
        /// Clients will trigger their own Interact() to spawn the boss locally
        /// </summary>
        [HarmonyPatch]
        class BossSpawnerInteractPatch
        {
            static bool Prepare()
            {
                // Enable this patch for stage transition sync
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
                    MelonLogger.Warning("Could not find Interact method on InteractableBossSpawner - patch disabled");
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
                    // Host can activate - broadcast the activation
                    try
                    {
                        var instanceType = __instance.GetType();
                        var transform = instanceType.GetProperty("transform")?.GetValue(__instance);
                        if (transform != null)
                        {
                            var positionProp = transform.GetType().GetProperty("position");
                            if (positionProp != null)
                            {
                                var position = (UnityEngine.Vector3)positionProp.GetValue(transform);
                                MelonLogger.Msg($"[Host] Boss spawner activated at ({position.x:F2}, {position.y:F2}, {position.z:F2})");
                                GameEvents.TriggerBossSpawnerActivate(position, 0);
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        MelonLogger.Error($"Error broadcasting boss spawner activation: {ex.Message}");
                    }
                    return true;
                }
                else
                {
                    if (AllowNetworkInteract)
                    {
                        MelonLogger.Msg("[Client] Allowing network-triggered boss spawner interaction");
                        return true;
                    }

                    // Client cannot activate boss spawners directly
                    // They will receive activation via packet handler
                    MelonLogger.Msg("[Client] Boss spawner interaction blocked - waiting for host activation");
                    return false; // Block the interaction
                }
            }
        }

        /// <summary>
        /// Patches InteractablePortal.Interact to sync stage transitions (Stages 1→2, 2→3)
        /// When host activates the portal after killing boss, broadcast to all clients
        /// </summary>
        [HarmonyPatch]
        class PortalInteractPatch
        {
            static System.Reflection.MethodBase TargetMethod()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                    
                if (assembly == null)
                {
                    MelonLogger.Warning("Could not find Assembly-CSharp for PortalInteractPatch");
                    return null;
                }

                var portalType = assembly.GetType("Il2Cpp.InteractablePortal");
                if (portalType == null)
                {
                    MelonLogger.Warning("Could not find InteractablePortal type");
                    return null;
                }

                // Find Interact method
                var interactMethod = portalType.GetMethod("Interact", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                
                if (interactMethod == null)
                {
                    MelonLogger.Warning("Could not find Interact method on InteractablePortal - patch disabled");
                    return null;
                }

                MelonLogger.Msg("Found InteractablePortal.Interact for patching");
                return interactMethod;
            }

            static bool Prefix(object __instance)
            {
                // Only host can activate portal in multiplayer
                if (!LobbyPatchFlags.InMultiplayer)
                    return true; // Single player, allow normal behavior

                if (LobbyPatchFlags.IsHosting)
                {
                    // Host activates portal and broadcasts to clients
                    MelonLogger.Msg("[Host] Stage portal activated - broadcasting transition");
                    GameEvents.TriggerStageTransition();
                    return true;
                }
                else
                {
                    // Client cannot activate portal directly
                    // They will receive stage transition via packet handler
                    MelonLogger.Msg("[Client] Portal interaction blocked - waiting for host transition");
                    return false; // Block the interaction
                }
            }
        }

        /// <summary>
        /// Patches InteractablePortalFinal.Interact to sync game completion
        /// When host activates the final portal, broadcast to all clients
        /// </summary>
        [HarmonyPatch]
        class PortalFinalInteractPatch
        {
            static System.Reflection.MethodBase TargetMethod()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                    
                if (assembly == null)
                {
                    MelonLogger.Warning("Could not find Assembly-CSharp for PortalFinalInteractPatch");
                    return null;
                }

                var portalType = assembly.GetType("Il2Cpp.InteractablePortalFinal");
                if (portalType == null)
                {
                    MelonLogger.Warning("Could not find InteractablePortalFinal type");
                    return null;
                }

                // Find Interact method
                var interactMethod = portalType.GetMethod("Interact", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                
                if (interactMethod == null)
                {
                    MelonLogger.Warning("Could not find Interact method on InteractablePortalFinal - patch disabled");
                    return null;
                }

                MelonLogger.Msg("Found InteractablePortalFinal.Interact for patching");
                return interactMethod;
            }

            static bool Prefix(object __instance)
            {
                // Only host can activate final portal in multiplayer
                if (!LobbyPatchFlags.InMultiplayer)
                    return true; // Single player, allow normal behavior

                if (LobbyPatchFlags.IsHosting)
                {
                    // Host activates final portal and broadcasts to clients
                    MelonLogger.Msg("[Host] Final portal activated - broadcasting game completion");
                    GameEvents.TriggerStageTransition();
                    return true;
                }
                else
                {
                    // Client cannot activate portal directly
                    // They will receive stage transition via packet handler
                    MelonLogger.Msg("[Client] Final portal interaction blocked - waiting for host");
                    return false; // Block the interaction
                }
            }
        }

        // NOTE on boss health bar sync (was TODO):
        //   Il2Cpp.EnemyHpBar has a direct `Enemy enemy` reference and an `Update()` method
        //   that polls enemy.hp every frame. Since EnemyHealthUpdatePacketHandler now writes
        //   enemy.hp / enemy.maxHp directly on clients, boss HP bars update automatically.
        //   No additional sync code is needed.

        // NOTE on boss phase transitions (was TODO):
        //   Boss phases exist only in Il2Cpp.FinalFightController (final boss) via
        //   `currentPhase` / `StartPhase(int)`. Whether StartPhase is triggered by
        //   FixedUpdate polling boss.hp or by OnEnemyDamage events cannot be determined
        //   from the API alone. If it polls boss.hp (the most likely pattern), phases
        //   already sync correctly because EnemyHealthUpdatePacketHandler keeps boss.hp
        //   in sync. Adding a phase sync packet without knowing the exact trigger would
        //   risk double-phase-transitions. Left for investigation with running game logs.

        /// <summary>
        /// Patches InteractableBossSpawnerFinal.Interact to sync final-boss spawner activation.
        /// This is the same host-authoritative pattern used by BossSpawnerInteractPatch for the
        /// regular (bush) boss spawner: host broadcasts the world-space position and clients
        /// find the nearest spawner of either type and call Interact() through AllowNetworkInteract.
        /// </summary>
        [HarmonyPatch]
        class BossSpawnerFinalInteractPatch
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
                    MelonLogger.Warning("Could not find Assembly-CSharp for BossSpawnerFinalInteractPatch");
                    return null;
                }

                var spawnerType = assembly.GetType("Il2Cpp.InteractableBossSpawnerFinal");
                if (spawnerType == null)
                {
                    MelonLogger.Warning("Could not find InteractableBossSpawnerFinal type - patch disabled");
                    return null;
                }

                var interactMethod = spawnerType.GetMethod("Interact",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (interactMethod == null)
                {
                    MelonLogger.Warning("Could not find Interact on InteractableBossSpawnerFinal - patch disabled");
                    return null;
                }

                MelonLogger.Msg("Found InteractableBossSpawnerFinal.Interact for patching");
                return interactMethod;
            }

            static bool Prefix(object __instance)
            {
                if (!LobbyPatchFlags.InMultiplayer)
                    return true; // Single player — no interference

                if (LobbyPatchFlags.IsHosting)
                {
                    try
                    {
                        var instanceType = __instance.GetType();
                        var transform = instanceType.GetProperty("transform")?.GetValue(__instance);
                        if (transform != null)
                        {
                            var positionProp = transform.GetType().GetProperty("position");
                            if (positionProp != null)
                            {
                                var position = (UnityEngine.Vector3)positionProp.GetValue(transform);
                                MelonLogger.Msg($"[Host] Final boss spawner activated at ({position.x:F2}, {position.y:F2}, {position.z:F2})");
                                GameEvents.TriggerBossSpawnerActivate(position, 1);
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        MelonLogger.Error($"Error broadcasting final boss spawner activation: {ex.Message}");
                    }
                    return true;
                }
                else
                {
                    if (AllowNetworkInteract)
                    {
                        MelonLogger.Msg("[Client] Allowing network-triggered final boss spawner interaction");
                        return true;
                    }

                    MelonLogger.Msg("[Client] Final boss spawner interaction blocked - waiting for host activation");
                    return false;
                }
            }
        }

        // NOTE: Boss spawning is now fully synchronized:
        // 1. Only host can activate InteractableBossSpawner (regular) and
        //    InteractableBossSpawnerFinal (final boss)
        // 2. Host broadcasts position via BossSpawnerActivate packet
        // 3. Clients find the nearest spawner of either type and Interact() through
        //    AllowNetworkInteract so the patch does not re-broadcast
        // 4. Clients receive EnemySpawnPacket with IsBoss=true
        // 5. Boss health syncs via EnemySyncEventHandler (10% threshold) and is
        //    applied by EnemyHealthUpdatePacketHandler; HP bars update automatically
        // 6. Boss death syncs immediately to all clients
    }
}
