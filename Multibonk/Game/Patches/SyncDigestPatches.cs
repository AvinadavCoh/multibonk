using HarmonyLib;
using Il2CppAssets.Scripts.Actors.Player;
using Il2CppAssets.Scripts.Utility;
using MelonLoader;
using Multibonk.Game.Diagnostics;
using Multibonk.Networking.Lobby;

namespace Multibonk.Game.Patches
{
    /// <summary>
    /// Host: emits a periodic state digest for the desync detector.
    ///
    /// This exists because a two-player session currently produces no evidence - things
    /// either "feel wrong" or don't. The digest turns one play session into a readable
    /// diff of which subsystems drifted and when.
    ///
    /// Diagnostics only: nothing here may alter gameplay, and every read is guarded so a
    /// missing game object degrades the digest instead of breaking the run.
    /// </summary>
    public static class SyncDigestPatches
    {
        private const float DIGEST_INTERVAL = 5f;

        private static float lastDigestSent = float.MinValue;
        private static ushort sequence = 0;

        /// <summary>Reset on run restart so counters line up with a fresh run.</summary>
        public static void Reset()
        {
            lastDigestSent = float.MinValue;
            sequence = 0;
            SyncTelemetry.Reset();
        }

        [HarmonyPatch(typeof(MyTime), nameof(MyTime.Update))]
        class MyTimeDigestPatch
        {
            static void Postfix()
            {
                if (!LobbyPatchFlags.InMultiplayer || !LobbyPatchFlags.IsHosting)
                    return;

                try
                {
                    float now = UnityEngine.Time.unscaledTime;
                    if (now - lastDigestSent < DIGEST_INTERVAL)
                        return;

                    lastDigestSent = now;

                    int gold = 0;
                    int level = 0;
                    var inventory = MyPlayer.Instance?.inventory;
                    if (inventory != null)
                    {
                        gold = inventory.goldInt;
                        level = inventory.GetCharacterLevel();
                    }

                    var digest = new SyncDigest
                    {
                        Sequence = sequence++,
                        StageTime = MyTime.stageTimer,
                        RunTime = MyTime.runTimer,
                        Gold = gold,
                        Level = level,
                        LiveEnemies = SyncTelemetry.LedgerLiveEnemies(hosting: true),
                        LivePickups = ItemDropPatches.HostLivePickupCount,
                        SentCounters = SyncTelemetry.SnapshotSent(),
                    };

                    GameEvents.TriggerStateDigest(digest);
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"[SyncCheck] Failed to build state digest: {ex.Message}");
                }
            }
        }
    }
}
