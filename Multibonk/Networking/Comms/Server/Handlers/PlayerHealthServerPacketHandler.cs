using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;
using Multibonk.Networking.Lobby;

namespace Multibonk.Networking.Comms.Server.Handlers
{
    /// <summary>
    /// Host-side handler for client health updates.
    /// Stores the values on the sender's lobby entry (for the host's Players HUD)
    /// and relays them to the other clients.
    /// </summary>
    public class PlayerHealthServerPacketHandler : IServerPacketHandler
    {
        public byte PacketId => (byte)ClientSentPacketId.PLAYER_HEALTH_PACKET;

        private readonly LobbyContext _lobbyContext;

        public PlayerHealthServerPacketHandler(LobbyContext lobbyContext)
        {
            _lobbyContext = lobbyContext;
        }

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new ClientHealthPacket(msg);

            var sender = _lobbyContext.GetPlayer(conn);
            if (sender == null) return;

            DebugLogger.Log($"[Host] {sender.Name} took {packet.DamageAmount:F1} damage ({packet.CurrentHealth:F1}/{packet.MaxHealth:F1})");

            sender.CurrentHealth = packet.CurrentHealth;
            sender.MaxHealth = packet.MaxHealth;

            // Relay to the other clients
            var relay = new SendPlayerDamagePacket(sender.UUID, packet.CurrentHealth, packet.MaxHealth, packet.DamageAmount);
            foreach (var player in _lobbyContext.GetPlayers())
            {
                if (player.Connection == null || player.UUID == sender.UUID)
                    continue;

                player.Connection.EnqueuePacket(relay);
            }
        }
    }
}
