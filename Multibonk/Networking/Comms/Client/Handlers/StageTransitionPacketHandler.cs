using Multibonk.Game.Handlers;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;
using MelonLoader;
using System;
using System.Linq;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    /// <summary>
    /// Client handler for stage transition (portal activation)
    /// When host activates portal, this finds InteractableBossSpawnerFinal and triggers Interact()
    /// The Interact() method internally calls DoLoadNextStage() coroutine
    /// </summary>
    public class StageTransitionPacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.STAGE_TRANSITION;

        public StageTransitionPacketHandler() { }

        public void Handle(IncomingMessage msg, Connection conn)
        {
            try
            {
                var packet = new StageTransitionPacket(msg);
                
                MelonLogger.Msg("[Client] Received stage transition (portal activation)");

                GameDispatcher.Enqueue(() =>
                {
                    try
                    {
                        // Find InteractableBossSpawnerFinal type via reflection
                        var assembly = AppDomain.CurrentDomain.GetAssemblies()
                            .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

                        if (assembly == null)
                        {
                            MelonLogger.Error("[Client] Could not find Assembly-CSharp for stage transition");
                            return;
                        }

                        var portalType = assembly.GetType("Il2Cpp.InteractableBossSpawnerFinal");
                        if (portalType == null)
                        {
                            MelonLogger.Error("[Client] Could not find InteractableBossSpawnerFinal type");
                            return;
                        }

                        // Find the portal in the scene
                        var findMethod = typeof(UnityEngine.Object).GetMethod("FindObjectOfType",
                            new[] { typeof(Type) });
                        
                        if (findMethod == null)
                        {
                            MelonLogger.Error("[Client] Could not find FindObjectOfType method");
                            return;
                        }

                        var portal = findMethod.Invoke(null, new object[] { portalType });
                        
                        if (portal == null)
                        {
                            MelonLogger.Warning("[Client] No portal (InteractableBossSpawnerFinal) found in scene");
                            return;
                        }

                        MelonLogger.Msg("[Client] Found portal in scene");

                        // Call Interact() method which triggers DoLoadNextStage() coroutine
                        var interactMethod = portalType.GetMethod("Interact");
                        if (interactMethod == null)
                        {
                            MelonLogger.Error("[Client] Could not find Interact method on InteractableBossSpawnerFinal");
                            return;
                        }

                        MelonLogger.Msg("[Client] Triggering portal interaction for stage transition");
                        var result = interactMethod.Invoke(portal, null);
                        
                        if (result is bool success && success)
                        {
                            MelonLogger.Msg("[Client] ✓ Stage transition started successfully");
                        }
                        else
                        {
                            MelonLogger.Warning("[Client] Portal Interact() returned false or null - transition may have already started");
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error($"[Client] Failed to trigger stage transition: {ex.Message}");
                        MelonLogger.Error($"Stack: {ex.StackTrace}");
                    }
                });
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Client] Error processing stage transition packet: {ex.Message}");
                MelonLogger.Error($"Stack: {ex.StackTrace}");
            }
        }
    }
}
