using Multibonk.Networking.Comms.Base.Packet.Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;
using Multibonk.Networking.Lobby;
using UnityEngine;
using Il2CppRewired.Utils;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Game.Handlers;
using Multibonk.Game;

namespace Multibonk.Networking.Comms.Server.Handlers
{
    public class PlayerMovePacketHandler : IServerPacketHandler
    {

        public byte PacketId => (byte)ClientSentPacketId.PLAYER_MOVE_PACKET;

        private readonly LobbyContext _lobbyContext;

        public PlayerMovePacketHandler(LobbyContext lobbyContext)
        {
            _lobbyContext = lobbyContext;
        }

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new PlayerMovePacket(msg);

            var player = _lobbyContext.GetPlayer(conn);
            if (player == null) return;

            var playerId = player.UUID;

            GameDispatcher.Enqueue(() =>
            {
                var go = GameFunctions.GetSpawnedPlayerFromId(playerId);

                if (go != null)
                {
                    go.Move(packet.Position);
                }
            });

            foreach (var otherPlayer in _lobbyContext.GetPlayers())
            {
                if (otherPlayer.Connection == null || otherPlayer.UUID == playerId)
                    continue;

                var sendPacket = new SendPlayerMovedPacket(
                    playerId,
                    packet.Position
                );

                otherPlayer.Connection.EnqueuePacket(sendPacket);
            }

        }
    }
}
