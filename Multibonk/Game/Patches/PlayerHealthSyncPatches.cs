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
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                    
                if (assembly == null)
                {
                    return false;
                }

                // Correct class name from dnSpy: Il2CppAssets.Scripts.Inventory__Items__Pickups.PlayerHealth
                var playerHealthType = assembly.GetType("Il2CppAssets.Scripts.Inventory__Items__Pickups.PlayerHealth");
                if (playerHealthType == null)
                {
                    return false;
                }

                // Method name from dnSpy: DamagePlayer (takes Enemy, Vector3, DcFlags)
                var damageMethod = playerHealthType.GetMethod("DamagePlayer", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (damageMethod != null)
                {
                    MelonLogger.Msg($"Found PlayerHealth.DamagePlayer for patching");
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

                var playerHealthType = assembly.GetType("Il2CppAssets.Scripts.Inventory__Items__Pickups.PlayerHealth");
                if (playerHealthType == null)
                    return null;

                return playerHealthType.GetMethod("DamagePlayer", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
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
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                    
                if (assembly == null)
                {
                    return false;
                }

                // Correct class name from dnSpy: Il2CppAssets.Scripts.Inventory__Items__Pickups.PlayerHealth
                var playerHealthType = assembly.GetType("Il2CppAssets.Scripts.Inventory__Items__Pickups.PlayerHealth");
                if (playerHealthType == null)
                {
                    return false;
                }

                // Method name from dnSpy: PlayerDied (void, no parameters)
                var diedMethod = playerHealthType.GetMethod("PlayerDied", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (diedMethod != null)
                {
                    MelonLogger.Msg($"Found PlayerHealth.PlayerDied for patching");
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

                var playerHealthType = assembly.GetType("Il2CppAssets.Scripts.Inventory__Items__Pickups.PlayerHealth");
                if (playerHealthType == null)
                    return null;

                return playerHealthType.GetMethod("PlayerDied", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
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
