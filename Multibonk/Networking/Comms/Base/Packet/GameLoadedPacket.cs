using System.Threading.Tasks;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;
using UnityEngine;

namespace Multibonk.Networking.Comms.Base.Packet
{
    internal class GameLoadedPacket
    {
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }

        public GameLoadedPacket(IncomingMessage msg)
        {
            Position = new Vector3(msg.ReadFloat(), msg.ReadFloat(), msg.ReadFloat());
            Rotation = new Quaternion(msg.ReadFloat(), msg.ReadFloat(), msg.ReadFloat(), msg.ReadFloat());
        }
    }

    public class SendGameLoadedPacket : OutgoingPacket
    {
        public readonly byte Id = (byte)ClientSentPacketId.GAME_LOADED_PACKET;

        public SendGameLoadedPacket(Vector3 position, Quaternion rotation)
        {
            Message.WriteByte(Id);
            Message.WriteFloat(position.x);
            Message.WriteFloat(position.y);
            Message.WriteFloat(position.z);
            Message.WriteFloat(rotation.x);
            Message.WriteFloat(rotation.y);
            Message.WriteFloat(rotation.z);
            Message.WriteFloat(rotation.w);
        }
    }
}
