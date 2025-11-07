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
        private int activeField = 0; // 0 = none, 1 = name, 2 = ip
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

                float x = rect.x + 10;
                float y = rect.y + 40;
                float width = rect.width - 20;
                float lineHeight = 25;
                float currentY = y;

                // Press F5 label
                GUI.Label(new Rect(x, currentY, width, 20), "Press F5 to hide/show this menu", CustomStyles.LabelStyle);
                currentY += 30;

                // Name field
                GUI.Label(new Rect(x, currentY, 60, lineHeight), "Name:", CustomStyles.LabelStyle);
                Rect nameRect = new Rect(x + 65, currentY, width - 65, lineHeight);
                
                // Draw name field box
                GUI.Box(nameRect, "", CustomStyles.TextFieldStyle);
                GUI.Label(nameRect, playerName, CustomStyles.TextFieldStyle);
                
                // Check for clicks on name field
                if (Event.current.type == EventType.MouseDown && nameRect.Contains(Event.current.mousePosition))
                {
                    activeField = 1;
                    Event.current.Use();
                }
                currentY += lineHeight + 5;

                // IP field
                GUI.Label(new Rect(x, currentY, 60, lineHeight), "IP:Port:", CustomStyles.LabelStyle);
                Rect ipRect = new Rect(x + 65, currentY, width - 65, lineHeight);
                
                // Draw IP field box
                GUI.Box(ipRect, "", CustomStyles.TextFieldStyle);
                GUI.Label(ipRect, ipAddress, CustomStyles.TextFieldStyle);
                
                // Check for clicks on IP field
                if (Event.current.type == EventType.MouseDown && ipRect.Contains(Event.current.mousePosition))
                {
                    activeField = 2;
                    Event.current.Use();
                }
                
                // Handle keyboard input for active field
                if (Event.current.type == EventType.KeyDown && activeField > 0)
                {
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
                currentY += 15;

                // Show My IP button
                if (GUI.Button(new Rect(x, currentY, width / 2 - 2, 30), "📍 Show My IP", CustomStyles.ButtonStyle))
                {
                    ShowMyIP();
                }
                
                // Test Connection button
                if (GUI.Button(new Rect(x + width / 2 + 2, currentY, width / 2 - 2, 30), "🔍 Test IP", CustomStyles.ButtonStyle))
                {
                    TestConnection();
                }
                currentY += 35;

                // Start Server button
                if (GUI.Button(new Rect(x, currentY, width, 35), "🖥️ Start Server (Host)", CustomStyles.ButtonStyle))
                {
                    Preferences.IpAddress.Value = ipAddress;
                    Preferences.PlayerName.Value = playerName;
                    OnStartServer();
                }
                currentY += 40;

                // Connect button
                if (GUI.Button(new Rect(x, currentY, width, 35), "🔌 Connect to Server", CustomStyles.ButtonStyle))
                {
                    Preferences.IpAddress.Value = ipAddress;
                    Preferences.PlayerName.Value = playerName;
                    OnConnect();
                }
                currentY += 45;

                // Steam overlay button
                bool originalState = GUI.enabled;
                GUI.enabled = steamOverlayAvailable;
                if (GUI.Button(new Rect(x, currentY, width, 30), "💬 Steam Friends Overlay", CustomStyles.ButtonStyle))
                {
                    OnSteamOverlayClicked?.Invoke();
                }
                GUI.enabled = originalState;
                currentY += 35;

                // Display Steam tunnel status
                if (steamTunnelStatus != null && steamTunnelStatus.Length > 0)
                {
                    GUI.Label(new Rect(x, currentY, width, 20), steamTunnelStatus, CustomStyles.LabelStyle);
                    currentY += 25;
                }

                // Display connection error
                if (connectionErrorMessage != null && connectionErrorMessage.Length > 0)
                {
                    GUI.Label(new Rect(x, currentY, width, 20), connectionErrorMessage, errorStyle);
                }
            }
            catch (System.NotSupportedException)
            {
                // Silently ignore IL2CPP unstripping failures
            }
            catch (System.Exception ex)
            {
                // Log other errors
                if (!(ex is System.NotSupportedException))
                {
                    MelonLoader.MelonLogger.Error($"ConnectionWindow error: {ex}");
                }
            }
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
