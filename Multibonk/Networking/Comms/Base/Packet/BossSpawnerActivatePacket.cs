using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;
using UnityEngine;

namespace Multibonk.Networking.Comms.Base.Packet
{
    /// <summary>
    /// Server -> Client: Boss spawner (bush) was activated by host
    /// Client should find the InteractableBossSpawner at this position and trigger Interact()
    /// </summary>
    public class SendBossSpawnerActivatePacket : OutgoingPacket
    {
        public readonly byte Id = (byte)ServerSentPacketId.BOSS_SPAWNER_ACTIVATE;

        public SendBossSpawnerActivatePacket(Vector3 position)
        {
            Message.WriteByte(Id);
            Message.WriteFloat(position.x);
            Message.WriteFloat(position.y);
            Message.WriteFloat(position.z);
        }
    }

    internal class BossSpawnerActivatePacket
    {
        public Vector3 Position { get; private set; }

        public BossSpawnerActivatePacket(IncomingMessage msg)
        {
            Position = new Vector3(
                msg.ReadFloat(),
                msg.ReadFloat(),
                msg.ReadFloat()
            );
        }
    }
}
