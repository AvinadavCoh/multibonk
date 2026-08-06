using Multibonk.Game.Diagnostics;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Multibonk.Networking.Comms;
using Multibonk.Networking.Lobby;

namespace Multibonk.Game.Handlers.NetworkNotify
{
    /// <summary>
    /// Routes pickup removal events onto the network in both directions.
    ///
    ///   Host: ItemPickedUpEvent fires when DespawnPickup is called on the host →
    ///     broadcasts SendItemPickedUpPacket to all clients.
    ///
    ///   Client: ClientPickupConsumedEvent fires when the local player consumes a
    ///     network pickup → sends SendClientPickupConsumedPacket to the host, which
    ///     despawns its copy and relays ITEM_PICKED_UP to the other clients.
    /// </summary>
    public class ItemPickedUpEventHandler : GameEventHandler
    {
        public ItemPickedUpEventHandler(LobbyContext lobbyContext, NetworkService network)
        {
            // Host path: a pickup was despawned on the host, tell all clients.
            GameEvents.ItemPickedUpEvent += (itemId, playerId) =>
            {
                if (!LobbyPatchFlags.IsHosting)
                    return;

                DebugLogger.Log($"Broadcasting pickup removal: {itemId}");

                var packet = new SendItemPickedUpPacket(itemId, playerId);
                foreach (var player in lobbyContext.GetPlayers())
                {
                    player.Connection?.EnqueuePacket(packet);
                }
                SyncTelemetry.RecordSent(SyncChannel.PickupDespawn);
            };

            // Client path: local player consumed a network pickup, notify the host.
            GameEvents.ClientPickupConsumedEvent += (hostId) =>
            {
                if (LobbyPatchFlags.IsHosting)
                    return; // should never happen, but guard anyway

                DebugLogger.Log($"[Client] Notifying host of pickup consumption: {hostId}");
                network.GetClientService().Enqueue(new SendClientPickupConsumedPacket(hostId));
                // SyncTelemetry intentionally NOT recorded: client->host traffic would corrupt
                // the host-sent vs client-applied comparison in the desync detector.
            };
        }
    }
}
