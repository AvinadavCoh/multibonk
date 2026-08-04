using MelonLoader;
using Multibonk.Game.Diagnostics;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Lobby;

namespace Multibonk.Game.Handlers.NetworkNotify
{
    /// <summary>
    /// Handles broadcasting map tile reveals from host to all clients
    /// Only runs on the host
    /// </summary>
    public class MapRevealEventHandler : GameEventHandler
    {
        private readonly LobbyContext lobbyContext;

        public MapRevealEventHandler(LobbyContext lobbyContext)
        {
            this.lobbyContext = lobbyContext;

            // Subscribe to map tile reveal events
            GameEvents.MapTileRevealedEvent += OnMapTileRevealed;
        }

        private void OnMapTileRevealed(int tileX, int tileY)
        {
            // Only host broadcasts map reveals
            if (!LobbyPatchFlags.IsHosting)
                return;

            MelonLogger.Msg($"[Host] Broadcasting map tile reveal: ({tileX}, {tileY})");

            // Create packet
            var packet = new SendMapRevealPacket(tileX, tileY);

            // Send to all connected players
            foreach (var player in lobbyContext.GetPlayers())
            {
                if (player.Connection != null)
                {
                    player.Connection.EnqueuePacket(packet);
                }
            }
            SyncTelemetry.RecordSent(SyncChannel.MapReveal);
        }

        /// <summary>
        /// Sends all currently revealed tiles to a specific player (used when they join)
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
