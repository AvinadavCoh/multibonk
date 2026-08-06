using HarmonyLib;
using MelonLoader;
using Multibonk.Networking.Lobby;
using System.Linq;
using UnityEngine.SceneManagement;

namespace Multibonk.Game.Patches
{
    /// <summary>
    /// Patches to handle game restarts and scene transitions.
    /// Ensures game state is cleared when the host restarts the game.
    /// </summary>
    public static class RestartPatches
    {
        /// <summary>
        /// Everything that must be forgotten when a run ends, in one place.
        ///
        /// The three entry points below (scene load, retry, restart) used to clear
        /// different subsets - scene load skipped pickups and fog - so state leaked
        /// between runs depending on how the previous one ended. On the host a stale
        /// pickup id makes DespawnPickupPatch drop a legitimate despawn broadcast.
        ///
        /// Telemetry and the run coordinator reset on BOTH sides; the rest is host-only
        /// because only the host owns that state.
        /// </summary>
        private static void ClearRunState()
        {
            SyncDigestPatches.Reset();
            Networking.Comms.Client.Handlers.StateDigestPacketHandler.Reset();
            RunCoordinator.Reset();

            if (!LobbyPatchFlags.IsHosting)
                return;

            GamePatchFlags.ClearGameState();
            EnemyDataCache.Clear();
            EnemyIdMapper.Clear();
            ItemDropPatches.Clear();
            MinimapSyncPatches.Clear();
        }

        /// <summary>
        /// Patches SceneManager.LoadScene to clear game state when loading a new scene.
        /// </summary>
        [HarmonyPatch(typeof(SceneManager), "LoadScene", new System.Type[] { typeof(string), typeof(LoadSceneMode) })]
        class LoadScenePatch
        {
            static void Prefix(string sceneName, LoadSceneMode mode)
            {
                MelonLogger.Msg($"[RestartPatches] Loading scene: {sceneName}, Mode: {mode}");

                // If loading the MainMenu or a Gameplay scene, we might need to clear state
                // Assuming "MainMenu" is the name of the menu scene
                if (sceneName.Contains("Menu") || sceneName.Contains("Map"))
                {
                    MelonLogger.Msg("[RestartPatches] Loading menu/map - clearing run state");
                    ClearRunState();
                }
            }
        }

        /// <summary>
        /// Patches GameManager.Retry (if it exists) to clear game state.
        /// </summary>
        [HarmonyPatch]
        class GameManagerRetryPatch
        {
            static bool Prepare()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

                if (assembly == null) return false;

                var gameManagerType = assembly.GetType("Il2Cpp.GameManager") ?? 
                                      assembly.GetType("Il2CppAssets.Scripts.GameManager") ?? 
                                      assembly.GetType("GameManager");

                if (gameManagerType == null) return false;

                var retryMethod = gameManagerType.GetMethod("Retry", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (retryMethod != null)
                {
                    MelonLogger.Msg($"Found GameManager.Retry for patching");
                    return true;
                }

                return false;
            }

            static System.Reflection.MethodBase TargetMethod()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

                if (assembly == null) return null;

                var gameManagerType = assembly.GetType("Il2Cpp.GameManager") ?? 
                                      assembly.GetType("Il2CppAssets.Scripts.GameManager") ?? 
                                      assembly.GetType("GameManager");

                if (gameManagerType == null) return null;

                return gameManagerType.GetMethod("Retry", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            }

            static void Prefix()
            {
                MelonLogger.Msg("[RestartPatches] Retrying game - clearing run state");
                ClearRunState();
            }
        }

        /// <summary>
        /// Patches GameManager.Restart (if it exists) to clear game state.
        /// </summary>
        [HarmonyPatch]
        class GameManagerRestartPatch
        {
            static bool Prepare()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

                if (assembly == null) return false;

                var gameManagerType = assembly.GetType("Il2Cpp.GameManager") ?? 
                                      assembly.GetType("Il2CppAssets.Scripts.GameManager") ?? 
                                      assembly.GetType("GameManager");

                if (gameManagerType == null) return false;

                var restartMethod = gameManagerType.GetMethod("Restart", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (restartMethod != null)
                {
                    MelonLogger.Msg($"Found GameManager.Restart for patching");
                    return true;
                }

                return false;
            }

            static System.Reflection.MethodBase TargetMethod()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

                if (assembly == null) return null;

                var gameManagerType = assembly.GetType("Il2Cpp.GameManager") ?? 
                                      assembly.GetType("Il2CppAssets.Scripts.GameManager") ?? 
                                      assembly.GetType("GameManager");

                if (gameManagerType == null) return null;

                return gameManagerType.GetMethod("Restart", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            }

            static void Prefix()
            {
                MelonLogger.Msg("[RestartPatches] Restarting game - clearing run state");
                ClearRunState();
            }
        }
    }
}
