using HarmonyLib;
using Il2CppAssets.Scripts.Game.Spawning.New;
using MelonLoader;
using Multibonk.Networking.Lobby;

namespace Multibonk.Game.Patches
{
    /// <summary>
    /// Synchronizes stage timeline events between players.
    ///
    /// Megabonk does not use classic numbered waves. Progression is driven by
    /// SummonerController (held by EnemyManager.Instance.summonerController), which ticks a
    /// StageTimeline and fires TimelineEvents (swarms, minibosses) via StartEvent(eventIndex),
    /// plus StartFinalSwarm() at the end of the stage.
    ///
    /// Sync model (host-authoritative):
    /// - Host: StartEvent / StartFinalSwarm run normally; a postfix broadcasts them.
    ///   (WaveStartPacket.WaveNumber carries the timeline event index;
    ///    WaveCompletePacket signals the final swarm.)
    /// - Client: locally ticked timeline events are blocked by a prefix, and are instead
    ///   triggered by WaveStart/WaveComplete packets from the host. This keeps swarm and
    ///   miniboss timing identical for everyone even if stage clocks drift.
    /// </summary>
    public static class WaveProgressionPatches
    {
        /// <summary>
        /// When true, allows a timeline event triggered by a network packet to run on a client,
        /// bypassing the local block. Set by the Wave packet handlers around the replayed call.
        /// </summary>
        public static bool AllowNetworkEvent = false;

        /// <summary>
        /// Host: SummonerController.StartEvent(eventIndex) fired -> broadcast to clients.
        /// Client: block locally ticked events (host packet will trigger them instead).
        /// </summary>
        [HarmonyPatch(typeof(SummonerController), nameof(SummonerController.StartEvent))]
        class StartEventPatch
        {
            static bool Prefix(int eventIndex)
            {
                if (LobbyPatchFlags.InMultiplayer && !LobbyPatchFlags.IsHosting && !AllowNetworkEvent)
                {
                    DebugLogger.Log($"[Client] Blocked local timeline event {eventIndex} (waiting for host packet)");
                    return false;
                }
                return true;
            }

            static void Postfix(int eventIndex)
            {
                if (!LobbyPatchFlags.InMultiplayer || !LobbyPatchFlags.IsHosting)
                    return;

                // Don't rebroadcast events that came from the network (host never sets the flag,
                // but guard anyway in case of future host migration).
                if (AllowNetworkEvent)
                    return;

                MelonLogger.Msg($"[Host] Timeline event {eventIndex} started, broadcasting...");
                GameEvents.TriggerWaveStart(eventIndex);
            }
        }

        /// <summary>
        /// Host: StartFinalSwarm fired -> broadcast (sent as WaveComplete packet).
        /// Client: block local final swarm trigger.
        /// </summary>
        [HarmonyPatch(typeof(SummonerController), nameof(SummonerController.StartFinalSwarm))]
        class StartFinalSwarmPatch
        {
            static bool Prefix()
            {
                if (LobbyPatchFlags.InMultiplayer && !LobbyPatchFlags.IsHosting && !AllowNetworkEvent)
                {
                    DebugLogger.Log("[Client] Blocked local final swarm (waiting for host packet)");
                    return false;
                }
                return true;
            }

            static void Postfix()
            {
                if (!LobbyPatchFlags.InMultiplayer || !LobbyPatchFlags.IsHosting || AllowNetworkEvent)
                    return;

                MelonLogger.Msg("[Host] Final swarm started, broadcasting...");
                GameEvents.TriggerWaveComplete(0);
            }
        }

        /// <summary>
        /// Helper used by the client packet handlers to locate the live SummonerController.
        /// </summary>
        public static SummonerController GetSummonerController()
        {
            try
            {
                var enemyManager = Il2CppAssets.Scripts.Managers.EnemyManager.Instance;
                return enemyManager?.summonerController;
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"Could not get SummonerController: {ex.Message}");
                return null;
            }
        }
    }
}
