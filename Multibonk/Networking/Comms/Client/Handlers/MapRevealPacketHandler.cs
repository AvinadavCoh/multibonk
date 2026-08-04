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
    /// Handles individual map fog reveals from the host.
    /// TileX/TileY carry world-space X/Z coordinates (see MinimapSyncPatches).
    /// </summary>
    public class MapRevealPacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.MAP_REVEAL;

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new MapRevealPacket(msg);

            DebugLogger.Log($"[Client] Received map reveal at world ({packet.TileX}, {packet.TileY})");

            // Queue the reveal to happen on the main Unity thread
            GameDispatcher.Enqueue(() =>
            {
                MinimapSyncPatches.RevealAt(packet.TileX, packet.TileY);
                SyncTelemetry.RecordApplied(SyncChannel.MapReveal);
            });
        }
    }

    /// <summary>
    /// Handles bulk map reveal data (sent when joining or on full sync)
    /// </summary>
    public class MapRevealBulkPacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.MAP_REVEAL_BULK;

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new MapRevealBulkPacket(msg);

            MelonLogger.Msg($"[Client] Received bulk map reveal: {packet.TileXCoords.Length} positions");

            // Queue the reveal to happen on the main Unity thread
            GameDispatcher.Enqueue(() =>
            {
                for (int i = 0; i < packet.TileXCoords.Length; i++)
                {
                    MinimapSyncPatches.RevealAt(packet.TileXCoords[i], packet.TileYCoords[i]);

                    // Counted per tile: the host already incremented MapReveal once per tile
                    // as it explored. A client that joins mid-run catches up through this bulk
                    // packet, and without counting each tile the detector would report the
                    // whole backlog as "applied 0 - handler appears to be a no-op".
                    SyncTelemetry.RecordApplied(SyncChannel.MapReveal);
                }
            });
        }
    }
}
