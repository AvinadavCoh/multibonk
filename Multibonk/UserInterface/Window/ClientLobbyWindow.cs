using Multibonk.Networking.Lobby;
using UnityEngine;

namespace Multibonk.UserInterface.Window
{
    public class ClientLobbyWindow : WindowBase
    {
        private LobbyContext lobby;
        public event Action OnLeaveLobby;
        public event Action OnSteamOverlayClicked;
        public event Action OnOptionsClicked;

        private bool steamOverlayAvailable = false;
        private string steamTunnelStatus = string.Empty;

        public ClientLobbyWindow(LobbyContext lobby) : base(new Rect(50, 50, 360, 360))
        {
            this.lobby = lobby;
        }

        protected override void RenderWindow(Rect rect)
        {
            CustomStyles.DrawWindowBackground(rect, "LOBBY");

            GUILayout.BeginArea(new Rect(
                rect.x + CustomStyles.Pad,
                rect.y + CustomStyles.TitleBarHeight + 10,
                rect.width - CustomStyles.Pad * 2,
                rect.height - CustomStyles.TitleBarHeight - 10 - CustomStyles.Pad));

            GUILayout.Label("Press F5 to hide / show this menu", CustomStyles.SubtleStyle);
            CustomStyles.Space(8);

            LobbyWindowShared.DrawPlayerList(lobby);

            GUILayout.FlexibleSpace();

            LobbyWindowShared.DrawFooterButtons(
                steamOverlayAvailable, steamTunnelStatus,
                OnOptionsClicked, OnSteamOverlayClicked,
                "Leave Lobby", () => OnLeaveLobby?.Invoke());

            GUILayout.EndArea();
        }

        public void SetSteamOverlayAvailability(bool available) => steamOverlayAvailable = available;
        public void SetSteamTunnelStatus(string status) => steamTunnelStatus = status;
    }
}
