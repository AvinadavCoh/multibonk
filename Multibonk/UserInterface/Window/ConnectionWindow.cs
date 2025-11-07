using MelonLoader;
using UnityEngine;

namespace Multibonk.UserInterface.Window
{
    public class ConnectionWindowEventArgs { 
        public string IP { get; }
        public string PlayerName { get; }

        public ConnectionWindowEventArgs(string playerName, string ip) 
        {
            IP = ip;
            PlayerName = playerName;
        }
    }

    public class ConnectionWindow : WindowBase
    {
        public event Action<ConnectionWindowEventArgs> OnStartServerClicked;
        public event Action<ConnectionWindowEventArgs> OnConnectClicked;
        public event Action OnSteamOverlayClicked;

        private string ipAddress = "127.0.0.1";
        private string playerName = "PlayerName";
        private bool nameIsFocused = false;
        private bool ipIsFocused = false;
        private bool steamOverlayAvailable = false;
        private string steamTunnelStatus = string.Empty;
        private string connectionErrorMessage = string.Empty;
        private GUIStyle errorStyle;

        public ConnectionWindow() : base(new Rect(10, 10, 300, 200)) 
        {
            ipAddress = Preferences.IpAddress.Value;
            playerName = Preferences.PlayerName.Value;
        }

        protected override void RenderWindow(Rect rect)
        {
            try
            {
                // Initialize error style if needed
                if (errorStyle == null)
                {
                    errorStyle = new GUIStyle(CustomStyles.LabelStyle);
                    errorStyle.normal.textColor = new Color(1f, 0.3f, 0.3f);
                }

                CustomStyles.DrawWindowBackground(rect, "🌐 Multibonk Multiplayer");

                GUILayout.BeginArea(new Rect(rect.x + 10, rect.y + 40, rect.width - 20, rect.height - 50));

                GUILayout.Label("Press F5 to hide/show this menu", CustomStyles.LabelStyle);
                GUILayout.Space(10);

                GUILayout.BeginHorizontal();
                GUILayout.Label("Name:", CustomStyles.LabelStyle, GUILayout.Width(60));
                playerName = GUILayout.TextField(playerName, CustomStyles.TextFieldStyle);
                GUILayout.EndHorizontal();

                GUILayout.Space(5);

                GUILayout.BeginHorizontal();
                GUILayout.Label("IP:Port:", CustomStyles.LabelStyle, GUILayout.Width(60));
                ipAddress = GUILayout.TextField(ipAddress, CustomStyles.TextFieldStyle);
                GUILayout.EndHorizontal();

                GUILayout.Space(15);

                if (GUILayout.Button("🖥️ Start Server (Host)", CustomStyles.ButtonStyle, GUILayout.Height(35)))
                {
                    Preferences.IpAddress.Value = ipAddress;
                    Preferences.PlayerName.Value = playerName;
                    OnStartServer();
                }

                GUILayout.Space(5);

                if (GUILayout.Button("🔌 Connect to Server", CustomStyles.ButtonStyle, GUILayout.Height(35)))
                {
                    Preferences.IpAddress.Value = ipAddress;
                    Preferences.PlayerName.Value = playerName;
                    OnConnect();
                }

                GUILayout.Space(10);

                // Steam overlay button
                bool originalState = GUI.enabled;
                GUI.enabled = steamOverlayAvailable;
                if (GUILayout.Button("💬 Steam Friends Overlay", CustomStyles.ButtonStyle, GUILayout.Height(30)))
                {
                    OnSteamOverlayClicked?.Invoke();
                }
                GUI.enabled = originalState;

                // Display Steam tunnel status
                if (steamTunnelStatus != null && steamTunnelStatus.Length > 0)
                {
                    GUILayout.Space(5);
                    GUILayout.Label(steamTunnelStatus, CustomStyles.LabelStyle);
                }

                // Display connection error
                if (connectionErrorMessage != null && connectionErrorMessage.Length > 0)
                {
                    GUILayout.Space(5);
                    GUILayout.Label(connectionErrorMessage, errorStyle);
                }

                GUILayout.EndArea();
            }
            catch (System.Exception ex)
            {
                MelonLoader.MelonLogger.Error($"ConnectionWindow error: {ex}");
            }
        }

        private void OnStartServer() => OnStartServerClicked?.Invoke(new ConnectionWindowEventArgs(playerName, ipAddress));
        private void OnConnect() => OnConnectClicked?.Invoke(new ConnectionWindowEventArgs(playerName, ipAddress));

        public void SetSteamOverlayAvailability(bool available) => steamOverlayAvailable = available;
        public void SetSteamTunnelStatus(string status) => steamTunnelStatus = status;
        public void SetIpAddress(string address) => ipAddress = address;
        public string GetPlayerName() => playerName;
        public void SetConnectionError(string message) => connectionErrorMessage = message;
    }
}