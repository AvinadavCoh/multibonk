using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Multibonk.Networking.Comms;
using Multibonk.Networking.Lobby;

namespace Multibonk.Game.Handlers.NetworkNotify
{
    /// <summary>
    /// Routes local player damage to the other players with real health values.
    /// - Host: updates its own lobby entry and broadcasts to all clients.
    /// - Client: updates its own lobby entry and sends to the host, which relays
    ///   (see PlayerHealthServerPacketHandler).
    /// </summary>
    public class PlayerDamageEventHandler : GameEventHandler
    {
        public PlayerDamageEventHandler(NetworkService network, LobbyContext lobbyContext)
        {
            GameEvents.PlayerTakeHitEvent += (currentHealth, maxHealth, damageAmount) =>
            {
                var myself = lobbyContext.GetMyself();
                if (myself == null)
                    return;

                // Keep our own HUD entry fresh
                myself.CurrentHealth = currentHealth;
                myself.MaxHealth = maxHealth;

                if (LobbyPatchFlags.IsHosting)
                {
                    DebugLogger.Log($"[Host] Broadcasting damage: {damageAmount:F1} ({currentHealth:F1}/{maxHealth:F1})");

                    var packet = new SendPlayerDamagePacket(myself.UUID, currentHealth, maxHealth, damageAmount);
                    foreach (var player in lobbyContext.GetPlayers())
                    {
                        player.Connection?.EnqueuePacket(packet);
                    }
                }
                else
                {
                    DebugLogger.Log($"[Client] Sending damage to host: {damageAmount:F1} ({currentHealth:F1}/{maxHealth:F1})");
                    network.GetClientService().Enqueue(new SendClientHealthPacket(currentHealth, maxHealth, damageAmount));
                }
            };
        }
    }
}
