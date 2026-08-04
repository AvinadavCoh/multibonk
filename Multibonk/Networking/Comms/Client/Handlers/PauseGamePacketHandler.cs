using Multibonk.Game.Handlers;
using Multibonk.Game.Patches;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    /// <summary>
    /// Client-side handler: the host paused - pause locally too.
    /// </summary>
    public class PauseGamePacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.PAUSE_GAME;

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var _ = new PauseGamePacket(msg);
            GameDispatcher.Enqueue(TimeSyncPatches.ApplyNetworkPause);
        }
    }

    /// <summary>
    /// Client-side handler: the host unpaused - resume (only network-initiated pauses).
    /// </summary>
    public class UnpauseGamePacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.UNPAUSE_GAME;

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var _ = new UnpauseGamePacket(msg);
            GameDispatcher.Enqueue(TimeSyncPatches.ApplyNetworkUnpause);
        }
    }
}
