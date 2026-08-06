using MelonLoader;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;
using Multibonk.Networking.Lobby;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    public class PlayerLevelUpPacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.PLAYER_LEVEL_UP_PACKET;

        public PlayerLevelUpPacketHandler()
        {
        }

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new PlayerLevelUpPacket(msg);

            MelonLogger.Msg($"Player {packet.PlayerId} leveled up to level {packet.NewLevel}!");

            // Update the player's level in LobbyContext so the Players HUD can display it.
            var lobby = LobbyPatchFlags.CurrentLobby;
            if (lobby == null)
                return;

            var player = lobby.GetPlayer(packet.PlayerId);
            if (player != null)
            {
                player.Level = packet.NewLevel;
            }
        }
    }
}
