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
                // Try to find the chest class
                return true;
            }

            static System.Reflection.MethodBase TargetMethod()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                    
                if (assembly == null)
                {
                    MelonLogger.Warning("Could not find Assembly-CSharp for ChestInteractPatch");
                    return null;
                }

                // Try multiple possible chest class names
                string[] possibleClasses = new[] 
                { 
                    "Il2Cpp.InteractableChest",
                    "Il2CppAssets.Scripts.Interactables.InteractableChest",
                    "InteractableChest",
                    "Chest",
                    "Il2Cpp.Chest"
                };

                foreach (var className in possibleClasses)
                {
                    var chestType = assembly.GetType(className);
                    if (chestType != null)
                    {
                        // Try to find Open or Interact method
                        var openMethod = chestType.GetMethod("Open", 
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        
                        if (openMethod == null)
                        {
                            openMethod = chestType.GetMethod("Interact", 
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        }

                        if (openMethod != null)
                        {
                            MelonLogger.Msg($"Found {className}.{openMethod.Name} for chest patching");
                            return openMethod;
                        }
                    }
                }

                MelonLogger.Warning("Could not find chest interaction method - chest sync disabled");
                return null;
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
                // Try to find the shrine class
                return true;
            }

            static System.Reflection.MethodBase TargetMethod()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                    
                if (assembly == null)
                {
                    MelonLogger.Warning("Could not find Assembly-CSharp for ShrineInteractPatch");
                    return null;
                }

                // Try multiple possible shrine class names
                string[] possibleClasses = new[] 
                { 
                    "Il2Cpp.InteractableShrine",
                    "Il2CppAssets.Scripts.Interactables.InteractableShrine",
                    "InteractableShrine",
                    "Shrine",
                    "Il2Cpp.Shrine"
                };

                foreach (var className in possibleClasses)
                {
                    var shrineType = assembly.GetType(className);
                    if (shrineType != null)
                    {
                        // Try to find Use or Interact method
                        var useMethod = shrineType.GetMethod("Use", 
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        
                        if (useMethod == null)
                        {
                            useMethod = shrineType.GetMethod("Interact", 
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        }

                        if (useMethod != null)
                        {
                            MelonLogger.Msg($"Found {className}.{useMethod.Name} for shrine patching");
                            return useMethod;
                        }
                    }
                }

                MelonLogger.Warning("Could not find shrine interaction method - shrine sync disabled");
                return null;
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
