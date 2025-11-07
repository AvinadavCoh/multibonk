using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using Multibonk.Networking.Lobby;

namespace Multibonk.Game.Patches
{
    /// <summary>
    /// Patches to synchronize gold/coin collection between players
    /// Uses PlayerInventory.goldInt property to track gold changes
    /// </summary>
    public static class PlayerGoldPatches
    {
        private static int lastGoldAmount = 0;

        /// <summary>
        /// Patch PlayerInventory.goldInt setter to detect when gold is added
        /// This is called whenever gold changes
        /// </summary>
        [HarmonyPatch(typeof(PlayerInventory), nameof(PlayerInventory.goldInt), MethodType.Setter)]
        [HarmonyPostfix]
        public static void AfterSetGold(PlayerInventory __instance, int value)
        {
            if (!LobbyPatchFlags.IsHosting) return;

            try
            {
                // Calculate gold gained (difference from last known amount)
                int goldGained = value - lastGoldAmount;
                lastGoldAmount = value;

                // Only broadcast positive gains (ignore losses/purchases)
                if (goldGained > 0)
                {
                    MelonLogger.Msg($"[Gold Patch] Player gained {goldGained} gold (total: {value})");
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
