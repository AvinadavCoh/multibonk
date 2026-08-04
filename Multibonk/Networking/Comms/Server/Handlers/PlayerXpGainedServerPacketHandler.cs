using Multibonk.Game.Handlers;
using Multibonk.Game.Patches;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;
using Multibonk.Networking.Lobby;

namespace Multibonk.Networking.Comms.Server.Handlers
{
    /// <summary>
    /// Host-side handler for XP gained by a client (Shared XP mode).
    /// Applies the XP to the host's player (suppressed, so it isn't re-broadcast)
    /// and relays it to every other client.
    /// </summary>
    public class PlayerXpGainedServerPacketHandler : IServerPacketHandler
    {
        public byte PacketId => (byte)ClientSentPacketId.PLAYER_XP_GAINED_PACKET;

        private readonly LobbyContext _lobbyContext;

        public PlayerXpGainedServerPacketHandler(LobbyContext lobbyContext)
        {
            _lobbyContext = lobbyContext;
        }

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new ClientXpGainedPacket(msg);

            var sender = _lobbyContext.GetPlayer(conn);
            if (sender == null) return;

            DebugLogger.Log($"[Host] Player {sender.Name} gained {packet.XpAmount} XP (shared)");

            if (Preferences.GetXpSharingMode() != Preferences.LootDistributionMode.Shared)
                return;

            // Apply to the host's own player (on the main thread, without re-broadcasting)
            GameDispatcher.Enqueue(() => PlayerXpPatches.ApplyNetworkXp(packet.XpAmount));

            // Relay to the other clients (not back to the sender)
            var relay = new SendPlayerXpGainedPacket(sender.UUID, packet.XpAmount);
            foreach (var player in _lobbyContext.GetPlayers())
            {
                if (player.Connection == null || player.UUID == sender.UUID)
                    continue;

                player.Connection.EnqueuePacket(relay);
            }
        }
    }
}
