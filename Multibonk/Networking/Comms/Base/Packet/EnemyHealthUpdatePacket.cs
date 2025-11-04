using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Base.Packet
{
    /// <summary>
    /// Packet for syncing enemy health, primarily for bosses and tanky enemies.
    /// Only sent when significant health changes occur (>10% threshold).
    /// 
    /// CURRENT IMPLEMENTATION: Full health snapshot
    /// Packet size: ~20 bytes (1 byte ID + string + 2 floats)
    /// Frequency: ~4-5 times per boss fight (10% thresholds)
    /// 
    /// IDEAL OPTIMIZATIONS (Future):
    /// 1. Delta Compression: Send health change amount instead of full value
    ///    - Current: 8 bytes (currentHP + maxHP)
    ///    - Optimized: 2 bytes (delta as short)
    /// 
    /// 2. Percentage Instead of Absolute:
    ///    - Current: float currentHP = 45000.0f (4 bytes)
    ///    - Optimized: byte percentage = 45 (1 byte, 0-100 range)
    /// 
    /// 3. Batching Multiple Enemies:
    ///    - Current: 1 packet per enemy
    ///    - Optimized: EnemyHealthBatchPacket { [id1: 50%], [id2: 75%], ... }
    /// 
    /// 4. Priority System (ROR2-style):
    ///    - High priority: Bosses, on-screen enemies, player-targeted
    ///    - Low priority: Off-screen, full-health enemies
    ///    - Sync high priority more frequently
    /// 
    /// Bandwidth comparison:
    /// - Current: 20 bytes × 5 updates = 100 bytes per boss
    /// - Optimized: 3 bytes × 5 updates = 15 bytes per boss (6.6x reduction)
    /// </summary>
    public class SendEnemyHealthUpdatePacket : OutgoingPacket
    {
        public readonly byte Id = (byte)ServerSentPacketId.ENEMY_HEALTH_UPDATE_PACKET;

        public SendEnemyHealthUpdatePacket(string enemyId, float currentHealth, float maxHealth)
        {
            Message.WriteByte(Id);
            Message.WriteString(enemyId);
            Message.WriteFloat(currentHealth);
            Message.WriteFloat(maxHealth);
        }
    }

    internal class EnemyHealthUpdatePacket
    {
        public string EnemyId { get; private set; }
        public float CurrentHealth { get; private set; }
        public float MaxHealth { get; private set; }

        public EnemyHealthUpdatePacket(IncomingMessage msg)
        {
            EnemyId = msg.ReadString();
            CurrentHealth = msg.ReadFloat();
            MaxHealth = msg.ReadFloat();
        }
    }
}
