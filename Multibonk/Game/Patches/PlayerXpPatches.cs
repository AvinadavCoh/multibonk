using HarmonyLib;
using Il2Cpp;
using Il2CppAssets.Scripts.Actors.Player;
using MelonLoader;

namespace Multibonk.Game.Patches
{
    /// <summary>
    /// Patches to intercept XP and level changes in the game
    /// </summary>
    public static class PlayerXpPatches
    {
        /// <summary>
        /// Patch for PlayerInventory.AddXp to broadcast XP gains to other players
        /// Triggers after the local player gains XP
        /// </summary>
        [HarmonyPatch(typeof(PlayerInventory), nameof(PlayerInventory.AddXp))]
        [HarmonyPostfix]
        public static void AfterAddXp(int xp)
        {
            try
            {
                MelonLogger.Msg($"[XP Patch] Player gained {xp} XP");
                GameEvents.TriggerPlayerXpGained(xp);
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Error in AfterAddXp patch: {ex}");
            }
        }

        /// <summary>
        /// Patch for MyPlayer.OnLevelUp to broadcast level ups to other players
        /// Triggers after the local player levels up (particles/SFX play)
        /// </summary>
        [HarmonyPatch(typeof(MyPlayer), nameof(MyPlayer.OnLevelUp))]
        [HarmonyPostfix]
        public static void AfterOnLevelUp(MyPlayer __instance)
        {
            try
            {
                var inventory = __instance.inventory;
                if (inventory != null)
                {
                    int newLevel = inventory.GetCharacterLevel();
                    MelonLogger.Msg($"[XP Patch] Player leveled up to level {newLevel}");
                    GameEvents.TriggerPlayerLevelUp(newLevel);
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Error in AfterOnLevelUp patch: {ex}");
            }
        }
        // 4. Uncomment the patches above
    }
}
