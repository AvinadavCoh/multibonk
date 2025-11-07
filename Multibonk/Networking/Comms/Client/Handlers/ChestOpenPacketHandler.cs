using MelonLoader;
using Multibonk.Game.Handlers;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;
using System.Linq;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    /// <summary>
    /// Client-side handler for chest open packets
    /// When server broadcasts a chest opening, client opens their local chest
    /// </summary>
    public class ChestOpenPacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.CHEST_OPEN;

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new ChestOpenPacket(msg);

            MelonLogger.Msg($"[Client] Chest opened: {packet.ChestId} by player {packet.PlayerId}");

            // Queue the chest opening to happen on the main Unity thread
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

                    // Get InteractableChest type
                    var chestType = assembly.GetType("Il2CppAssets.Scripts.Inventory__Items__Pickups.Chests.InteractableChest");
                    if (chestType == null)
                    {
                        MelonLogger.Warning("[Client] Could not find InteractableChest type");
                        return;
                    }

                    // Find all chests in the scene
                    var findObjectsMethod = typeof(UnityEngine.Object)
                        .GetMethods()
                        .Where(m => m.Name == "FindObjectsOfType" && m.IsGenericMethod && m.GetParameters().Length == 0)
                        .FirstOrDefault();

                    if (findObjectsMethod == null)
                    {
                        MelonLogger.Warning("[Client] Could not find FindObjectsOfType method");
                        return;
                    }

                    var genericMethod = findObjectsMethod.MakeGenericMethod(chestType);
                    var chests = (System.Array)genericMethod.Invoke(null, null);

                    if (chests == null || chests.Length == 0)
                    {
                        MelonLogger.Warning("[Client] No chests found in scene");
                        return;
                    }

                    // Find the chest with matching ID (hash code)
                    int targetId = int.Parse(packet.ChestId);
                    foreach (var chest in chests)
                    {
                        if (chest.GetHashCode() == targetId)
                        {
                            // Call Interact method on the chest
                            var interactMethod = chestType.GetMethod("Interact", 
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                
                            if (interactMethod != null)
                            {
                                interactMethod.Invoke(chest, null);
                                MelonLogger.Msg($"[Client] ✓ Opened chest {packet.ChestId} locally");
                                return;
                            }
                        }
                    }

                    MelonLogger.Warning($"[Client] Could not find chest with ID {packet.ChestId}");
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"[Client] Failed to open chest: {ex.Message}");
                    MelonLogger.Error($"Stack: {ex.StackTrace}");
                }
            });
        }
    }
}
