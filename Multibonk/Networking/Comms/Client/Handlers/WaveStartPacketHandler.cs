using MelonLoader;
using Multibonk.Game.Diagnostics;
using Multibonk.Game.Handlers;
using Multibonk.Game.Patches;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    /// <summary>
    /// Client-side handler for wave start packets.
    /// WaveNumber carries the host's StageTimeline event index. The client's own timeline
    /// ticking is blocked by WaveProgressionPatches, so this replays the event locally,
    /// keeping swarm/miniboss timing in sync with the host.
    /// </summary>
    public class WaveStartPacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.WAVE_START;

        public WaveStartPacketHandler() { }

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new WaveStartPacket(msg);

            MelonLogger.Msg($"[Client] Timeline event {packet.WaveNumber} started by host");

            // Queue to the main Unity thread
            GameDispatcher.Enqueue(() =>
            {
                try
                {
                    var controller = WaveProgressionPatches.GetSummonerController();
                    if (controller == null)
                    {
                        MelonLogger.Warning("[Client] SummonerController not found - cannot replay timeline event");
                        return;
                    }

                    WaveProgressionPatches.AllowNetworkEvent = true;
                    try
                    {
                        controller.StartEvent(packet.WaveNumber);
                    }
                    finally
                    {
                        WaveProgressionPatches.AllowNetworkEvent = false;
                    }

                    MelonLogger.Msg($"[Client] ✓ Replayed timeline event {packet.WaveNumber}");
                    SyncTelemetry.RecordApplied(SyncChannel.TimelineEvent);
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"[Client] Failed to replay timeline event {packet.WaveNumber}: {ex.Message}");
                }
            });
        }
    }
}
