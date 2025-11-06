using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Base.Packet
{
    /// <summary>
    /// Server broadcasts when a player takes damage
    /// Allows all clients to see health changes and damage feedback
    /// </summary>
    public class SendPlayerDamagePacket : OutgoingPacket
    {
        public readonly byte Id = (byte)ServerSentPacketId.PLAYER_DAMAGE;

        public SendPlayerDamagePacket(ushort playerId, float currentHealth, float maxHealth, float damageAmount)
        {
            Message.WriteByte(Id);
            Message.WriteUShort(playerId);
            Message.WriteFloat(currentHealth);
            Message.WriteFloat(maxHealth);
            Message.WriteFloat(damageAmount);
        }
    }

    internal class PlayerDamagePacket
    {
        public ushort PlayerId { get; private set; }
        public float CurrentHealth { get; private set; }
        public float MaxHealth { get; private set; }
        public float DamageAmount { get; private set; }

        public PlayerDamagePacket(IncomingMessage msg)
        {
            PlayerId = msg.ReadUShort();
            CurrentHealth = msg.ReadFloat();
            MaxHealth = msg.ReadFloat();
            DamageAmount = msg.ReadFloat();
        }
    }
}
