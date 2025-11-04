using HarmonyLib;
using MelonLoader;
using Multibonk.Networking.Lobby;

namespace Multibonk.Game.Patches
{
    /// <summary>
    /// Patches to intercept enemy health changes and death events.
    /// 
    /// IMPLEMENTATION STRATEGY:
    /// 1. Find enemy health/damage system in game code
    /// 2. Patch the methods that modify enemy health
    /// 3. Trigger our sync events
    /// 
    /// IDEAL ARCHITECTURE:
    /// - Patch at the lowest level (health modification)
    /// - Don't patch visual/UI updates (too frequent)
    /// - Use Postfix patches to ensure game logic runs first
    /// 
    /// COMMON PATTERNS TO LOOK FOR (in dnSpy):
    /// - Enemy.TakeDamage(float amount)
    /// - Enemy.Die() or Enemy.Kill()
    /// - EnemyHealth.ModifyHealth(float delta)
    /// - HealthComponent.Damage(DamageInfo info)
    /// 
    /// OPTIMIZATION NOTES:
    /// - Only host should broadcast (check LobbyPatchFlags.IsHosting)
    /// - Cache enemy references to avoid repeated lookups
    /// - Use object pooling for frequent packet creation
    /// </summary>
    public static class EnemySyncPatches
    {
        // TODO: Find the actual enemy health/damage classes in Assembly-CSharp.dll
        
        /*
        [HarmonyPatch(typeof(Enemy), "TakeDamage")] // TODO: Find actual class/method
        class EnemyTakeDamagePatch
        {
            static void Postfix(Enemy __instance, float damage)
            {
                if (!LobbyPatchFlags.IsHosting)
                    return;

                // Get enemy identifier (might be instance ID, name, or custom ID)
                string enemyId = __instance.GetInstanceID().ToString();
                
                // Get current health after damage
                float currentHealth = __instance.currentHealth;
                float maxHealth = __instance.maxHealth;

                // Trigger health changed event
                GameEvents.TriggerEnemyHealthChanged(enemyId, currentHealth, maxHealth);
            }
        }
        */

        /*
        [HarmonyPatch(typeof(Enemy), "Die")] // TODO: Find actual method
        class EnemyDiePatch
        {
            static void Postfix(Enemy __instance)
            {
                if (!LobbyPatchFlags.IsHosting)
                    return;

                string enemyId = __instance.GetInstanceID().ToString();
                
                MelonLogger.Msg($"Enemy died: {enemyId}");
                GameEvents.TriggerEnemyDie(enemyId);
            }
        }
        */

        // ALTERNATIVE APPROACH: If the game uses a centralized damage system
        /*
        [HarmonyPatch(typeof(DamageSystem), "ApplyDamage")] 
        class DamageSystemPatch
        {
            static void Postfix(GameObject target, float damage)
            {
                if (!LobbyPatchFlags.IsHosting)
                    return;

                // Check if target is an enemy
                var enemy = target.GetComponent<Enemy>();
                if (enemy != null)
                {
                    string enemyId = enemy.GetInstanceID().ToString();
                    GameEvents.TriggerEnemyHealthChanged(
                        enemyId, 
                        enemy.currentHealth, 
                        enemy.maxHealth
                    );
                }
            }
        }
        */

        // TODO: Steps to implement:
        // 1. Open Assembly-CSharp.dll in dnSpy
        // 2. Search for keywords: "Enemy", "Health", "Damage", "Die", "Kill"
        // 3. Find the classes/methods that handle enemy health/death
        // 4. Update the [HarmonyPatch] attributes above with correct names
        // 5. Adjust field/property names based on actual game code
        // 6. Uncomment the patches
        // 7. Test with debug commands (F10)
    }
}
