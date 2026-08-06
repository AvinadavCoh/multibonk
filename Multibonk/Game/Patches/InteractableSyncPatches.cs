using System.Collections.Generic;
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
        /// Set to true while a network-sourced shrine Interact() call is in progress so
        /// the Postfix does not re-broadcast it, preventing an infinite loop.
        /// </summary>
        public static bool ApplyingNetworkShrine = false;

        /// <summary>
        /// Set to true while a network-sourced chest Interact() call is in progress so
        /// the Postfix does not re-broadcast it, preventing an infinite loop.
        /// </summary>
        public static bool ApplyingNetworkChest = false;
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
                // Suppressed when we are the ones calling Interact() from a network packet.
                if (!LobbyPatchFlags.IsHosting || ApplyingNetworkChest)
                    return;

                try
                {
                    // Derive a stable cross-machine identity from the chest's world position
                    // (quantized to whole units).  Map generation is seed-synced, so chests
                    // spawn at identical positions on host and client.
                    var transformProp = __instance.GetType().GetProperty("transform");
                    if (transformProp == null)
                    {
                        MelonLogger.Warning("[Host] ChestInteractPatch: could not find 'transform' property");
                        return;
                    }

                    var transform = transformProp.GetValue(__instance);
                    if (transform == null)
                    {
                        MelonLogger.Warning("[Host] ChestInteractPatch: transform is null");
                        return;
                    }

                    var posProp = transform.GetType().GetProperty("position");
                    if (posProp == null)
                    {
                        MelonLogger.Warning("[Host] ChestInteractPatch: could not find 'position' property");
                        return;
                    }

                    var position = (UnityEngine.Vector3)posProp.GetValue(transform);
                    int qx = (int)System.Math.Round((double)position.x);
                    int qy = (int)System.Math.Round((double)position.y);
                    int qz = (int)System.Math.Round((double)position.z);
                    string chestId = $"{qx}_{qy}_{qz}";

                    MelonLogger.Msg($"[Host] Chest opened: {chestId} at ({position.x:F1},{position.y:F1},{position.z:F1})");
                    GameEvents.TriggerOpenChest(chestId);
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"Failed to handle chest open: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Patches Interact() on all six shrine types.
        /// Derives a stable cross-machine identity from the shrine's world position
        /// (quantized to whole units) so the client can locate the matching object.
        /// </summary>
        [HarmonyPatch]
        class ShrineInteractPatch
        {
            // Ordered list used both to build target methods and to encode shrineType.
            // Index 0..5 maps to the int sent in ShrineUsePacket.ShrineType.
            internal static readonly string[] ShrineClassNames = new[]
            {
                "Il2Cpp.InteractableShrineBalance",    // 0
                "Il2Cpp.InteractableShrineChallenge",  // 1
                "Il2Cpp.InteractableShrineCursed",     // 2
                "Il2Cpp.InteractableShrineGreed",      // 3
                "Il2Cpp.InteractableShrineMagnet",     // 4
                "Il2Cpp.InteractableShrineMoai",       // 5
            };

            static bool Prepare()
            {
                // Abort gracefully if the game assembly isn't loaded yet.
                return System.AppDomain.CurrentDomain.GetAssemblies()
                    .Any(a => a.GetName().Name == "Assembly-CSharp");
            }

            static IEnumerable<System.Reflection.MethodBase> TargetMethods()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                if (assembly == null)
                    yield break;

                foreach (var className in ShrineClassNames)
                {
                    var t = assembly.GetType(className);
                    if (t == null) continue;

                    var m = t.GetMethod("Interact",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (m == null) continue;

                    MelonLogger.Msg($"[ShrineInteractPatch] Patching {className}.Interact");
                    yield return m;
                }
            }

            static void Postfix(object __instance)
            {
                // Suppressed when we are the ones calling Interact() from a network packet.
                if (!LobbyPatchFlags.IsHosting || ApplyingNetworkShrine)
                    return;

                try
                {
                    // --- Determine shrine type index from the concrete runtime type name ---
                    string runtimeName = __instance.GetType().Name; // e.g. "InteractableShrineBalance"
                    int shrineType = 0;
                    for (int i = 0; i < ShrineClassNames.Length; i++)
                    {
                        // ShrineClassNames[i] is "Il2Cpp.InteractableShrineBalance"; short name after '.'
                        string shortName = ShrineClassNames[i].Substring(ShrineClassNames[i].LastIndexOf('.') + 1);
                        if (runtimeName == shortName)
                        {
                            shrineType = i;
                            break;
                        }
                    }

                    // --- Get world position via reflection (same pattern as BossSpawnerActivatePacketHandler) ---
                    var transformProp = __instance.GetType().GetProperty("transform");
                    if (transformProp == null)
                    {
                        MelonLogger.Warning("[Host] ShrineInteractPatch: could not find 'transform' property");
                        return;
                    }

                    var transform = transformProp.GetValue(__instance);
                    if (transform == null)
                    {
                        MelonLogger.Warning("[Host] ShrineInteractPatch: transform is null");
                        return;
                    }

                    var posProp = transform.GetType().GetProperty("position");
                    if (posProp == null)
                    {
                        MelonLogger.Warning("[Host] ShrineInteractPatch: could not find 'position' property");
                        return;
                    }

                    var position = (UnityEngine.Vector3)posProp.GetValue(transform);

                    MelonLogger.Msg($"[Host] Shrine used: {runtimeName} at ({position.x:F1},{position.y:F1},{position.z:F1})");
                    GameEvents.TriggerUseShrine(position, shrineType);
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"[Host] ShrineInteractPatch failed: {ex.Message}");
                }
            }
        }
    }
}
