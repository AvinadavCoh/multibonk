using MelonLoader;
using Multibonk.Game.Handlers;
using Multibonk.Game.Patches;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;
using Multibonk.Networking.Lobby;

namespace Multibonk.Networking.Comms.Server.Handlers
{
    /// <summary>
    /// Host-side handler for MAP_REVEAL_PACKET (id=8) sent by a client.
    ///
    /// What it does:
    ///   1. Applies the reveal to the host's own FullMap (on the main thread via
    ///      GameDispatcher) so areas the client explores become visible for the host.
    ///   2. Relays a server-sent MAP_REVEAL packet to every *other* client so all
    ///      players converge on the same fog state.
    ///
    /// Loop-suppression:
    ///   MinimapSyncPatches.RevealAt() sets ApplyingNetworkReveal = true before calling
    ///   FullMap.QueueRevealFog, and clears it in a finally block.  The
    ///   QueueRevealFogPatch.Postfix checks that flag first and returns without firing
    ///   GameEvents.TriggerMapTileRevealed when it is set, so the host patch never
    ///   re-broadcasts a reveal that originated from a client packet.
    ///   The relay goes only to the *other* clients (sender excluded), so the originating
    ///   client's MapRevealPacketHandler calls RevealAt() which also sets the flag, so its
    ///   patch also skips sending another packet to the host.
    ///   The cycle cannot happen: every call to QueueRevealFog that comes from the network
    ///   is wrapped in the ApplyingNetworkReveal guard.
    ///
    /// SyncTelemetry:
    ///   Client->host traffic is intentionally NOT recorded on SyncChannel.MapReveal.
    ///   That channel compares host-sent vs client-applied; mixing in the reverse direction
    ///   would corrupt the detector's comparison.
    /// </summary>
    public class MapRevealServerPacketHandler : IServerPacketHandler
    {
        public byte PacketId => (byte)ClientSentPacketId.MAP_REVEAL_PACKET;

        private readonly LobbyContext _lobbyContext;

        public MapRevealServerPacketHandler(LobbyContext lobbyContext)
        {
            _lobbyContext = lobbyContext;
        }

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new ClientMapRevealPacket(msg);

            var sender = _lobbyContext.GetPlayer(conn);
            if (sender == null) return;

            DebugLogger.Log($"[Host] {sender.Name} revealed map area ({packet.TileX}, {packet.TileY})");

            // Apply to the host's own FullMap on the main thread.
            // RevealAt sets ApplyingNetworkReveal so the QueueRevealFog patch does not
            // re-broadcast this reveal via GameEvents.
            int tileX = packet.TileX;
            int tileY = packet.TileY;
            GameDispatcher.Enqueue(() =>
            {
                try
                {
                    MinimapSyncPatches.RevealAt(tileX, tileY);
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"[Host] Failed to apply client map reveal ({tileX}, {tileY}): {ex.Message}");
                }
            });

            // Relay to all other clients (not back to the sender).
            // This is the only path that broadcasts the reveal further — the patch is suppressed.
            var relay = new SendMapRevealPacket(packet.TileX, packet.TileY);
            foreach (var player in _lobbyContext.GetPlayers())
            {
                if (player.Connection == null || player.UUID == sender.UUID)
                    continue;

                player.Connection.EnqueuePacket(relay);
            }
        }
    }
}
