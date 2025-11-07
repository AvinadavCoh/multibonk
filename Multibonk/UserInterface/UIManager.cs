using UnityEngine;
using Multibonk.UserInterface.Window;
using MelonLoader;
using Multibonk.Networking.Lobby;
using Multibonk.Networking.Steam;

namespace Multibonk
{
    public class UIManager
    {
        public enum UIState
        {
            Connection,
            ClientLobby,
            HostLobby
        }

        private UIState currentState = UIState.Connection;

        public bool IsShowingMenu { get; set; } = true;
        private bool _showingMenuBuffer = true;

        public ConnectionWindow connectionWindow;
        public ClientLobbyWindow clientLobbyWindow;
        public HostLobbyWindow hostLobbyWindow;
        public PlayerHealthHUD playerHealthHUD;
        public OptionsWindow optionsWindow;

        private readonly SteamTunnelService steamTunnelService;
        private readonly LobbyService lobbyService;
        private bool lastOverlayAvailability = false;
        private string steamTunnelStatusMessage = string.Empty;
        private SteamTunnelEndpoint? displayedSteamEndpoint;

        public UIManager(
            ConnectionWindow connectionWindow,
            ClientLobbyWindow clientLobbyWindow,
            HostLobbyWindow hostLobbyWindow,
            PlayerHealthHUD playerHealthHUD,
            OptionsWindow optionsWindow,

            LobbyContext lobby,
            LobbyService lobbyService,
            SteamTunnelService steamTunnelService
        )
        {
            this.connectionWindow = connectionWindow;
            this.clientLobbyWindow = clientLobbyWindow;
            this.hostLobbyWindow = hostLobbyWindow;
            this.playerHealthHUD = playerHealthHUD;
            this.optionsWindow = optionsWindow;
            this.lobbyService = lobbyService;
            this.steamTunnelService = steamTunnelService;

            connectionWindow.OnConnectClicked += (args) =>
            {
                var parts = args.IP.Split(':');
                if (parts.Length != 2) return;

                var ip = parts[0];
                if (!int.TryParse(parts[1], out var port)) return;

                MelonLogger.Msg("Connecting to ip " + args.IP);

                lobbyService.JoinLobby(ip, port, args.PlayerName);

            };

            connectionWindow.OnStartServerClicked += (args) =>
            {
                lobbyService.CreateLobby(args.PlayerName);
            };

            connectionWindow.OnSteamOverlayClicked += HandleSteamOverlayRequest;

            hostLobbyWindow.OnCloseLobby += () => lobbyService.CloseLobby();
            hostLobbyWindow.OnSteamOverlayClicked += HandleSteamOverlayRequest;
            hostLobbyWindow.OnOptionsClicked += ToggleOptions;

            clientLobbyWindow.OnLeaveLobby += () => lobbyService.CloseLobby();
            clientLobbyWindow.OnSteamOverlayClicked += HandleSteamOverlayRequest;
            clientLobbyWindow.OnOptionsClicked += ToggleOptions;

            optionsWindow.OpenSteamOverlayRequested += HandleSteamOverlayRequest;

            lobby.OnLobbyJoin += (_) => SetState(UIState.ClientLobby);
            lobby.OnLobbyCreated += (_) => SetState(UIState.HostLobby);
            lobby.OnLobbyClosed += (_) => SetState(UIState.Connection);
            lobby.OnLobbyJoinFailed += (reason) => HandleLobbyJoinFailed(reason);

        }

        public void OnGUI()
        {
            Event e = Event.current;
            if(e.rawType == EventType.KeyDown && e.keyCode == KeyCode.F5)
            {
                _showingMenuBuffer = !_showingMenuBuffer;
            }

            if(e.rawType == EventType.Layout && _showingMenuBuffer != IsShowingMenu)
            {
                IsShowingMenu = _showingMenuBuffer;
            }

            // Refresh Steam tunnel status periodically
            RefreshSteamTunnelStatus();

            // DEBUG: Press F6 to spawn a test network player
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F6)
            {
                if (LobbyPatchFlags.InMultiplayer && Il2CppAssets.Scripts.Actors.Player.MyPlayer.Instance != null)
                {
                    var myPos = Il2CppAssets.Scripts.Actors.Player.MyPlayer.Instance.transform.position;
                    var offset = new UnityEngine.Vector3(2, 0, 0); // Spawn 2 units to the right
                    var testPos = myPos + offset;
                    
                    MelonLogger.Msg("=== SPAWNING TEST NETWORK PLAYER ===");
                    Game.GameFunctions.SpawnNetworkPlayer(
                        playerId: 999, 
                        character: Il2Cpp.ECharacter.Fox, 
                        position: testPos, 
                        rotation: UnityEngine.Quaternion.identity
                    );
                }
            }

            if (IsShowingMenu) {
                switch (currentState)
                {
                    case UIState.Connection:
                        connectionWindow.Handle();
                        break;

                    case UIState.ClientLobby:
                        clientLobbyWindow.Handle();
                        break;

                    case UIState.HostLobby:
                        hostLobbyWindow.Handle();
                        break;
                }
            }

            // Always show health HUD when in-game (even with F5 menu hidden)
            playerHealthHUD.Handle();

            // Always render options window (it controls its own visibility)
            optionsWindow.Handle();
        }
        public void SetState(UIState newState)
        {
            currentState = newState;
        }

        public UIState GetState() => currentState;

        private void HandleSteamOverlayRequest()
        {
            if (!steamTunnelService.TryOpenFriendsOverlay())
            {
                RefreshSteamTunnelStatus(forceUpdate: true);
            }
        }

        private void RefreshSteamTunnelStatus(bool forceUpdate = false)
        {
            bool overlayAvailable = steamTunnelService.IsOverlayAvailable;
            SteamTunnelEndpoint? endpoint = null;
            string status;

            if (!overlayAvailable)
            {
                status = "Steam overlay is unavailable. Make sure Steam is running and overlay access is enabled.";
            }
            else if (steamTunnelService.TryPeekEndpoint(out var pending))
            {
                endpoint = pending;
                status = $"Steam invite ready: {pending.Address}:{pending.Port}.";
            }
            else
            {
                status = "Open the Steam friends overlay to invite or join friends.";
            }

            bool overlayChanged = forceUpdate || overlayAvailable != lastOverlayAvailability;
            if (overlayChanged)
            {
                connectionWindow.SetSteamOverlayAvailability(overlayAvailable);
                clientLobbyWindow.SetSteamOverlayAvailability(overlayAvailable);
                hostLobbyWindow.SetSteamOverlayAvailability(overlayAvailable);
                optionsWindow.SetSteamOverlayAvailability(overlayAvailable);
                lastOverlayAvailability = overlayAvailable;
            }

            if (forceUpdate || !string.Equals(status, steamTunnelStatusMessage, StringComparison.Ordinal))
            {
                steamTunnelStatusMessage = status;
                connectionWindow.SetSteamTunnelStatus(status);
                clientLobbyWindow.SetSteamTunnelStatus(status);
                hostLobbyWindow.SetSteamTunnelStatus(status);
                optionsWindow.SetSteamTunnelStatus(status);
            }

            if (endpoint.HasValue)
            {
                bool shouldUpdateEndpoint = forceUpdate || !displayedSteamEndpoint.HasValue || !displayedSteamEndpoint.Value.Equals(endpoint.Value);
                if (shouldUpdateEndpoint)
                {
                    displayedSteamEndpoint = endpoint;
                    connectionWindow.SetIpAddress(endpoint.Value.ToString());
                    AttemptAutoJoinFromSteam(endpoint.Value);
                }
            }
            else if (displayedSteamEndpoint.HasValue)
            {
                displayedSteamEndpoint = null;
            }
        }

        private void AttemptAutoJoinFromSteam(SteamTunnelEndpoint endpoint)
        {
            if (currentState != UIState.Connection)
            {
                return;
            }

            if (!steamTunnelService.TryConsumeEndpoint(out var consumed))
            {
                return;
            }

            var playerName = connectionWindow.GetPlayerName();
            if (string.IsNullOrWhiteSpace(playerName))
            {
                playerName = Preferences.PlayerName.Value;
            }

            if (string.IsNullOrWhiteSpace(playerName))
            {
                playerName = "Player";
            }

            Preferences.PlayerName.Value = playerName;

            var message = $"Connecting to Steam invite {consumed.Address}:{consumed.Port}...";
            steamTunnelStatusMessage = message;
            connectionWindow.SetConnectionError(string.Empty);
            connectionWindow.SetSteamTunnelStatus(message);
            clientLobbyWindow.SetSteamTunnelStatus(message);
            hostLobbyWindow.SetSteamTunnelStatus(message);

            MelonLogger.Msg($"Automatically joining Steam tunnel endpoint {consumed}.");
            lobbyService.JoinLobby(consumed.Address, consumed.Port, playerName);
        }

        private void HandleLobbyJoinFailed(string reason)
        {
            SetState(UIState.Connection);
            connectionWindow.SetConnectionError(reason);
        }

        private void ToggleOptions()
        {
            if (optionsWindow.IsOpen)
            {
                optionsWindow.Hide();
            }
            else
            {
                optionsWindow.Show();
            }
        }

    }
}
