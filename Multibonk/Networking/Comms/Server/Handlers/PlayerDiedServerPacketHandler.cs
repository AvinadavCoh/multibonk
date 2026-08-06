using MelonLoader;
using Multibonk.Game;
using Multibonk.Game.Handlers;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;
using Multibonk.Networking.Lobby;
using UnityEngine;

namespace Multibonk.Networking.Comms.Server.Handlers
{
    /// <summary>
    /// Host-side handler for PLAYER_DIED_PACKET (ClientSentPacketId = 10).
    ///
    /// When a client's local player dies it sends this packet to the host.
    /// The host:
    ///   1. Marks that player dead in LobbyContext.
    ///   2. Relays a PLAYER_DEATH broadcast so every other client updates its HUD.
    ///   3. Calls RunCoordinator.TryEndRun — the shared, idempotent end-run check.
    ///      If all lobby players are now dead, TryEndRun broadcasts RUN_OVER and
    ///      triggers the host's own game-over screen.
    /// </summary>
    public class PlayerDiedServerPacketHandler : IServerPacketHandler
    {
        public byte PacketId => (byte)ClientSentPacketId.PLAYER_DIED_PACKET;

        private readonly LobbyContext _lobbyContext;

        public PlayerDiedServerPacketHandler(LobbyContext lobbyContext)
        {
            _lobbyContext = lobbyContext;
        }

        public void Handle(IncomingMessage msg, Connection conn)
        {
            try
            {
                var sender = _lobbyContext.GetPlayer(conn);
                if (sender == null)
                {
                    MelonLogger.Warning("[Host] Received PLAYER_DIED_PACKET from unknown connection - ignoring");
                    return;
                }

                sender.IsDead = true;
                MelonLogger.Msg($"[Host] Player '{sender.Name}' (UUID={sender.UUID}) confirmed dead.");

                // Relay a PLAYER_DEATH notification to all OTHER clients so they can
                // update their HUD / spectator view.
                var deathNotify = new SendPlayerDeathPacket(sender.UUID, Vector3.zero);
                foreach (var player in _lobbyContext.GetPlayers())
                {
                    if (player.Connection != null && player.UUID != sender.UUID)
                        player.Connection.EnqueuePacket(deathNotify);
                }

                // Shared, idempotent end-run evaluation (DEFECT 2).
                // No-op if other players are still alive; broadcasts RUN_OVER and ends
                // the run if every lobby player is now confirmed dead.
                RunCoordinator.TryEndRun(_lobbyContext);
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[Host] Failed to handle PLAYER_DIED_PACKET: {ex.Message}");
            }
        }
    }
}
