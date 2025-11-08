using MelonLoader;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Lobby;

namespace Multibonk.Game.Handlers.NetworkNotify
{
    /// <summary>
    /// Handles host clicking "Start Game" button in lobby
    /// Broadcasts to all clients to start the actual game
    /// </summary>
    public class LobbyStartGameEventHandler : GameEventHandler
    {
        public LobbyStartGameEventHandler(LobbyContext lobbyContext)
        {
            GameEvents.LobbyStartGameEvent += () =>
            {
                MelonLogger.Msg("[Host] Starting actual game from lobby");

                // Send packet to all clients
                var packet = new SendStartActualGamePacket();
                foreach (var player in lobbyContext.GetPlayers())
                {
                    player.Connection?.EnqueuePacket(packet);
                }

                // Host also starts game
                LobbyManager.DestroyLobbyScene();

                // Start the map
                var ui = UnityEngine.Object.FindObjectOfType<Il2Cpp.MapSelectionUi>();
                if (ui != null)
                {
                    GamePatchFlags.AllowStartMapCall = true;
                    MelonLogger.Msg("[Host] Starting map from lobby...");
                    ui.StartMap();
                    GamePatchFlags.AllowStartMapCall = false;
                }
            };
        }
    }
}
