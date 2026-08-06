using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Base.Packet
{
    /// <summary>
    /// Client → host: the local player has finished picking (or skipping) their upgrade.
    /// The host identifies the sender by Connection; no extra payload is needed.
    /// Packet ID: ClientSentPacketId.LEVELUP_DONE_PACKET (11)
    /// </summary>
    public class SendLevelupDonePacket : OutgoingPacket
    {
        public SendLevelupDonePacket()
        {
            Message.WriteByte((byte)ClientSentPacketId.LEVELUP_DONE_PACKET);
        }
    }

    /// <summary>
    /// Host → all clients: every player has finished picking; resume the game.
    /// Carries a monotonic cycle number so the client can detect and discard stale
    /// resumes from a previous level-up cycle that arrive during a new one.
    /// Packet ID: ServerSentPacketId.LEVELUP_RESUME (31)
    /// </summary>
    public class SendLevelupResumePacket : OutgoingPacket
    {
        public SendLevelupResumePacket(int cycleNumber)
        {
            Message.WriteByte((byte)ServerSentPacketId.LEVELUP_RESUME);
            Message.WriteInt(cycleNumber);
        }
    }

    // Incoming wrappers

    internal class LevelupDonePacket
    {
        public LevelupDonePacket(IncomingMessage msg) { }
    }

    internal class LevelupResumePacket
    {
        public int CycleNumber { get; }

        public LevelupResumePacket(IncomingMessage msg)
        {
            CycleNumber = msg.ReadInt();
        }
    }
}
