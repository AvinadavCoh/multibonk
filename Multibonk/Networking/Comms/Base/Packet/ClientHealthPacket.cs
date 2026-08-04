using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Base.Packet
{
    /// <summary>
    /// Client -> server: "my health changed" (took damage).
    /// The server updates its lobby state and relays it to the other clients
    /// as a SendPlayerDamagePacket.
    /// </summary>
    public class SendClientHealthPacket : OutgoingPacket
    {
        public readonly byte Id = (byte)ClientSentPacketId.PLAYER_HEALTH_PACKET;

        public SendClientHealthPacket(float currentHealth, float maxHealth, float damageAmount)
        {
            Message.WriteByte(Id);
            Message.WriteFloat(currentHealth);
            Message.WriteFloat(maxHealth);
            Message.WriteFloat(damageAmount);
        }
    }

    internal class ClientHealthPacket
    {
        public float CurrentHealth { get; private set; }
        public float MaxHealth { get; private set; }
        public float DamageAmount { get; private set; }

        public ClientHealthPacket(IncomingMessage msg)
        {
            CurrentHealth = msg.ReadFloat();
            MaxHealth = msg.ReadFloat();
            DamageAmount = msg.ReadFloat();
        }
    }
}
