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
        private const float Width = 340f;
        private const float Height = 442f;

        public event Action<ConnectionWindowEventArgs> OnStartServerClicked;
        public event Action<ConnectionWindowEventArgs> OnConnectClicked;
        public event Action OnSteamOverlayClicked;

        private string ipAddress = "127.0.0.1";
        private string playerName = "PlayerName";
        private int activeField = 0; // 0 = none, 1 = name, 2 = ip
        private bool steamOverlayAvailable = false;
        private string steamTunnelStatus = string.Empty;
        private string connectionErrorMessage = string.Empty;

        public ConnectionWindow() : base(new Rect(20, 20, Width, Height))
        {
            ipAddress = Preferences.IpAddress.Value;
            playerName = Preferences.PlayerName.Value;
        }

        protected override void RenderWindow(Rect rect)
        {
            try
            {
                CustomStyles.DrawWindowBackground(rect, "MULTIBONK  MULTIPLAYER");

                float x = rect.x + CustomStyles.Pad;
                float width = rect.width - CustomStyles.Pad * 2;
                float y = rect.y + CustomStyles.TitleBarHeight + 10;

                // Hint line
                GUI.Label(new Rect(x, y, width, 16), "Press F5 to hide / show this menu", CustomStyles.SubtleStyle);
                y += 24;

                // --- Player name ---
                GUI.Label(new Rect(x, y, width, 16), "PLAYER NAME", CustomStyles.HeaderStyle);
                y += 19;
                var nameRect = new Rect(x, y, width, 28);
                DrawTextField(nameRect, playerName, activeField == 1);
                HandleFieldClick(nameRect, 1);
                y += 36;

                // --- Server address ---
                GUI.Label(new Rect(x, y, width, 16), "SERVER ADDRESS (IP:PORT)", CustomStyles.HeaderStyle);
                y += 19;
                var ipRect = new Rect(x, y, width, 28);
                DrawTextField(ipRect, ipAddress, activeField == 2);
                HandleFieldClick(ipRect, 2);
                y += 34;

                HandleKeyboardInput();

                // --- IP utility buttons ---
                float half = (width - 6) / 2;
                if (GUI.Button(new Rect(x, y, half, 28), "Show My IP", CustomStyles.ButtonStyle))
                    ShowMyIP();
                if (GUI.Button(new Rect(x + half + 6, y, half, 28), "Test Connection", CustomStyles.ButtonStyle))
                    TestConnection();
                y += 38;

                CustomStyles.DrawDivider(x, y, width);
                y += 12;

                // --- Primary actions ---
                if (GUI.Button(new Rect(x, y, width, 36), "HOST GAME", CustomStyles.PrimaryButtonStyle))
                {
                    SavePreferences();
                    OnStartServer();
                }
                y += 42;

                if (GUI.Button(new Rect(x, y, width, 36), "JOIN GAME", CustomStyles.PrimaryButtonStyle))
                {
                    SavePreferences();
                    OnConnect();
                }
                y += 44;

                CustomStyles.DrawDivider(x, y, width);
                y += 12;

                // --- Steam ---
                bool originalState = GUI.enabled;
                GUI.enabled = steamOverlayAvailable;
                if (GUI.Button(new Rect(x, y, width, 30), "Steam Friends Overlay", CustomStyles.ButtonStyle))
                {
                    OnSteamOverlayClicked?.Invoke();
                }
                GUI.enabled = originalState;
                y += 36;

                // --- Status / error ---
                if (steamTunnelStatus != null && steamTunnelStatus.Length > 0)
                {
                    GUI.Label(new Rect(x, y, width, 30), steamTunnelStatus, CustomStyles.SubtleStyle);
                    y += 32;
                }

                if (connectionErrorMessage != null && connectionErrorMessage.Length > 0)
                {
                    GUI.Label(new Rect(x, y, width, 30), connectionErrorMessage, CustomStyles.ErrorStyle);
                }
            }
            catch (System.NotSupportedException)
            {
                // Silently ignore IL2CPP unstripping failures
            }
            catch (System.Exception ex)
            {
                if (!(ex is System.NotSupportedException))
                {
                    MelonLoader.MelonLogger.Error($"ConnectionWindow error: {ex}");
                }
            }
        }

        private void DrawTextField(Rect rect, string value, bool focused)
        {
            var style = focused ? CustomStyles.TextFieldFocusedStyle : CustomStyles.TextFieldStyle;
            string display = value;
            if (focused && (Time.unscaledTime * 2f) % 2f < 1f)
                display += "|"; // blinking caret
            GUI.Box(rect, "", style);
            GUI.Label(rect, display, style);
        }

        private void HandleFieldClick(Rect rect, int fieldId)
        {
            if (Event.current.type == EventType.MouseDown)
            {
                if (rect.Contains(Event.current.mousePosition))
                {
                    activeField = fieldId;
                    Event.current.Use();
                }
                else if (activeField == fieldId)
                {
                    activeField = 0;
                }
            }
        }

        private void HandleKeyboardInput()
        {
            if (Event.current.type != EventType.KeyDown || activeField == 0)
                return;

            if (Event.current.keyCode == KeyCode.Backspace)
            {
                if (activeField == 1 && playerName.Length > 0)
                    playerName = playerName.Substring(0, playerName.Length - 1);
                else if (activeField == 2 && ipAddress.Length > 0)
                    ipAddress = ipAddress.Substring(0, ipAddress.Length - 1);
                Event.current.Use();
            }
            else if (Event.current.keyCode == KeyCode.Tab)
            {
                activeField = activeField == 1 ? 2 : 1;
                Event.current.Use();
            }
            else if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.Escape)
            {
                activeField = 0;
                Event.current.Use();
            }
            else if (Event.current.character != '\0' && Event.current.character != '\n' && Event.current.character != '\r')
            {
                if (activeField == 1)
                    playerName += Event.current.character;
                else if (activeField == 2)
                    ipAddress += Event.current.character;
                Event.current.Use();
            }
        }

        private void SavePreferences()
        {
            Preferences.IpAddress.Value = ipAddress;
            Preferences.PlayerName.Value = playerName;
        }

        private void OnStartServer() => OnStartServerClicked?.Invoke(new ConnectionWindowEventArgs(playerName, ipAddress));
        private void OnConnect() => OnConnectClicked?.Invoke(new ConnectionWindowEventArgs(playerName, ipAddress));

        private void ShowMyIP()
        {
            try
            {
                var localIP = Networking.NetworkDiagnostics.GetLocalIPAddress();
                DebugLogger.Log($"========================================");
                DebugLogger.Log($"YOUR IP ADDRESS: {localIP}:25565");
                DebugLogger.Log($"========================================");
                DebugLogger.Log($"Share this with other players so they can connect to you!");
                SetConnectionError($"Your IP: {localIP}:25565");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Error($"Failed to get IP: {ex.Message}");
                SetConnectionError("Failed to get IP");
            }
        }

        private void TestConnection()
        {
            try
            {
                // Parse IP and port
                string host = ipAddress;
                int port = 25565;

                if (ipAddress.Contains(":"))
                {
                    var parts = ipAddress.Split(':');
                    host = parts[0];
                    if (parts.Length > 1 && int.TryParse(parts[1], out int parsedPort))
                    {
                        port = parsedPort;
                    }
                }

                // Show local IP first
                var localIP = Networking.NetworkDiagnostics.GetLocalIPAddress();
                DebugLogger.Log($"Your local IP address is: {localIP}");
                SetConnectionError($"Your IP: {localIP}");

                // Run diagnostics in background thread
                new System.Threading.Thread(() =>
                {
                    var result = Networking.NetworkDiagnostics.RunDiagnostics(host, port);
                    SetConnectionError(result);
                }).Start();
            }
            catch (System.Exception ex)
            {
                DebugLogger.Error($"Test connection error: {ex.Message}");
                SetConnectionError($"Test failed: {ex.Message}");
            }
        }

        public void SetSteamOverlayAvailability(bool available) => steamOverlayAvailable = available;
        public void SetSteamTunnelStatus(string status) => steamTunnelStatus = status;
        public void SetIpAddress(string address) => ipAddress = address;
        public string GetPlayerName() => playerName;
        public void SetConnectionError(string message) => connectionErrorMessage = message;
    }
}
