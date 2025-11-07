using MelonLoader;
using Multibonk.UserInterface.Window;
using Microsoft.Extensions.DependencyInjection;
using Multibonk.Networking.Lobby;
using Multibonk.Networking.Comms.Server.Protocols;
using Multibonk.Networking.Comms.Client.Protocols;
using Multibonk.Networking.Comms.Multibonk.Networking.Comms;
using Multibonk.Networking.Comms.Server.Handlers;
using Multibonk.Networking.Comms.Client.Handlers;
using Multibonk.Game.Handlers;
using Multibonk.Game;
using Multibonk.Networking.Comms.Base;
using Multibonk.Game.Handlers.NetworkNotify;
using Multibonk.Game.Handlers.Logic;
using Multibonk.Networking.Steam;

namespace Multibonk
{
    public class MultibonkMod : MelonMod
    {
        UIManager manager;
        EventHandlerExecutor executor;

        public override void OnGUI()
        {
            if(manager != null)
                manager.OnGUI();

        }

        public override void OnUpdate()
        {
            if (executor != null)
                executor.Update();

            // Debug commands for testing
            Game.DebugCommands.CheckInput();
        }

        public override void OnFixedUpdate()
        {
            if (executor != null)
                executor.FixedUpdate();
        }

        public override void OnLateUpdate()
        {
            if (executor != null)
                executor.LateUpdate();
        }


        public override void OnInitializeMelon()
        {
            var services = new ServiceCollection();

            services.AddSingleton<IGameEventHandler, CharacterChangedEventHandler>();
            services.AddSingleton<IGameEventHandler, GameLoadedEventHandler>();
            services.AddSingleton<IGameEventHandler, PlayerMovementEventHandler>();
            services.AddSingleton<IGameEventHandler, StartGameEventHandler>();
            services.AddSingleton<IGameEventHandler, UpdateNetworkPlayerAnimationsEventHandler>();
            services.AddSingleton<IGameEventHandler, PlayerXpEventHandler>();
            services.AddSingleton<IGameEventHandler, ItemDropEventHandler>();
            services.AddSingleton<IGameEventHandler, EnemySyncEventHandler>();
            services.AddSingleton<IGameEventHandler, EnemySpawnedEventHandler>();
            services.AddSingleton<IGameEventHandler, MapRevealEventHandler>();
            services.AddSingleton<IGameEventHandler, ChestOpenEventHandler>();
            services.AddSingleton<IGameEventHandler, ShrineUseEventHandler>();
            services.AddSingleton<IGameEventHandler, PlayerDamageEventHandler>();
            services.AddSingleton<IGameEventHandler, PlayerDeathEventHandler>();
            services.AddSingleton<IGameEventHandler, GameDispatcher>();
            services.AddSingleton<IGameEventHandler, GameplayRuleSynchronizer>();

            services.AddSingleton<EventHandlerExecutor>();

            services.AddSingleton<IServerPacketHandler, JoinLobbyPacketHandler>();
            services.AddSingleton<IServerPacketHandler, SelectCharacterPacketHandler>();
            services.AddSingleton<IServerPacketHandler, PlayerMovePacketHandler>();
            services.AddSingleton<IServerPacketHandler, PlayerRotatePacketHandler>();
            services.AddSingleton<IServerPacketHandler, GameLoadedPacketHandler>();

            services.AddSingleton<IClientPacketHandler, LobbyPlayerListPacketHandler>();
            services.AddSingleton<IClientPacketHandler, PlayerSelectedCharacterPacketHandler>();
            services.AddSingleton<IClientPacketHandler, SpawnPlayerPacketHandler>();
            services.AddSingleton<IClientPacketHandler, StartGamePacketHandler>();
            services.AddSingleton<IClientPacketHandler, PlayerMovedPacketHandler>();
            services.AddSingleton<IClientPacketHandler, PlayerRotatedPacketHandler>();
            services.AddSingleton<IClientPacketHandler, PlayerXpGainedPacketHandler>();
            services.AddSingleton<IClientPacketHandler, PlayerLevelUpPacketHandler>();
            services.AddSingleton<IClientPacketHandler, ItemDroppedPacketHandler>();
            services.AddSingleton<IClientPacketHandler, ItemPickedUpPacketHandler>();
            services.AddSingleton<IClientPacketHandler, EnemyDeathPacketHandler>();
            services.AddSingleton<IClientPacketHandler, EnemyHealthUpdatePacketHandler>();
            services.AddSingleton<IClientPacketHandler, EnemySpawnPacketHandler>();
            services.AddSingleton<IClientPacketHandler, MapRevealPacketHandler>();
            services.AddSingleton<IClientPacketHandler, MapRevealBulkPacketHandler>();
            services.AddSingleton<IClientPacketHandler, ChestOpenPacketHandler>();
            services.AddSingleton<IClientPacketHandler, ShrineUsePacketHandler>();
            services.AddSingleton<IClientPacketHandler, PlayerDamagePacketHandler>();
            services.AddSingleton<IClientPacketHandler, PlayerDeathPacketHandler>();

            services.AddSingleton<ClientProtocol>();
            services.AddSingleton<ServerProtocol>();

            services.AddSingleton<LobbyContext>();

            // Packet Handlers cannot call services. Otherwise, it will cause circular dependency
            services.AddSingleton<NetworkService>();
            services.AddSingleton<SteamTunnelService>();
            services.AddSingleton<SteamTunnelCallbackBinder>();
            services.AddSingleton<LobbyService>();

            services.AddSingleton<ClientLobbyWindow>();
            services.AddSingleton<ConnectionWindow>();
            services.AddSingleton<HostLobbyWindow>();
            services.AddSingleton<PlayerHealthHUD>();
            services.AddSingleton<OptionsWindow>();

            services.AddSingleton<UIManager>();

            var serviceProvider = services.BuildServiceProvider();

            manager = serviceProvider.GetService<UIManager>();
            executor = serviceProvider.GetService<EventHandlerExecutor>();

            var _lobbyContext = serviceProvider.GetService<LobbyContext>();

            // Initialize Steam callback binder (activates Steam Rich Presence join support)
            serviceProvider.GetService<SteamTunnelCallbackBinder>();

            // Apply Harmony patches for game hooks
            var harmony = new HarmonyLib.Harmony("com.avinadavcoh.multibonk");
            harmony.PatchAll();
            MelonLogger.Msg("Harmony patches applied successfully");

            base.OnInitializeMelon();
        }
    }


}
