using MelonLoader;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    public class EnemyHealthUpdatePacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.ENEMY_HEALTH_UPDATE_PACKET;

        public EnemyHealthUpdatePacketHandler()
        {
        }

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new EnemyHealthUpdatePacket(msg);

            float healthPercent = (packet.CurrentHealth / packet.MaxHealth) * 100f;
            MelonLogger.Msg($"Enemy {packet.EnemyId} health: {packet.CurrentHealth}/{packet.MaxHealth} ({healthPercent:F1}%)");

            // TODO: Update enemy health in game world
            // Will require finding the enemy object and updating its health component
            
            // IDEAL IMPLEMENTATION:
            // 1. Find enemy GameObject by ID
            // 2. Get enemy health component
            // 3. Smoothly interpolate health bar from current to new value (not instant)
            // 4. Play damage effect if health decreased significantly
        }
    }
}
