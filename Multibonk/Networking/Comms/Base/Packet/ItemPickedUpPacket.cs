using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Base.Packet
{
    public class SendItemPickedUpPacket : OutgoingPacket
    {
        public readonly byte Id = (byte)ServerSentPacketId.ITEM_PICKED_UP_PACKET;

        public SendItemPickedUpPacket(string itemId, ushort playerId)
        {
            Message.WriteByte(Id);
            Message.WriteString(itemId);
            Message.WriteUShort(playerId);
        }
    }

    internal class ItemPickedUpPacket
    {
        public string ItemId { get; private set; }
        public ushort PlayerId { get; private set; }

        public ItemPickedUpPacket(IncomingMessage msg)
        {
            ItemId = msg.ReadString();
            PlayerId = msg.ReadUShort();
        }
    }
}
