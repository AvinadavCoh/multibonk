using MelonLoader;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Lobby;
using UnityEngine;

namespace Multibonk.Game.Handlers.NetworkNotify
{
    public class ItemDropEventHandler : GameEventHandler
    {
        public ItemDropEventHandler(LobbyContext lobbyContext)
        {
            GameEvents.SpawnDropEvent += (itemId, position, itemType, value) =>
            {
                if (!LobbyPatchFlags.IsHosting)
                    return;

                DebugLogger.Log($"Broadcasting item drop: {itemId} (type {itemType}, value {value}) at {position}");

                var packet = new SendItemDroppedPacket(itemId, position, itemType, value);

                // Broadcast to all connected clients
                foreach (var player in lobbyContext.GetPlayers())
                {
                    if (player.Connection != null)
                    {
                        player.Connection.EnqueuePacket(packet);
                    }
                }
            };
        }
    }
}
