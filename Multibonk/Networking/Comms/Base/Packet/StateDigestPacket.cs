using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Base.Packet
{
    /// <summary>
    /// Host -> clients: periodic snapshot of authoritative state plus the host's
    /// per-channel sent counters, so a client can diff it against its own ledger and
    /// report which subsystem is drifting. Diagnostics only - never changes gameplay.
    /// </summary>
    public class SendStateDigestPacket : OutgoingPacket
    {
        public readonly byte Id = (byte)ServerSentPacketId.STATE_DIGEST;

        public SendStateDigestPacket(ushort sequence, float stageTime, float runTime,
            int gold, int level, int liveEnemies, int livePickups, int[] sentCounters)
        {
            Message.WriteByte(Id);
            Message.WriteUShort(sequence);
            Message.WriteFloat(stageTime);
            Message.WriteFloat(runTime);
            Message.WriteInt(gold);
            Message.WriteInt(level);
            Message.WriteInt(liveEnemies);
            Message.WriteInt(livePickups);

            Message.WriteByte((byte)sentCounters.Length);
            for (int i = 0; i < sentCounters.Length; i++)
                Message.WriteInt(sentCounters[i]);
        }
    }

    internal class StateDigestPacket
    {
        public ushort Sequence { get; private set; }
        public float StageTime { get; private set; }
        public float RunTime { get; private set; }
        public int Gold { get; private set; }
        public int Level { get; private set; }
        public int LiveEnemies { get; private set; }
        public int LivePickups { get; private set; }
        public int[] SentCounters { get; private set; }

        public StateDigestPacket(IncomingMessage msg)
        {
            Sequence = msg.ReadUShort();
            StageTime = msg.ReadFloat();
            RunTime = msg.ReadFloat();
            Gold = msg.ReadInt();
            Level = msg.ReadInt();
            LiveEnemies = msg.ReadInt();
            LivePickups = msg.ReadInt();

            int count = msg.ReadByte();
            SentCounters = new int[count];
            for (int i = 0; i < count; i++)
                SentCounters[i] = msg.ReadInt();
        }
    }
}
