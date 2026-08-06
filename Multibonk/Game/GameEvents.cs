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
        public static event Action<float, float, float> PlayerTakeHitEvent; // currentHealth, maxHealth, damageAmount

        public static event Action<int, int, Vector3, int, bool, int> EnemySpawnedEvent; // enemyId, enemyType, position, level, isBoss, flag (EEnemyFlag)
        public static event Action<string> EnemyDieEvent; // enemyId
        public static event Action<string, float, float> EnemyHealthChangedEvent; // enemyId, currentHealth, maxHealth

        public static event Action InGamePauseEvent;
        public static event Action InGameUnpauseEvent;

        public static event Action<Vector3, int> UseShrineEvent;

        public static event Action<string, Vector3, int, int> SpawnDropEvent; // itemId, position, itemType, value
        public static event Action<string, ushort> ItemPickedUpEvent; // itemId, playerId
        public static event Action<string> OpenChestEvent; // chestId

        public static event Action<int> PlayerLevelUpEvent; // newLevel
        public static event Action<int> PlayerXpGainedEvent; // xpAmount
        public static event Action<int> PlayerGoldGainedEvent; // goldAmount

        public static event Action<int> WaveStartEvent; // waveNumber
        public static event Action<int> WaveCompleteEvent; // waveNumber

        public static event Action<int, int> MapTileRevealedEvent; // tileX, tileY

        public static event Action<Vector3, byte> BossSpawnerActivateEvent; // spawnerPosition, spawnerType (0=regular, 1=final)
        public static event Action StageTransitionEvent; // portal activated

        public static event Action<float, float, bool> TimeSyncEvent; // stageTime, runTime, paused

        public static event Action<Diagnostics.SyncDigest> StateDigestEvent; // desync detector snapshot

        /// <summary>
        /// Fired by a client's DespawnPickupPatch when the local player consumes a network
        /// pickup (one that was spawned by a host packet).  The payload is the host-assigned
        /// wire id so the server can look up and despawn its own copy.
        /// Only meaningful on the client side; event handlers must guard with !IsHosting.
        /// </summary>
        public static event Action<string> ClientPickupConsumedEvent; // hostId


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

        public static void TriggerPlayerGoldGained(int goldAmount)
        {
            PlayerGoldGainedEvent?.Invoke(goldAmount);
        }

        public static void TriggerWaveStart(int waveNumber)
        {
            WaveStartEvent?.Invoke(waveNumber);
        }

        public static void TriggerWaveComplete(int waveNumber)
        {
            WaveCompleteEvent?.Invoke(waveNumber);
        }

        public static void TriggerSpawnDrop(string itemId, Vector3 position, int itemType, int value)
        {
            SpawnDropEvent?.Invoke(itemId, position, itemType, value);
        }

        public static void TriggerItemPickedUp(string itemId, ushort playerId)
        {
            ItemPickedUpEvent?.Invoke(itemId, playerId);
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

        public static void TriggerEnemySpawned(int enemyId, int enemyType, Vector3 position, int level, bool isBoss, int flag)
        {
            EnemySpawnedEvent?.Invoke(enemyId, enemyType, position, level, isBoss, flag);
        }

        public static void TriggerMapTileRevealed(int tileX, int tileY)
        {
            MapTileRevealedEvent?.Invoke(tileX, tileY);
        }

        public static void TriggerUseShrine(Vector3 position, int shrineType)
        {
            UseShrineEvent?.Invoke(position, shrineType);
        }

        public static void TriggerPlayerTakeHit(float currentHealth, float maxHealth, float damageAmount)
        {
            PlayerTakeHitEvent?.Invoke(currentHealth, maxHealth, damageAmount);
        }

        public static void TriggerInGamePause()
        {
            InGamePauseEvent?.Invoke();
        }

        public static void TriggerInGameUnpause()
        {
            InGameUnpauseEvent?.Invoke();
        }

        public static void TriggerPlayerDie()
        {
            PlayerDieEvent?.Invoke();
        }

        public static void TriggerBossSpawnerActivate(Vector3 spawnerPosition, byte spawnerType)
        {
            BossSpawnerActivateEvent?.Invoke(spawnerPosition, spawnerType);
        }

        public static void TriggerStageTransition()
        {
            StageTransitionEvent?.Invoke();
        }

        public static void TriggerTimeSync(float stageTime, float runTime, bool paused)
        {
            TimeSyncEvent?.Invoke(stageTime, runTime, paused);
        }

        public static void TriggerStateDigest(Diagnostics.SyncDigest digest)
        {
            StateDigestEvent?.Invoke(digest);
        }

        public static void TriggerClientPickupConsumed(string hostId)
        {
            ClientPickupConsumedEvent?.Invoke(hostId);
        }
    }
}
