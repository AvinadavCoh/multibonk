using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Lobby;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;
using Multibonk.Game.Handlers;
using Multibonk.Game;

namespace Multibonk.Networking.Comms.Server.Handlers
{
    /// <summary>
    /// Class copied from PlayerMovePacketHandler
    /// </summary>
    public class PlayerRotatePacketHandler : IServerPacketHandler
    {
        public byte PacketId => (byte)ClientSentPacketId.PLAYER_ROTATE_PACKET;
        private readonly LobbyContext _lobbyContext;

        public PlayerRotatePacketHandler(LobbyContext lobbyContext)
        {
            _lobbyContext = lobbyContext;
        }


        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new PlayerRotatePacket(msg);

            var player = _lobbyContext.GetPlayer(conn);
            if (player == null) return;

            var playerId = player.UUID;

            GameDispatcher.Enqueue(() =>
            {
                var go = GameFunctions.GetSpawnedPlayerFromId(playerId);

                if (go != null)
                {
                    go.Rotate(packet.Rotation.eulerAngles);
                }
            });


            foreach (var otherPlayer in _lobbyContext.GetPlayers())
            {
                if (otherPlayer.Connection == null || otherPlayer.UUID == playerId)
                    continue;

                var sendPacket = new SendPlayerRotatedPacket(
                    playerId,
                    packet.Rotation.eulerAngles
                );

                otherPlayer.Connection.EnqueuePacket(sendPacket);
            }
        }
    }
}




