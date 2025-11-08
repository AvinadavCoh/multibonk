using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Base.Packet
{
    /// <summary>
    /// Server -> Client: Join the multiplayer lobby scene
    /// Sent after map selection, before actual game start
    /// </summary>
    public class SendJoinLobbyScenePacket : OutgoingPacket
    {
        public readonly byte Id = (byte)ServerSentPacketId.JOIN_LOBBY_SCENE;

        public SendJoinLobbyScenePacket(int seed)
        {
            Message.WriteByte(Id);
            Message.WriteInt(seed);
        }
    }

    internal class JoinLobbyScenePacket
    {
        public int Seed { get; private set; }

        public JoinLobbyScenePacket(IncomingMessage msg)
        {
            Seed = msg.ReadInt();
        }
    }
}
