using Multibonk.Game.Handlers;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;
using MelonLoader;
using System;
using System.Linq;
using UnityEngine;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    /// <summary>
    /// Client handler for boss spawner activation
    /// When host activates bush boss spawner, this finds the corresponding spawner and triggers Interact()
    /// </summary>
    public class BossSpawnerActivatePacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.BOSS_SPAWNER_ACTIVATE;

        public BossSpawnerActivatePacketHandler() { }

        public void Handle(IncomingMessage msg, Connection conn)
        {
            try
            {
                var packet = new BossSpawnerActivatePacket(msg);
                
                MelonLogger.Msg($"[Client] Received boss spawner activation at position ({packet.Position.x}, {packet.Position.y}, {packet.Position.z})");

                GameDispatcher.Enqueue(() =>
                {
                    try
                    {
                        // Find InteractableBossSpawner type via reflection
                        var assembly = AppDomain.CurrentDomain.GetAssemblies()
                            .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

                        if (assembly == null)
                        {
                            MelonLogger.Error("[Client] Could not find Assembly-CSharp for boss spawner activation");
                            return;
                        }

                        var bossSpawnerType = assembly.GetType("Il2Cpp.InteractableBossSpawner");
                        if (bossSpawnerType == null)
                        {
                            MelonLogger.Error("[Client] Could not find InteractableBossSpawner type");
                            return;
                        }

                        // Find all boss spawners in scene
                        var findMethod = typeof(UnityEngine.Object).GetMethod("FindObjectsOfType",
                            new[] { typeof(Type) });
                        
                        if (findMethod == null)
                        {
                            MelonLogger.Error("[Client] Could not find FindObjectsOfType method");
                            return;
                        }

                        var spawners = findMethod.Invoke(null, new object[] { bossSpawnerType });
                        if (spawners == null)
                        {
                            MelonLogger.Warning("[Client] No boss spawners found in scene");
                            return;
                        }

                        // Convert to array and find closest spawner to packet position
                        var spawnersArray = ((Array)spawners).Cast<object>().ToArray();
                        MelonLogger.Msg($"[Client] Found {spawnersArray.Length} boss spawners in scene");

                        object closestSpawner = null;
                        float closestDistance = float.MaxValue;

                        foreach (var spawner in spawnersArray)
                        {
                            // Get transform and position
                            var transform = bossSpawnerType.GetProperty("transform")?.GetValue(spawner);
                            if (transform == null) continue;

                            var positionProp = transform.GetType().GetProperty("position");
                            if (positionProp == null) continue;

                            var spawnerPos = (Vector3)positionProp.GetValue(transform);
                            float distance = Vector3.Distance(spawnerPos, packet.Position);

                            if (distance < closestDistance)
                            {
                                closestDistance = distance;
                                closestSpawner = spawner;
                            }
                        }

                        if (closestSpawner == null)
                        {
                            MelonLogger.Error("[Client] Could not find boss spawner near packet position");
                            return;
                        }

                        if (closestDistance > 5f)
                        {
                            MelonLogger.Warning($"[Client] Closest boss spawner is {closestDistance}m away - might be wrong spawner");
                        }

                        // Call Interact() method on the boss spawner
                        var interactMethod = bossSpawnerType.GetMethod("Interact");
                        if (interactMethod == null)
                        {
                            MelonLogger.Error("[Client] Could not find Interact method on InteractableBossSpawner");
                            return;
                        }

                        MelonLogger.Msg($"[Client] Activating boss spawner (distance: {closestDistance:F2}m)");
                        
                        // Allow network interaction
                        Game.Patches.BossSyncPatches.AllowNetworkInteract = true;
                        object result = null;
                        try
                        {
                            result = interactMethod.Invoke(closestSpawner, null);
                        }
                        finally
                        {
                            Game.Patches.BossSyncPatches.AllowNetworkInteract = false;
                        }
                        
                        if (result is bool success && success)
                        {
                            MelonLogger.Msg("[Client] ✓ Boss spawner activated successfully");
                        }
                        else
                        {
                            MelonLogger.Warning("[Client] Boss spawner Interact() returned false or null");
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error($"[Client] Failed to activate boss spawner: {ex.Message}");
                        MelonLogger.Error($"Stack: {ex.StackTrace}");
                    }
                });
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Client] Error processing boss spawner activation packet: {ex.Message}");
                MelonLogger.Error($"Stack: {ex.StackTrace}");
            }
        }
    }
}
