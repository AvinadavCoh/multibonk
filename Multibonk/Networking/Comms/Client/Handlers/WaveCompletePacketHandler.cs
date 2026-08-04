using MelonLoader;
using Multibonk.Game.Handlers;
using Multibonk.Game.Patches;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    /// <summary>
    /// Client-side handler for wave complete packets.
    /// In Megabonk this signals that the host's SummonerController started the final swarm.
    /// The client's own trigger is blocked by WaveProgressionPatches, so replay it here.
    /// </summary>
    public class WaveCompletePacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.WAVE_COMPLETE;

        public WaveCompletePacketHandler() { }

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new WaveCompletePacket(msg);

            MelonLogger.Msg("[Client] Host started the final swarm");

            // Queue to the main Unity thread
            GameDispatcher.Enqueue(() =>
            {
                try
                {
                    var controller = WaveProgressionPatches.GetSummonerController();
                    if (controller == null)
                    {
                        MelonLogger.Warning("[Client] SummonerController not found - cannot start final swarm");
                        return;
                    }

                    WaveProgressionPatches.AllowNetworkEvent = true;
                    try
                    {
                        controller.StartFinalSwarm();
                    }
                    finally
                    {
                        WaveProgressionPatches.AllowNetworkEvent = false;
                    }

                    MelonLogger.Msg("[Client] ✓ Final swarm started");
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"[Client] Failed to start final swarm: {ex.Message}");
                }
            });
        }
    }
}
