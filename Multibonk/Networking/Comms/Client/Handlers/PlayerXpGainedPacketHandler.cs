using MelonLoader;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    public class PlayerXpGainedPacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.PLAYER_XP_GAINED_PACKET;

        public PlayerXpGainedPacketHandler()
        {
        }

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new PlayerXpGainedPacket(msg);

            MelonLogger.Msg($"Player {packet.PlayerId} gained {packet.XpAmount} XP");

            // Here we would update the visual representation or store the XP state
            // For now, we just log it
        }
    }
}
