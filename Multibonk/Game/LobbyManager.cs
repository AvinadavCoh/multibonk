using MelonLoader;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Multibonk.Game
{
    /// <summary>
    /// Manages the 3D multiplayer lobby scene
    /// Creates a dynamic scene with a ground plane where players can walk around before game starts
    /// </summary>
    public static class LobbyManager
    {
        private static Scene? lobbyScene;
        private static GameObject groundPlane;
        private static bool isInLobby = false;

        public static bool IsInLobby => isInLobby;

        /// <summary>
        /// Creates and loads the lobby scene
        /// </summary>
        public static void CreateLobbyScene()
        {
            try
            {
                MelonLogger.Msg("[LobbyManager] Creating multiplayer lobby scene...");

                // Create a new empty scene
                lobbyScene = SceneManager.CreateScene("MultiplayerLobby");

                if (!lobbyScene.HasValue || !lobbyScene.Value.IsValid())
                {
                    MelonLogger.Error("[LobbyManager] Failed to create lobby scene!");
                    return;
                }

                // Set as active scene
                SceneManager.SetActiveScene(lobbyScene.Value);

                // Create ground plane
                CreateGroundPlane();

                // Create lighting
                CreateLighting();

                isInLobby = true;

                MelonLogger.Msg("[LobbyManager] ✓ Lobby scene created successfully");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[LobbyManager] Failed to create lobby scene: {ex.Message}");
                MelonLogger.Error($"Stack: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Creates a simple ground plane for players to walk on
        /// </summary>
        private static void CreateGroundPlane()
        {
            try
            {
                // Create a plane GameObject
                groundPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                groundPlane.name = "LobbyGround";

                // Scale it up (plane is 10x10 by default, scale to 50x50)
                groundPlane.transform.localScale = new Vector3(5f, 1f, 5f);
                groundPlane.transform.position = Vector3.zero;

                // Move it to lobby scene
                if (lobbyScene.HasValue)
                {
                    SceneManager.MoveGameObjectToScene(groundPlane, lobbyScene.Value);
                }

                // Try to make it green/grass colored
                var renderer = groundPlane.GetComponent<Renderer>();
                if (renderer != null && renderer.material != null)
                {
                    renderer.material.color = new Color(0.2f, 0.6f, 0.2f); // Green
                }

                MelonLogger.Msg("[LobbyManager] ✓ Ground plane created");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[LobbyManager] Failed to create ground plane: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a directional light so the scene isn't pitch black
        /// </summary>
        private static void CreateLighting()
        {
            try
            {
                var lightObj = new GameObject("LobbyLight");
                var light = lightObj.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.0f;
                light.color = Color.white;
                lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

                if (lobbyScene.HasValue)
                {
                    SceneManager.MoveGameObjectToScene(lightObj, lobbyScene.Value);
                }

                MelonLogger.Msg("[LobbyManager] ✓ Lighting created");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[LobbyManager] Failed to create lighting: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets a spawn position for a player in the lobby
        /// Arranges players in a circle
        /// </summary>
        public static Vector3 GetSpawnPosition(int playerIndex)
        {
            // Spawn players in a circle around the center
            float radius = 10f;
            float angle = (playerIndex * 360f / 4f) * Mathf.Deg2Rad; // Assuming max 4 players
            
            return new Vector3(
                Mathf.Cos(angle) * radius,
                2f, // Slightly above ground
                Mathf.Sin(angle) * radius
            );
        }

        /// <summary>
        /// Destroys the lobby scene and prepares for game start
        /// </summary>
        public static void DestroyLobbyScene()
        {
            try
            {
                MelonLogger.Msg("[LobbyManager] Destroying lobby scene...");

                if (groundPlane != null)
                {
                    UnityEngine.Object.Destroy(groundPlane);
                    groundPlane = null;
                }

                if (lobbyScene.HasValue && lobbyScene.Value.IsValid())
                {
                    SceneManager.UnloadSceneAsync(lobbyScene.Value);
                }

                lobbyScene = null;
                isInLobby = false;

                MelonLogger.Msg("[LobbyManager] ✓ Lobby scene destroyed");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[LobbyManager] Failed to destroy lobby scene: {ex.Message}");
            }
        }
    }
}
