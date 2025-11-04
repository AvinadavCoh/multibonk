using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Base.Packet
{
    public class SendPlayerXpGainedPacket : OutgoingPacket
    {
        public readonly byte Id = (byte)ServerSentPacketId.PLAYER_XP_GAINED_PACKET;

        public SendPlayerXpGainedPacket(ushort playerId, int xpAmount)
        {
            Message.WriteByte(Id);
            Message.WriteUShort(playerId);
            Message.WriteInt(xpAmount);
        }
    }

    internal class PlayerXpGainedPacket
    {
        public ushort PlayerId { get; private set; }
        public int XpAmount { get; private set; }

        public PlayerXpGainedPacket(IncomingMessage msg)
        {
            PlayerId = msg.ReadUShort();
            XpAmount = msg.ReadInt();
        }
    }
}
