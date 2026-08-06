using MelonLoader;
using Multibonk.Game;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Multibonk.Networking.Comms;
using Multibonk.Networking.Comms.Server.Protocols;
using Multibonk.Networking.Steam;

namespace Multibonk.Networking.Lobby
{
    public class LobbyService
    {
        private NetworkService NetworkService { get; }
        private LobbyContext CurrentLobby { get; }
        private SteamTunnelService SteamTunnelService { get; }

        public LobbyService(
            NetworkService service,
            LobbyContext context,
            SteamTunnelService steamTunnelService,
            ServerProtocol serverProtocol)
        {
            NetworkService = service;
            CurrentLobby = context;
            SteamTunnelService = steamTunnelService;

            // DEFECT 1: subscribe to client disconnect so a dropped player's seat is
            // removed from the lobby and the all-dead condition is re-evaluated.
            // This prevents a permanent soft-lock when a client disconnects mid-run
            // while still alive (IsDead == false), which would otherwise keep
            // AreAllPlayersDead() from ever returning true.
            serverProtocol.OnClientDisconnected += OnClientDisconnected;
        }

        // DEFECT 1 + DEFECT 2: called on the network thread whenever a TCP connection drops.
        private void OnClientDisconnected(Connection conn)
        {
            // The host is the only side that owns the canonical lobby player table.
            if (!LobbyPatchFlags.IsHosting || !LobbyPatchFlags.InMultiplayer)
                return;

            try
            {
                var player = CurrentLobby.RemovePlayer(conn);
                if (player == null)
                {
                    MelonLogger.Warning("[Host] Disconnected connection had no matching LobbyPlayer - already removed or never registered.");
                    return;
                }

                MelonLogger.Msg($"[Host] Player '{player.Name}' (UUID={player.UUID}) disconnected - removed from lobby.");

                // DEFECT 2: re-evaluate the all-dead condition after removing the player.
                // Scenario: A is dead, B is alive, B disconnects.  Removing B via this
                // handler leaves only A (dead), so AreAllPlayersDead() now returns true
                // and TryEndRun broadcasts RUN_OVER and ends the run.
                RunCoordinator.TryEndRun(CurrentLobby);

                // If a level-up is in progress and the disconnected player was still in
                // the pending set, prune them now so everyone else is not forced to wait
                // the full 20-second timeout before the game resumes.
                LevelUpCoordinator.TryResumeAll();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Host] Exception handling client disconnect: {ex.Message}");
            }
        }

        public void CreateLobby(string myName)
        {
            MelonLogger.Msg($"Creating lobby");
            try
            {
                NetworkService.StartServer();
            }
            catch (Exception e)
            {
                MelonLogger.Msg($"Failed to start lobby {e.Message}");
                CurrentLobby.TriggerLobbyJoinFailed($"Failed to start lobby {e.Message}");
                return;
            }

            SteamTunnelService.ClearEndpoints();

            CurrentLobby.GetPlayers().Clear();
            CurrentLobby.SetMyself(new LobbyPlayer(name: myName));
            CurrentLobby.SetState(LobbyState.Hosting);
            CurrentLobby.TriggerLobbyCreated();
            LobbyPatchFlags.IsHosting = true;
            LobbyPatchFlags.InMultiplayer = true;
            LobbyPatchFlags.CurrentLobby = CurrentLobby;
        }

        public void JoinLobby(string ip, int port, string myName)
        {
            // Check if we have a Steam invite waiting
            if (SteamTunnelService.TryConsumeEndpoint(out var endpoint))
            {
                ip = endpoint.Address;
                port = endpoint.Port;
                MelonLogger.Msg($"Using Steam tunnel endpoint {endpoint}.");
            }

            MelonLogger.Msg($"Joining lobby {ip}:{port} with the username: {myName}");

            try
            {
                var joinGamePacket = new SendJoinLobbyPacket(100, myName);
                NetworkService.GetClientService().Enqueue(joinGamePacket);
                NetworkService.StartClient(ip, port);
            }
            catch (Exception e)
            {
                CurrentLobby.TriggerLobbyJoinFailed($"Couldn't connect to lobby {e.Message}");
                return;
            }

            CurrentLobby.GetPlayers().Clear();
            CurrentLobby.SetState(LobbyState.Connected);
            CurrentLobby.TriggerLobbyJoin();
            LobbyPatchFlags.IsHosting = false;
            LobbyPatchFlags.InMultiplayer = true;
            LobbyPatchFlags.CurrentLobby = CurrentLobby;
        }

        public void AddPlayer(string playerName)
        {
            CurrentLobby.AddPlayer(playerName);
        }

        public void RemovePlayer(ushort uuid)
        {
            if (CurrentLobby.RemovePlayer(uuid) != null && LobbyPatchFlags.IsHosting)
            {
                // Same reason as the disconnect path: losing a live player can be the
                // step that makes everyone remaining dead.
                RunCoordinator.TryEndRun(CurrentLobby);
            }
        }

        public void CloseLobby()
        {
            try
            {
                NetworkService.Disconnect();
            }
            catch (Exception e)
            {
                MelonLogger.Msg($"NetworkService failed to disconnect {e}");
                return;
            }
            finally
            {
                SteamTunnelService.ClearEndpoints();
                CurrentLobby.TriggerLobbyClosed();
                CurrentLobby.GetPlayers().Clear();
                CurrentLobby.SetState(LobbyState.None);
                LobbyPatchFlags.IsHosting = false;
                LobbyPatchFlags.InMultiplayer = false;
            }
        }
    }
}
