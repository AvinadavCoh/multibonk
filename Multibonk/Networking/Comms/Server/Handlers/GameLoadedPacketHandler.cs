using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;
using Multibonk.Networking.Lobby;

namespace Multibonk.Networking.Comms.Server.Handlers
{

    public class GameLoadedPacketHandler : IServerPacketHandler
    {
        public byte PacketId => (byte)ClientSentPacketId.GAME_LOADED_PACKET;
        private readonly LobbyContext lobbyContext;

        public GameLoadedPacketHandler(LobbyContext lobbyContext)
        {
            this.lobbyContext = lobbyContext;
        }

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new GameLoadedPacket(msg);
            
            // Find the player and store their spawn position
            var player = lobbyContext.GetPlayers().FirstOrDefault(p => p.Connection == conn);
            if (player != null)
            {
                player.SpawnPosition = packet.Position;
                player.SpawnRotation = packet.Rotation;
                // Don't call ToString() on Il2Cpp Vector3 - causes AccessViolationException
                MelonLoader.MelonLogger.Msg($"[GameLoadedPacketHandler] Stored spawn position for {player.Name}: ({packet.Position.x}, {packet.Position.y}, {packet.Position.z})");
            }
        }
    }
}
