using MelonLoader;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Lobby;
using UnityEngine;

namespace Multibonk.Game.Handlers.NetworkNotify
{
    /// <summary>
    /// Handles broadcasting player death events from host to all clients
    /// In multiplayer, players stay in lobby after death
    /// Game only ends when all players are dead
    /// Only runs on the host
    /// </summary>
    public class PlayerDeathEventHandler : GameEventHandler
    {
        public PlayerDeathEventHandler(LobbyContext lobbyContext)
        {
            GameEvents.PlayerDieEvent += () =>
            {
                if (!LobbyPatchFlags.IsHosting)
                    return;

                // TODO: Get actual death position from game
                var myUuid = lobbyContext.GetMyself().UUID;
                Vector3 deathPosition = Vector3.zero; // Placeholder

                MelonLogger.Msg($"[Host] Player {myUuid} died. Broadcasting to clients.");

                var packet = new SendPlayerDeathPacket(myUuid, deathPosition);
                
                // Broadcast to all connected clients
                foreach (var player in lobbyContext.GetPlayers())
                {
                    if (player.Connection != null)
                    {
                        player.Connection.EnqueuePacket(packet);
                    }
                }

                // TODO: Check if all players are dead
                // If all dead -> trigger game over
                // Otherwise -> allow spectating/waiting for respawn
            };
        }
    }
}
