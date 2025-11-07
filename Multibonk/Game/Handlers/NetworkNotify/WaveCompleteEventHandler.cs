using MelonLoader;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Lobby;

namespace Multibonk.Game.Handlers.NetworkNotify
{
    /// <summary>
    /// Handles broadcasting wave complete events to all connected clients
    /// Works with WaveProgressionPatches to sync wave completion
    /// </summary>
    public class WaveCompleteEventHandler : GameEventHandler
    {
        public WaveCompleteEventHandler(LobbyContext lobbyContext)
        {
            GameEvents.WaveCompleteEvent += (waveNumber) =>
            {
                MelonLogger.Msg($"[Host] Broadcasting wave complete: Wave {waveNumber}");

                var packet = new SendWaveCompletePacket(waveNumber);
                foreach (var player in lobbyContext.GetPlayers())
                {
                    player.Connection?.EnqueuePacket(packet);
                }
            };
        }
    }
}
