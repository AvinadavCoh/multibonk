using MelonLoader;
using Multibonk.Game.Diagnostics;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Multibonk.Networking.Comms;
using Multibonk.Networking.Lobby;

namespace Multibonk.Game.Handlers.NetworkNotify
{
    /// <summary>
    /// Routes map fog reveals onto the network in both directions.
    ///
    ///   Host: fires MapTileRevealedEvent → broadcasts SendMapRevealPacket to all clients.
    ///   Client: fires MapTileRevealedEvent → sends SendClientMapRevealPacket to the host,
    ///     which applies it and relays it to the other clients.
    ///
    /// MapRevealEventHandler and MapRevealServerPacketHandler together close the full loop
    /// so every player's map converges on the union of all players' explored areas.
    /// </summary>
    public class MapRevealEventHandler : GameEventHandler
    {
        private readonly LobbyContext _lobbyContext;
        private readonly NetworkService _network;

        public MapRevealEventHandler(LobbyContext lobbyContext, NetworkService network)
        {
            _lobbyContext = lobbyContext;
            _network = network;

            GameEvents.MapTileRevealedEvent += OnMapTileRevealed;
        }

        private void OnMapTileRevealed(int tileX, int tileY)
        {
            if (LobbyPatchFlags.IsHosting)
            {
                // Host: broadcast to all connected clients.
                MelonLogger.Msg($"[Host] Broadcasting map tile reveal: ({tileX}, {tileY})");

                var packet = new SendMapRevealPacket(tileX, tileY);
                foreach (var player in _lobbyContext.GetPlayers())
                {
                    player.Connection?.EnqueuePacket(packet);
                }
                SyncTelemetry.RecordSent(SyncChannel.MapReveal);
            }
            else
            {
                // Client: forward to the host (MapRevealServerPacketHandler handles it there).
                DebugLogger.Log($"[Client] Sending map reveal to host: ({tileX}, {tileY})");
                _network.GetClientService().Enqueue(new SendClientMapRevealPacket(tileX, tileY));
                // SyncTelemetry intentionally NOT recorded: client->host direction would
                // corrupt the host-sent vs client-applied comparison in the desync detector.
            }
        }

        /// <summary>
        /// Sends all currently revealed tiles to a specific player (used when they join).
        /// </summary>
        public void SyncRevealedTilesToPlayer(Connection connection, int[] tileXCoords, int[] tileYCoords)
        {
            if (!LobbyPatchFlags.IsHosting)
                return;

            if (tileXCoords.Length == 0)
                return;

            MelonLogger.Msg($"[Host] Syncing {tileXCoords.Length} revealed tiles to new player");

            var packet = new SendMapRevealBulkPacket(tileXCoords, tileYCoords);
            connection.EnqueuePacket(packet);
        }
    }
}
