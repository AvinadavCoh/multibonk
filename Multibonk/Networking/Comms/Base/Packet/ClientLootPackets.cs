using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Base.Packet
{
    /// <summary>
    /// Client -> server: "I gained XP" (used for Shared XP mode).
    /// The server applies it locally and rebroadcasts to the other clients.
    /// </summary>
    public class SendClientXpGainedPacket : OutgoingPacket
    {
        public readonly byte Id = (byte)ClientSentPacketId.PLAYER_XP_GAINED_PACKET;

        public SendClientXpGainedPacket(int xpAmount)
        {
            Message.WriteByte(Id);
            Message.WriteInt(xpAmount);
        }
    }

    internal class ClientXpGainedPacket
    {
        public int XpAmount { get; private set; }

        public ClientXpGainedPacket(IncomingMessage msg)
        {
            XpAmount = msg.ReadInt();
        }
    }

    /// <summary>
    /// Client -> server: "I gained gold" (used for Shared gold mode).
    /// </summary>
    public class SendClientGoldGainedPacket : OutgoingPacket
    {
        public readonly byte Id = (byte)ClientSentPacketId.PLAYER_GOLD_GAINED_PACKET;

        public SendClientGoldGainedPacket(int goldAmount)
        {
            Message.WriteByte(Id);
            Message.WriteInt(goldAmount);
        }
    }

    internal class ClientGoldGainedPacket
    {
        public int GoldAmount { get; private set; }

        public ClientGoldGainedPacket(IncomingMessage msg)
        {
            GoldAmount = msg.ReadInt();
        }
    }
}
