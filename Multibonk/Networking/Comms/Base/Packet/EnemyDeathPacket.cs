using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Base.Packet
{
    /// <summary>
    /// Packet sent when an enemy dies. Simple and efficient for regular enemies.
    /// 
    /// CURRENT IMPLEMENTATION: Basic death notification
    /// Packet size: ~12 bytes (1 byte ID + string enemy identifier)
    /// 
    /// IDEAL OPTIMIZATION (Future):
    /// - Use ushort (2 bytes) instead of string for enemy ID
    /// - Batch multiple deaths: SendEnemyDeathBatchPacket([id1, id2, id3])
    /// - Add death cause/killer info for better visual feedback
    /// </summary>
    public class SendEnemyDeathPacket : OutgoingPacket
    {
        public readonly byte Id = (byte)ServerSentPacketId.ENEMY_DEATH_PACKET;

        public SendEnemyDeathPacket(string enemyId)
        {
            Message.WriteByte(Id);
            Message.WriteString(enemyId);
        }
    }

    internal class EnemyDeathPacket
    {
        public string EnemyId { get; private set; }

        public EnemyDeathPacket(IncomingMessage msg)
        {
            EnemyId = msg.ReadString();
        }
    }
}
