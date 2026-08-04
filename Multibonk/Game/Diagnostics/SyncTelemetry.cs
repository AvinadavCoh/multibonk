using System;

namespace Multibonk.Game.Diagnostics
{
    /// <summary>
    /// Subsystems tracked by the desync detector.
    ///
    /// APPEND-ONLY: the ordinal values are written on the wire in StateDigestPacket.
    /// Never reorder or remove entries, or a host and client on different builds will
    /// silently compare unrelated counters.
    /// </summary>
    public enum SyncChannel
    {
        EnemySpawn = 0,
        EnemyDeath = 1,
        PickupSpawn = 2,
        PickupDespawn = 3,
        TimelineEvent = 4,
        MapReveal = 5,
        XpGain = 6,
        GoldGain = 7,
        FinalSwarm = 8,
    }

    /// <summary>
    /// A snapshot of the host's authoritative state, shipped to clients periodically.
    /// </summary>
    public struct SyncDigest
    {
        public ushort Sequence;
        public float StageTime;
        public float RunTime;
        public int Gold;
        public int Level;
        public int LiveEnemies;
        public int LivePickups;
        public int[] SentCounters;
    }

    /// <summary>
    /// Per-subsystem ledgers backing the desync detector.
    ///
    /// The host counts what it <i>sent</i> on each channel; the client counts what it
    /// actually <i>applied</i> to the game world. The host ships its counters in a
    /// StateDigestPacket every few seconds and the client diffs them against its own,
    /// so a dropped, ignored or double-applied packet shows up as a number in the log
    /// instead of a hunch after a play session.
    ///
    /// A channel whose count climbs on the host and stays flat on the client is a
    /// handler that receives packets and does nothing - which is the exact bug class
    /// this was built to surface (see the stub handlers listed in TODO.md).
    ///
    /// Counting only - never let this throw or block the calling path.
    /// </summary>
    public static class SyncTelemetry
    {
        public static readonly int ChannelCount = Enum.GetValues(typeof(SyncChannel)).Length;

        private static readonly int[] sent = new int[ChannelCount];
        private static readonly int[] applied = new int[ChannelCount];

        /// <summary>Host: a packet went out on this channel.</summary>
        public static void RecordSent(SyncChannel channel)
        {
            sent[(int)channel]++;
        }

        /// <summary>
        /// Client: a packet on this channel was applied to the game world.
        /// Call this only after the game call actually succeeded - recording it on
        /// receipt would hide exactly the failures we are looking for.
        /// </summary>
        public static void RecordApplied(SyncChannel channel)
        {
            applied[(int)channel]++;
        }

        public static int Sent(SyncChannel channel) => sent[(int)channel];

        public static int Applied(SyncChannel channel) => applied[(int)channel];

        public static int[] SnapshotSent() => (int[])sent.Clone();

        public static int[] SnapshotApplied() => (int[])applied.Clone();

        /// <summary>
        /// Net live enemies according to this side's own ledger (spawns minus deaths).
        /// Deliberately independent of the game's own enemy list so the two can be
        /// compared against each other.
        /// </summary>
        public static int LedgerLiveEnemies(bool hosting)
        {
            return hosting
                ? sent[(int)SyncChannel.EnemySpawn] - sent[(int)SyncChannel.EnemyDeath]
                : applied[(int)SyncChannel.EnemySpawn] - applied[(int)SyncChannel.EnemyDeath];
        }

        /// <summary>Clear all counters. Call on run restart / scene load.</summary>
        public static void Reset()
        {
            Array.Clear(sent, 0, sent.Length);
            Array.Clear(applied, 0, applied.Length);
        }

        public static string ChannelName(int index)
        {
            return index >= 0 && index < ChannelCount
                ? ((SyncChannel)index).ToString()
                : $"Channel{index}";
        }
    }
}
