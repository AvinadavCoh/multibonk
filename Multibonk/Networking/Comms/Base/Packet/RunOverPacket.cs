namespace Multibonk.Networking.Comms.Base.Packet
{
    /// <summary>
    /// Host → all clients: every player is confirmed dead; the run is over.
    /// Packet ID: ServerSentPacketId.RUN_OVER (29)
    /// </summary>
    public class SendRunOverPacket : OutgoingPacket
    {
        public SendRunOverPacket()
        {
            Message.WriteByte((byte)ServerSentPacketId.RUN_OVER);
        }
    }
}
