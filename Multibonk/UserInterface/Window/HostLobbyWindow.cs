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

        public HostLobbyWindow(LobbyContext context) : base(new Rect(50, 50, 360, 360))
        {
            LobbyContext = context;
        }

        protected override void RenderWindow(Rect rect)
        {
            CustomStyles.DrawWindowBackground(rect, "HOST LOBBY");

            GUILayout.BeginArea(new Rect(
                rect.x + CustomStyles.Pad,
                rect.y + CustomStyles.TitleBarHeight + 10,
                rect.width - CustomStyles.Pad * 2,
                rect.height - CustomStyles.TitleBarHeight - 10 - CustomStyles.Pad));

            GUILayout.Label("Press F5 to hide / show this menu", CustomStyles.SubtleStyle);
            CustomStyles.Space(8);

            LobbyWindowShared.DrawPlayerList(LobbyContext);

            GUILayout.FlexibleSpace();

            LobbyWindowShared.DrawFooterButtons(
                steamOverlayAvailable, steamTunnelStatus,
                OnOptionsClicked, OnSteamOverlayClicked,
                "Close Lobby", () => OnCloseLobby?.Invoke());

            GUILayout.EndArea();
        }

        public void SetSteamOverlayAvailability(bool available) => steamOverlayAvailable = available;
        public void SetSteamTunnelStatus(string status) => steamTunnelStatus = status;
    }

    /// <summary>
    /// Shared rendering for the host/client lobby windows so they stay visually identical.
    /// </summary>
    internal static class LobbyWindowShared
    {
        public static void DrawPlayerList(LobbyContext lobby)
        {
            var players = lobby.GetPlayers();

            GUILayout.Label($"CONNECTED PLAYERS ({players.Count})", CustomStyles.HeaderStyle);
            CustomStyles.Space(4);

            int index = 0;
            foreach (var player in players)
            {
                // Fixed-height row with alternating background for clean alignment
                Rect row = GUILayoutUtility.GetRect(10, 24, GUILayout.ExpandWidth(true));

                if (index % 2 == 0)
                {
                    GUI.color = CustomStyles.RowAltCol;
                    GUI.DrawTexture(row, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }

                // Name (left)
                GUI.Label(new Rect(row.x + 6, row.y, row.width * 0.45f, row.height), player.Name, CustomStyles.LabelStyle);

                // Character status (middle-right)
                string character = (player.SelectedCharacter == null || player.SelectedCharacter.Length == 0 || player.SelectedCharacter == "None")
                    ? "selecting..."
                    : player.SelectedCharacter;
                var charStyle = new GUIStyle(CustomStyles.SubtleStyle) { alignment = TextAnchor.MiddleRight, clipping = TextClipping.Clip };
                GUI.Label(new Rect(row.x + row.width * 0.45f, row.y, row.width * 0.40f, row.height), character, charStyle);

                // Ping (right, color-coded)
                var pingStyle = new GUIStyle(CustomStyles.LabelStyle)
                {
                    alignment = TextAnchor.MiddleRight,
                    normal = { textColor = CustomStyles.PingColor(player.Ping) }
                };
                GUI.Label(new Rect(row.x + row.width - 64, row.y, 58, row.height), $"{player.Ping}ms", pingStyle);

                index++;
            }

            if (index == 0)
            {
                GUILayout.Label("Waiting for players to join...", CustomStyles.SubtleStyle);
            }
        }

        public static void DrawFooterButtons(
            bool steamOverlayAvailable, string steamTunnelStatus,
            Action onOptions, Action onSteam,
            string closeText, Action onClose)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Options", CustomStyles.ButtonStyle, GUILayout.Height(30)))
                onOptions?.Invoke();

            GUILayout.Label("", GUILayout.Width(6));

            bool originalState = GUI.enabled;
            GUI.enabled = steamOverlayAvailable;
            if (GUILayout.Button("Steam Friends", CustomStyles.ButtonStyle, GUILayout.Height(30)))
                onSteam?.Invoke();
            GUI.enabled = originalState;
            GUILayout.EndHorizontal();

            if (steamTunnelStatus != null && steamTunnelStatus.Length > 0)
            {
                CustomStyles.Space(4);
                GUILayout.Label(steamTunnelStatus, CustomStyles.SubtleStyle);
            }

            CustomStyles.Space(8);

            if (GUILayout.Button(closeText, CustomStyles.DangerButtonStyle, GUILayout.Height(34)))
                onClose?.Invoke();
        }
    }
}
