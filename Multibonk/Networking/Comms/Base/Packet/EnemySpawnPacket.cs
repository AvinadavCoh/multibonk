using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;
using UnityEngine;

namespace Multibonk.Networking.Comms.Base.Packet
{
    /// <summary>
    /// Packet received by client when an enemy spawns on the host
    /// </summary>
    internal class EnemySpawnPacket
    {
        public int EnemyId { get; set; }
        public int EnemyType { get; set; } // EEnemy enum value
        public Vector3 Position { get; set; }
        public int Level { get; set; }
        public bool IsBoss { get; set; }
        public int Flag { get; set; } // EEnemyFlag enum value (Boss/StageBoss/Elite/...)

        public EnemySpawnPacket(IncomingMessage msg)
        {
            EnemyId = msg.ReadInt();
            EnemyType = msg.ReadInt();
            Position = new Vector3(msg.ReadFloat(), msg.ReadFloat(), msg.ReadFloat());
            Level = msg.ReadInt();
            IsBoss = msg.ReadBool();
            Flag = msg.ReadInt();
        }
    }

    /// <summary>
    /// Packet sent by host to all clients when an enemy spawns
    /// </summary>
    public class SendEnemySpawnPacket : OutgoingPacket
    {
        public readonly byte Id = (byte)ServerSentPacketId.ENEMY_SPAWN_PACKET;

        public SendEnemySpawnPacket(int enemyId, int enemyType, Vector3 position, int level, bool isBoss, int flag)
        {
            Message.WriteByte(Id);
            Message.WriteInt(enemyId);
            Message.WriteInt(enemyType);
            Message.WriteFloat(position.x);
            Message.WriteFloat(position.y);
            Message.WriteFloat(position.z);
            Message.WriteInt(level);
            Message.WriteBool(isBoss);
            Message.WriteInt(flag);
        }
    }
}
