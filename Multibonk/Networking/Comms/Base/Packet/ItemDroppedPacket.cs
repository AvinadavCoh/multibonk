using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;
using UnityEngine;

namespace Multibonk.Networking.Comms.Base.Packet
{
    public class SendItemDroppedPacket : OutgoingPacket
    {
        public readonly byte Id = (byte)ServerSentPacketId.ITEM_DROPPED_PACKET;

        public SendItemDroppedPacket(string itemId, Vector3 position, int itemType)
        {
            Message.WriteByte(Id);
            Message.WriteString(itemId);
            Message.WriteFloat(position.x);
            Message.WriteFloat(position.y);
            Message.WriteFloat(position.z);
            Message.WriteInt(itemType);
        }
    }

    internal class ItemDroppedPacket
    {
        public string ItemId { get; private set; }
        public Vector3 Position { get; private set; }
        public int ItemType { get; private set; }

        public ItemDroppedPacket(IncomingMessage msg)
        {
            ItemId = msg.ReadString();
            Position = new Vector3(msg.ReadFloat(), msg.ReadFloat(), msg.ReadFloat());
            ItemType = msg.ReadInt();
        }
    }
}
