using MelonLoader;
using Multibonk.Game.Handlers;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;
using Multibonk.Networking.Lobby;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    /// <summary>
    /// Client-side handler for PLAYER_DEATH (ServerSentPacketId = 21).
    ///
    /// The host broadcasts this when any player (including itself) dies.
    /// This handler marks that player as dead in LobbyContext so the local
    /// client's all-dead checks stay consistent.
    ///
    /// No death VFX or corpse spawning is attempted; the API dump does not expose
    /// a safe remote-death visual entry point.
    /// </summary>
    public class PlayerDeathPacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.PLAYER_DEATH;

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new PlayerDeathPacket(msg);

            MelonLogger.Msg($"[Client] Player {packet.PlayerId} died at " +
                            $"({packet.DeathPosition.x:F2}, {packet.DeathPosition.y:F2}, {packet.DeathPosition.z:F2})");

            GameDispatcher.Enqueue(() =>
            {
                try
                {
                    var lobby = LobbyPatchFlags.CurrentLobby;
                    if (lobby == null)
                    {
                        MelonLogger.Warning("[Client] Received PLAYER_DEATH but CurrentLobby is null");
                        return;
                    }

                    var player = lobby.GetPlayer(packet.PlayerId);
                    if (player != null)
                    {
                        player.IsDead = true;
                        MelonLogger.Msg($"[Client] Marked '{player.Name}' (UUID={packet.PlayerId}) as dead in lobby.");
                    }
                    else
                    {
                        MelonLogger.Warning($"[Client] Received PLAYER_DEATH for unknown UUID={packet.PlayerId}");
                    }
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"[Client] Failed to handle PLAYER_DEATH: {ex.Message}");
                }
            });
        }
    }
}
