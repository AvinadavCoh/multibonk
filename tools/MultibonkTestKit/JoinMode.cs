using Multibonk.Networking.Comms.Base;
using MultibonkTestKit.Logging;
using MultibonkTestKit.Net;
using MultibonkTestKit.Protocol;
using IncomingMessage = Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms.IncomingMessage;

namespace MultibonkTestKit
{
    /// <summary>
    /// `join` mode - MultibonkTestKit impersonates a second Multibonk CLIENT connecting
    /// to a real hosted game. See README.md for usage and the interactive key legend.
    ///
    /// This is the original (and still fully supported) mode of the tool. Its logic is
    /// unchanged from before `host` mode was added - only moved out of Program.cs, which
    /// is now a thin dispatcher between this and HostMode.
    /// </summary>
    public static class JoinMode
    {
        private static PacketLog _log;
        private static WireConnection _conn;
        private static volatile bool _running = true;
        private static ushort _myUuid;
        private static readonly Random _rng = new();

        public static async Task<int> RunAsync(string[] args)
        {
            var opts = ParseArgs(args);
            if (opts == null)
            {
                PrintUsage();
                return 1;
            }

            _log = new PacketLog();
            PrintLegend();

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                _log.Info("Ctrl+C received - shutting down.");
                FinishAndExit();
            };

            var lobbyListTcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            var startGameTcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

            _conn = new WireConnection();
            _conn.OnClosed += ex =>
            {
                _running = false;
                if (ex != null)
                    _log.Warn($"Connection closed: {ex.GetType().Name}: {ex.Message}");
                else
                    _log.Info("Connection closed gracefully.");
            };

            _conn.OnPacket += payload => HandleIncoming(payload, lobbyListTcs, startGameTcs);

            _log.Info($"Connecting to {opts.Host}:{opts.Port} as \"{opts.Name}\" (character=\"{opts.Character}\")...");
            try
            {
                await _conn.ConnectAsync(opts.Host, opts.Port);
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to connect: {ex.Message}");
                return 1;
            }
            _conn.StartReadLoop();
            _log.Info("TCP connected. Starting handshake.");

            // --- Handshake step 1: JOIN_LOBBY_PACKET ---
            await SendPacket(new SendJoinLobbyPacket(100, opts.Name), "JOIN_LOBBY_PACKET", $"version=100 name=\"{opts.Name}\"");

            var lobbyListWait = await WaitWithTimeout(lobbyListTcs.Task, TimeSpan.FromSeconds(10), "LOBBY_PLAYER_LIST_PACKET");
            if (lobbyListWait == null)
            {
                _log.Error("Host never sent LOBBY_PLAYER_LIST_PACKET - is the host actually hosting on that port? Aborting.");
                _log.PrintSummary();
                return 1;
            }

            // --- Handshake step 2: CHARACTER_SELECTION ---
            await SendPacket(new SendSelectCharacterPacket(opts.Character), "CHARACTER_SELECTION", $"character=\"{opts.Character}\"");

            _log.Info("Waiting for the host to click Start Game in their lobby UI...");
            var startGameWait = await WaitWithTimeout(startGameTcs.Task, TimeSpan.FromMinutes(10), "START_GAME");
            if (startGameWait == null)
            {
                _log.Error("Timed out waiting for START_GAME (10 min). Aborting.");
                _log.PrintSummary();
                return 1;
            }

            // --- Handshake step 3: simulate map load time, then GAME_LOADED_PACKET ---
            _log.Info("Host started the game. Simulating map load (1s)...");
            await Task.Delay(1000);
            await SendPacket(new SendGameLoadedPacket(0f, 0f, 0f, 0f, 0f, 0f, 1f), "GAME_LOADED_PACKET",
                "pos=(0,0,0) rot=(0,0,0,1) [dummy - no real game world]");

            _log.Info("Handshake complete - now running as a live fake player.");
            PrintLegend();

            var backgroundCts = new CancellationTokenSource();
            _ = RunHealthHeartbeat(backgroundCts.Token);
            _ = RunIdleMovement(backgroundCts.Token);

            await RunInteractiveKeyLoop();

            backgroundCts.Cancel();
            FinishAndExit();
            return 0;
        }

        private static void HandleIncoming(byte[] payload, TaskCompletionSource<byte[]> lobbyListTcs, TaskCompletionSource<byte[]> startGameTcs)
        {
            if (payload.Length < 1)
            {
                _log.Warn("Received empty packet payload (0 bytes) - ignoring.");
                return;
            }

            byte id = payload[0];
            string name = Enum.IsDefined(typeof(ServerSentPacketId), id)
                ? ((ServerSentPacketId)id).ToString()
                : $"UNKNOWN_SERVER_ID_{id}";

            string decoded = null;
            int? levelupCycle = null;
            if (Enum.IsDefined(typeof(ServerSentPacketId), id))
            {
                decoded = ServerPacketDecoder.TryDecode((ServerSentPacketId)id, payload, out levelupCycle);
            }

            _log.Received(id, name, decoded, payload);

            if (Enum.IsDefined(typeof(ServerSentPacketId), id))
            {
                switch ((ServerSentPacketId)id)
                {
                    case ServerSentPacketId.LOBBY_PLAYER_LIST_PACKET:
                        TryCaptureOwnUuid(payload);
                        lobbyListTcs.TrySetResult(payload);
                        break;

                    case ServerSentPacketId.START_GAME:
                        startGameTcs.TrySetResult(payload);
                        break;

                    case ServerSentPacketId.LEVELUP_RESUME:
                        _log.Info($"*** LEVELUP_RESUME received - cycle={levelupCycle} - all players done, game resumed ***");
                        break;
                }
            }
        }

        /// <summary>
        /// The lobby list's first entry is always the connection's own player (see
        /// JoinLobbyPacketHandler.cs: Prepend(newPlayer) before broadcasting), mirroring
        /// the real client's LobbyPlayerListPacketHandler which does SetMyself(Players[0]).
        /// </summary>
        private static void TryCaptureOwnUuid(byte[] payload)
        {
            try
            {
                var msg = new IncomingMessage(payload);
                msg.ReadByte(); // id
                int count = msg.ReadByte();
                if (count <= 0) return;
                _myUuid = msg.ReadUShort();
                _log.Info($"Assigned player UUID={_myUuid} by host.");
            }
            catch (Exception ex)
            {
                _log.Warn($"Could not parse own UUID from LOBBY_PLAYER_LIST_PACKET: {ex.Message}");
            }
        }

        private static async Task<byte[]> WaitWithTimeout(Task<byte[]> task, TimeSpan timeout, string what)
        {
            var completed = await Task.WhenAny(task, Task.Delay(timeout));
            if (completed != task)
            {
                return null;
            }
            return await task;
        }

        private static async Task SendPacket(OutgoingPacket packet, string name, string details = null)
        {
            byte[] buf = packet.ToBuffer();
            byte id = buf.Length > 0 ? buf[0] : (byte)0;
            try
            {
                await _conn.SendAsync(buf);
                _log.Sent(id, name, details);
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to send {name}: {ex.Message}");
            }
        }

        // Periodically reports full health so the host's Players HUD has live data.
        private static async Task RunHealthHeartbeat(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _running)
            {
                try
                {
                    if (_conn.IsConnected)
                        await SendPacket(new SendClientHealthPacket(100f, 100f, 0f), "PLAYER_HEALTH_PACKET", "cur=100 max=100 dmg=0");
                }
                catch { /* best effort heartbeat */ }

                try { await Task.Delay(TimeSpan.FromSeconds(5), ct); } catch { }
            }
        }

        // Low-rate move/rotate so the fake player "looks alive" on the host without flooding the log.
        private static async Task RunIdleMovement(CancellationToken ct)
        {
            double t = 0;
            while (!ct.IsCancellationRequested && _running)
            {
                try { await Task.Delay(TimeSpan.FromSeconds(4), ct); } catch { break; }
                if (!_conn.IsConnected) continue;

                t += 4.0;
                float x = (float)(Math.Sin(t * 0.1) * 2.0);
                float z = (float)(Math.Cos(t * 0.1) * 2.0);
                await SendPacket(new SendPlayerMovePacket(x, 0f, z), "PLAYER_MOVE_PACKET", $"pos=({x:0.##},0,{z:0.##})");
                await SendPacket(new SendPlayerRotatePacket(0f, (float)(t * 5 % 360), 0f), "PLAYER_ROTATE_PACKET", null);
            }
        }

        private static Task RunInteractiveKeyLoop()
        {
            return Task.Run(async () =>
            {
                // Console.KeyAvailable/ReadKey throw if stdin has no real console (e.g. output
                // redirected to a file, or launched from some IDE run windows). Detect that once
                // up front and fall back to "just stay connected until the process is killed"
                // instead of crashing the whole tool.
                bool interactiveAvailable = true;
                try { _ = Console.KeyAvailable; }
                catch (InvalidOperationException)
                {
                    interactiveAvailable = false;
                    _log.Warn("Console input is not interactive (redirected/no console) - interactive keys (d/l/x/g/k/q) are disabled. " +
                               "Running until the process is killed or the host disconnects.");
                }

                if (!interactiveAvailable)
                {
                    while (_running) await Task.Delay(500);
                    return;
                }

                while (_running)
                {
                    bool keyAvailable;
                    try { keyAvailable = Console.KeyAvailable; }
                    catch (InvalidOperationException)
                    {
                        // Became non-interactive mid-run (shouldn't normally happen) - stop polling.
                        while (_running) await Task.Delay(500);
                        return;
                    }

                    if (!keyAvailable)
                    {
                        await Task.Delay(50);
                        continue;
                    }

                    var key = Console.ReadKey(intercept: true);
                    switch (char.ToLowerInvariant(key.KeyChar))
                    {
                        case 'd':
                            await SendPacket(new SendPlayerDiedPacket(), "PLAYER_DIED_PACKET");
                            break;

                        case 'l':
                            await SendPacket(new SendLevelupDonePacket(), "LEVELUP_DONE_PACKET");
                            break;

                        case 'x':
                        {
                            int xp = _rng.Next(10, 100);
                            await SendPacket(new SendClientXpGainedPacket(xp), "PLAYER_XP_GAINED_PACKET (client)", $"xpAmount={xp}");
                            break;
                        }

                        case 'g':
                        {
                            int gold = _rng.Next(5, 50);
                            await SendPacket(new SendClientGoldGainedPacket(gold), "PLAYER_GOLD_GAINED_PACKET (client)", $"goldAmount={gold}");
                            break;
                        }

                        case 'k':
                            _log.Warn("Hard disconnect requested (key 'k') - closing socket abruptly (RST, no FIN).");
                            _conn.HardDisconnect();
                            _running = false;
                            break;

                        case 'q':
                            _log.Info("Quit requested (key 'q').");
                            _running = false;
                            break;
                    }
                }
            });
        }

        private static void FinishAndExit()
        {
            _running = false;
            try { _conn?.Dispose(); } catch { }
            _log.PrintSummary();
            _log.Dispose();
            Environment.Exit(0);
        }

        private sealed class Options
        {
            public string Host = "127.0.0.1";
            public int Port = 25565;
            public string Name = "TestBot";
            public string Character = "Warrior";
        }

        private static Options ParseArgs(string[] args)
        {
            if (args.Length == 0 || !string.Equals(args[0], "join", StringComparison.OrdinalIgnoreCase))
                return null;

            var opts = new Options();
            for (int i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--host" when i + 1 < args.Length:
                        opts.Host = args[++i];
                        break;
                    case "--port" when i + 1 < args.Length:
                        opts.Port = int.Parse(args[++i]);
                        break;
                    case "--name" when i + 1 < args.Length:
                        opts.Name = args[++i];
                        break;
                    case "--character" when i + 1 < args.Length:
                        opts.Character = args[++i];
                        break;
                }
            }
            return opts;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("MultibonkTestKit - fake second player for testing the Multibonk co-op mod solo.");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotnet run -- join --host 127.0.0.1 --port 25565 --name TestBot [--character Warrior]");
        }

        private static void PrintLegend()
        {
            _log.Info("Key legend: d=die  l=levelup-done  x=xp-gain  g=gold-gain  k=hard-disconnect  q=quit+summary");
        }
    }
}
