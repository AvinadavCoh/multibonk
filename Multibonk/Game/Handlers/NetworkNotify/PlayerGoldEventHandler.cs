using Multibonk.Game.Diagnostics;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Multibonk.Networking.Comms;
using Multibonk.Networking.Lobby;

namespace Multibonk.Game.Handlers.NetworkNotify
{
    /// <summary>
    /// Routes local gold gains to the other players (Shared gold).
    /// Host broadcasts; clients send to the host which applies and relays.
    /// </summary>
    public class PlayerGoldEventHandler : GameEventHandler
    {
        public PlayerGoldEventHandler(NetworkService network, LobbyContext lobbyContext)
        {
            GameEvents.PlayerGoldGainedEvent += (goldAmount) =>
            {
                if (Preferences.GetGoldSharingMode() != Preferences.LootDistributionMode.Shared)
                    return;

                if (LobbyPatchFlags.IsHosting)
                {
                    DebugLogger.Log($"[Host] Broadcasting gold gain: {goldAmount}");

                    var packet = new SendPlayerGoldGainedPacket(goldAmount);
                    foreach (var player in lobbyContext.GetPlayers())
                    {
                        player.Connection?.EnqueuePacket(packet);
                    }
                    SyncTelemetry.RecordSent(SyncChannel.GoldGain);
                }
                else
                {
                    DebugLogger.Log($"[Client] Sending gold gain to host: {goldAmount}");
                    network.GetClientService().Enqueue(new SendClientGoldGainedPacket(goldAmount));
                }
            };
        }
    }
}
