using Il2Cpp;
using MelonLoader;
using Multibonk.Game;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;
using UnityEngine;

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

            try
            {
                // Find the Assembly-CSharp
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                    
                if (assembly == null)
                {
                    MelonLogger.Warning("[Client] Could not find Assembly-CSharp");
                    return;
                }

                // Get EnemyManager type
                var enemyManagerType = assembly.GetType("Il2CppAssets.Scripts.Managers.EnemyManager");
                if (enemyManagerType == null)
                {
                    MelonLogger.Warning("[Client] Could not find EnemyManager type");
                    return;
                }

                // Get EnemyManager.Instance
                var instanceProp = enemyManagerType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (instanceProp == null)
                {
                    MelonLogger.Warning("[Client] Could not find EnemyManager.Instance property");
                    return;
                }

                var enemyManager = instanceProp.GetValue(null);
                if (enemyManager == null)
                {
                    MelonLogger.Warning("[Client] EnemyManager.Instance is null - game not loaded yet");
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
                var enemyDataType = assembly.GetType("EnemyData");
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
                            
                            // Try to find matching enemy by type
                            foreach (var data in allEnemyData)
                            {
                                var typeFieldInfo = enemyDataType.GetField("Type");
                                var typePropInfo = enemyDataType.GetProperty("Type");
                                
                                if (typeFieldInfo != null || typePropInfo != null)
                                {
                                    object typeValue = null;
                                    if (typeFieldInfo != null)
                                        typeValue = typeFieldInfo.GetValue(data);
                                    else if (typePropInfo != null)
                                        typeValue = typePropInfo.GetValue(data);
                                        
                                    if (typeValue != null && (int)typeValue == packet.EnemyType)
                                    {
                                        MelonLogger.Msg($"[Client] Found matching EnemyData for type {packet.EnemyType}!");
                                        
                                        // Spawn the enemy with the found EnemyData
                                        var spawnMethod = enemyManagerType.GetMethod("SpawnEnemy", new[]
                                        {
                                            enemyDataType,
                                            typeof(Vector3),
                                            typeof(int),
                                            typeof(bool),
                                            assembly.GetType("Il2CppAssets.Scripts.Actors.Enemies.EEnemyFlag"),
                                            typeof(bool)
                                        });
                                        
                                        if (spawnMethod != null)
                                        {
                                            var enemyFlagType = assembly.GetType("Il2CppAssets.Scripts.Actors.Enemies.EEnemyFlag");
                                            var noneFlag = System.Enum.ToObject(enemyFlagType, 0); // EEnemyFlag.None = 0
                                            
                                            var parameters = new object[] { data, packet.Position, packet.Level, false, noneFlag, true };
                                            var spawnedEnemy = spawnMethod.Invoke(enemyManager, parameters);
                                            
                                            MelonLogger.Msg($"[Client] Successfully spawned enemy at ({packet.Position.x:F2}, {packet.Position.y:F2}, {packet.Position.z:F2})");
                                            return;
                                        }
                                    }
                                }
                            }
                            
                            MelonLogger.Warning($"[Client] Could not find EnemyData for type {packet.EnemyType}");
                        }
                        else
                        {
                            MelonLogger.Warning("[Client] No EnemyData objects found in memory");
                        }
                    }
                }

                // Fallback: just log the packet
                MelonLogger.Msg($"[Client] Enemy spawn packet received but could not spawn: Type={packet.EnemyType}, Pos=({packet.Position.x:F2}, {packet.Position.y:F2}, {packet.Position.z:F2}), Level={packet.Level}");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[Client] Failed to handle enemy spawn: {ex.Message}");
                MelonLogger.Error($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}
