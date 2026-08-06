using Il2Cpp;
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
    /// Host-side handler for PICKUP_CONSUMED_PACKET (id=9) sent by a client.
    ///
    /// What it does:
    ///   1. Looks up the host's live Pickup object by the wire id.
    ///   2. Calls PickupManager.DespawnPickup on the host (on the main thread).
    ///   3. Relays an ITEM_PICKED_UP packet to every *other* client so they despawn
    ///      their copies too.
    ///
    /// Loop-suppression:
    ///   ItemDropPatches.TryTakeHostPickup removes the pickup's entry from the host's
    ///   live-pickup dictionary *before* DespawnPickup is called.  The DespawnPickupPatch
    ///   postfix checks that dictionary first; because the entry is already gone it
    ///   returns early without firing GameEvents.TriggerItemPickedUp, so
    ///   ItemPickedUpEventHandler never broadcasts back to clients.
    ///   The relay here goes only to *other* clients (sender excluded), so the originating
    ///   client does not receive a redundant ITEM_PICKED_UP for a pickup it already
    ///   consumed locally.
    ///   The cycle cannot happen: the dict removal is the single gate that prevents the
    ///   postfix from re-broadcasting, and the sender exclusion prevents the echo.
    ///
    /// SyncTelemetry:
    ///   Client->host traffic is intentionally NOT recorded on SyncChannel.PickupDespawn.
    ///   That channel compares host-sent vs client-applied; mixing in the reverse direction
    ///   would corrupt the detector's comparison.
    /// </summary>
    public class PickupConsumedServerPacketHandler : IServerPacketHandler
    {
        public byte PacketId => (byte)ClientSentPacketId.PICKUP_CONSUMED_PACKET;

        private readonly LobbyContext _lobbyContext;

        public PickupConsumedServerPacketHandler(LobbyContext lobbyContext)
        {
            _lobbyContext = lobbyContext;
        }

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new ClientPickupConsumedPacket(msg);

            var sender = _lobbyContext.GetPlayer(conn);
            if (sender == null) return;

            DebugLogger.Log($"[Host] {sender.Name} consumed pickup {packet.HostId}");

            // Capture for closure — do NOT capture 'sender' (Connection may change)
            string hostId = packet.HostId;
            ushort senderUuid = sender.UUID;

            GameDispatcher.Enqueue(() =>
            {
                try
                {
                    // TryTakeHostPickup removes the entry from the live dict first.
                    // That removal is what prevents DespawnPickupPatch from re-broadcasting
                    // (the postfix checks the dict and exits early when the id is missing).
                    if (!ItemDropPatches.TryTakeHostPickup(hostId, out var pickup))
                    {
                        DebugLogger.Warning($"[Host] Pickup {hostId} not found — already despawned or unknown id");
                        return;
                    }

                    // Relay to every other client first — this is pure network I/O and does NOT
                    // require PickupManager.  Doing it before the null-guard ensures other clients
                    // always despawn their copies, even if the host's PickupManager is unavailable.
                    // The sender is excluded so it does not receive a redundant ITEM_PICKED_UP for
                    // a pickup it already consumed locally (loop-suppression is preserved).
                    var relay = new SendItemPickedUpPacket(hostId, 0);
                    foreach (var player in _lobbyContext.GetPlayers())
                    {
                        if (player.Connection == null || player.UUID == senderUuid)
                            continue;

                        player.Connection.EnqueuePacket(relay);
                    }

                    // Despawn on the host side (guarded because PickupManager may not be ready).
                    // The relay above has already gone out, so returning here only skips the host
                    // visual — it does NOT leave other clients with stale pickups.
                    var pickupManager = PickupManager.Instance;
                    if (pickupManager == null || pickup == null)
                        return;

                    pickupManager.DespawnPickup(pickup);
                    // Postfix fires here but finds the id already removed → exits silently.
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"[Host] Failed to despawn client pickup {hostId}: {ex.Message}");
                }
            });
        }
    }
}
