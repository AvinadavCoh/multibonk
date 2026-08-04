using Multibonk.Game.Diagnostics;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Lobby;

namespace Multibonk.Game.Handlers.NetworkNotify
{
    /// <summary>
    /// Broadcasts pickup removals (consumed or expired on the host) to all clients.
    /// Works with ItemDropPatches, which triggers the event from PickupManager.DespawnPickup.
    /// </summary>
    public class ItemPickedUpEventHandler : GameEventHandler
    {
        public ItemPickedUpEventHandler(LobbyContext lobbyContext)
        {
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
        }
    }
}
