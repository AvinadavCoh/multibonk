using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Base.Packet
{
    /// <summary>
    /// Packet sent when a wave starts
    /// Ensures both host and clients are synchronized on wave progression
    /// </summary>
    public class SendWaveStartPacket : OutgoingPacket
    {
        public readonly byte Id = (byte)ServerSentPacketId.WAVE_START;

        public SendWaveStartPacket(int waveNumber)
        {
            Message.WriteByte(Id);
            Message.WriteInt(waveNumber); // Current wave number
        }
    }

    internal class WaveStartPacket
    {
        public int WaveNumber { get; private set; }

        public WaveStartPacket(IncomingMessage msg)
        {
            WaveNumber = msg.ReadInt();
        }
    }
}
