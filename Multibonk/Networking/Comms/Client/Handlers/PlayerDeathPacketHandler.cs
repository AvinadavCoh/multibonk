using MelonLoader;
using Multibonk.Game.Handlers;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    /// <summary>
    /// Client-side handler for player death packets
    /// In multiplayer mode, players don't leave lobby on death
    /// Game only ends when all players are dead
    /// </summary>
    public class PlayerDeathPacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.PLAYER_DEATH;

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new PlayerDeathPacket(msg);

            MelonLogger.Msg($"[Client] Player {packet.PlayerId} died at position ({packet.DeathPosition.x:F2}, {packet.DeathPosition.y:F2}, {packet.DeathPosition.z:F2})");

            // Queue the death handling to happen on the main Unity thread
            GameDispatcher.Enqueue(() =>
            {
                try
                {
                    // TODO: Show death animation/effect for the player
                    // TODO: Keep player in lobby (don't disconnect)
                    // TODO: Check if all players are dead -> end game
                    // Example: PlayerDeathHandler.HandleDeath(packet.PlayerId, packet.DeathPosition);
                    MelonLogger.Msg($"[Client] Processing death for player {packet.PlayerId}");
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"[Client] Failed to handle player death: {ex.Message}");
                }
            });
        }
    }
}
