using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Base.Packet
{
    public class SendPlayerLevelUpPacket : OutgoingPacket
    {
        public readonly byte Id = (byte)ServerSentPacketId.PLAYER_LEVEL_UP_PACKET;

        public SendPlayerLevelUpPacket(ushort playerId, int newLevel)
        {
            Message.WriteByte(Id);
            Message.WriteUShort(playerId);
            Message.WriteInt(newLevel);
        }
    }

    internal class PlayerLevelUpPacket
    {
        public ushort PlayerId { get; private set; }
        public int NewLevel { get; private set; }

        public PlayerLevelUpPacket(IncomingMessage msg)
        {
            PlayerId = msg.ReadUShort();
            NewLevel = msg.ReadInt();
        }
    }
}
