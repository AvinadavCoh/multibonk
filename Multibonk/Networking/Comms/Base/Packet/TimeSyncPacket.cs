using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Base.Packet
{
    /// <summary>
    /// Host -> clients: periodic clock sync so everyone runs on the host's stage/run timers.
    /// </summary>
    public class SendTimeSyncPacket : OutgoingPacket
    {
        public readonly byte Id = (byte)ServerSentPacketId.TIME_SYNC;

        public SendTimeSyncPacket(float stageTime, float runTime, bool paused)
        {
            Message.WriteByte(Id);
            Message.WriteFloat(stageTime);
            Message.WriteFloat(runTime);
            Message.WriteBool(paused);
        }
    }

    internal class TimeSyncPacket
    {
        public float StageTime { get; private set; }
        public float RunTime { get; private set; }
        public bool Paused { get; private set; }

        public TimeSyncPacket(IncomingMessage msg)
        {
            StageTime = msg.ReadFloat();
            RunTime = msg.ReadFloat();
            Paused = msg.ReadBool();
        }
    }
}
