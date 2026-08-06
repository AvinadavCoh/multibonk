using MelonLoader;
using Multibonk.Game;
using Multibonk.Game.Handlers;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    /// <summary>
    /// Client-side handler for LEVELUP_RESUME (ServerSentPacketId = 31).
    ///
    /// The host sends this when all players have finished picking their upgrade.
    /// The packet carries a monotonic cycle number; ClientResume() discards it if the
    /// number does not match the client's current cycle (stale packet from a previous
    /// level-up cycle that arrived late).
    ///
    /// ClientResume() is called on the Unity main thread (via GameDispatcher.Enqueue)
    /// and calls MyTime.Unpause() directly — bypassing the networkPauseActive guard in
    /// TimeSyncPatches so the client resumes reliably regardless of whether its initial
    /// level-up pause was self-initiated or network-initiated.
    /// </summary>
    public class LevelupResumePacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.LEVELUP_RESUME;

        public void Handle(IncomingMessage msg, Connection conn)
        {
            try
            {
                var packet = new LevelupResumePacket(msg);

                MelonLogger.Msg($"[Client] Received LEVELUP_RESUME (cycle {packet.CycleNumber}) – resuming game");
                int cycle = packet.CycleNumber;
                GameDispatcher.Enqueue(() => LevelUpCoordinator.ClientResume(cycle));
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Client] Failed to handle LEVELUP_RESUME: {ex.Message}");
            }
        }
    }
}
