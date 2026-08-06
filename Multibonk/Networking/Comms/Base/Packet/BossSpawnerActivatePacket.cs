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

        /// <summary>
        /// Wire layout (after packet-id byte):
        ///   float x, float y, float z  — world position (12 bytes)
        ///   byte  spawnerType           — 0 = InteractableBossSpawner (regular/bush)
        ///                                 1 = InteractableBossSpawnerFinal
        /// </summary>
        public SendBossSpawnerActivatePacket(Vector3 position, byte spawnerType)
        {
            Message.WriteByte(Id);
            Message.WriteFloat(position.x);
            Message.WriteFloat(position.y);
            Message.WriteFloat(position.z);
            Message.WriteByte(spawnerType);
        }
    }

    internal class BossSpawnerActivatePacket
    {
        public Vector3 Position { get; private set; }
        /// <summary>0 = regular (InteractableBossSpawner), 1 = final (InteractableBossSpawnerFinal)</summary>
        public byte SpawnerType { get; private set; }

        public BossSpawnerActivatePacket(IncomingMessage msg)
        {
            Position = new Vector3(
                msg.ReadFloat(),
                msg.ReadFloat(),
                msg.ReadFloat()
            );
            SpawnerType = msg.ReadByte();
        }
    }
}
