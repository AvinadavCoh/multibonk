using Il2Cpp;
using Il2CppRewired.Utils;
using UnityEngine;

namespace Multibonk.Game
{
    public class GameFunctions
    {

        public static void GetCharacterDataFromMainMenu()
        {
            if (GamePatchFlags.CharacterDataInitialized)
                return;

            GamePatchFlags.CharacterData.Clear();

            MyButtonCharacter[] buttons = UnityEngine.Object.FindObjectsOfType<MyButtonCharacter>();
            GamePatchFlags.CharacterData = buttons.Select(b => b.characterData).ToList();
            GamePatchFlags.CharacterDataInitialized = true;
        }

        public static SpawnedNetworkPlayer GetSpawnedPlayerFromId(ushort playerId)
        {
            if (!GamePatchFlags.PlayersCache.TryGetValue(playerId, out var obj) || obj.PlayerObject == null || obj.PlayerObject.IsNullOrDestroyed())
            {
                GamePatchFlags.PlayersCache.Remove(playerId);
                return null;
            }

            return obj;
        }


        /// <summary>
        /// Spawns a player in the map
        /// </summary>
        /// <param name="playerId">The id of the player in the lobby</param>
        /// <param name="character">Character chosen by the player</param>
        /// <param name="position">Spawn position</param>
        /// <param name="rotation">Spawn rotation</param>
        public static void SpawnNetworkPlayer(ushort playerId, ECharacter character, Vector3 position, Quaternion rotation)
        {
            try
            {
                DebugLogger.LogSpawn($"Starting spawn for player {playerId}, character: {character}");

                // Check if character data is initialized
                if (!GamePatchFlags.CharacterDataInitialized || GamePatchFlags.CharacterData == null || GamePatchFlags.CharacterData.Count == 0)
                {
                    DebugLogger.Error("Character data not initialized! Attempting to initialize...");
                    GetCharacterDataFromMainMenu();
                }

                var data = GamePatchFlags.CharacterData.Find(data => data.eCharacter == character);
                if (data == null)
                {
                    DebugLogger.Error($"Could not find character data for {character}!");
                    return;
                }

                DebugLogger.LogSpawn($"Found character data for {character}");

                // Check if player already exists
                if (GamePatchFlags.PlayersCache.ContainsKey(playerId))
                {
                    DebugLogger.Warning($"Player {playerId} already exists! Removing old instance...");
                    var oldPlayer = GamePatchFlags.PlayersCache[playerId];
                    if (oldPlayer != null && oldPlayer.PlayerObject != null)
                    {
                        UnityEngine.Object.Destroy(oldPlayer.PlayerObject);
                    }
                    GamePatchFlags.PlayersCache.Remove(playerId);
                }

                var player = new GameObject("player-from-id-" + playerId.ToString());
                player.transform.position = position;
                player.transform.rotation = rotation;

                DebugLogger.LogSpawn($"Created GameObject at position ({position.x}, {position.y}, {position.z})");

                var rendererContainer = new GameObject("NetworkPlayer");
                rendererContainer.transform.SetParent(player.transform);

                var renderer = rendererContainer.AddComponent<PlayerRenderer>();

                // Network players don't need a functional inventory, just visual rendering
                // Try to create inventory, but use null if it fails (visual-only mode)
                PlayerInventory inv = null;
                try
                {
                    DebugLogger.LogSpawn($"Attempting to create PlayerInventory with ignoreShopItems=true");
                    inv = new PlayerInventory(data, ignoreShopItems: true);
                    DebugLogger.LogSpawn($"PlayerInventory created successfully");
                }
                catch (Exception invEx)
                {
                    DebugLogger.Warning($"Failed to create PlayerInventory (will use visual-only mode): {invEx.Message}");
                    DebugLogger.Warning($"Stack: {invEx.StackTrace}");
                    inv = null;
                }
                
                try
                {
                    DebugLogger.LogSpawn($"Setting character on renderer...");
                    renderer.SetCharacter(data, inv, position);
                    DebugLogger.LogSpawn($"Character set successfully");
                }
                catch (Exception setCharEx)
                {
                    DebugLogger.Error($"Failed to SetCharacter: {setCharEx.Message}");
                    DebugLogger.Error($"Stack: {setCharEx.StackTrace}");
                    throw;
                }
                
                renderer.CreateMaterials(4);

                rendererContainer.transform.localPosition = new Vector3(0, -(data.colliderHeight / 2), 0);
                rendererContainer.transform.localRotation = Quaternion.identity;

                GamePatchFlags.PlayersCache.Add(playerId, new SpawnedNetworkPlayer(player));

                DebugLogger.LogSpawn($"Successfully spawned player {playerId}! Total players cached: {GamePatchFlags.PlayersCache.Count}");
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"CRITICAL ERROR spawning player {playerId}: {ex.GetType().Name}");
                DebugLogger.Error($"Message: {ex.Message}");
                DebugLogger.Error($"Stack: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    DebugLogger.Error($"Inner: {ex.InnerException.Message}");
                }
            }
        }
    }


    public class SpawnedNetworkPlayer
    {
        public GameObject PlayerObject { get; set; }
        public float LastSeenTime { get; set; }

        public float LastTimeMoved { get; set; }

        public SpawnedNetworkPlayer(GameObject playerObject)
        {
            PlayerObject = playerObject;
            LastSeenTime = Time.time;
            LastTimeMoved = Time.time;
        }

        public bool IsNullOrDestroyed()
        {
            return PlayerObject == null || PlayerObject.Equals(null);
        }

        public void Move(Vector3 position)
        {
            PlayerObject.transform.position = position;
            LastTimeMoved = Time.time;

            var renderer = PlayerObject.GetComponentInChildren<PlayerRenderer>();

            if (!renderer.moving)
            {
                renderer.moving = true;
                renderer.ForceMoving(true);
            }
        }
        public void Rotate(Vector3 rotation)
        {
            PlayerObject.transform.rotation = Quaternion.Euler(rotation);
        }
    }
}
 
