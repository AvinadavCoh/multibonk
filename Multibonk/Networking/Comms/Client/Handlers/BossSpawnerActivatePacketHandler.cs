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

                // Map the type byte to the exact C# class name so we only search spawners
                // of the same type the host actually activated (0=regular, 1=final).
                string targetTypeName = packet.SpawnerType == 1
                    ? "Il2Cpp.InteractableBossSpawnerFinal"
                    : "Il2Cpp.InteractableBossSpawner";

                MelonLogger.Msg($"[Client] Received boss spawner activation: type={packet.SpawnerType} ({targetTypeName}) " +
                                $"at ({packet.Position.x}, {packet.Position.y}, {packet.Position.z})");

                GameDispatcher.Enqueue(() =>
                {
                    try
                    {
                        var assembly = AppDomain.CurrentDomain.GetAssemblies()
                            .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

                        if (assembly == null)
                        {
                            MelonLogger.Error("[Client] Could not find Assembly-CSharp for boss spawner activation");
                            return;
                        }

                        var findMethod = typeof(UnityEngine.Object).GetMethod("FindObjectsOfType",
                            new[] { typeof(Type) });

                        if (findMethod == null)
                        {
                            MelonLogger.Error("[Client] Could not find FindObjectsOfType method");
                            return;
                        }

                        // Search ONLY among spawners of the type the host activated.
                        var spawnerType = assembly.GetType(targetTypeName);
                        if (spawnerType == null)
                        {
                            MelonLogger.Error($"[Client] Could not find spawner type '{targetTypeName}'");
                            return;
                        }

                        var spawners = findMethod.Invoke(null, new object[] { spawnerType });
                        if (spawners == null)
                        {
                            MelonLogger.Error($"[Client] FindObjectsOfType returned null for '{targetTypeName}'");
                            return;
                        }

                        var interactMethod = spawnerType.GetMethod("Interact");
                        if (interactMethod == null)
                        {
                            MelonLogger.Error($"[Client] No Interact() method on '{targetTypeName}'");
                            return;
                        }

                        object closestSpawner = null;
                        float closestDistance = float.MaxValue;

                        foreach (var spawner in ((Array)spawners).Cast<object>())
                        {
                            var transform = spawnerType.GetProperty("transform")?.GetValue(spawner);
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
                            MelonLogger.Error($"[Client] Could not find any '{targetTypeName}' near packet position");
                            return;
                        }

                        if (closestDistance > 5f)
                        {
                            MelonLogger.Warning($"[Client] Closest {targetTypeName} is {closestDistance:F2}m away - might be wrong spawner");
                        }

                        MelonLogger.Msg($"[Client] Activating {targetTypeName} (distance: {closestDistance:F2}m)");

                        // Allow network interaction — both patches check this flag before blocking
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
                            MelonLogger.Msg("[Client] Boss spawner activated successfully");
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
