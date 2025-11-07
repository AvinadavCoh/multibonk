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

                    // TODO: Find the correct class for player gold/coins
                    // Likely candidates: PlayerInventory, CoinManager, GoldManager, PlayerWallet, etc.
                    var playerInventoryType = assembly.GetType("PlayerInventory") 
                        ?? assembly.GetType("Il2Cpp.PlayerInventory")
                        ?? assembly.GetType("Il2CppAssets.Scripts.PlayerInventory");

                    if (playerInventoryType == null)
                    {
                        MelonLogger.Warning("[Client] Could not find PlayerInventory type - need to find correct class with dnSpy");
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

                    // TODO: Find the correct method to add gold
                    // Likely method names: AddGold, AddCoins, AddMoney, GainGold, etc.
                    var addGoldMethod = playerInventoryType.GetMethod("AddGold") 
                        ?? playerInventoryType.GetMethod("AddCoins")
                        ?? playerInventoryType.GetMethod("AddMoney");

                    if (addGoldMethod == null)
                    {
                        MelonLogger.Warning("[Client] Could not find AddGold/AddCoins method - need to find with dnSpy");
                        MelonLogger.Msg("[Client] Available methods: " + string.Join(", ", 
                            playerInventoryType.GetMethods().Select(m => m.Name).Take(10)));
                        return;
                    }

                    // Apply the gold
                    addGoldMethod.Invoke(playerInventory, new object[] { packet.GoldAmount });
                    MelonLogger.Msg($"[Client] ✓ Applied {packet.GoldAmount} gold to local player");
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
