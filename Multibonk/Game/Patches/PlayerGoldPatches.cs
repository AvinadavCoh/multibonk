using HarmonyLib;
using Il2Cpp;
using Il2CppAssets.Scripts.Actors.Player;
using MelonLoader;
using Multibonk.Networking.Lobby;
using System.Linq;
using System.Reflection;

namespace Multibonk.Game.Patches
{
    /// <summary>
    /// Patches to synchronize gold collection between players (Shared mode).
    /// Detection: PlayerInventory.goldInt setter postfix computes the gained delta.
    /// Application: ApplyNetworkGold adds to PlayerInventory.gold (the float master value,
    /// which keeps goldInt consistent) with a suppression flag so network gold is never
    /// re-broadcast (which would loop).
    /// </summary>
    public static class PlayerGoldPatches
    {
        private static int lastGoldAmount = 0;

        /// <summary>
        /// True while gold received from the network is being applied locally.
        /// </summary>
        public static bool ApplyingNetworkGold = false;

        /// <summary>
        /// Applies gold that another player earned, without re-broadcasting it.
        /// Must be called on the main Unity thread.
        /// </summary>
        public static void ApplyNetworkGold(int amount)
        {
            try
            {
                var inventory = MyPlayer.Instance?.inventory;
                if (inventory == null)
                {
                    MelonLogger.Warning("[Gold] Cannot apply shared gold - local player inventory not found");
                    return;
                }

                ApplyingNetworkGold = true;
                try
                {
                    inventory.gold = inventory.gold + amount;
                }
                finally
                {
                    ApplyingNetworkGold = false;
                }

                DebugLogger.Log($"[Gold] Applied {amount} shared gold from another player");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[Gold] Failed to apply shared gold: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch PlayerInventory.goldInt setter to detect when gold is added
        /// </summary>
        [HarmonyPatch]
        class PlayerGoldSetterPatch
        {
            static bool Prepare()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

                if (assembly == null) return false;

                var type = assembly.GetType("Il2Cpp.PlayerInventory") ??
                           assembly.GetType("Il2CppAssets.Scripts.Inventory__Items__Pickups.PlayerInventory") ??
                           assembly.GetType("PlayerInventory");

                if (type == null)
                {
                    MelonLogger.Warning("[PlayerGoldPatches] Could not find PlayerInventory type");
                    return false;
                }

                var prop = type.GetProperty("goldInt");
                if (prop == null || prop.SetMethod == null)
                {
                    MelonLogger.Warning("[PlayerGoldPatches] Could not find goldInt property or setter");
                    return false;
                }

                return true;
            }

            static MethodBase TargetMethod()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

                if (assembly == null) return null;

                var type = assembly.GetType("Il2Cpp.PlayerInventory") ??
                           assembly.GetType("Il2CppAssets.Scripts.Inventory__Items__Pickups.PlayerInventory") ??
                           assembly.GetType("PlayerInventory");

                if (type == null) return null;

                var prop = type.GetProperty("goldInt");
                return prop?.SetMethod;
            }

            static void Postfix(object __instance, int value)
            {
                try
                {
                    // Always track the latest value so deltas stay correct,
                    // even when the change came from the network.
                    int goldGained = value - lastGoldAmount;
                    lastGoldAmount = value;

                    if (!LobbyPatchFlags.InMultiplayer || ApplyingNetworkGold)
                        return;

                    // Only broadcast positive gains (ignore losses/purchases)
                    if (goldGained > 0)
                    {
                        DebugLogger.Log($"[Gold Patch] Local player gained {goldGained} gold (total: {value})");
                        GameEvents.TriggerPlayerGoldGained(goldGained);
                    }
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"Error in AfterSetGold patch: {ex}");
                }
            }
        }
    }
}
