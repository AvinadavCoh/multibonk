using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Base.Packet
{
    /// <summary>
    /// Packet sent when a player collects gold/coins
    /// Allows synchronizing gold collection across all clients based on sharing mode
    /// </summary>
    public class SendPlayerGoldGainedPacket : OutgoingPacket
    {
        public readonly byte Id = (byte)ServerSentPacketId.PLAYER_GOLD_GAINED;

        public SendPlayerGoldGainedPacket(int goldAmount)
        {
            Message.WriteByte(Id);
            Message.WriteInt(goldAmount); // Amount of gold collected
        }
    }

    internal class PlayerGoldGainedPacket
    {
        public int GoldAmount { get; private set; }

        public PlayerGoldGainedPacket(IncomingMessage msg)
        {
            GoldAmount = msg.ReadInt();
        }
    }
}
