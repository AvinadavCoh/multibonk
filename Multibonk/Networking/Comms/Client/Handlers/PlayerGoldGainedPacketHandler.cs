using System.Linq;
using MelonLoader;
using Multibonk.Game.Handlers;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    /// <summary>
    /// Client-side handler for gold gain packets
    /// When server broadcasts a gold pickup, client applies the gold locally based on sharing mode
    /// 
    /// TEST: 
    /// 1. Set GoldSharingMode to "Shared" in preferences
    /// 2. Host picks up gold
    /// 3. Both players should receive the gold amount
    /// 4. Check logs for "[Client] ✓ Applied X gold to local player"
    /// </summary>
    public class PlayerGoldGainedPacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.PLAYER_GOLD_GAINED;

        public PlayerGoldGainedPacketHandler() { }

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new PlayerGoldGainedPacket(msg);

            MelonLogger.Msg($"[Client] Received gold gain packet: {packet.GoldAmount} gold");

            // Queue the gold application to happen on the main Unity thread
            GameDispatcher.Enqueue(() =>
            {
                try
                {
                    // Check gold sharing mode
                    var goldSharingMode = Preferences.GetGoldSharingMode();
                    if (goldSharingMode != Preferences.LootDistributionMode.Shared)
                    {
                        MelonLogger.Msg($"[Client] Gold sharing is {goldSharingMode}, not applying remote gold");
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
                        playerInventoryType = assembly.GetType("Il2Cpp.PlayerInventory");
                    }

                    if (playerInventoryType == null)
                    {
                        MelonLogger.Warning("[Client] Could not find PlayerInventory type");
                        return;
                    }

                    // Find the PlayerInventory instance
                    var findObjectMethod = typeof(UnityEngine.Object).GetMethods()
                        .Where(m => m.Name == "FindObjectOfType" && m.IsGenericMethod)
                        .FirstOrDefault();

                    if (findObjectMethod == null)
                    {
                        MelonLogger.Warning("[Client] Could not find FindObjectOfType method");
                        return;
                    }

                    var playerInventory = findObjectMethod.MakeGenericMethod(playerInventoryType).Invoke(null, null);
                    if (playerInventory == null)
                    {
                        MelonLogger.Warning("[Client] Could not find PlayerInventory instance");
                        return;
                    }

                    // Get current gold
                    var goldIntProp = playerInventoryType.GetProperty("goldInt");
                    if (goldIntProp == null)
                    {
                        MelonLogger.Warning("[Client] Could not find goldInt property");
                        return;
                    }

                    int currentGold = (int)goldIntProp.GetValue(playerInventory);
                    int newGold = currentGold + packet.GoldAmount;

                    // Set new gold amount
                    goldIntProp.SetValue(playerInventory, newGold);
                    MelonLogger.Msg($"[Client] ✓ Applied {packet.GoldAmount} gold to local player (total: {newGold})");
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"[Client] Failed to apply gold: {ex.Message}");
                    MelonLogger.Error($"[Client] Stack trace: {ex.StackTrace}");
                }
            });
        }
    }
}
