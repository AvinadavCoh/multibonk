using MelonLoader;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Lobby;
using UnityEngine;

namespace Multibonk.Game.Handlers.NetworkNotify
{
    /// <summary>
    /// Handles broadcasting shrine usage from host to all clients.
    /// Only runs on the host.
    ///
    /// Identity scheme: the shrine's world position quantized to whole units,
    /// formatted as "x_y_z".  Map generation is seed-synced so every machine
    /// places shrines at identical world positions — this key is therefore stable
    /// and cross-machine deterministic without any per-instance ID field in the
    /// game's API.
    /// </summary>
    public class ShrineUseEventHandler : GameEventHandler
    {
        public ShrineUseEventHandler(LobbyContext lobbyContext)
        {
            GameEvents.UseShrineEvent += (Vector3 position, int shrineType) =>
            {
                if (!LobbyPatchFlags.IsHosting)
                    return;

                // Quantize position to whole units for a stable, cross-machine key.
                int qx = (int)System.Math.Round((double)position.x);
                int qy = (int)System.Math.Round((double)position.y);
                int qz = (int)System.Math.Round((double)position.z);
                string shrineId = $"{qx}_{qy}_{qz}";

                MelonLogger.Msg($"[Host] Broadcasting shrine use: id={shrineId} type={shrineType}");

                var myself = lobbyContext.GetMyself();
                if (myself == null)
                {
                    MelonLogger.Error("[Host] GetMyself() returned null — shrine use cannot be synced to clients.");
                    return;
                }

                var myUuid = myself.UUID;
                var packet = new SendShrineUsePacket(shrineId, myUuid, shrineType);

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
