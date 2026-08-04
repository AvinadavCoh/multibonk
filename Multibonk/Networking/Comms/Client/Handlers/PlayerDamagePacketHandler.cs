using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;
using Multibonk.Networking.Lobby;
using System.Linq;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    /// <summary>
    /// Client-side handler for other players' damage.
    /// Stores the synced health on the matching lobby player so the Players HUD shows it.
    /// </summary>
    public class PlayerDamagePacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.PLAYER_DAMAGE;

        private readonly LobbyContext _lobbyContext;

        public PlayerDamagePacketHandler(LobbyContext lobbyContext)
        {
            _lobbyContext = lobbyContext;
        }

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new PlayerDamagePacket(msg);

            DebugLogger.Log($"[Client] Player {packet.PlayerId} took {packet.DamageAmount:F1} damage. Health: {packet.CurrentHealth:F1}/{packet.MaxHealth:F1}");

            var player = _lobbyContext.GetPlayers().FirstOrDefault(p => p.UUID == packet.PlayerId);
            if (player == null)
                return;

            player.CurrentHealth = packet.CurrentHealth;
            player.MaxHealth = packet.MaxHealth;
        }
    }
}
