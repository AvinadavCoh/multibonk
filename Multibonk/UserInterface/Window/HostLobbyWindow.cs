using Multibonk.Networking.Lobby;
using UnityEngine;

namespace Multibonk.UserInterface.Window
{
    public class HostLobbyWindow : WindowBase
    {
        private LobbyContext LobbyContext { get; }
        public event Action OnCloseLobby;
        public event Action OnSteamOverlayClicked;
        public event Action OnOptionsClicked;

        private bool steamOverlayAvailable = false;
        private string steamTunnelStatus = string.Empty;

        public HostLobbyWindow(LobbyContext context) : base(new Rect(50, 50, 400, 300))
        {
            LobbyContext = context;
        }

        protected override void RenderWindow(Rect rect)
        {
            CustomStyles.DrawWindowBackground(rect, "🖥️ Host Lobby");

            GUILayout.BeginArea(new Rect(rect.x + 10, rect.y + 40, rect.width - 20, rect.height - 50));

            GUILayout.Label("Press F5 to hide this menu", CustomStyles.LabelStyle);
            GUILayout.Space(10);

            GUILayout.Label("Connected Players:", CustomStyles.HeaderStyle);
            GUILayout.Space(5);

            foreach (var player in LobbyContext.GetPlayers())
            {
                string playerIcon = "👤";
                string character = (player.SelectedCharacter == null || player.SelectedCharacter.Length == 0 || player.SelectedCharacter == "None")
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

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("⚙️ Options", CustomStyles.ButtonStyle, GUILayout.Height(30)))
            {
                OnOptionsClicked?.Invoke();
            }
            GUILayout.Space(5);
            bool originalState = GUI.enabled;
            GUI.enabled = steamOverlayAvailable;
            if (GUILayout.Button("💬 Steam Friends", CustomStyles.ButtonStyle, GUILayout.Height(30)))
            {
                OnSteamOverlayClicked?.Invoke();
            }
            GUI.enabled = originalState;
            GUILayout.EndHorizontal();

            if (steamTunnelStatus != null && steamTunnelStatus.Length > 0)
            {
                GUILayout.Space(5);
                GUILayout.Label(steamTunnelStatus, CustomStyles.LabelStyle);
            }

            GUILayout.Space(10);

            if (GUILayout.Button("❌ Close Lobby", CustomStyles.ButtonStyle, GUILayout.Height(35))) 
                CloseLobby();

            GUILayout.EndArea();
        }


        private void CloseLobby()
        {
            OnCloseLobby?.Invoke();
        }

        public void SetSteamOverlayAvailability(bool available) => steamOverlayAvailable = available;
        public void SetSteamTunnelStatus(string status) => steamTunnelStatus = status;
    }
}
