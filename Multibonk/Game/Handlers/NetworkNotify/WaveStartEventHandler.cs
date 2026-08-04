using MelonLoader;
using Multibonk.Game.Diagnostics;
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
                // Replaying a host timeline event on a client re-raises WaveStartEvent, so
                // without this guard a client would rebroadcast it and count it as sent.
                if (!LobbyPatchFlags.IsHosting)
                    return;

                MelonLogger.Msg($"[Host] Broadcasting wave start: Wave {waveNumber}");

                var packet = new SendWaveStartPacket(waveNumber);
                foreach (var player in lobbyContext.GetPlayers())
                {
                    player.Connection?.EnqueuePacket(packet);
                }
                SyncTelemetry.RecordSent(SyncChannel.TimelineEvent);
            };
        }
    }
}
