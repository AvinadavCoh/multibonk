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

                // Try using Resources.FindObjectsOfTypeAll to find all EnemyData ScriptableObjects
                var enemyDataType = assembly.GetType("Il2Cpp.EnemyData");
                if (enemyDataType != null)
                {
                    MelonLogger.Msg($"[Client] Attempting to find EnemyData resources...");
                    
                    // Try to find all EnemyData assets loaded in memory
                    var resourcesType = typeof(UnityEngine.Resources);
                    var findMethod = resourcesType.GetMethod("FindObjectsOfTypeAll");
                    if (findMethod != null)
                    {
                        var genericMethod = findMethod.MakeGenericMethod(enemyDataType);
                        var allEnemyData = genericMethod.Invoke(null, null) as System.Array;
                        
                        if (allEnemyData != null && allEnemyData.Length > 0)
                        {
                            MelonLogger.Msg($"[Client] Found {allEnemyData.Length} EnemyData objects in memory");
                            
                            // Try to find matching enemy by enemyName enum (EEnemy type)
                            foreach (var data in allEnemyData)
                            {
                                // Look for enemyName property (EEnemy enum type)
                                var enemyNameProp = enemyDataType.GetProperty("enemyName");
                                
                                if (enemyNameProp != null)
                                {
                                    var enemyNameValue = enemyNameProp.GetValue(data);
                                    
                                    if (enemyNameValue != null)
                                    {
                                        int enumIntValue = (int)enemyNameValue;
                                        DebugLogger.Log($"[Client] Checking EnemyData: enemyName enum value = {enemyNameValue} (int: {enumIntValue})");
                                        
                                        if (enumIntValue == packet.EnemyType)
                                        {
                                            MelonLogger.Msg($"[Client] ✓ Found matching EnemyData for type {packet.EnemyType} (enemyName: {enemyNameValue})!");
                                        
                                            // Find the SpawnEnemy method with 6 parameters (avoiding ambiguity)
                                            var enemyFlagType = assembly.GetType("Il2CppAssets.Scripts.Actors.Enemies.EEnemyFlag");
                                            
                                            var allSpawnMethods = enemyManagerType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                                                .Where(m => m.Name == "SpawnEnemy" && m.GetParameters().Length == 6)
                                                .ToList();
                                            
                                            DebugLogger.Log($"[Client] Found {allSpawnMethods.Count} SpawnEnemy methods with 6 parameters");
                                            
                                            if (allSpawnMethods.Count > 0)
                                            {
                                                var spawnMethod = allSpawnMethods[0]; // Use the first one
                                                var noneFlag = System.Enum.ToObject(enemyFlagType, 0); // EEnemyFlag.None = 0
                                            
                                                var parameters = new object[] { data, packet.Position, packet.Level, false, noneFlag, true };
                                            
                                                DebugLogger.Log($"[Client] Invoking SpawnEnemy with: EnemyType={packet.EnemyType}, Pos=({packet.Position.x}, {packet.Position.y}, {packet.Position.z}), Level={packet.Level}");
                                                var spawnedEnemy = spawnMethod.Invoke(enemyManager, parameters);
                                            
                                                if (spawnedEnemy != null)
                                                {
                                                    DebugLogger.Log($"[Client] ✓ Successfully spawned enemy at ({packet.Position.x:F2}, {packet.Position.y:F2}, {packet.Position.z:F2})");
                                                }
                                                else
                                                {
                                                    DebugLogger.Warning($"[Client] SpawnEnemy returned null");
                                                }
                                                return;
                                            }
                                            else
                                            {
                                                DebugLogger.Error($"[Client] Could not find SpawnEnemy method with 6 parameters on EnemyManager");
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    DebugLogger.Warning($"[Client] Could not find enemyName property on EnemyData");
                                }
                            }
                            
                            DebugLogger.Warning($"[Client] Could not find EnemyData with enemyName enum value {packet.EnemyType}");
                        }
                        else
                        {
                            DebugLogger.Warning("[Client] No EnemyData objects found in memory");
                        }
                    }
                    else
                    {
                        DebugLogger.Error("[Client] Could not find Resources.FindObjectsOfTypeAll method");
                    }
                }
                else
                {
                    DebugLogger.Error("[Client] Could not find EnemyData type");
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
