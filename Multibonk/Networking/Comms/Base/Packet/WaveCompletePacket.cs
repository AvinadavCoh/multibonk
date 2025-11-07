using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Base.Packet
{
    /// <summary>
    /// Packet sent when a wave completes
    /// Keeps wave state synchronized between all players
    /// </summary>
    public class SendWaveCompletePacket : OutgoingPacket
    {
        public readonly byte Id = (byte)ServerSentPacketId.WAVE_COMPLETE;

        public SendWaveCompletePacket(int waveNumber)
        {
            Message.WriteByte(Id);
            Message.WriteInt(waveNumber); // Completed wave number
        }
    }

    internal class WaveCompletePacket
    {
        public int WaveNumber { get; private set; }

        public WaveCompletePacket(IncomingMessage msg)
        {
            WaveNumber = msg.ReadInt();
        }
    }
}
