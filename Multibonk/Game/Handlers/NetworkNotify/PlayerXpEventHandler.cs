using MelonLoader;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Multibonk.Networking.Comms;
using Multibonk.Networking.Lobby;

namespace Multibonk.Game.Handlers.NetworkNotify
{
    /// <summary>
    /// Routes local XP gains to the other players (Shared XP, RoR2-style).
    /// - Host: broadcasts its gains to all clients.
    /// - Client: sends its gains to the host, which applies them and relays them
    ///   to the other clients (see PlayerXpGainedServerPacketHandler).
    /// </summary>
    public class PlayerXpEventHandler : GameEventHandler
    {
        public PlayerXpEventHandler(NetworkService network, LobbyContext lobbyContext)
        {
            GameEvents.PlayerXpGainedEvent += (xpAmount) =>
            {
                if (Preferences.GetXpSharingMode() != Preferences.LootDistributionMode.Shared)
                    return;

                if (LobbyPatchFlags.IsHosting)
                {
                    DebugLogger.Log($"[Host] Broadcasting XP gain: {xpAmount}");

                    var myUuid = lobbyContext.GetMyself().UUID;
                    var packet = new SendPlayerXpGainedPacket(myUuid, xpAmount);

                    foreach (var player in lobbyContext.GetPlayers())
                    {
                        player.Connection?.EnqueuePacket(packet);
                    }
                }
                else
                {
                    DebugLogger.Log($"[Client] Sending XP gain to host: {xpAmount}");
                    network.GetClientService().Enqueue(new SendClientXpGainedPacket(xpAmount));
                }
            };

            GameEvents.PlayerLevelUpEvent += (newLevel) =>
            {
                if (!LobbyPatchFlags.IsHosting)
                    return;

                MelonLogger.Msg($"Broadcasting level up to level {newLevel}");

                var myUuid = lobbyContext.GetMyself().UUID;
                var packet = new SendPlayerLevelUpPacket(myUuid, newLevel);

                foreach (var player in lobbyContext.GetPlayers())
                {
                    player.Connection?.EnqueuePacket(packet);
                }
            };
        }
    }
}
