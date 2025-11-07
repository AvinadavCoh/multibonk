using HarmonyLib;
using MelonLoader;
using Multibonk.Game;
using System.Linq;

namespace Multibonk.Game.Patches
{
    /// <summary>
    /// Patches to synchronize gold/coin collection between players
    /// 
    /// IMPLEMENTATION STATUS: Needs dnSpy investigation
    /// 
    /// TODO: Find the correct class and method that handles gold pickup
    /// Search in dnSpy for:
    /// - "gold", "coin", "money", "currency"
    /// - "AddGold", "AddCoins", "CollectGold", "PickupCoin"
    /// - Classes like: CoinPickup, GoldPickup, CollectibleCoin, etc.
    /// 
    /// Expected method signature (example):
    /// public void CollectCoin(int amount) 
    /// or
    /// private void OnCoinPickup(CoinData coin)
    /// 
    /// Once found, update the patch below with correct type and method name.
    /// </summary>
    public static class PlayerGoldPatches
    {
        /*
        [HarmonyPatch] // TODO: Add correct type and method
        class CollectGoldPatch
        {
            static MethodBase TargetMethod()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                
                if (assembly == null)
                {
                    MelonLogger.Warning("Could not find Assembly-CSharp for gold patch");
                    return null;
                }

                // TODO: Find the correct class
                var coinPickupType = assembly.GetType("Il2Cpp.CoinPickup") 
                    ?? assembly.GetType("Il2CppAssets.Scripts.CoinPickup")
                    ?? assembly.GetType("Il2Cpp.CollectibleCoin");

                if (coinPickupType == null)
                {
                    MelonLogger.Warning("Could not find CoinPickup type - gold sync disabled");
                    return null;
                }

                // TODO: Find the correct method
                var collectMethod = coinPickupType.GetMethod("Collect")
                    ?? coinPickupType.GetMethod("OnPickup")
                    ?? coinPickupType.GetMethod("AddGold");

                if (collectMethod == null)
                {
                    MelonLogger.Warning("Could not find gold collection method - gold sync disabled");
                    return null;
                }

                MelonLogger.Msg($"Found {coinPickupType.Name}.{collectMethod.Name} for gold sync patching");
                return collectMethod;
            }

            // Prefix: Block gold collection on client
            static bool Prefix()
            {
                // Only host should collect gold (clients will receive via packet)
                if (!LobbyPatchFlags.IsHosting)
                {
                    MelonLogger.Msg("[Client] Blocked local gold collection (will receive from server)");
                    return false; // Block execution
                }
                return true; // Allow host to collect
            }

            // Postfix: Broadcast gold gain to all clients
            static void Postfix(int goldAmount) // TODO: Match actual parameter
            {
                if (!LobbyPatchFlags.IsHosting) return;

                MelonLogger.Msg($"[Host] Player collected {goldAmount} gold, broadcasting...");
                GameEvents.TriggerPlayerGoldGained(goldAmount);
            }
        }
        */

        // ALTERNATIVE: If gold is added directly to player inventory
        /*
        [HarmonyPatch] // TODO: Add correct type and method
        class AddGoldToInventoryPatch
        {
            static MethodBase TargetMethod()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                
                if (assembly == null) return null;

                // TODO: Find PlayerInventory or similar
                var inventoryType = assembly.GetType("Il2Cpp.PlayerInventory")
                    ?? assembly.GetType("Il2CppAssets.Scripts.PlayerInventory");

                if (inventoryType == null)
                {
                    MelonLogger.Warning("Could not find PlayerInventory for gold patch");
                    return null;
                }

                // TODO: Find the method that adds gold
                var addGoldMethod = inventoryType.GetMethod("AddGold")
                    ?? inventoryType.GetMethod("AddCoins")
                    ?? inventoryType.GetMethod("AddMoney");

                if (addGoldMethod == null)
                {
                    MelonLogger.Warning("Could not find AddGold method - gold sync disabled");
                    return null;
                }

                MelonLogger.Msg($"Found {inventoryType.Name}.{addGoldMethod.Name} for gold sync patching");
                return addGoldMethod;
            }

            static void Postfix(int amount) // TODO: Match actual parameter type
            {
                if (!LobbyPatchFlags.IsHosting) return;

                MelonLogger.Msg($"[Host] Gold added to inventory: {amount}, broadcasting...");
                GameEvents.TriggerPlayerGoldGained(amount);
            }
        }
        */
    }
}
