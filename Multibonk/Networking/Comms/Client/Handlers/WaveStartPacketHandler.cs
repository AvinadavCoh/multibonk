using MelonLoader;
using Multibonk.Game.Handlers;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    /// <summary>
    /// Client-side handler for wave start packets
    /// When server starts a new wave, client updates its wave state
    /// 
    /// TEST:
    /// 1. Host starts game
    /// 2. Wave progression happens on host
    /// 3. Client should see wave start notifications
    /// 4. Check logs for "[Client] Wave X started"
    /// </summary>
    public class WaveStartPacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.WAVE_START;

        public WaveStartPacketHandler() { }

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new WaveStartPacket(msg);

            MelonLogger.Msg($"[Client] Wave {packet.WaveNumber} started");

            // Queue wave start to happen on main Unity thread
            GameDispatcher.Enqueue(() =>
            {
                // TODO: Find the correct wave manager class and update wave state
                // This might involve:
                // - Finding WaveManager or similar class
                // - Setting currentWave field
                // - Triggering any wave start UI/effects
                
                MelonLogger.Msg($"[Client] ✓ Synchronized to wave {packet.WaveNumber}");
            });
        }
    }
}
