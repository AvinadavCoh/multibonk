using MelonLoader;
using Multibonk.Game;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Server.Handlers
{
    /// <summary>
    /// Host-side handler for LEVELUP_DONE_PACKET (ClientSentPacketId = 11).
    ///
    /// A client sends this when it has closed its upgrade screen (picked, skipped, or
    /// banished an upgrade).  The host removes the sender from the pending-player set;
    /// if no one is left waiting LevelUpCoordinator sends LEVELUP_RESUME to all clients
    /// and unpauses the host.
    /// </summary>
    public class LevelupDoneServerPacketHandler : IServerPacketHandler
    {
        public byte PacketId => (byte)ClientSentPacketId.LEVELUP_DONE_PACKET;

        public void Handle(IncomingMessage msg, Connection conn)
        {
            try
            {
                var _ = new LevelupDonePacket(msg); // empty payload

                DebugLogger.Log("[Host] Received LEVELUP_DONE from a client");
                LevelUpCoordinator.OnClientPlayerDone(conn);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Host] Failed to handle LEVELUP_DONE_PACKET: {ex.Message}");
            }
        }
    }
}
