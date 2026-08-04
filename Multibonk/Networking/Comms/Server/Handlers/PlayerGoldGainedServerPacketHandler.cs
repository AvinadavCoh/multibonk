using Multibonk.Game.Handlers;
using Multibonk.Game.Patches;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;
using Multibonk.Networking.Lobby;

namespace Multibonk.Networking.Comms.Server.Handlers
{
    /// <summary>
    /// Host-side handler for gold gained by a client (Shared gold mode).
    /// Applies the gold to the host's player (suppressed) and relays to other clients.
    /// </summary>
    public class PlayerGoldGainedServerPacketHandler : IServerPacketHandler
    {
        public byte PacketId => (byte)ClientSentPacketId.PLAYER_GOLD_GAINED_PACKET;

        private readonly LobbyContext _lobbyContext;

        public PlayerGoldGainedServerPacketHandler(LobbyContext lobbyContext)
        {
            _lobbyContext = lobbyContext;
        }

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new ClientGoldGainedPacket(msg);

            var sender = _lobbyContext.GetPlayer(conn);
            if (sender == null) return;

            DebugLogger.Log($"[Host] Player {sender.Name} gained {packet.GoldAmount} gold (shared)");

            if (Preferences.GetGoldSharingMode() != Preferences.LootDistributionMode.Shared)
                return;

            // Apply to the host's own player (on the main thread, without re-broadcasting)
            GameDispatcher.Enqueue(() => PlayerGoldPatches.ApplyNetworkGold(packet.GoldAmount));

            // Relay to the other clients (not back to the sender)
            var relay = new SendPlayerGoldGainedPacket(packet.GoldAmount);
            foreach (var player in _lobbyContext.GetPlayers())
            {
                if (player.Connection == null || player.UUID == sender.UUID)
                    continue;

                player.Connection.EnqueuePacket(relay);
            }
        }
    }
}
