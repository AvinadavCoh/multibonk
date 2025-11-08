using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Base.Packet
{
    /// <summary>
    /// Server -> Client: Host clicked "Start Game" in lobby, begin actual game
    /// This is sent from the lobby after players are ready
    /// </summary>
    public class SendStartActualGamePacket : OutgoingPacket
    {
        public readonly byte Id = (byte)ServerSentPacketId.START_ACTUAL_GAME;

        public SendStartActualGamePacket()
        {
            Message.WriteByte(Id);
        }
    }

    internal class StartActualGamePacket
    {
        public StartActualGamePacket(IncomingMessage msg)
        {
            // No additional data needed
        }
    }
}
