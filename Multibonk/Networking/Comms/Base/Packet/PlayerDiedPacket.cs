namespace Multibonk.Networking.Comms.Base.Packet
{
    /// <summary>
    /// Client → host: the local player has died.
    /// The server identifies the sender by Connection, so no extra payload is needed.
    /// Packet ID: ClientSentPacketId.PLAYER_DIED_PACKET (10)
    /// </summary>
    public class SendPlayerDiedPacket : OutgoingPacket
    {
        public SendPlayerDiedPacket()
        {
            Message.WriteByte((byte)ClientSentPacketId.PLAYER_DIED_PACKET);
        }
    }
}
