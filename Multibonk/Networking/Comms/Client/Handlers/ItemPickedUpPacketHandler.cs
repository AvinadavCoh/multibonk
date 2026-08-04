using Il2Cpp;
using MelonLoader;
using Multibonk.Game.Handlers;
using Multibonk.Game.Patches;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    /// <summary>
    /// Client-side handler for pickup removals from the host (consumed or expired).
    /// Looks up the local pickup mapped to the host id and despawns it.
    /// </summary>
    public class ItemPickedUpPacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.ITEM_PICKED_UP_PACKET;

        public ItemPickedUpPacketHandler()
        {
        }

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new ItemPickedUpPacket(msg);

            DebugLogger.Log($"[Client] Pickup removed by host: {packet.ItemId} (player {packet.PlayerId})");

            GameDispatcher.Enqueue(() =>
            {
                try
                {
                    if (!ItemDropPatches.TryTakeNetworkPickup(packet.ItemId, out var pickup))
                    {
                        // Unknown id - pickup may have already despawned locally
                        return;
                    }

                    var pickupManager = PickupManager.Instance;
                    if (pickupManager == null || pickup == null)
                        return;

                    pickupManager.DespawnPickup(pickup);
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"[Client] Failed to despawn pickup {packet.ItemId}: {ex.Message}");
                }
            });
        }
    }
}
