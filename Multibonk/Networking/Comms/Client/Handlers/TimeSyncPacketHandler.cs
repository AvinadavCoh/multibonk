using Multibonk.Game.Handlers;
using Multibonk.Game.Patches;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    /// <summary>
    /// Client-side handler for the host's periodic clock sync.
    /// Snaps the local stage/run timers to the host's when drift exceeds tolerance.
    /// </summary>
    public class TimeSyncPacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.TIME_SYNC;

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new TimeSyncPacket(msg);

            GameDispatcher.Enqueue(() =>
            {
                TimeSyncPatches.ApplyHostTime(packet.StageTime, packet.RunTime, packet.Paused);
            });
        }
    }
}
