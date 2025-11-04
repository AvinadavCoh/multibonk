using MelonLoader;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    public class EnemyDeathPacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.ENEMY_DEATH_PACKET;

        public EnemyDeathPacketHandler()
        {
        }

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new EnemyDeathPacket(msg);

            MelonLogger.Msg($"Enemy died: {packet.EnemyId}");

            // TODO: Remove enemy from game world
            // Will require finding the game's enemy management system
            // Likely: GameObject.Destroy(enemyObject) or similar
            
            // IDEAL: Also play death animation/effects on client
        }
    }
}
