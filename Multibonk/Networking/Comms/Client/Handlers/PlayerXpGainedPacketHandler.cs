using MelonLoader;
using Multibonk.Game.Handlers;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;
using System.Linq;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    public class PlayerXpGainedPacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.PLAYER_XP_GAINED_PACKET;

        public PlayerXpGainedPacketHandler()
        {
        }

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new PlayerXpGainedPacket(msg);

            MelonLogger.Msg($"[Client] Player {packet.PlayerId} gained {packet.XpAmount} XP");

            // Apply XP to the local player's inventory
            GameDispatcher.Enqueue(() =>
            {
                try
                {
                    // Check XP sharing mode preference
                    var xpSharingMode = Preferences.GetXpSharingMode();
                    if (xpSharingMode != Preferences.LootDistributionMode.Shared)
                    {
                        MelonLogger.Msg($"[Client] XP sharing is {xpSharingMode}, not applying remote XP");
                        return;
                    }

                    // Find Assembly-CSharp
                    var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                        
                    if (assembly == null)
                    {
                        MelonLogger.Warning("[Client] Could not find Assembly-CSharp");
                        return;
                    }

                    // Get PlayerInventory type
                    var playerInventoryType = assembly.GetType("PlayerInventory");
                    if (playerInventoryType == null)
                    {
                        // Try with Il2Cpp namespace
                        playerInventoryType = assembly.GetType("Il2Cpp.PlayerInventory");
                    }
                    
                    if (playerInventoryType == null)
                    {
                        MelonLogger.Warning("[Client] Could not find PlayerInventory type");
                        return;
                    }

                    // Find PlayerInventory instance
                    var findObjectMethod = typeof(UnityEngine.Object)
                        .GetMethods()
                        .Where(m => m.Name == "FindObjectOfType" && m.IsGenericMethod && m.GetParameters().Length == 0)
                        .FirstOrDefault();

                    if (findObjectMethod == null)
                    {
                        MelonLogger.Warning("[Client] Could not find FindObjectOfType method");
                        return;
                    }

                    var genericMethod = findObjectMethod.MakeGenericMethod(playerInventoryType);
                    var playerInventory = genericMethod.Invoke(null, null);

                    if (playerInventory == null)
                    {
                        MelonLogger.Warning("[Client] Could not find PlayerInventory instance");
                        return;
                    }

                    // Call AddXp method
                    var addXpMethod = playerInventoryType.GetMethod("AddXp", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        
                    if (addXpMethod == null)
                    {
                        MelonLogger.Warning("[Client] Could not find AddXp method");
                        return;
                    }

                    addXpMethod.Invoke(playerInventory, new object[] { packet.XpAmount });
                    MelonLogger.Msg($"[Client] ✓ Applied {packet.XpAmount} XP to local player");
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"[Client] Failed to apply XP: {ex.Message}");
                }
            });
        }
    }
}
