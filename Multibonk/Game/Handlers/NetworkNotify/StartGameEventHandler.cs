using MelonLoader;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Lobby;

namespace Multibonk.Game.Handlers.NetworkNotify
{
    /// <summary>
    /// Handles map confirmation - now sends players to lobby instead of starting game directly
    /// </summary>
    public class StartGameEventHandler : GameEventHandler
    {
        public StartGameEventHandler(
            LobbyContext lobbyContext
        )
        {
            GameEvents.ConfirmMapEvent += () =>
            {
                MelonLogger.Msg($"[Host] Map selected, sending players to lobby (seed: {GamePatchFlags.Seed})");
                
                // Send players to lobby scene instead of starting game directly
                var packet = new SendJoinLobbyScenePacket(GamePatchFlags.Seed);

                foreach (var player in lobbyContext.GetPlayers())
                {
                    player.Connection?.EnqueuePacket(packet);
                }

                // Host also joins lobby
                LobbyManager.CreateLobbyScene();
                
                MelonLogger.Msg("[Host] ✓ Lobby packets sent, host lobby created");
            };

        }
    }
}
