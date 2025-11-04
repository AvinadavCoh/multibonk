using MelonLoader;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Lobby;

namespace Multibonk.Game.Handlers.NetworkNotify
{
    public class PlayerXpEventHandler : GameEventHandler
    {
        public PlayerXpEventHandler(LobbyContext lobbyContext)
        {
            GameEvents.PlayerXpGainedEvent += (xpAmount) =>
            {
                if (!LobbyPatchFlags.IsHosting)
                    return;

                MelonLogger.Msg($"Broadcasting XP gain: {xpAmount}");

                var myUuid = lobbyContext.GetMyself().UUID;
                var packet = new SendPlayerXpGainedPacket(myUuid, xpAmount);

                // Broadcast to all connected clients
                foreach (var player in lobbyContext.GetPlayers())
                {
                    if (player.Connection != null)
                    {
                        player.Connection.EnqueuePacket(packet);
                    }
                }
            };

            GameEvents.PlayerLevelUpEvent += (newLevel) =>
            {
                if (!LobbyPatchFlags.IsHosting)
                    return;

                MelonLogger.Msg($"Broadcasting level up to level {newLevel}");

                var myUuid = lobbyContext.GetMyself().UUID;
                var packet = new SendPlayerLevelUpPacket(myUuid, newLevel);

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
