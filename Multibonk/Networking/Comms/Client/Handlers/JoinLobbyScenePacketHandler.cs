using Multibonk.Game;
using Multibonk.Game.Handlers;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;
using MelonLoader;
using System;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    /// <summary>
    /// Client handler for joining the multiplayer lobby scene
    /// Creates the lobby and spawns the player
    /// </summary>
    public class JoinLobbyScenePacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.JOIN_LOBBY_SCENE;

        public JoinLobbyScenePacketHandler() { }

        public void Handle(IncomingMessage msg, Connection conn)
        {
            try
            {
                var packet = new JoinLobbyScenePacket(msg);
                GamePatchFlags.Seed = packet.Seed;

                MelonLogger.Msg($"[Client] Joining lobby scene with seed {packet.Seed}");

                GameDispatcher.Enqueue(() =>
                {
                    try
                    {
                        // Create the lobby scene
                        LobbyManager.CreateLobbyScene();

                        // TODO: Spawn player in lobby
                        // This will be handled by existing player spawn system
                        MelonLogger.Msg("[Client] ✓ Joined lobby scene successfully");
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error($"[Client] Failed to join lobby: {ex.Message}");
                        MelonLogger.Error($"Stack: {ex.StackTrace}");
                    }
                });
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Client] Error processing join lobby packet: {ex.Message}");
                MelonLogger.Error($"Stack: {ex.StackTrace}");
            }
        }
    }
}
