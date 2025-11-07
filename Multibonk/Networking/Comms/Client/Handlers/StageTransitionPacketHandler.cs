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
    /// When host activates portal, this finds InteractablePortal or InteractablePortalFinal and triggers Interact()
    /// Works for both stage transitions (1→2, 2→3) and final game completion
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
                        // Find portal types via reflection
                        var assembly = AppDomain.CurrentDomain.GetAssemblies()
                            .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

                        if (assembly == null)
                        {
                            MelonLogger.Error("[Client] Could not find Assembly-CSharp for stage transition");
                            return;
                        }

                        // Try to find InteractablePortal (stages 1→2, 2→3)
                        var portalType = assembly.GetType("Il2Cpp.InteractablePortal");
                        var portalFinalType = assembly.GetType("Il2Cpp.InteractablePortalFinal");

                        if (portalType == null || portalFinalType == null)
                        {
                            MelonLogger.Error("[Client] Could not find portal types");
                            return;
                        }

                        // Try to find InteractablePortal first (more common)
                        var findMethod = typeof(UnityEngine.Object).GetMethod("FindObjectOfType",
                            new[] { typeof(Type) });
                        
                        if (findMethod == null)
                        {
                            MelonLogger.Error("[Client] Could not find FindObjectOfType method");
                            return;
                        }

                        object portal = findMethod.Invoke(null, new object[] { portalType });
                        Type activePortalType = portalType;
                        
                        // If no regular portal, try final portal
                        if (portal == null)
                        {
                            portal = findMethod.Invoke(null, new object[] { portalFinalType });
                            activePortalType = portalFinalType;
                            
                            if (portal == null)
                            {
                                MelonLogger.Warning("[Client] No portal found in scene (InteractablePortal or InteractablePortalFinal)");
                                return;
                            }
                            
                            MelonLogger.Msg("[Client] Found InteractablePortalFinal in scene");
                        }
                        else
                        {
                            MelonLogger.Msg("[Client] Found InteractablePortal in scene");
                        }

                        // Call Interact() method which triggers DoLoadNextStage() or DoFinishGame()
                        var interactMethod = activePortalType.GetMethod("Interact");
                        if (interactMethod == null)
                        {
                            MelonLogger.Error("[Client] Could not find Interact method on portal");
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
