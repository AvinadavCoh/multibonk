using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Base.Packet
{
    /// <summary>
    /// Client -> server: "I consumed this pickup object".
    /// HostId is the pickup's wire id as assigned by the host (GetHashCode of the Pickup
    /// object on the host side, serialized as a decimal string).
    /// The server despawns its own copy and relays an ITEM_PICKED_UP packet to every
    /// other client so they remove the pickup from their worlds.
    /// </summary>
    public class SendClientPickupConsumedPacket : OutgoingPacket
    {
        public readonly byte Id = (byte)ClientSentPacketId.PICKUP_CONSUMED_PACKET;

        public SendClientPickupConsumedPacket(string hostId)
        {
            Message.WriteByte(Id);
            Message.WriteString(hostId);
        }
    }

    internal class ClientPickupConsumedPacket
    {
        public string HostId { get; private set; }

        public ClientPickupConsumedPacket(IncomingMessage msg)
        {
            HostId = msg.ReadString();
        }
    }
}
