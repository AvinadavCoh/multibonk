using MelonLoader;
using Multibonk.Game.Handlers;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    /// <summary>
    /// Handles individual map tile reveals from the host
    /// </summary>
    public class MapRevealPacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.MAP_REVEAL;

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new MapRevealPacket(msg);

            MelonLogger.Msg($"[Client] Received map reveal: tile ({packet.TileX}, {packet.TileY})");

            // Queue the reveal to happen on the main Unity thread
            GameDispatcher.Enqueue(() =>
            {
                try
                {
                    // TODO: Find the minimap/map reveal class and call its reveal method
                    // Example: MinimapManager.RevealTile(packet.TileX, packet.TileY);
                    MelonLogger.Msg($"[Client] Revealing tile ({packet.TileX}, {packet.TileY}) on minimap");
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"Error revealing map tile: {ex.Message}");
                }
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

            MelonLogger.Msg($"[Client] Received bulk map reveal: {packet.TileXCoords.Length} tiles");

            // Queue the reveal to happen on the main Unity thread
            GameDispatcher.Enqueue(() =>
            {
                try
                {
                    // TODO: Find the minimap/map reveal class and call its reveal method for all tiles
                    for (int i = 0; i < packet.TileXCoords.Length; i++)
                    {
                        // Example: MinimapManager.RevealTile(packet.TileXCoords[i], packet.TileYCoords[i]);
                        MelonLogger.Msg($"[Client] Revealing bulk tile ({packet.TileXCoords[i]}, {packet.TileYCoords[i]})");
                    }
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"Error revealing bulk map tiles: {ex.Message}");
                }
            });
        }
    }
}
