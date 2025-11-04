using HarmonyLib;
using MelonLoader;
using Multibonk.Networking.Lobby;
using UnityEngine;

namespace Multibonk.Game.Patches
{
    /// <summary>
    /// Patches to intercept item drop and pickup events in the game
    /// </summary>
    public static class ItemDropPatches
    {
        // TODO: Find the actual game methods that handle item dropping
        // Likely in classes like: ItemManager, DropSystem, LootManager, etc.
        
        /*
        [HarmonyPatch(typeof(ItemManager), "SpawnItem")] // TODO: Find actual class/method
        class SpawnItemPatch
        {
            static void Postfix(string itemId, Vector3 position, int itemType)
            {
                if (!LobbyPatchFlags.IsHosting)
                    return;

                MelonLogger.Msg($"Item spawned: {itemId} at {position}");
                GameEvents.TriggerSpawnDrop(itemId, position, itemType);
            }
        }
        */

        /*
        [HarmonyPatch(typeof(MyPlayer), "PickupItem")] // TODO: Find actual method
        class PickupItemPatch
        {
            static void Postfix(string itemId)
            {
                if (!LobbyPatchFlags.IsHosting)
                    return;

                MelonLogger.Msg($"Item picked up: {itemId}");
                // Send pickup notification to all clients
            }
        }
        */

        // TODO: To implement these patches:
        // 1. Use dnSpy to inspect Assembly-CSharp.dll
        // 2. Search for: "Drop", "Spawn", "Item", "Loot", "Pickup"
        // 3. Find methods related to item spawning/collection
        // 4. Update the [HarmonyPatch] attributes above
        // 5. Uncomment the patches
    }
}
