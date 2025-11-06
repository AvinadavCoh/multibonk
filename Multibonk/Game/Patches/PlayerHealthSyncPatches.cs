using System.Linq;
using HarmonyLib;
using MelonLoader;
using Multibonk.Networking.Lobby;

namespace Multibonk.Game.Patches
{
    /// <summary>
    /// Patches for synchronizing player damage and death events
    /// Host takes damage/dies → triggers GameEvent → broadcasts to clients
    /// </summary>
    public static class PlayerHealthSyncPatches
    {
        /// <summary>
        /// Patches player damage/hit detection
        /// When host player takes damage, broadcast to all clients
        /// </summary>
        [HarmonyPatch]
        class PlayerTakeDamagePatch
        {
            static bool Prepare()
            {
                return true;
            }

            static System.Reflection.MethodBase TargetMethod()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                    
                if (assembly == null)
                {
                    MelonLogger.Warning("Could not find Assembly-CSharp for PlayerTakeDamagePatch");
                    return null;
                }

                // Try multiple possible player/health class names
                string[] possibleClasses = new[] 
                { 
                    "Il2Cpp.Player",
                    "Il2CppAssets.Scripts.Player.Player",
                    "Il2Cpp.PlayerController",
                    "Il2Cpp.PlayerHealth",
                    "Player",
                    "PlayerController",
                    "PlayerHealth"
                };

                foreach (var className in possibleClasses)
                {
                    var playerType = assembly.GetType(className);
                    if (playerType != null)
                    {
                        // Try to find damage/hit methods
                        var damageMethod = playerType.GetMethod("TakeDamage", 
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        
                        if (damageMethod == null)
                        {
                            damageMethod = playerType.GetMethod("Damage", 
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        }

                        if (damageMethod == null)
                        {
                            damageMethod = playerType.GetMethod("Hit", 
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        }

                        if (damageMethod != null)
                        {
                            MelonLogger.Msg($"Found {className}.{damageMethod.Name} for player damage patching");
                            return damageMethod;
                        }
                    }
                }

                MelonLogger.Warning("Could not find player damage method - player damage sync disabled");
                return null;
            }

            static void Postfix(object __instance)
            {
                if (!LobbyPatchFlags.IsHosting)
                    return;

                try
                {
                    MelonLogger.Msg($"[Host] Player took damage");
                    GameEvents.TriggerPlayerTakeHit();
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"Failed to handle player damage: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Patches player death
        /// When host player dies, broadcast to all clients
        /// In multiplayer: player stays in lobby, game continues until all dead
        /// </summary>
        [HarmonyPatch]
        class PlayerDeathPatch
        {
            static bool Prepare()
            {
                return true;
            }

            static System.Reflection.MethodBase TargetMethod()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                    
                if (assembly == null)
                {
                    MelonLogger.Warning("Could not find Assembly-CSharp for PlayerDeathPatch");
                    return null;
                }

                // Try multiple possible player class names
                string[] possibleClasses = new[] 
                { 
                    "Il2Cpp.Player",
                    "Il2CppAssets.Scripts.Player.Player",
                    "Il2Cpp.PlayerController",
                    "Il2Cpp.PlayerHealth",
                    "Player",
                    "PlayerController",
                    "PlayerHealth"
                };

                foreach (var className in possibleClasses)
                {
                    var playerType = assembly.GetType(className);
                    if (playerType != null)
                    {
                        // Try to find death methods
                        var deathMethod = playerType.GetMethod("Die", 
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        
                        if (deathMethod == null)
                        {
                            deathMethod = playerType.GetMethod("Death", 
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        }

                        if (deathMethod == null)
                        {
                            deathMethod = playerType.GetMethod("OnDeath", 
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        }

                        if (deathMethod != null)
                        {
                            MelonLogger.Msg($"Found {className}.{deathMethod.Name} for player death patching");
                            return deathMethod;
                        }
                    }
                }

                MelonLogger.Warning("Could not find player death method - player death sync disabled");
                return null;
            }

            static void Postfix(object __instance)
            {
                if (!LobbyPatchFlags.IsHosting)
                    return;

                try
                {
                    MelonLogger.Msg($"[Host] Player died");
                    GameEvents.TriggerPlayerDie();
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"Failed to handle player death: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Optional: Patch game over logic to only trigger when ALL players are dead
        /// This prevents early game over in multiplayer
        /// </summary>
        [HarmonyPatch]
        class GameOverPatch
        {
            static bool Prepare()
            {
                // Only enable this if we want to modify game over logic
                return false; // Disabled for now - can enable later if needed
            }

            static System.Reflection.MethodBase TargetMethod()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                    
                if (assembly == null)
                    return null;

                // Try to find game over/game manager class
                string[] possibleClasses = new[] 
                { 
                    "Il2Cpp.GameManager",
                    "Il2CppAssets.Scripts.GameManager",
                    "GameManager"
                };

                foreach (var className in possibleClasses)
                {
                    var managerType = assembly.GetType(className);
                    if (managerType != null)
                    {
                        var gameOverMethod = managerType.GetMethod("GameOver", 
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        
                        if (gameOverMethod != null)
                        {
                            MelonLogger.Msg($"Found {className}.GameOver for game over patching");
                            return gameOverMethod;
                        }
                    }
                }

                return null;
            }

            static bool Prefix()
            {
                // In multiplayer, prevent game over until all players are dead
                if (LobbyPatchFlags.InMultiplayer)
                {
                    MelonLogger.Msg("[GameOver] Blocked - multiplayer mode, checking if all players dead");
                    // TODO: Check if all players in lobby are dead
                    // Return false to cancel game over if any players alive
                    // Return true to allow game over if all dead
                    return true; // For now, allow game over normally
                }
                
                return true; // Allow game over in single player
            }
        }
    }
}
