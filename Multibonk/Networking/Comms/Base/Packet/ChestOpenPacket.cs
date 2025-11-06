using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Base.Packet
{
    /// <summary>
    /// Server broadcasts when a chest is opened by any player
    /// Ensures all clients see the chest as opened and can see/collect the loot
    /// </summary>
    public class SendChestOpenPacket : OutgoingPacket
    {
        public readonly byte Id = (byte)ServerSentPacketId.CHEST_OPEN;

        public SendChestOpenPacket(string chestId, ushort playerId)
        {
            Message.WriteByte(Id);
            Message.WriteString(chestId);
            Message.WriteUShort(playerId);
        }
    }

    internal class ChestOpenPacket
    {
        public string ChestId { get; private set; }
        public ushort PlayerId { get; private set; }

        public ChestOpenPacket(IncomingMessage msg)
        {
            ChestId = msg.ReadString();
            PlayerId = msg.ReadUShort();
        }
    }
}
