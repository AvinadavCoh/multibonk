using System.Linq;
using HarmonyLib;
using MelonLoader;
using Multibonk.Networking.Lobby;

namespace Multibonk.Game.Patches
{
    /// <summary>
    /// Patches for synchronizing player damage and death events.
    ///
    /// PlayerTakeDamagePatch   — both sides: fires GameEvents.TriggerPlayerTakeHit so the
    ///                           damage is routed to other players via the network layer.
    ///
    /// PlayerDeathPatch        — both sides (in multiplayer): fires GameEvents.TriggerPlayerDie
    ///                           so each side can report its death upstream.
    ///
    /// GameOverPatch           — all machines: suppresses GameManager.OnDied() in multiplayer
    ///                           until RunCoordinator.AllowGameOver is set (meaning every
    ///                           lobby player has been confirmed dead).
    /// </summary>
    public static class PlayerHealthSyncPatches
    {
        // ─────────────────────────────────────────────────────────────────────
        // PlayerTakeDamagePatch
        // ─────────────────────────────────────────────────────────────────────
        [HarmonyPatch]
        class PlayerTakeDamagePatch
        {
            static bool Prepare()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

                if (assembly == null)
                    return false;

                var playerHealthType = assembly.GetType("Il2CppAssets.Scripts.Inventory__Items__Pickups.PlayerHealth");
                if (playerHealthType == null)
                    return false;

                var damageMethod = playerHealthType.GetMethod("DamagePlayer",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (damageMethod != null)
                {
                    MelonLogger.Msg("Found PlayerHealth.DamagePlayer for patching");
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

            static void Prefix(object __instance, ref float __state)
            {
                __state = ReadFloat(__instance, "hp");
            }

            static void Postfix(object __instance, float __state)
            {
                if (!LobbyPatchFlags.InMultiplayer)
                    return;

                try
                {
                    float current = ReadFloat(__instance, "hp");
                    float max = ReadFloat(__instance, "maxHp");
                    float damage = __state - current;

                    if (damage <= 0f)
                        return;

                    DebugLogger.Log($"[Health] Local player took {damage:F1} damage ({current:F1}/{max:F1})");
                    GameEvents.TriggerPlayerTakeHit(current, max, damage);
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"Failed to handle player damage: {ex.Message}");
                }
            }

            static float ReadFloat(object instance, string name)
            {
                try
                {
                    var prop = instance.GetType().GetProperty(name);
                    if (prop != null)
                        return System.Convert.ToSingle(prop.GetValue(instance));

                    var field = instance.GetType().GetField(name);
                    if (field != null)
                        return System.Convert.ToSingle(field.GetValue(instance));
                }
                catch { }
                return 0f;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // PlayerDeathPatch
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Fires on both host and client whenever the local player's HP hits zero.
        /// Triggers GameEvents.PlayerDieEvent so:
        ///   • the host's PlayerDeathEventHandler can mark itself dead and check all-dead,
        ///   • the client's PlayerDiedReportEventHandler can send PLAYER_DIED_PACKET upstream.
        /// </summary>
        [HarmonyPatch]
        class PlayerDeathPatch
        {
            static bool Prepare()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

                if (assembly == null)
                    return false;

                var playerHealthType = assembly.GetType("Il2CppAssets.Scripts.Inventory__Items__Pickups.PlayerHealth");
                if (playerHealthType == null)
                    return false;

                var diedMethod = playerHealthType.GetMethod("PlayerDied",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (diedMethod != null)
                {
                    MelonLogger.Msg("Found PlayerHealth.PlayerDied for patching");
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
                // Fire for every player in multiplayer (not just the host).
                // Host → PlayerDeathEventHandler handles the all-dead check.
                // Client → PlayerDiedReportEventHandler sends PLAYER_DIED_PACKET.
                if (!LobbyPatchFlags.InMultiplayer)
                    return;

                try
                {
                    MelonLogger.Msg("[Player] Local player died - triggering death event");
                    GameEvents.TriggerPlayerDie();
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"Failed to handle player death: {ex.Message}");
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // GameOverPatch
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Suppresses GameManager.OnDied() (the game-over trigger) in multiplayer
        /// until RunCoordinator.AllowGameOver is set.
        ///
        /// The gate is opened by:
        ///   • PlayerDeathEventHandler / PlayerDiedServerPacketHandler on the host
        ///     (when all lobby players are confirmed dead), or
        ///   • RunOverPacketHandler on clients (on receipt of RUN_OVER from the host).
        ///
        /// After opening, GameManager.OnDied() is called explicitly so the game-over
        /// screen appears exactly once.
        ///
        /// Target: Il2Cpp.GameManager.OnDied()  (confirmed in API dump)
        /// </summary>
        [HarmonyPatch]
        class GameOverPatch
        {
            static bool Prepare()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

                if (assembly == null)
                    return false;

                // Confirmed class name from API dump: Il2Cpp.GameManager
                var gameManagerType = assembly.GetType("Il2Cpp.GameManager");
                if (gameManagerType == null)
                {
                    // DEFECT 6: loud error — a silent Warning meant this failure was invisible
                    // in normal log scans, allowing the mod to ship in a state where every
                    // player death immediately ends the run (single-player behaviour restored).
                    MelonLogger.Error(
                        "[GameOverPatch] Il2Cpp.GameManager not found — multiplayer run-end gating is DISABLED. " +
                        "The game will revert to ending on the first death. Check the assembly name in the API dump.");
                    return false;
                }

                // Confirmed method name from API dump: OnDied (void, no params)
                var onDiedMethod = gameManagerType.GetMethod("OnDied",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (onDiedMethod == null)
                {
                    MelonLogger.Error(
                        "[GameOverPatch] GameManager.OnDied not found — multiplayer run-end gating is DISABLED. " +
                        "The game will revert to ending on the first death. Check the method signature in the API dump.");
                    return false;
                }

                MelonLogger.Msg("[GameOverPatch] Found Il2Cpp.GameManager.OnDied - game-over gate enabled");
                return true;
            }

            static System.Reflection.MethodBase TargetMethod()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

                if (assembly == null)
                    return null;

                var gameManagerType = assembly.GetType("Il2Cpp.GameManager");
                if (gameManagerType == null)
                    return null;

                return gameManagerType.GetMethod("OnDied",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            }

            /// <summary>
            /// Returns false (block) when we are in multiplayer and the host has not yet
            /// confirmed that all players are dead.
            /// Returns true (allow) in single player, or once the gate is open.
            /// </summary>
            static bool Prefix()
            {
                if (!LobbyPatchFlags.InMultiplayer)
                    return true; // single player - always allow

                if (RunCoordinator.AllowGameOver)
                {
                    MelonLogger.Msg("[GameOverPatch] Gate open - allowing GameManager.OnDied");
                    return true;
                }

                MelonLogger.Msg("[GameOverPatch] Blocking GameManager.OnDied - waiting for all players to die");
                return false;
            }
        }
    }
}
