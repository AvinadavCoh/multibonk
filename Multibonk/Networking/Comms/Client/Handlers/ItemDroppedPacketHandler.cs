using Il2Cpp;
using Il2CppAssets.Scripts.Inventory__Items__Pickups.Pickups;
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
    /// Client-side handler for pickup drops from the host.
    /// Spawns the pickup locally via PickupManager.SpawnPickup (local RNG drops are blocked
    /// by ItemDropPatches) and maps the host's pickup id to the local instance so it can be
    /// despawned later.
    /// </summary>
    public class ItemDroppedPacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.ITEM_DROPPED_PACKET;

        public ItemDroppedPacketHandler()
        {
        }

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new ItemDroppedPacket(msg);

            // NOTE: only access Vector3 fields here (x/y/z) - calling interop methods like
            // Vector3.ToString() on the network thread crashes the process (unattached IL2CPP thread)
            DebugLogger.Log($"[Client] Pickup dropped: {packet.ItemId} type {packet.ItemType} value {packet.Value} at ({packet.Position.x:F1}, {packet.Position.y:F1}, {packet.Position.z:F1})");

            GameDispatcher.Enqueue(() =>
            {
                try
                {
                    var pickupManager = PickupManager.Instance;
                    if (pickupManager == null)
                    {
                        DebugLogger.Warning("[Client] PickupManager.Instance is null - cannot spawn pickup");
                        return;
                    }

                    ItemDropPatches.AllowNetworkSpawn = true;
                    try
                    {
                        // Host already applied the random offset; replay at the exact position
                        var pickup = pickupManager.SpawnPickup(
                            (EPickup)packet.ItemType,
                            packet.Position,
                            packet.Value,
                            false, // useRandomOffsetPosition
                            0f);   // pickupDelay

                        if (pickup != null)
                        {
                            ItemDropPatches.RegisterNetworkPickup(packet.ItemId, pickup);
                            SyncTelemetry.RecordApplied(SyncChannel.PickupSpawn);
                        }
                    }
                    finally
                    {
                        ItemDropPatches.AllowNetworkSpawn = false;
                    }
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"[Client] Failed to spawn pickup {packet.ItemId}: {ex.Message}");
                }
            });
        }
    }
}
