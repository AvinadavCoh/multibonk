using HarmonyLib;
using MelonLoader;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Multibonk.Game.Patches
{
    /// <summary>
    /// Diagnostic patches to help identify missing class and method names.
    /// This runs once at game start and logs potential candidates for:
    /// - Wave Manager
    /// - Minimap / Fog of War
    /// - Loot / Items
    /// </summary>
    public static class DiagnosticsPatches
    {
        // NOTE: must be a bare [HarmonyPatch] - combining typeof/method arguments with
        // TargetMethod() throws "You cannot combine TargetMethod ... with individual annotations"
        [HarmonyPatch]
        class DiagnosticsPatch
        {
            static bool Prepare()
            {
                // Only run if we can find GameManager
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                return assembly?.GetType("Il2Cpp.GameManager") != null || 
                       assembly?.GetType("Il2CppAssets.Scripts.GameManager") != null ||
                       assembly?.GetType("GameManager") != null;
            }

            static MethodBase TargetMethod()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                
                if (assembly == null) return null;

                var type = assembly.GetType("Il2Cpp.GameManager") ?? 
                           assembly.GetType("Il2CppAssets.Scripts.GameManager") ?? 
                           assembly.GetType("GameManager");

                return type?.GetMethod("Start", BindingFlags.Public | BindingFlags.Instance);
            }

            static void Postfix()
            {
                MelonLogger.Msg("================ DIAGNOSTICS START ================");
                RunDiagnostics();
                MelonLogger.Msg("================ DIAGNOSTICS END ================");
            }
        }

        private static void RunDiagnostics()
        {
            var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

            if (assembly == null)
            {
                MelonLogger.Error("Could not find Assembly-CSharp");
                return;
            }

            MelonLogger.Msg("Scanning Assembly-CSharp for missing types...");

            var allTypes = assembly.GetTypes();

            // 1. Search for Wave Manager candidates
            SearchTypes(allTypes, new[] { "Wave", "Round", "Stage" }, "Wave Manager Candidates");

            // 2. Search for Minimap candidates
            SearchTypes(allTypes, new[] { "Minimap", "MapReveal", "FogOfWar", "MapRenderer" }, "Minimap Candidates");

            // 3. Search for Loot/Item candidates
            SearchTypes(allTypes, new[] { "Loot", "ItemDrop", "Pickup", "Treasure", "Collectible" }, "Loot/Item Candidates");
            
            // 4. Search for Gold/Coin candidates
            SearchTypes(allTypes, new[] { "Gold", "Coin", "Money", "Currency" }, "Gold/Coin Candidates");
        }

        private static void SearchTypes(System.Type[] types, string[] keywords, string category)
        {
            MelonLogger.Msg($"--- {category} ---");
            int count = 0;
            foreach (var type in types)
            {
                foreach (var keyword in keywords)
                {
                    if (type.Name.Contains(keyword, System.StringComparison.OrdinalIgnoreCase))
                    {
                        MelonLogger.Msg($"Found Type: {type.FullName}");
                        
                        // Log interesting methods
                        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                        {
                            if (method.Name.Contains("Start") || method.Name.Contains("Begin") || 
                                method.Name.Contains("End") || method.Name.Contains("Complete") ||
                                method.Name.Contains("Add") || method.Name.Contains("Remove") ||
                                method.Name.Contains("Spawn") || method.Name.Contains("Reveal") ||
                                method.Name.Contains("Drop") || method.Name.Contains("Create"))
                            {
                                MelonLogger.Msg($"  - Method: {method.Name}");
                            }
                        }

                        // Log interesting properties
                        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                        {
                            MelonLogger.Msg($"  - Property: {prop.Name} ({prop.PropertyType.Name})");
                        }

                        count++;
                        break; // Found one keyword, move to next type
                    }
                }
            }
            if (count == 0) MelonLogger.Msg("No candidates found.");
        }
    }
}
