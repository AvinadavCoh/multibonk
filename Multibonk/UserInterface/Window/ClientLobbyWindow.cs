using MelonLoader;
using Multibonk.Networking.Lobby;
using UnityEngine;

namespace Multibonk.UserInterface.Window
{
    public class ClientLobbyWindow : WindowBase
    {
        private LobbyContext lobby;
        public event Action OnLeaveLobby;

        public ClientLobbyWindow(LobbyContext lobby) : base(new Rect(50, 50, 300, 200)) 
        {
            this.lobby = lobby;
        }

        protected override void RenderWindow(Rect rect)
        {
            CustomStyles.DrawWindowBackground(rect, "🔌 Client Lobby");

            GUILayout.BeginArea(new Rect(rect.x + 10, rect.y + 40, rect.width - 20, rect.height - 50));

            GUILayout.Label("Press F5 to hide this menu", CustomStyles.LabelStyle);
            GUILayout.Space(10);

            GUILayout.Label("Connected Players:", CustomStyles.HeaderStyle);
            GUILayout.Space(5);

            foreach (var player in lobby.GetPlayers())
            {
                string playerIcon = "👤";
                string character = string.IsNullOrEmpty(player.SelectedCharacter) || player.SelectedCharacter == "None" 
                    ? "⏳ Selecting..." 
                    : $"✓ {player.SelectedCharacter}";
                
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{playerIcon} {player.Name}", CustomStyles.LabelStyle);
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{player.Ping}ms | {character}", CustomStyles.LabelStyle);
                GUILayout.EndHorizontal();
                
                GUILayout.Space(3);
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("❌ Leave Lobby", CustomStyles.ButtonStyle, GUILayout.Height(35))) 
                LeaveLobby();

            GUILayout.EndArea();
        }

        private void LeaveLobby()
        {
            OnLeaveLobby?.Invoke();
        }
    }

}
