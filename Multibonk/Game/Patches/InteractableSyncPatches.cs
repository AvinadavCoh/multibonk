using System.Linq;
using HarmonyLib;
using MelonLoader;
using Multibonk.Networking.Lobby;

namespace Multibonk.Game.Patches
{
    /// <summary>
    /// Patches for synchronizing chest and shrine interactions
    /// Host interacts with chest/shrine → triggers GameEvent → broadcasts to clients
    /// </summary>
    public static class InteractableSyncPatches
    {
        /// <summary>
        /// Patches chest interaction/opening
        /// When a chest is opened, broadcast to all clients
        /// </summary>
        [HarmonyPatch]
        class ChestInteractPatch
        {
            static bool Prepare()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                    
                if (assembly == null)
                {
                    return false;
                }

                // Correct class name from dnSpy: Il2CppAssets.Scripts.Inventory__Items__Pickups.Chests.InteractableChest
                var chestType = assembly.GetType("Il2CppAssets.Scripts.Inventory__Items__Pickups.Chests.InteractableChest");
                if (chestType == null)
                {
                    return false;
                }

                // Method name from dnSpy: Interact (returns Boolean)
                var interactMethod = chestType.GetMethod("Interact", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (interactMethod != null)
                {
                    MelonLogger.Msg($"Found InteractableChest.Interact for patching");
                    return true;
                }

                return false;
            }

            static System.Reflection.MethodBase TargetMethod()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                    
                if (assembly == null)
                    return null;

                var chestType = assembly.GetType("Il2CppAssets.Scripts.Inventory__Items__Pickups.Chests.InteractableChest");
                if (chestType == null)
                    return null;

                return chestType.GetMethod("Interact", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            }

            static void Postfix(object __instance)
            {
                if (!LobbyPatchFlags.IsHosting)
                    return;

                try
                {
                    // Try to get chest ID from the instance
                    // This will need to be refined based on actual game structure
                    string chestId = __instance.GetHashCode().ToString();
                    
                    MelonLogger.Msg($"[Host] Chest opened: {chestId}");
                    GameEvents.TriggerOpenChest(chestId);
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"Failed to handle chest open: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Patches shrine interaction/usage
        /// When a shrine is used, broadcast to all clients
        /// </summary>
        [HarmonyPatch]
        class ShrineInteractPatch
        {
            static bool Prepare()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                    
                if (assembly == null)
                {
                    return false;
                }

                // Correct class name from dnSpy: Il2Cpp.InteractableShrineBalance (empty namespace)
                var shrineType = assembly.GetType("Il2Cpp.InteractableShrineBalance");
                if (shrineType == null)
                {
                    return false;
                }

                // Method name from dnSpy: Interact (returns Boolean)
                var interactMethod = shrineType.GetMethod("Interact", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (interactMethod != null)
                {
                    MelonLogger.Msg($"Found InteractableShrineBalance.Interact for patching");
                    return true;
                }

                return false;
            }

            static System.Reflection.MethodBase TargetMethod()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                    
                if (assembly == null)
                    return null;

                var shrineType = assembly.GetType("Il2Cpp.InteractableShrineBalance");
                if (shrineType == null)
                    return null;

                return shrineType.GetMethod("Interact", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            }

            static void Postfix(object __instance)
            {
                if (!LobbyPatchFlags.IsHosting)
                    return;

                try
                {
                    MelonLogger.Msg($"[Host] Shrine used");
                    GameEvents.TriggerUseShrine();
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"Failed to handle shrine use: {ex.Message}");
                }
            }
        }
    }
}
