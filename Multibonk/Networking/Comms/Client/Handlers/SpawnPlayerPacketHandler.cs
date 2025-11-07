using Multibonk.Game;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;
using UnityEngine;
using Multibonk.Game.Handlers;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    public class SpawnPlayerPacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.SPAWN_PLAYER_PACKET;

        public SpawnPlayerPacketHandler() { }

        public void Handle(IncomingMessage msg, Connection conn)
        {
            try
            {
                var packet = new SpawnPlayerPacket(msg);

                DebugLogger.LogSpawn($"Received spawn packet for player {packet.PlayerId}, character: {packet.Character}");

                GameDispatcher.Enqueue(() =>
                {
                    try
                    {
                        var pos = packet.Position;
                        DebugLogger.LogSpawn($"Spawning network player {packet.PlayerId} at position ({pos.x}, {pos.y}, {pos.z})");
                        GameFunctions.SpawnNetworkPlayer(packet.PlayerId, packet.Character, packet.Position, packet.Rotation);
                        DebugLogger.LogSpawn($"Successfully spawned network player {packet.PlayerId}");
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Error($"Failed to spawn player {packet.PlayerId}: {ex.Message}");
                        DebugLogger.Error($"Stack: {ex.StackTrace}");
                    }
                });
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Error processing spawn packet: {ex.Message}");
                DebugLogger.Error($"Stack: {ex.StackTrace}");
            }
        }
    }
}
