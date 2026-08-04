using HarmonyLib;
using Il2Cpp;
using Il2CppAssets.Scripts.Actors.Player;
using MelonLoader;
using Multibonk.Networking.Lobby;

namespace Multibonk.Game.Patches
{
    /// <summary>
    /// Patches to intercept XP and level changes in the game.
    ///
    /// Shared XP (RoR2-style): whenever any player gains XP, every player gains it.
    /// - The AddXp postfix raises PlayerXpGainedEvent (host broadcasts it; clients send it
    ///   to the host, which applies it and relays it to the other clients).
    /// - ApplyingNetworkXp suppresses the postfix while network XP is being applied,
    ///   otherwise applying remote XP would rebroadcast it in an infinite loop.
    /// </summary>
    public static class PlayerXpPatches
    {
        /// <summary>
        /// True while XP received from the network is being applied locally.
        /// Prevents the AddXp postfix from re-broadcasting it.
        /// </summary>
        public static bool ApplyingNetworkXp = false;

        /// <summary>
        /// Applies XP that another player earned, without re-broadcasting it.
        /// Must be called on the main Unity thread.
        /// </summary>
        public static void ApplyNetworkXp(int amount)
        {
            try
            {
                var inventory = MyPlayer.Instance?.inventory;
                if (inventory == null)
                {
                    MelonLogger.Warning("[XP] Cannot apply shared XP - local player inventory not found");
                    return;
                }

                ApplyingNetworkXp = true;
                try
                {
                    inventory.AddXp(amount);
                }
                finally
                {
                    ApplyingNetworkXp = false;
                }

                DebugLogger.Log($"[XP] Applied {amount} shared XP from another player");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[XP] Failed to apply shared XP: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch for PlayerInventory.AddXp to broadcast XP gains to other players
        /// </summary>
        [HarmonyPatch(typeof(PlayerInventory), nameof(PlayerInventory.AddXp))]
        [HarmonyPostfix]
        public static void AfterAddXp(int xp)
        {
            try
            {
                if (!LobbyPatchFlags.InMultiplayer)
                    return;

                // XP that came from the network must not be re-broadcast
                if (ApplyingNetworkXp)
                    return;

                DebugLogger.Log($"[XP Patch] Local player gained {xp} XP");
                GameEvents.TriggerPlayerXpGained(xp);
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Error in AfterAddXp patch: {ex}");
            }
        }

        /// <summary>
        /// Patch for MyPlayer.OnLevelUp to broadcast level ups to other players
        /// </summary>
        [HarmonyPatch(typeof(MyPlayer), nameof(MyPlayer.OnLevelUp))]
        [HarmonyPostfix]
        public static void AfterOnLevelUp(MyPlayer __instance)
        {
            try
            {
                if (!LobbyPatchFlags.InMultiplayer)
                    return;

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
    }
}
