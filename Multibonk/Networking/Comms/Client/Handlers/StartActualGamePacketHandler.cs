using Multibonk.Game;
using Multibonk.Game.Handlers;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;
using MelonLoader;
using System;
using Il2Cpp;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    /// <summary>
    /// Client handler for starting the actual game from lobby
    /// Destroys lobby and loads the game map
    /// </summary>
    public class StartActualGamePacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.START_ACTUAL_GAME;

        public StartActualGamePacketHandler() { }

        public void Handle(IncomingMessage msg, Connection conn)
        {
            try
            {
                var packet = new StartActualGamePacket(msg);

                MelonLogger.Msg("[Client] Starting actual game from lobby...");

                GameDispatcher.Enqueue(() =>
                {
                    try
                    {
                        // Destroy lobby scene
                        LobbyManager.DestroyLobbyScene();

                        // Start the actual game map
                        var ui = UnityEngine.Object.FindObjectOfType<MapSelectionUi>();
                        if (ui != null)
                        {
                            GamePatchFlags.AllowStartMapCall = true;
                            MelonLogger.Msg("[Client] Starting map from lobby...");
                            ui.StartMap();
                            MelonLogger.Msg("[Client] ✓ Map started successfully");
                            GamePatchFlags.AllowStartMapCall = false;
                        }
                        else
                        {
                            MelonLogger.Error("[Client] Could not find MapSelectionUi to start game!");
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error($"[Client] Failed to start game from lobby: {ex.Message}");
                        MelonLogger.Error($"Stack: {ex.StackTrace}");
                    }
                });
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Client] Error processing start actual game packet: {ex.Message}");
                MelonLogger.Error($"Stack: {ex.StackTrace}");
            }
        }
    }
}
