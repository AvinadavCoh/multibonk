using Il2Cpp;
using UnityEngine;

namespace Multibonk.Game
{
    internal static class GameEvents
    {
        public static event Action ConfirmCharacterEvent;
        public static event Action ConfirmMapEvent;
        public static event Action<CharacterData> CharacterChanged;
        public static event Action GameLoadedEvent;

        public static event Action<Vector3> PlayerMoveEvent;
        public static event Action<Quaternion> PlayerRotateEvent;

        public static event Action PlayerDieEvent;
        public static event Action PlayerTakeHitEvent;

        public static event Action BossSpawnEvent;
        public static event Action BossDamagedEvent;

        public static event Action EnemySpawnEvent;
        public static event Action<int, int, Vector3, int, bool> EnemySpawnedEvent; // enemyId, enemyType, position, level, isBoss
        public static event Action<string> EnemyDieEvent; // enemyId
        public static event Action<string, float, float> EnemyHealthChangedEvent; // enemyId, currentHealth, maxHealth

        public static event Action InGamePauseEvent;
        public static event Action InGameUnpauseEvent;

        public static event Action UseShrineEvent;

        public static event Action<string, Vector3, int> SpawnDropEvent; // itemId, position, itemType
        public static event Action<string> OpenChestEvent; // chestId

        public static event Action<int> PlayerLevelUpEvent; // newLevel
        public static event Action<int> PlayerXpGainedEvent; // xpAmount

        public static event Action<int, int> MapTileRevealedEvent; // tileX, tileY


        public static void TriggerConfirmMap()
        {
            ConfirmMapEvent?.Invoke();
        }

        public static void TriggerConfirmCharacter()
        {
            ConfirmCharacterEvent?.Invoke();
        }

        public static void TriggerCharacterChanged(CharacterData character)
        {
            CharacterChanged?.Invoke(character);
        }

        public static void TriggerGameLoadedEvent()
        {
            GameLoadedEvent?.Invoke();
        }
        public static void TriggerPlayerMoved(Vector3 newPosition)
        {
            PlayerMoveEvent?.Invoke(newPosition);
        }

        public static void TriggerPlayerRotated(Quaternion newRotation)
        {
            PlayerRotateEvent?.Invoke(newRotation);
        }

        public static void TriggerPlayerLevelUp(int newLevel)
        {
            PlayerLevelUpEvent?.Invoke(newLevel);
        }

        public static void TriggerPlayerXpGained(int xpAmount)
        {
            PlayerXpGainedEvent?.Invoke(xpAmount);
        }

        public static void TriggerSpawnDrop(string itemId, Vector3 position, int itemType)
        {
            SpawnDropEvent?.Invoke(itemId, position, itemType);
        }

        public static void TriggerOpenChest(string chestId)
        {
            OpenChestEvent?.Invoke(chestId);
        }

        public static void TriggerEnemyDie(string enemyId)
        {
            EnemyDieEvent?.Invoke(enemyId);
        }

        public static void TriggerEnemyHealthChanged(string enemyId, float currentHealth, float maxHealth)
        {
            EnemyHealthChangedEvent?.Invoke(enemyId, currentHealth, maxHealth);
        }

        public static void TriggerEnemySpawned(int enemyId, int enemyType, Vector3 position, int level, bool isBoss)
        {
            EnemySpawnedEvent?.Invoke(enemyId, enemyType, position, level, isBoss);
        }

        public static void TriggerMapTileRevealed(int tileX, int tileY)
        {
            MapTileRevealedEvent?.Invoke(tileX, tileY);
        }

        public static void TriggerUseShrine()
        {
            UseShrineEvent?.Invoke();
        }

        public static void TriggerPlayerTakeHit()
        {
            PlayerTakeHitEvent?.Invoke();
        }

        public static void TriggerPlayerDie()
        {
            PlayerDieEvent?.Invoke();
        }
    }
}
