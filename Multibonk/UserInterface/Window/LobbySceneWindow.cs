using MelonLoader;
using Multibonk.Game;
using Multibonk.Networking.Lobby;
using Multibonk.UserInterface;
using UnityEngine;
using System.Linq;

namespace Multibonk.UserInterface.Window
{
    /// <summary>
    /// Lobby window displayed while in the 3D multiplayer lobby
    /// Shows connected players and Start Game button for host
    /// </summary>
    public class LobbySceneWindow : WindowBase
    {
        private readonly LobbyContext _lobbyContext;

        public LobbySceneWindow(LobbyContext lobbyContext) : base(new Rect(0, 0, 400, 300))
        {
            _lobbyContext = lobbyContext;
        }

        protected override void RenderWindow(Rect rect)
        {
            // Only show if we're in the lobby
            if (!LobbyManager.IsInLobby)
                return;

            // Center the window
            float windowWidth = 400f;
            float windowHeight = 300f;
            float x = (Screen.width - windowWidth) / 2f;
            float y = (Screen.height - windowHeight) / 2f;

            GUILayout.BeginArea(new Rect(x, y, windowWidth, windowHeight));
            
            // Background box
            GUI.Box(new Rect(0, 0, windowWidth, windowHeight), "");

            GUILayout.BeginVertical();
            
            GUILayout.Space(20);
            
            // Title
            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 24;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.alignment = TextAnchor.MiddleCenter;
            GUILayout.Label("Multiplayer Lobby", titleStyle);
            
            GUILayout.Space(20);

            // Players list
            var playerStyle = new GUIStyle(GUI.skin.label);
            playerStyle.fontSize = 16;
            playerStyle.alignment = TextAnchor.MiddleCenter;
            
            GUILayout.Label("Connected Players:", playerStyle);
            GUILayout.Space(10);

            var players = _lobbyContext.GetPlayers();
            var hostPlayer = players.FirstOrDefault(); // First player is usually host
            foreach (var player in players)
            {
                var isHost = hostPlayer?.UUID == player.UUID && _lobbyContext.State == LobbyState.Hosting;
                var playerName = isHost ? $"👑 {player.Name} (Host)" : player.Name;
                GUILayout.Label(playerName, playerStyle);
            }

            GUILayout.Space(20);

            // Instructions
            var instructionStyle = new GUIStyle(GUI.skin.label);
            instructionStyle.fontSize = 14;
            instructionStyle.alignment = TextAnchor.MiddleCenter;
            instructionStyle.wordWrap = true;
            
            GUILayout.Label("Walk around and explore the lobby!", instructionStyle);
            GUILayout.Label("Host: Click Start Game when ready", instructionStyle);

            GUILayout.Space(20);

            // Start Game button (host only)
            if (_lobbyContext.State == LobbyState.Hosting)
            {
                var buttonStyle = new GUIStyle(GUI.skin.button);
                buttonStyle.fontSize = 18;
                buttonStyle.fontStyle = FontStyle.Bold;
                
                if (GUILayout.Button("🎮 Start Game", buttonStyle, GUILayout.Height(50)))
                {
                    MelonLogger.Msg("[LobbyWindow] Host clicked Start Game");
                    GameEvents.TriggerLobbyStartGame();
                }
            }
            else
            {
                var waitingStyle = new GUIStyle(GUI.skin.label);
                waitingStyle.fontSize = 16;
                waitingStyle.alignment = TextAnchor.MiddleCenter;
                waitingStyle.normal.textColor = Color.yellow;
                GUILayout.Label("Waiting for host to start game...", waitingStyle);
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
}
