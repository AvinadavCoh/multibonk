using Il2CppAssets.Scripts.Utility;
using MelonLoader;
using Multibonk.Game.Handlers;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Lobby;
using UnityEngine;

namespace Multibonk.Game
{
    /// <summary>
    /// Coordinates the "wait for all players to finish the level-up upgrade screen"
    /// gate in multiplayer.
    ///
    /// HOST-SIDE FLOW:
    ///   1. ShowLevelupScreenPatch calls InitForLevelUp() – populates _pendingPlayers
    ///      with alive lobby players and increments the monotonic _hostCycle counter.
    ///   2. When the host closes its own screen, CloseLevelupScreenPatch calls
    ///      OnHostPlayerDone().
    ///   3. When a client sends LEVELUP_DONE_PACKET, LevelupDoneServerPacketHandler
    ///      calls OnClientPlayerDone(conn).
    ///   4. TryResumeAll() prunes dead/disconnected players and, when pending is empty,
    ///      schedules DoResume().  Also called by LobbyService.OnClientDisconnected so a
    ///      dropped player cannot stall everyone for the full timeout.
    ///   5. DoResume() sends LEVELUP_RESUME (with the cycle number) to every client,
    ///      then calls MyTime.Unpause() on the host.
    ///   6. A 20-second hard timeout (TickHostTimeout, ticked via MyTime.Update patch)
    ///      calls ForceResumeOnTimeout() as a host-side soft-lock guard.
    ///
    /// CLIENT-SIDE FLOW:
    ///   1. ShowLevelupScreenPatch calls SetActiveOnClient() – arms _isActive, increments
    ///      _clientCycle.
    ///   2. CloseLevelupScreenPatch re-pauses the client (cancelling CloseLevelupScreen's
    ///      auto-unpause), calls ClientStartWaiting() to arm the escape-hatch timer, then
    ///      fires GameEvents.LevelupScreenClosedEvent → LevelUpEventHandler sends
    ///      LEVELUP_DONE_PACKET to the host.
    ///   3. LevelupResumePacketHandler receives LEVELUP_RESUME and calls
    ///      ClientResume(cycle).  If the cycle matches the client's _clientCycle, the
    ///      client clears the waiting flag and calls MyTime.Unpause().  A non-matching
    ///      cycle (stale resume from a previous level-up) is silently discarded.
    ///   4. CLIENT ESCAPE HATCH (MANDATORY SOFT-LOCK SAFETY): if LEVELUP_RESUME does not
    ///      arrive within 20 seconds of the client closing its screen, TickClientTimeout()
    ///      force-unpauses the client and logs MelonLogger.Error.  The client NEVER
    ///      depends solely on the host to be un-paused (host may have crashed, or an XP
    ///      desync may have prevented the host from ever opening its own level-up screen).
    ///
    /// PAUSE-BROADCAST SUPPRESSION:
    ///   IsActive returning true during coordination tells TimeSyncPatches to suppress
    ///   the normal MyTime.Pause → PAUSE_GAME and MyTime.Unpause → UNPAUSE_GAME
    ///   broadcasts.  The coordinator owns the pause/resume signalling during this window.
    ///   The initial PAUSE_GAME (when ShowLevelupScreen first calls MyTime.Pause, before
    ///   InitForLevelUp sets IsActive) is intentionally NOT suppressed so clients know
    ///   the game is pausing.
    ///
    /// CYCLE NUMBERS:
    ///   _hostCycle (incremented in InitForLevelUp) and _clientCycle (incremented in
    ///   SetActiveOnClient) are both monotonically increasing.  The LEVELUP_RESUME packet
    ///   carries _hostCycle; the client accepts it only when its own _clientCycle matches.
    ///   This drops stale resumes from a previous level-up cycle that arrives during a new
    ///   one.  Cycle counters are not reset between runs to catch cross-run stale packets.
    ///
    /// RESET: RunCoordinator.RunReset → LevelUpEventHandler → Reset().
    /// </summary>
    public static class LevelUpCoordinator
    {
        // ── HOST STATE ────────────────────────────────────────────────────────────

        private static readonly HashSet<ushort> _pendingPlayers = new HashSet<ushort>();
        private static readonly object _lock = new object();

        // Volatile so reads on the Unity main thread always see latest network-thread writes.
        private static volatile bool _isActive = false;

        // Guards against scheduling DoResume() more than once per cycle.
        private static bool _resumeScheduled = false;

        // Monotonic counter: incremented each InitForLevelUp(). Carried by LEVELUP_RESUME.
        private static int _hostCycle = 0;

        // Timestamp set by InitForLevelUp(); used by TickHostTimeout().
        private static float _hostTimeoutStart = 0f;

        // ── CLIENT STATE ──────────────────────────────────────────────────────────

        // True while this client has re-paused itself waiting for LEVELUP_RESUME.
        // Set by ClientStartWaiting(); cleared by ClientResume() or TickClientTimeout().
        private static bool _clientWaitingForResume = false;

        // Timestamp of when this client started waiting (set by ClientStartWaiting()).
        private static float _clientWaitStart = 0f;

        // Monotonic counter: incremented each SetActiveOnClient().
        // LEVELUP_RESUME packets whose cycle != _clientCycle are discarded as stale.
        private static int _clientCycle = 0;

        // ── CONSTANTS ─────────────────────────────────────────────────────────────

        /// <summary>Hard timeout to prevent a permanent soft-lock on either side.</summary>
        public const float TIMEOUT_SECONDS = 20f;

        // ── PUBLIC ACCESSORS ──────────────────────────────────────────────────────

        /// <summary>
        /// True while level-up coordination is in progress on this machine (host or client).
        /// Read by TimeSyncPatches to suppress normal PAUSE_GAME / UNPAUSE_GAME broadcasts
        /// during the coordination window, so the coordinator exclusively owns pause signalling.
        /// </summary>
        public static bool IsActive => _isActive;

        // ── HOST-SIDE API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Initialises the pending-player set and starts the host-side timeout.
        /// Called on the HOST in the ShowLevelupScreen POSTFIX.
        /// Idempotent: a second call while already active is silently ignored.
        /// </summary>
        public static void InitForLevelUp()
        {
            var lobby = LobbyPatchFlags.CurrentLobby;
            if (lobby == null) return;

            lock (_lock)
            {
                if (_isActive) return;

                _pendingPlayers.Clear();
                foreach (var p in lobby.GetPlayers())
                {
                    if (!p.IsDead)
                        _pendingPlayers.Add(p.UUID);
                }

                _hostCycle++;
                _isActive = true;
                _resumeScheduled = false;
                _hostTimeoutStart = Time.unscaledTime;
            }

            MelonLogger.Msg(
                $"[LevelUp] Host coordination started (cycle {_hostCycle}) " +
                $"– waiting on {_pendingPlayers.Count} player(s)");
        }

        /// <summary>
        /// Call when the HOST's local player closes its upgrade screen.
        /// Removes the host from the pending set and attempts a resume.
        /// </summary>
        public static void OnHostPlayerDone()
        {
            var lobby = LobbyPatchFlags.CurrentLobby;
            var self = lobby?.GetMyself();
            if (self != null)
                lock (_lock) { _pendingPlayers.Remove(self.UUID); }

            MelonLogger.Msg("[LevelUp] Host finished upgrade selection");
            TryResumeAll();
        }

        /// <summary>
        /// Call when a CLIENT sends LEVELUP_DONE_PACKET.
        /// Removes that client from the pending set and attempts a resume.
        /// Safe to call from the network thread.
        /// </summary>
        public static void OnClientPlayerDone(Connection conn)
        {
            var lobby = LobbyPatchFlags.CurrentLobby;
            if (lobby == null) return;

            var player = lobby.GetPlayer(conn);
            if (player != null)
            {
                lock (_lock) { _pendingPlayers.Remove(player.UUID); }
                MelonLogger.Msg($"[LevelUp] Player '{player.Name}' finished upgrade selection");
            }

            TryResumeAll();
        }

        /// <summary>
        /// Prunes dead/disconnected players from the pending set, then resumes all if
        /// no one is left waiting.  Safe to call from any thread.
        ///
        /// Called from OnHost/OnClientPlayerDone() and also from
        /// LobbyService.OnClientDisconnected() so a dropped player immediately
        /// removes itself from the gate — not 20 seconds later.
        /// </summary>
        public static void TryResumeAll()
        {
            if (!LobbyPatchFlags.IsHosting) return;

            var lobby = LobbyPatchFlags.CurrentLobby;
            if (lobby != null)
            {
                var alive = new HashSet<ushort>(
                    lobby.GetPlayers()
                         .Where(p => !p.IsDead)
                         .Select(p => p.UUID));

                lock (_lock) { _pendingPlayers.IntersectWith(alive); }
            }

            bool allDone;
            lock (_lock) { allDone = _pendingPlayers.Count == 0; }

            if (allDone)
                ScheduleResume(forced: false);
        }

        /// <summary>
        /// Called by TickHostTimeout() when 20 s elapse without all players reporting done.
        /// Logs an error and forces a resume to prevent a permanent soft-lock.
        /// </summary>
        public static void ForceResumeOnTimeout()
        {
            string pending;
            lock (_lock) { pending = string.Join(", ", _pendingPlayers); }

            MelonLogger.Error(
                $"[LevelUp] SOFT-LOCK PREVENTION: level-up timed out after {TIMEOUT_SECONDS:F0} s " +
                $"on host (cycle {_hostCycle}). Still pending UUIDs: [{pending}]. Forcing resume.");

            ScheduleResume(forced: true);
        }

        // ── CLIENT-SIDE API ───────────────────────────────────────────────────────

        /// <summary>
        /// Called on a CLIENT in the ShowLevelupScreen POSTFIX.
        /// Sets IsActive (so broadcast suppression activates) and increments the client
        /// cycle counter so we can detect stale LEVELUP_RESUME packets.
        /// </summary>
        public static void SetActiveOnClient()
        {
            _clientCycle++;
            _isActive = true;
            // _clientWaitingForResume is NOT set here; it is set in ClientStartWaiting()
            // which fires only after the player actually closes the screen.
            MelonLogger.Msg($"[LevelUp] Client level-up active (client cycle {_clientCycle})");
        }

        /// <summary>
        /// Called from CloseLevelupScreenPatch on the CLIENT, immediately after the
        /// re-pause.  Arms the client-side escape-hatch timer.
        /// </summary>
        public static void ClientStartWaiting()
        {
            _clientWaitingForResume = true;
            _clientWaitStart = Time.unscaledTime;
        }

        /// <summary>
        /// Called by LevelupResumePacketHandler when LEVELUP_RESUME arrives.
        /// Clears coordination state and unpauses the local game.
        ///
        /// If <paramref name="cycle"/> != _clientCycle the packet is stale (from a
        /// previous level-up cycle that arrived late) and is silently discarded.
        ///
        /// Must be called on the Unity main thread (via GameDispatcher.Enqueue).
        /// </summary>
        public static void ClientResume(int cycle)
        {
            if (cycle != _clientCycle)
            {
                MelonLogger.Warning(
                    $"[LevelUp] Ignoring stale LEVELUP_RESUME " +
                    $"(packet cycle {cycle}, current client cycle {_clientCycle})");
                return;
            }

            try
            {
                _isActive = false;
                _clientWaitingForResume = false;
                MyTime.Unpause();
                MelonLogger.Msg($"[LevelUp] Client resumed via LEVELUP_RESUME (cycle {cycle})");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[LevelUp] ClientResume failed: {ex.Message}");
            }
        }

        // ── TIMEOUT TICKS ─────────────────────────────────────────────────────────

        /// <summary>
        /// Host-side: ticked every frame (Unity main thread, via MyTime.Update patch).
        /// After TIMEOUT_SECONDS, calls ForceResumeOnTimeout().
        /// </summary>
        public static void TickHostTimeout()
        {
            if (!_isActive) return;

            float elapsed = Time.unscaledTime - _hostTimeoutStart;
            if (elapsed >= TIMEOUT_SECONDS)
                ForceResumeOnTimeout();
        }

        /// <summary>
        /// Client-side escape hatch: ticked every frame (Unity main thread, via
        /// MyTime.Update patch).
        ///
        /// If the client has already been unpaused (e.g. by a race with UNPAUSE_GAME),
        /// the hatch is disarmed silently.  If LEVELUP_RESUME has not arrived after
        /// TIMEOUT_SECONDS, the client force-unpauses itself and logs MelonLogger.Error.
        ///
        /// The client NEVER depends solely on the host to be un-paused.  The host may
        /// have crashed, or an XP desync may have prevented its level-up screen from
        /// opening, leaving InitForLevelUp() never called and LEVELUP_RESUME never sent.
        /// </summary>
        public static void TickClientTimeout()
        {
            if (!_clientWaitingForResume) return;

            // If something else already unpaused us (e.g. an UNPAUSE_GAME that arrived
            // while networkPauseActive was true), disarm the hatch silently.
            try
            {
                if (!MyTime.paused)
                {
                    _clientWaitingForResume = false;
                    _isActive = false;
                    return;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[LevelUp] TickClientTimeout paused-check failed: {ex.Message}");
                return;
            }

            float elapsed = Time.unscaledTime - _clientWaitStart;
            if (elapsed < TIMEOUT_SECONDS) return;

            MelonLogger.Error(
                $"[LevelUp] SOFT-LOCK PREVENTION: LEVELUP_RESUME not received within " +
                $"{TIMEOUT_SECONDS:F0} s on client (client cycle {_clientCycle}). " +
                "Force-unpausing. Host likely crashed or had an XP desync — check host logs.");

            _clientWaitingForResume = false;
            _isActive = false;

            try { MyTime.Unpause(); }
            catch (Exception ex)
            {
                MelonLogger.Error($"[LevelUp] Client escape-hatch unpause failed: {ex.Message}");
            }
        }

        // ── RUN RESET ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Clears per-run coordination state.
        /// Wired to RunCoordinator.RunReset via LevelUpEventHandler.
        /// Note: cycle counters are intentionally NOT reset — they must survive across
        /// run boundaries to correctly reject stale cross-run LEVELUP_RESUME packets.
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                _isActive = false;
                _resumeScheduled = false;
                _pendingPlayers.Clear();
            }

            _clientWaitingForResume = false;
            _clientWaitStart = 0f;
        }

        // ── INTERNAL HELPERS ──────────────────────────────────────────────────────

        private static void ScheduleResume(bool forced)
        {
            lock (_lock)
            {
                if (!_isActive || _resumeScheduled) return;
                _resumeScheduled = true;
            }

            // Always dispatch to the main thread; DoResume calls Unity APIs.
            GameDispatcher.Enqueue(() => DoResume(forced));
        }

        private static void DoResume(bool forced)
        {
            try
            {
                int cycle;
                lock (_lock)
                {
                    _isActive = false;    // IsActive → false BEFORE MyTime.Unpause() so
                    _resumeScheduled = false; // TimeSyncPatches does NOT suppress the final
                    _pendingPlayers.Clear();  // UNPAUSE_GAME broadcast (belt-and-suspenders).
                    cycle = _hostCycle;
                }

                if (forced)
                    MelonLogger.Error($"[LevelUp] Forced resume executed (host timeout, cycle {cycle})");
                else
                    MelonLogger.Msg($"[LevelUp] All players done – resuming game (cycle {cycle})");

                // Send LEVELUP_RESUME (with cycle number) to every connected client.
                var lobby = LobbyPatchFlags.CurrentLobby;
                if (lobby != null)
                {
                    var packet = new SendLevelupResumePacket(cycle);
                    foreach (var player in lobby.GetPlayers())
                        player.Connection?.EnqueuePacket(packet);
                }

                // Unpause the host.  _isActive is now false, so MyTimeUnpausePatch will
                // also broadcast UNPAUSE_GAME — that is intentional belt-and-suspenders.
                // Clients whose networkPauseActive == false will ignore UNPAUSE_GAME and
                // rely on LEVELUP_RESUME instead; clients whose networkPauseActive == true
                // (initial PAUSE_GAME arrived before their self-pause) will be unpaused by
                // whichever packet arrives first.
                MyTime.Unpause();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[LevelUp] DoResume failed: {ex.Message}");
            }
        }
    }
}
