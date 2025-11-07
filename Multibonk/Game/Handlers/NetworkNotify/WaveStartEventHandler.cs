using MelonLoader;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Lobby;

namespace Multibonk.Game.Handlers.NetworkNotify
{
    /// <summary>
    /// Handles broadcasting wave start events to all connected clients
    /// Works with WaveProgressionPatches to sync wave progression
    /// </summary>
    public class WaveStartEventHandler : GameEventHandler
    {
        public WaveStartEventHandler(LobbyContext lobbyContext)
        {
            GameEvents.WaveStartEvent += (waveNumber) =>
            {
                MelonLogger.Msg($"[Host] Broadcasting wave start: Wave {waveNumber}");

                var packet = new SendWaveStartPacket(waveNumber);
                foreach (var player in lobbyContext.GetPlayers())
                {
                    player.Connection?.EnqueuePacket(packet);
                }
            };
        }
    }
}
