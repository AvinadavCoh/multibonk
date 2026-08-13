using System.Globalization;
using Multibonk.Networking.Comms.Base;
using MultibonkTestKit.Logging;
using MultibonkTestKit.Net;
using MultibonkTestKit.Protocol;

namespace MultibonkTestKit
{
    /// <summary>
    /// `host` mode - MultibonkTestKit acts as a fake HOST that a real Multibonk game
    /// CLIENT connects to. Unlike `join` mode (which drives the mod's host-side code by
    /// impersonating a client), this mode drives the mod's CLIENT-side apply code - enemy
    /// death/health, pickups, chests/shrines, level-up resume, run-over, stage transition -
    /// by feeding it hand-picked scripted packets while a human watches the real game
    /// window. See README.md for the full walkthrough and key legend.
    /// </summary>
    public static class HostMode
    {
        private enum State
        {
            WaitingForClient,
            WaitingForJoin,
            WaitingForCharacterSelect,
            ReadyToStart,
            WaitingForGameLoaded,
            Running,
        }

        private static PacketLog _log;
        private static WireListener _listener;
        private static WireConnection _conn;
        private static volatile bool _running = true;
        private static State _state = State.WaitingForClient;
        private static readonly object _stateLock = new();
        private static readonly Random _rng = new();

        private const ushort HostUuid = 1;
        private static ushort _nextClientUuid = 2;
        private static ushort _clientUuid;
        private static string _clientName = "";
        private static string _clientCharacter = "";

        private static int _port;
        private static int _seed;
        private static string _hostName;
        private static byte _hostCharacterByte;

        private static float _lastClientX, _lastClientY, _lastClientZ;
        private static bool _haveClientPos;
        private static int _spreadCounter;
        private static int _mapRevealCount;

        private static readonly List<int> _spawnedEnemyIds = new();
        private static readonly HashSet<int> _deadEnemyIds = new();
        private static int _nextEnemyId = 1000;

        private static readonly List<string> _spawnedPickupIds = new();
        private static int _nextPickupId = 1;

        private static int _levelupCycle;
        private static int _waveNumber;
        private static bool _paused;
        private static int _totalGoldGranted;

        private static ushort _digestSequence;

        /// <summary>
        /// Per-channel "sent" counters in the exact order of
        /// Multibonk/Game/Diagnostics/SyncTelemetry.cs's SyncChannel enum (append-only,
        /// values are written on the wire in STATE_DIGEST - never reorder):
        /// 0 EnemySpawn, 1 EnemyDeath, 2 PickupSpawn, 3 PickupDespawn, 4 TimelineEvent,
        /// 5 MapReveal, 6 XpGain, 7 GoldGain, 8 FinalSwarm.
        /// </summary>
        private static readonly int[] _channelSent = new int[9];

        private static readonly System.Diagnostics.Stopwatch _runClock = new();

        public static async Task<int> RunAsync(string[] args)
        {
            var opts = ParseArgs(args);
            if (opts == null)
            {
                PrintUsage();
                return 1;
            }

            _port = opts.Port;
            _seed = opts.Seed;
            _hostName = opts.Name;
            _hostCharacterByte = opts.CharacterByte;

            _log = new PacketLog();
            PrintLegend();

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                _log.Info("Ctrl+C received - shutting down.");
                FinishAndExit();
            };

            _listener = new WireListener(_port);
            _listener.OnClientConnected += OnClientConnected;
            _listener.OnAcceptError += ex => _log.Warn($"Accept error (still listening): {ex.Message}");

            try
            {
                _listener.Start();
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to start listening on port {_port}: {ex.Message}");
                return 1;
            }

            _log.Info($"Listening on 0.0.0.0:{_port} (map seed={_seed}). Waiting for a real Multibonk game client to connect " +
                       $"(F5 menu -> Join -> 127.0.0.1:{_port})...");

            var backgroundCts = new CancellationTokenSource();
            _ = RunPeriodicBroadcastsAsync(backgroundCts.Token);

            await RunInteractiveKeyLoop();

            backgroundCts.Cancel();
            FinishAndExit();
            return 0;
        }

        // ---------------- connection lifecycle ----------------

        private static void OnClientConnected(WireConnection conn)
        {
            lock (_stateLock)
            {
                if (_conn != null && _conn.IsConnected)
                {
                    _log.Warn("A second client tried to connect while one is already active - this tool drives one client at a time. Rejecting the new connection.");
                    try { conn.Dispose(); } catch { }
                    return;
                }

                _conn = conn;
                _state = State.WaitingForJoin;
            }

            _log.Info("Client connected. Waiting for JOIN_LOBBY_PACKET...");

            conn.OnPacket += HandleIncoming;
            conn.OnClosed += ex =>
            {
                lock (_stateLock)
                {
                    if (_conn == conn)
                    {
                        _conn = null;
                        _state = State.WaitingForClient;
                    }
                }
                if (ex != null)
                    _log.Warn($"Client disconnected: {ex.GetType().Name}: {ex.Message}. Still listening - reconnect anytime, no restart needed.");
                else
                    _log.Info("Client disconnected gracefully. Still listening - reconnect anytime, no restart needed.");
            };

            conn.StartReadLoop();
        }

        private static void HandleIncoming(byte[] payload)
        {
            if (payload.Length < 1)
            {
                _log.Warn("Received empty packet payload (0 bytes) - ignoring.");
                return;
            }

            byte id = payload[0];
            bool known = Enum.IsDefined(typeof(ClientSentPacketId), id);
            string name = known ? ((ClientSentPacketId)id).ToString() : $"UNKNOWN_CLIENT_ID_{id}";

            string decoded = null;
            ClientDecodedData data = null;
            if (known)
                decoded = ClientPacketDecoder.TryDecode((ClientSentPacketId)id, payload, out data);

            _log.Received(id, name, decoded, payload);

            if (!known) return;

            try
            {
                switch ((ClientSentPacketId)id)
                {
                    case ClientSentPacketId.JOIN_LOBBY_PACKET:
                        _ = HandleJoinLobby(data);
                        break;

                    case ClientSentPacketId.CHARACTER_SELECTION:
                        _ = HandleCharacterSelection(data);
                        break;

                    case ClientSentPacketId.GAME_LOADED_PACKET:
                        _ = HandleGameLoaded(data);
                        break;

                    case ClientSentPacketId.PLAYER_MOVE_PACKET:
                        _lastClientX = data.X; _lastClientY = data.Y; _lastClientZ = data.Z;
                        _haveClientPos = true;
                        break;

                    case ClientSentPacketId.LEVELUP_DONE_PACKET:
                        _log.Info("*** LEVELUP_DONE received - client finished its upgrade pick. Press 'r' to send LEVELUP_RESUME (or wait 20s for the client's own soft-lock timeout). ***");
                        break;

                    case ClientSentPacketId.PICKUP_CONSUMED_PACKET:
                        _log.Info($"*** PICKUP_CONSUMED received for hostId=\"{data.StringA}\" - proves the client->host direction works for pickups. ***");
                        break;

                    case ClientSentPacketId.PLAYER_DIED_PACKET:
                        _log.Info("*** PLAYER_DIED received - proves the client->host direction works for player death. ***");
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Warn($"Error handling {name}: {ex.Message}");
            }
        }

        private static async Task HandleJoinLobby(ClientDecodedData data)
        {
            ushort uuid;
            lock (_stateLock) { uuid = _nextClientUuid++; }
            _clientUuid = uuid;
            _clientName = data.StringA;
            _clientCharacter = "";

            _log.Info($"Assigning UUID={uuid} to client \"{_clientName}\" (mod version={data.IntVersion}).");

            // Mirrors JoinLobbyPacketHandler.cs: Prepend(newPlayer) then broadcast - the
            // newly-joined player is always Players[0], which is exactly what the real
            // client's LobbyPlayerListPacketHandler uses to identify itself.
            var players = new List<(ushort, string, string)>
            {
                (uuid, _clientName, ""),
                (HostUuid, _hostName, ""),
            };
            await SendServerPacket(new SendLobbyPlayerListPacket(players), "LOBBY_PLAYER_LIST_PACKET",
                $"players=2 | [0] uuid={uuid} name=\"{_clientName}\" (client identifies itself from this entry) | [1] uuid={HostUuid} name=\"{_hostName}\"");

            lock (_stateLock) { _state = State.WaitingForCharacterSelect; }
        }

        private static async Task HandleCharacterSelection(ClientDecodedData data)
        {
            _clientCharacter = data.StringA;
            await SendServerPacket(new SendPlayerSelectedCharacterPacket(_clientUuid, _clientCharacter), "PLAYER_SELECTED_CHARACTER",
                $"playerId={_clientUuid} character=\"{_clientCharacter}\"");

            lock (_stateLock) { _state = State.ReadyToStart; }
            _log.Info($"Ready. Press 'g' to send START_GAME (seed={_seed}) - mirrors clicking 'Start Game' in a real host's lobby UI.");
        }

        private static async Task HandleGameLoaded(ClientDecodedData data)
        {
            _lastClientX = data.X; _lastClientY = data.Y; _lastClientZ = data.Z;
            _haveClientPos = true;

            lock (_stateLock) { _state = State.WaitingForGameLoaded; }
            _log.Info("Client sent GAME_LOADED_PACKET. Waiting 500ms (mirrors GameLoadedEventHandler's real host-side delay) before SPAWN_PLAYER_PACKET...");

            await Task.Delay(500);

            if (_conn == null || !_conn.IsConnected)
            {
                _log.Warn("Client disconnected before the spawn delay elapsed - not sending SPAWN_PLAYER_PACKET.");
                return;
            }

            // Offset a couple of units from the client's own position so the fake host's
            // body is visually distinct instead of exactly overlapping the real player.
            float hx = data.X + 2f, hy = data.Y, hz = data.Z;
            await SendServerPacket(
                new SendSpawnPlayerPacket(_hostCharacterByte, HostUuid, hx, hy, hz, 0f, 0f, 0f, 1f),
                "SPAWN_PLAYER_PACKET",
                $"playerId={HostUuid} characterByte={_hostCharacterByte} pos=({F(hx)},{F(hy)},{F(hz)})");

            lock (_stateLock) { _state = State.Running; }
            _runClock.Restart();
            _log.Info("*** Client should now spawn a second player body ('" + _hostName + "') a couple of units away. LOOK AT THE SCREEN. ***");
            if (_hostCharacterByte == 0)
            {
                _log.Info("(characterByte=0 is a best-effort default - ECharacter is an Il2Cpp-only enum this tool can't read from source. " +
                           "If no body appears, pass --character-byte <n> with a value matching a real character index in your build; a bad " +
                           "value is caught and logged by the client, it never crashes.)");
            }
            PrintLegend();
        }

        // ---------------- interactive key loop ----------------

        private static Task RunInteractiveKeyLoop()
        {
            return Task.Run(async () =>
            {
                bool interactiveAvailable = true;
                try { _ = Console.KeyAvailable; }
                catch (InvalidOperationException)
                {
                    interactiveAvailable = false;
                    _log.Warn("Console input is not interactive (redirected/no console) - interactive keys are disabled. " +
                               "Running until the process is killed.");
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
                        while (_running) await Task.Delay(500);
                        return;
                    }

                    if (!keyAvailable)
                    {
                        await Task.Delay(50);
                        continue;
                    }

                    var key = Console.ReadKey(intercept: true);
                    await HandleKey(char.ToLowerInvariant(key.KeyChar));
                }
            });
        }

        private static async Task HandleKey(char c)
        {
            switch (c)
            {
                case 'g':
                {
                    State current;
                    lock (_stateLock) { current = _state; }
                    if (current != State.ReadyToStart)
                    {
                        _log.Warn($"'g' (start game) is only valid once a client has joined and picked a character (current state={current}).");
                        break;
                    }
                    await SendServerPacket(new SendStartGamePacket(_seed), "START_GAME", $"seed={_seed}");
                    lock (_stateLock) { _state = State.WaitingForGameLoaded; }
                    _log.Info("Sent START_GAME. Waiting for the client to load the map and send GAME_LOADED_PACKET (map generation can take a while).");
                    break;
                }

                case '1':
                {
                    if (!EnsureRunning()) break;
                    int id = _nextEnemyId++;
                    var (px, py, pz) = NearPlayerPos();
                    await SendServerPacket(new SendEnemySpawnPacket(id, 0, px, py, pz, 1, false, 0), "ENEMY_SPAWN_PACKET",
                        $"enemyId={id} enemyType=0 pos=({F(px)},{F(py)},{F(pz)}) level=1 isBoss=false flag=0");
                    _channelSent[0]++; // EnemySpawn
                    _spawnedEnemyIds.Add(id);
                    _log.Info($"EXPECT: an enemy (id={id}) should appear on the client near the player. enemyType=0 is best-effort (EEnemy is " +
                               "an Il2Cpp-only enum this tool can't read). If the client logs 'No cached EnemyData for type 0', let a couple of " +
                               "real enemies spawn locally first (its cache scrapes from real spawns), then retry.");
                    break;
                }

                case '2':
                {
                    if (!EnsureRunning()) break;
                    if (!TryGetLastLiveEnemy(out int id)) { _log.Warn("No live enemy to kill this session - press '1' first."); break; }
                    await SendServerPacket(new SendEnemyDeathPacket(id.ToString()), "ENEMY_DEATH_PACKET", $"enemyId={id}");
                    _channelSent[1]++; // EnemyDeath
                    _deadEnemyIds.Add(id);
                    _log.Info($"*** KEY CHECK: enemy {id} should die/despawn on the client right now. This handler used to be a no-op stub - " +
                               "if nothing happens, that regression is back. ***");
                    break;
                }

                case '3':
                {
                    if (!EnsureRunning()) break;
                    if (!TryGetLastLiveEnemy(out int id)) { _log.Warn("No live enemy to damage this session - press '1' first."); break; }
                    await SendServerPacket(new SendEnemyHealthUpdatePacket(id.ToString(), 50f, 100f), "ENEMY_HEALTH_UPDATE_PACKET",
                        $"enemyId={id} currentHealth=50 maxHealth=100");
                    _log.Info($"EXPECT: enemy {id}'s HP bar drops to 50%.");
                    break;
                }

                case '4':
                {
                    if (!EnsureRunning()) break;
                    string pid = $"testkit-pickup-{_nextPickupId++}";
                    var (px, py, pz) = NearPlayerPos();
                    await SendServerPacket(new SendItemDroppedPacket(pid, px, py, pz, 0, 1), "ITEM_DROPPED_PACKET",
                        $"itemId=\"{pid}\" pos=({F(px)},{F(py)},{F(pz)}) itemType=0 value=1");
                    _channelSent[2]++; // PickupSpawn
                    _spawnedPickupIds.Add(pid);
                    _log.Info($"EXPECT: an orb (id={pid}) appears near the player. itemType=0 is best-effort (EPickup is an Il2Cpp-only enum).");
                    break;
                }

                case '5':
                {
                    if (!EnsureRunning()) break;
                    if (_spawnedPickupIds.Count == 0) { _log.Warn("No pickup to despawn this session - press '4' first."); break; }
                    string pid = _spawnedPickupIds[^1];
                    _spawnedPickupIds.RemoveAt(_spawnedPickupIds.Count - 1);
                    await SendServerPacket(new SendItemPickedUpPacket(pid, _clientUuid), "ITEM_PICKED_UP_PACKET", $"itemId=\"{pid}\" playerId={_clientUuid}");
                    _channelSent[3]++; // PickupDespawn
                    _log.Info($"EXPECT: the orb {pid} vanishes from the client.");
                    break;
                }

                case '6':
                {
                    if (!EnsureRunning()) break;
                    int tx, ty;
                    if (_haveClientPos)
                    {
                        tx = (int)Math.Round(_lastClientX) + _mapRevealCount * 5;
                        ty = (int)Math.Round(_lastClientZ) + _mapRevealCount * 5;
                    }
                    else
                    {
                        tx = _mapRevealCount * 5;
                        ty = _mapRevealCount * 5;
                    }
                    _mapRevealCount++;
                    await SendServerPacket(new SendMapRevealPacket(tx, ty), "MAP_REVEAL", $"tileX={tx} tileY={ty}");
                    _channelSent[5]++; // MapReveal
                    _log.Info($"EXPECT: fog clears on the client's minimap near world position ({tx},{ty}).");
                    break;
                }

                case '7':
                {
                    if (!EnsureRunning()) break;
                    if (!TryGetOperatorPosition("chest", out int qx, out int qy, out int qz)) break;
                    string chestId = $"{qx}_{qy}_{qz}";
                    await SendServerPacket(new SendChestOpenPacket(chestId, _clientUuid), "CHEST_OPEN", $"chestId=\"{chestId}\" playerId={_clientUuid}");
                    _log.Info($"EXPECT: the chest AT world position ({qx},{qy},{qz}) opens on the client. A MISMATCH means the position key " +
                               $"didn't resolve - the client logs 'No InteractableChest found at position {chestId}' if so. Stand next to a real " +
                               "chest and read its coordinates from the game log/debug overlay for a reliable test, rather than relying on the " +
                               "player's own position (players are rarely standing exactly on a chest).");
                    break;
                }

                case '8':
                {
                    if (!EnsureRunning()) break;
                    if (!TryGetOperatorPosition("shrine", out int qx, out int qy, out int qz)) break;
                    int shrineType = PromptShrineType();
                    string shrineId = $"{qx}_{qy}_{qz}";
                    await SendServerPacket(new SendShrineUsePacket(shrineId, _clientUuid, shrineType), "SHRINE_USE",
                        $"shrineId=\"{shrineId}\" playerId={_clientUuid} shrineType={shrineType}");
                    _log.Info($"EXPECT: the shrine AT world position ({qx},{qy},{qz}) (type={shrineType}) activates on the client. A MISMATCH " +
                               $"means the position key didn't resolve - the client logs 'No <ShrineClass> found at position {shrineId}' if so.");
                    break;
                }

                case '9':
                {
                    if (!EnsureRunning()) break;
                    int xp = _rng.Next(50, 200);
                    await SendServerPacket(new SendServerPlayerXpGainedPacket(_clientUuid, xp), "PLAYER_XP_GAINED_PACKET", $"playerId={_clientUuid} xpAmount={xp}");
                    _channelSent[6]++; // XpGain
                    _log.Info($"EXPECT: client gains {xp} XP (bar fills, may trigger a level-up screen).");
                    break;
                }

                case '0':
                {
                    if (!EnsureRunning()) break;
                    int gold = _rng.Next(20, 100);
                    _totalGoldGranted += gold;
                    await SendServerPacket(new SendServerPlayerGoldGainedPacket(gold), "PLAYER_GOLD_GAINED", $"goldAmount={gold}");
                    _channelSent[7]++; // GoldGain
                    _log.Info($"EXPECT: client's gold counter increases by {gold}.");
                    break;
                }

                case 'w':
                {
                    if (!EnsureRunning()) break;
                    _waveNumber++;
                    await SendServerPacket(new SendWaveStartPacket(_waveNumber), "WAVE_START", $"waveNumber={_waveNumber}");
                    _channelSent[4]++; // TimelineEvent
                    _log.Info($"EXPECT: client shows/updates the wave-start indicator for wave {_waveNumber}.");
                    break;
                }

                case 'f':
                {
                    if (!EnsureRunning()) break;
                    await SendServerPacket(new SendWaveCompletePacket(_waveNumber), "WAVE_COMPLETE", $"waveNumber={_waveNumber}");
                    _channelSent[8]++; // FinalSwarm
                    _log.Info($"EXPECT: client shows the wave-complete / final swarm indicator for wave {_waveNumber}.");
                    break;
                }

                case 'p':
                    if (!EnsureRunning()) break;
                    _paused = true;
                    await SendServerPacket(new SendPauseGamePacket(), "PAUSE_GAME");
                    _log.Info("EXPECT: client's game freezes/pauses.");
                    break;

                case 'u':
                    if (!EnsureRunning()) break;
                    _paused = false;
                    await SendServerPacket(new SendUnpauseGamePacket(), "UNPAUSE_GAME");
                    _log.Info("EXPECT: client's game resumes.");
                    break;

                case 'r':
                {
                    if (!EnsureRunning()) break;
                    _levelupCycle++;
                    await SendServerPacket(new SendLevelupResumePacket(_levelupCycle), "LEVELUP_RESUME", $"cycleNumber={_levelupCycle}");
                    _log.Info($"EXPECT: if the client is paused on its upgrade screen, it resumes now (cycle={_levelupCycle}). REMINDER: the " +
                               "client also has a 20s self-escape hatch - waiting 20s WITHOUT pressing 'r' should ALSO resume it, and the game " +
                               "log should show a '[LevelUp] SOFT-LOCK PREVENTION' line when that happens. Worth verifying both paths.");
                    break;
                }

                case 'o':
                    if (!EnsureRunning()) break;
                    await SendServerPacket(new SendRunOverPacket(), "RUN_OVER");
                    _log.Info("EXPECT: client shows the game-over screen.");
                    break;

                case 't':
                    if (!EnsureRunning()) break;
                    await SendServerPacket(new SendStageTransitionPacket(), "STAGE_TRANSITION");
                    _log.Info("EXPECT: client loads the next stage.");
                    break;

                case 's':
                    if (!EnsureRunning()) break;
                    await SendStateDigestNow();
                    break;

                case 'h':
                case '?':
                    PrintLegend();
                    break;

                case 'q':
                    _log.Info("Quit requested (key 'q').");
                    _running = false;
                    break;
            }
        }

        // ---------------- periodic broadcasts ----------------

        private static async Task RunPeriodicBroadcastsAsync(CancellationToken ct)
        {
            var lastTimeSync = DateTime.MinValue;
            var lastDigest = DateTime.MinValue;

            while (!ct.IsCancellationRequested && _running)
            {
                try { await Task.Delay(250, ct); } catch { break; }

                State current;
                lock (_stateLock) { current = _state; }
                if (current != State.Running || _conn == null || !_conn.IsConnected) continue;

                var now = DateTime.UtcNow;

                if ((now - lastTimeSync).TotalSeconds >= 2)
                {
                    lastTimeSync = now;
                    float t = (float)_runClock.Elapsed.TotalSeconds;
                    await SendServerPacket(new SendTimeSyncPacket(t, t, _paused), "TIME_SYNC", $"stageTime={F(t)} runTime={F(t)} paused={_paused}");
                }

                if ((now - lastDigest).TotalSeconds >= 5)
                {
                    lastDigest = now;
                    await SendStateDigestNow();
                }
            }
        }

        private static async Task SendStateDigestNow()
        {
            float t = (float)_runClock.Elapsed.TotalSeconds;
            int liveEnemies = _spawnedEnemyIds.Count - _deadEnemyIds.Count;
            int livePickups = _spawnedPickupIds.Count;
            ushort seq = _digestSequence++;
            int[] counters = (int[])_channelSent.Clone();

            // gold/level are placeholders (real total granted / 1) - this tool doesn't
            // track the client's actual inventory, so a client-side desync comparison
            // against these two fields is expected to show a mismatch; that's a limitation
            // of the tool, not a real desync.
            await SendServerPacket(
                new SendStateDigestPacket(seq, t, t, _totalGoldGranted, 1, liveEnemies, livePickups, counters, -1),
                "STATE_DIGEST",
                $"sequence={seq} liveEnemies={liveEnemies} livePickups={livePickups} engineEnemyCount=-1 (no real EnemyManager here)");
        }

        // ---------------- helpers ----------------

        private static bool EnsureRunning()
        {
            State current;
            lock (_stateLock) { current = _state; }
            if (current != State.Running)
            {
                _log.Warn($"Not ready yet (state={current}) - the client must be connected and in a running map before scenario packets make sense.");
                return false;
            }
            return true;
        }

        private static bool TryGetLastLiveEnemy(out int id)
        {
            for (int i = _spawnedEnemyIds.Count - 1; i >= 0; i--)
            {
                if (!_deadEnemyIds.Contains(_spawnedEnemyIds[i]))
                {
                    id = _spawnedEnemyIds[i];
                    return true;
                }
            }
            id = 0;
            return false;
        }

        /// <summary>Spreads consecutive spawns around the client's last known position instead of stacking them.</summary>
        private static (float x, float y, float z) NearPlayerPos()
        {
            float baseX = _haveClientPos ? _lastClientX : 0f;
            float baseY = _haveClientPos ? _lastClientY : 0f;
            float baseZ = _haveClientPos ? _lastClientZ : 0f;
            float angle = (_spreadCounter++ % 8) * (float)(Math.PI / 4);
            float ox = (float)Math.Cos(angle) * 3f;
            float oz = (float)Math.Sin(angle) * 3f;
            return (baseX + ox, baseY, baseZ + oz);
        }

        private static bool TryGetOperatorPosition(string what, out int qx, out int qy, out int qz)
        {
            qx = qy = qz = 0;
            Console.Write($"  Enter {what} position as 'x y z' (Enter = client's last known position): ");
            string line;
            try { line = Console.ReadLine(); }
            catch (InvalidOperationException) { _log.Warn("Console input is not interactive - cannot prompt for a position."); return false; }

            if (string.IsNullOrWhiteSpace(line))
            {
                if (!_haveClientPos)
                {
                    _log.Warn("No client position known yet (no GAME_LOADED_PACKET/PLAYER_MOVE_PACKET received) - cannot default to it.");
                    return false;
                }
                qx = (int)Math.Round(_lastClientX);
                qy = (int)Math.Round(_lastClientY);
                qz = (int)Math.Round(_lastClientZ);
                return true;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3
                || !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var fx)
                || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var fy)
                || !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var fz))
            {
                _log.Warn($"Could not parse '{line}' as 'x y z' - command aborted.");
                return false;
            }

            qx = (int)Math.Round(fx);
            qy = (int)Math.Round(fy);
            qz = (int)Math.Round(fz);
            return true;
        }

        private static int PromptShrineType()
        {
            Console.Write("  Shrine type 0-5 (0=Balance 1=Challenge 2=Cursed 3=Greed 4=Magnet 5=Moai, Enter=0): ");
            string line;
            try { line = Console.ReadLine(); }
            catch (InvalidOperationException) { return 0; }

            if (!string.IsNullOrWhiteSpace(line) && int.TryParse(line, out int type) && type is >= 0 and <= 5)
                return type;
            return 0;
        }

        private static async Task SendServerPacket(OutgoingPacket packet, string name, string details = null)
        {
            var conn = _conn;
            if (conn == null || !conn.IsConnected)
            {
                _log.Warn($"Cannot send {name} - no client connected.");
                return;
            }

            byte[] buf = packet.ToBuffer();
            byte id = buf.Length > 0 ? buf[0] : (byte)0;
            try
            {
                await conn.SendAsync(buf);
                _log.Sent(id, name, details);
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to send {name}: {ex.Message}");
            }
        }

        private static void FinishAndExit()
        {
            _running = false;
            try { _listener?.Dispose(); } catch { }
            try { _conn?.Dispose(); } catch { }
            _log.PrintSummary(asHost: true);
            _log.Dispose();
            Environment.Exit(0);
        }

        private static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        private sealed class Options
        {
            public int Port = 25565;
            public int Seed = 12345;
            public string Name = "TestKit-Host";
            public byte CharacterByte;
        }

        private static Options ParseArgs(string[] args)
        {
            if (args.Length == 0 || !string.Equals(args[0], "host", StringComparison.OrdinalIgnoreCase))
                return null;

            var opts = new Options();
            for (int i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--port" when i + 1 < args.Length:
                        opts.Port = int.Parse(args[++i]);
                        break;
                    case "--seed" when i + 1 < args.Length:
                        opts.Seed = int.Parse(args[++i]);
                        break;
                    case "--name" when i + 1 < args.Length:
                        opts.Name = args[++i];
                        break;
                    case "--character-byte" when i + 1 < args.Length:
                        opts.CharacterByte = byte.Parse(args[++i]);
                        break;
                }
            }
            return opts;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotnet run -- host [--port 25565] [--seed 12345] [--name TestKit-Host] [--character-byte 0]");
        }

        private static void PrintLegend()
        {
            _log.Info("Key legend (host mode):");
            _log.Info("  g = send START_GAME (only once a client has joined & picked a character)");
            _log.Info("  1 = spawn enemy        2 = kill most-recently-spawned enemy   3 = damage it to 50% HP");
            _log.Info("  4 = drop pickup        5 = despawn most-recently-dropped pickup");
            _log.Info("  6 = reveal map area near the player");
            _log.Info("  7 = chest open (position-keyed)        8 = shrine use (position-keyed)");
            _log.Info("  9 = grant XP           0 = grant gold");
            _log.Info("  w = wave start         f = wave complete / final swarm");
            _log.Info("  p = pause game         u = unpause game");
            _log.Info("  r = LEVELUP_RESUME (incrementing cycle) - resumes a client stuck on its upgrade screen");
            _log.Info("  o = RUN_OVER (game over)               t = STAGE_TRANSITION (next stage)");
            _log.Info("  s = send STATE_DIGEST immediately");
            _log.Info("  h / ? = this legend    q = quit + summary");
        }
    }
}
