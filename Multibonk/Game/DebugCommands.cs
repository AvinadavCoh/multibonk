using MelonLoader;
using UnityEngine;

namespace Multibonk.Game
{
    /// <summary>
    /// Debug commands for testing multiplayer features when you only have one client
    /// </summary>
    public static class DebugCommands
    {
        public static void CheckInput()
        {
            // F6 - Simulate gaining 50 XP
            if (Input.GetKeyDown(KeyCode.F6))
            {
                MelonLogger.Msg("[DEBUG] Simulating +50 XP gain");
                GameEvents.TriggerPlayerXpGained(50);
            }

            // F7 - Simulate level up
            if (Input.GetKeyDown(KeyCode.F7))
            {
                int newLevel = 5; // Simulate leveling up to level 5
                MelonLogger.Msg($"[DEBUG] Simulating level up to level {newLevel}");
                GameEvents.TriggerPlayerLevelUp(newLevel);
            }

            // F8 - Print current network state
            if (Input.GetKeyDown(KeyCode.F8))
            {
                string state = Networking.Lobby.LobbyPatchFlags.IsHosting ? "Hosting" : "Not Hosting";
                MelonLogger.Msg($"[DEBUG] Network State: {state}");
            }

            // F9 - Simulate item drop
            if (Input.GetKeyDown(KeyCode.F9))
            {
                string itemId = $"item_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
                Vector3 position = new Vector3(10f, 5f, 10f); // Random position for testing
                int itemType = 1; // Example item type
                
                MelonLogger.Msg($"[DEBUG] Simulating item drop: {itemId} at {position}");
                GameEvents.TriggerSpawnDrop(itemId, position, itemType, 1);
            }

            // F10 - Simulate enemy taking damage (boss scenario)
            if (Input.GetKeyDown(KeyCode.F10))
            {
                string enemyId = "test_boss_001";
                float maxHealth = 10000f;
                
                // Simulate progressive damage (50% -> 40% -> 30% -> death)
                if (!_testBossHealth.ContainsKey(enemyId))
                {
                    _testBossHealth[enemyId] = maxHealth;
                }

                float currentHealth = _testBossHealth[enemyId];
                currentHealth -= 1000f; // Deal 1000 damage (10% of max)

                if (currentHealth > 0)
                {
                    _testBossHealth[enemyId] = currentHealth;
                    MelonLogger.Msg($"[DEBUG] Boss takes damage: {currentHealth}/{maxHealth}");
                    GameEvents.TriggerEnemyHealthChanged(enemyId, currentHealth, maxHealth);
                }
                else
                {
                    MelonLogger.Msg($"[DEBUG] Boss dies!");
                    GameEvents.TriggerEnemyDie(enemyId);
                    _testBossHealth.Remove(enemyId);
                }
            }

            // F11 - Simulate regular enemy death
            if (Input.GetKeyDown(KeyCode.F11))
            {
                string enemyId = $"enemy_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
                MelonLogger.Msg($"[DEBUG] Enemy dies: {enemyId}");
                GameEvents.TriggerEnemyDie(enemyId);
            }
        }

        // Track test boss health for F10 debug command
        private static Dictionary<string, float> _testBossHealth = new Dictionary<string, float>();
    }
}
