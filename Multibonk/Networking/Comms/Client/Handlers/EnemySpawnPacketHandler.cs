using Il2Cpp;
using MelonLoader;
using Multibonk.Game;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;
using UnityEngine;
using System.Linq;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    /// <summary>
    /// Client-side handler for enemy spawn packets
    /// Spawns enemies on the client to mirror host's game state
    /// </summary>
    public class EnemySpawnPacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.ENEMY_SPAWN_PACKET;

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new EnemySpawnPacket(msg);

            DebugLogger.Log($"[Client] Received enemy spawn: ID={packet.EnemyId}, Type={packet.EnemyType}, Level={packet.Level}, IsBoss={packet.IsBoss}");
            DebugLogger.Log($"[Client] Spawn position: ({packet.Position.x}, {packet.Position.y}, {packet.Position.z})");

            // Spawn on main game thread
            Game.Handlers.GameDispatcher.Enqueue(() =>
            {
                SpawnEnemyOnClient(packet);
            });
        }

        private void SpawnEnemyOnClient(EnemySpawnPacket packet)
        {
            try
            {
                // Find the Assembly-CSharp
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                    
                if (assembly == null)
                {
                    DebugLogger.Error("[Client] Could not find Assembly-CSharp");
                    return;
                }

                // Get EnemyManager type
                var enemyManagerType = assembly.GetType("Il2CppAssets.Scripts.Managers.EnemyManager");
                if (enemyManagerType == null)
                {
                    DebugLogger.Error("[Client] Could not find EnemyManager type");
                    return;
                }

                // Get EnemyManager.Instance
                var instanceProp = enemyManagerType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (instanceProp == null)
                {
                    DebugLogger.Error("[Client] Could not find EnemyManager.Instance property");
                    return;
                }

                var enemyManager = instanceProp.GetValue(null);
                if (enemyManager == null)
                {
                    DebugLogger.Error("[Client] EnemyManager.Instance is null - game not loaded yet");
                    return;
                }

                // Try to get the SummonerController which likely has enemy data
                var summonerField = enemyManagerType.GetField("summonerController");
                if (summonerField != null)
                {
                    var summonerController = summonerField.GetValue(enemyManager);
                    if (summonerController != null)
                    {
                        var summonerType = summonerController.GetType();
                        MelonLogger.Msg($"[Client] Found SummonerController: {summonerType.Name}");
                        
                        // Look for enemy data collections in SummonerController
                        var summonerFields = summonerType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        foreach (var field in summonerFields)
                        {
                            if (field.Name.ToLower().Contains("enemy") || field.Name.ToLower().Contains("data"))
                            {
                                DebugLogger.Log($"[Client] SummonerController field: {field.FieldType.Name} {field.Name}");
                            }
                        }
                    }
                }

                // Try to get EnemyData from cache (populated by local spawns)
                var cachedData = Game.Patches.EnemyDataCache.GetEnemyData(packet.EnemyType);
                
                if (cachedData != null)
                {
                    MelonLogger.Msg($"[Client] ✓ Found cached EnemyData for type {packet.EnemyType}!");
                    
                    // Find the SpawnEnemy method with 6 parameters
                    var enemyFlagType = assembly.GetType("Il2CppAssets.Scripts.Actors.Enemies.EEnemyFlag");
                    
                    var allSpawnMethods = enemyManagerType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                        .Where(m => m.Name == "SpawnEnemy" && m.GetParameters().Length == 6)
                        .ToList();
                    
                    if (allSpawnMethods.Count > 0)
                    {
                        var spawnMethod = allSpawnMethods[0];
                        var noneFlag = System.Enum.ToObject(enemyFlagType, 0);
                    
                        var parameters = new object[] { cachedData, packet.Position, packet.Level, false, noneFlag, true };
                    
                        // Set flag to allow network spawn (bypasses Prefix blocking)
                        Game.Patches.EnemySyncPatches.EnemyManagerSpawnEnemyPatch.AllowNetworkSpawn = true;
                        
                        try
                        {
                            DebugLogger.Log($"[Client] Invoking SpawnEnemy with cached data: Type={packet.EnemyType}, Pos=({packet.Position.x}, {packet.Position.y}, {packet.Position.z}), Level={packet.Level}");
                            var spawnedEnemy = spawnMethod.Invoke(enemyManager, parameters);
                        
                            if (spawnedEnemy != null)
                            {
                                MelonLogger.Msg($"[Client] ✓ Successfully spawned enemy at ({packet.Position.x:F2}, {packet.Position.y:F2}, {packet.Position.z:F2})");
                                
                                // Register enemy ID mapping (client instance → host ID)
                                Game.Patches.EnemyIdMapper.RegisterMapping(spawnedEnemy, packet.EnemyId);
                            }
                            else
                            {
                                DebugLogger.Warning($"[Client] SpawnEnemy returned null");
                            }
                        }
                        finally
                        {
                            // Always reset flag after spawn attempt
                            Game.Patches.EnemySyncPatches.EnemyManagerSpawnEnemyPatch.AllowNetworkSpawn = false;
                        }
                        return;
                    }
                    else
                    {
                        DebugLogger.Error($"[Client] Could not find SpawnEnemy method");
                    }
                }
                else
                {
                    DebugLogger.Warning($"[Client] No cached EnemyData for type {packet.EnemyType}. Cache has {Game.Patches.EnemyDataCache.Count} entries.");
                    DebugLogger.Warning($"[Client] Waiting for at least one local enemy spawn to populate cache...");
                }

                // Fallback: just log the packet
                DebugLogger.Warning($"[Client] Enemy spawn packet received but could not spawn: Type={packet.EnemyType}, Pos=({packet.Position.x:F2}, {packet.Position.y:F2}, {packet.Position.z:F2}), Level={packet.Level}");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Error($"[Client] Failed to handle enemy spawn: {ex.Message}");
                DebugLogger.Error($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}
