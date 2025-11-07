using MelonLoader;
using Multibonk.Game.Handlers;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    /// <summary>
    /// Client-side handler for wave complete packets
    /// When server completes a wave, client updates its wave state
    /// 
    /// TEST:
    /// 1. Host completes a wave
    /// 2. Client should receive wave complete notification
    /// 3. Check logs for "[Client] Wave X completed"
    /// </summary>
    public class WaveCompletePacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.WAVE_COMPLETE;

        public WaveCompletePacketHandler() { }

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new WaveCompletePacket(msg);

            MelonLogger.Msg($"[Client] Wave {packet.WaveNumber} completed");

            // Queue wave complete to happen on main Unity thread
            GameDispatcher.Enqueue(() =>
            {
                // TODO: Find the correct wave manager class and trigger wave complete
                // This might involve:
                // - Finding WaveManager or similar class
                // - Calling CompleteWave() or similar method
                // - Triggering wave complete UI/rewards
                
                MelonLogger.Msg($"[Client] ✓ Wave {packet.WaveNumber} complete acknowledged");
            });
        }
    }
}
