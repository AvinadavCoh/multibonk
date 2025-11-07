using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Base.Packet
{
    /// <summary>
    /// Server -> Client: Host activated portal to load next stage
    /// Client should trigger InteractableBossSpawnerFinal.DoLoadNextStage()
    /// No additional data needed - the portal itself handles which stage to load
    /// </summary>
    public class SendStageTransitionPacket : OutgoingPacket
    {
        public readonly byte Id = (byte)ServerSentPacketId.STAGE_TRANSITION;

        public SendStageTransitionPacket()
        {
            Message.WriteByte(Id);
        }
    }

    internal class StageTransitionPacket
    {
        public StageTransitionPacket(IncomingMessage msg)
        {
            // No additional data needed - just the packet ID triggers the transition
        }
    }
}
