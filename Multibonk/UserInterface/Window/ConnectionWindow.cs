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

        private string ipAddress = "127.0.0.1";
        private string playerName = "PlayerName";
        private bool nameIsFocused = false;
        private bool ipIsFocused = false;

        public ConnectionWindow() : base(new Rect(10, 10, 300, 200)) 
        {
            ipAddress = Preferences.IpAddress.Value;
            playerName = Preferences.PlayerName.Value;
        }

        protected override void RenderWindow(Rect rect)
        {
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

            GUILayout.EndArea();
        }

        private void OnStartServer() => OnStartServerClicked?.Invoke(new ConnectionWindowEventArgs(playerName, ipAddress));
        private void OnConnect() => OnConnectClicked?.Invoke(new ConnectionWindowEventArgs(playerName, ipAddress));
    }
}