using MelonLoader;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Lobby;

namespace Multibonk.Game.Handlers.NetworkNotify
{
    /// <summary>
    /// Handles broadcasting player damage events from host to all clients
    /// Only runs on the host
    /// </summary>
    public class PlayerDamageEventHandler : GameEventHandler
    {
        public PlayerDamageEventHandler(LobbyContext lobbyContext)
        {
            GameEvents.PlayerTakeHitEvent += () =>
            {
                if (!LobbyPatchFlags.IsHosting)
                    return;

                // TODO: Get actual damage values from game
                // For now using placeholder values
                var myUuid = lobbyContext.GetMyself().UUID;
                float currentHealth = 80f; // Placeholder
                float maxHealth = 100f; // Placeholder
                float damageAmount = 20f; // Placeholder

                MelonLogger.Msg($"[Host] Player {myUuid} took {damageAmount:F1} damage. Broadcasting to clients.");

                var packet = new SendPlayerDamagePacket(myUuid, currentHealth, maxHealth, damageAmount);
                
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
