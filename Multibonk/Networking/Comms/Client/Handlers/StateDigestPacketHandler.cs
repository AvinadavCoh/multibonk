using Il2CppAssets.Scripts.Actors.Player;
using Il2CppAssets.Scripts.Managers;
using Il2CppAssets.Scripts.Utility;
using MelonLoader;
using Multibonk.Game.Diagnostics;
using Multibonk.Game.Handlers;
using Multibonk.Game.Patches;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;
using System.Collections.Generic;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    /// <summary>
    /// Client: diffs the host's state digest against local state and reports what drifted.
    ///
    /// This is the readable output of the desync detector. Rather than guessing which
    /// subsystem broke during a play session, the log names it: a channel the host keeps
    /// sending on while the client applies nothing is a handler that does nothing.
    ///
    /// Read-only - this never corrects anything. Correction is the job of the individual
    /// sync subsystems, and having the detector paper over their failures would defeat it.
    /// </summary>
    public class StateDigestPacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.STATE_DIGEST;

        /// <summary>Clock drift worth reporting. TimeSync corrects at 0.3s, so 1s means it is not converging.</summary>
        private const float DRIFT_REPORT_THRESHOLD = 1.0f;

        /// <summary>Live-object count difference tolerated before reporting (covers in-flight packets).</summary>
        private const int ENEMY_COUNT_TOLERANCE = 2;
        private const int PICKUP_COUNT_TOLERANCE = 3;

        /// <summary>
        /// Engine enemy count tolerance. Looser than the ledger tolerance because
        /// in-flight spawn/death packets can transiently inflate the engine count.
        /// </summary>
        private const int ENGINE_ENEMY_TOLERANCE = 5;

        /// <summary>How far a channel may lag the host before it is called out.</summary>
        private const int CHANNEL_LAG_TOLERANCE = 3;

        /// <summary>
        /// Channels where host-sent and client-applied are not expected to match, because
        /// the client also originates events on them (shared XP/gold flow both ways).
        /// Reported for context, never flagged as a fault.
        /// </summary>
        private static readonly HashSet<SyncChannel> BidirectionalChannels = new HashSet<SyncChannel>
        {
            SyncChannel.XpGain,
            SyncChannel.GoldGain,
        };

        private static int lastSequence = -1;

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new StateDigestPacket(msg);

            GameDispatcher.Enqueue(() =>
            {
                try
                {
                    Compare(packet);
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"[SyncCheck] Failed to compare state digest: {ex.Message}");
                }
            });
        }

        private static void Compare(StateDigestPacket host)
        {
            var issues = new List<string>();
            var notes = new List<string>();

            // Dropped digests are themselves a signal - the transport is losing packets.
            // Compared in ushort space so the counter's wraparound is not read as a gap.
            if (lastSequence >= 0 && host.Sequence != (ushort)((ushort)lastSequence + 1))
                issues.Add($"digest sequence jumped {lastSequence} -> {host.Sequence} ({(ushort)(host.Sequence - (ushort)lastSequence - 1)} lost)");
            lastSequence = host.Sequence;

            // --- clock ---
            float localStage = MyTime.stageTimer;
            float localRun = MyTime.runTimer;
            float stageDrift = System.Math.Abs(localStage - host.StageTime);
            if (stageDrift > DRIFT_REPORT_THRESHOLD)
                issues.Add($"clock: host stage {host.StageTime:F2} / local {localStage:F2} (drift {stageDrift:F2}s) - TimeSync not converging");

            float runDrift = System.Math.Abs(localRun - host.RunTime);
            if (runDrift > DRIFT_REPORT_THRESHOLD)
                issues.Add($"clock: host run {host.RunTime:F2} / local {localRun:F2} (drift {runDrift:F2}s)");

            // --- shared pools ---
            var inventory = MyPlayer.Instance?.inventory;
            if (inventory != null)
            {
                int localGold = inventory.goldInt;
                if (localGold != host.Gold)
                    issues.Add($"gold: host {host.Gold} / local {localGold} (diff {localGold - host.Gold})");

                int localLevel = inventory.GetCharacterLevel();
                if (localLevel != host.Level)
                    issues.Add($"level: host {host.Level} / local {localLevel}");
            }
            else
            {
                notes.Add("local inventory unavailable - gold/level not compared");
            }

            // --- live world objects ---
            // Two independent local numbers: what our ledger thinks we applied, and what the
            // ID mapper actually holds. If those disagree, spawns are counted as applied
            // without producing a usable enemy.
            int localEnemyLedger = SyncTelemetry.LedgerLiveEnemies(hosting: false);
            int localEnemyMappings = EnemyIdMapper.MappingCount;
            if (System.Math.Abs(localEnemyLedger - host.LiveEnemies) > ENEMY_COUNT_TOLERANCE)
                issues.Add($"enemies: host {host.LiveEnemies} / local ledger {localEnemyLedger} / local mappings {localEnemyMappings}");
            else if (System.Math.Abs(localEnemyLedger - localEnemyMappings) > ENEMY_COUNT_TOLERANCE)
                issues.Add($"enemies: ledger says {localEnemyLedger} applied but only {localEnemyMappings} are mapped");

            int localPickups = ItemDropPatches.ClientLivePickupCount;
            if (System.Math.Abs(localPickups - host.LivePickups) > PICKUP_COUNT_TOLERANCE)
                issues.Add($"pickups: host {host.LivePickups} / local {localPickups}");

            // --- engine enemy count (catches locally-spawned enemies that bypass the ledger) ---
            // Both sides must be >= 0 (a -1 means EnemyManager.Instance was null — not in a run yet).
            var localEnemyMgr = EnemyManager.Instance;
            int localEngineEnemies = localEnemyMgr != null ? localEnemyMgr.GetNumEnemies() : -1;
            if (host.EngineEnemyCount >= 0 && localEngineEnemies >= 0)
            {
                int engineDiff = localEngineEnemies - host.EngineEnemyCount;
                if (System.Math.Abs(engineDiff) > ENGINE_ENEMY_TOLERANCE)
                    issues.Add($"enemies (engine): host {host.EngineEnemyCount} / local {localEngineEnemies} (diff {engineDiff}) - client may be spawning locally");
            }

            // --- per-channel ledgers ---
            int channels = System.Math.Min(host.SentCounters.Length, SyncTelemetry.ChannelCount);
            for (int i = 0; i < channels; i++)
            {
                var channel = (SyncChannel)i;
                int sent = host.SentCounters[i];
                int applied = SyncTelemetry.Applied(channel);

                if (sent == 0)
                    continue;

                if (BidirectionalChannels.Contains(channel))
                {
                    notes.Add($"{channel}: host sent {sent}, local applied {applied} (bidirectional - mismatch expected)");
                    continue;
                }

                if (applied == 0)
                    issues.Add($"{channel}: host sent {sent}, local applied 0 - handler appears to be a no-op");
                else if (sent - applied > CHANNEL_LAG_TOLERANCE)
                    issues.Add($"{channel}: host sent {sent}, local applied {applied} ({sent - applied} never applied)");
                else if (applied - sent > CHANNEL_LAG_TOLERANCE)
                    issues.Add($"{channel}: host sent {sent}, local applied {applied} ({applied - sent} applied twice?)");
            }

            if (issues.Count == 0)
            {
                DebugLogger.Log($"[SyncCheck] #{host.Sequence} t={host.StageTime:F1} OK");
            }
            else
            {
                DebugLogger.Warning($"[SyncCheck] #{host.Sequence} t={host.StageTime:F1} DESYNC ({issues.Count} issue(s))");
                foreach (var issue in issues)
                    DebugLogger.Warning($"[SyncCheck]   {issue}");
            }

            foreach (var note in notes)
                DebugLogger.Log($"[SyncCheck]   note: {note}");
        }

        /// <summary>Reset cross-run state (call on restart).</summary>
        public static void Reset()
        {
            lastSequence = -1;
        }
    }
}
