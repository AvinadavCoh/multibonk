using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Base.Packet
{
    /// <summary>
    /// Server broadcasts when a shrine is used by any player
    /// All clients should see the shrine effect and apply upgrades
    /// </summary>
    public class SendShrineUsePacket : OutgoingPacket
    {
        public readonly byte Id = (byte)ServerSentPacketId.SHRINE_USE;

        public SendShrineUsePacket(string shrineId, ushort playerId, int shrineType)
        {
            Message.WriteByte(Id);
            Message.WriteString(shrineId);
            Message.WriteUShort(playerId);
            Message.WriteInt(shrineType);
        }
    }

    internal class ShrineUsePacket
    {
        public string ShrineId { get; private set; }
        public ushort PlayerId { get; private set; }
        public int ShrineType { get; private set; }

        public ShrineUsePacket(IncomingMessage msg)
        {
            ShrineId = msg.ReadString();
            PlayerId = msg.ReadUShort();
            ShrineType = msg.ReadInt();
        }
    }
}
