using MelonLoader;
using Multibonk.Game.Handlers;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;
using System.Linq;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    /// <summary>
    /// Client-side handler for shrine use packets
    /// When server broadcasts a shrine use, client applies the shrine effect locally
    /// </summary>
    public class ShrineUsePacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.SHRINE_USE;

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new ShrineUsePacket(msg);

            MelonLogger.Msg($"[Client] Shrine used: {packet.ShrineId} (type {packet.ShrineType}) by player {packet.PlayerId}");

            // Queue the shrine activation to happen on the main Unity thread
            GameDispatcher.Enqueue(() =>
            {
                try
                {
                    // Find Assembly-CSharp
                    var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                        
                    if (assembly == null)
                    {
                        MelonLogger.Warning("[Client] Could not find Assembly-CSharp");
                        return;
                    }

                    // Get InteractableShrineBalance type
                    var shrineType = assembly.GetType("Il2Cpp.InteractableShrineBalance");
                    if (shrineType == null)
                    {
                        MelonLogger.Warning("[Client] Could not find InteractableShrineBalance type");
                        return;
                    }

                    // Find all shrines in the scene
                    var findObjectsMethod = typeof(UnityEngine.Object)
                        .GetMethods()
                        .Where(m => m.Name == "FindObjectsOfType" && m.IsGenericMethod && m.GetParameters().Length == 0)
                        .FirstOrDefault();

                    if (findObjectsMethod == null)
                    {
                        MelonLogger.Warning("[Client] Could not find FindObjectsOfType method");
                        return;
                    }

                    var genericMethod = findObjectsMethod.MakeGenericMethod(shrineType);
                    var shrines = (System.Array)genericMethod.Invoke(null, null);

                    if (shrines == null || shrines.Length == 0)
                    {
                        MelonLogger.Warning("[Client] No shrines found in scene");
                        return;
                    }

                    // Find the shrine with matching ID (hash code)
                    int targetId = int.Parse(packet.ShrineId);
                    foreach (var shrine in shrines)
                    {
                        if (shrine.GetHashCode() == targetId)
                        {
                            // Call Interact method on the shrine
                            var interactMethod = shrineType.GetMethod("Interact", 
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                
                            if (interactMethod != null)
                            {
                                interactMethod.Invoke(shrine, null);
                                MelonLogger.Msg($"[Client] ✓ Activated shrine {packet.ShrineId} locally");
                                return;
                            }
                        }
                    }

                    MelonLogger.Warning($"[Client] Could not find shrine with ID {packet.ShrineId}");
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"[Client] Failed to activate shrine: {ex.Message}");
                    MelonLogger.Error($"Stack: {ex.StackTrace}");
                }
            });
        }
    }
}
