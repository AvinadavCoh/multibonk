using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using Multibonk.Networking.Lobby;
using UnityEngine;

namespace Multibonk.Game.Patches
{
    /// <summary>
    /// Forces all players to generate identical maps.
    ///
    /// Megabonk generates each stage procedurally. The mod already shares a run seed
    /// (GamePatchFlags.Seed, carried by StartGamePacket), but until now nothing applied it,
    /// so host and client played on different layouts (different shrines, chests, etc.).
    ///
    /// The generators expose developer seed hooks which we set before generation runs:
    /// - Il2Cpp.MapGenerationController (overworld): testSeed / mapSeed fields
    /// - Il2Cpp.RsgController (crypt/dungeon): static SetCustomSeed(int)
    /// We also seed UnityEngine.Random right before generation so any direct Random.Range
    /// calls inside the generators line up.
    ///
    /// The seed varies per stage (seed + stageIndex * prime) so stages don't repeat layouts.
    /// </summary>
    public static class MapSeedPatches
    {
        /// <summary>
        /// Per-stage deterministic seed derived from the shared run seed.
        /// </summary>
        public static int EffectiveSeed()
        {
            int stage = 0;
            try
            {
                stage = Il2CppAssets.Scripts.Managers.MapController.GetStageIndex();
            }
            catch { /* not in a run yet */ }

            unchecked
            {
                return GamePatchFlags.Seed + stage * 7919;
            }
        }

        private static void ApplyToMapGenerator(MapGenerationController instance, string source)
        {
            if (!LobbyPatchFlags.InMultiplayer || instance == null)
                return;

            try
            {
                int seed = EffectiveSeed();
                instance.testSeed = seed;
                MapGenerationController.mapSeed = seed; // static in v1.0.69
                DebugLogger.Log($"[MapSeed] ({source}) Applied shared map seed {seed} (run seed {GamePatchFlags.Seed})");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[MapSeed] Failed to apply seed in {source}: {ex.Message}");
            }
        }

        /// <summary>
        /// Overworld generator: set the seed as soon as the controller wakes up.
        /// </summary>
        [HarmonyPatch(typeof(MapGenerationController), "Awake")]
        class MapGenAwakePatch
        {
            static void Postfix(MapGenerationController __instance)
            {
                ApplyToMapGenerator(__instance, "Awake");
            }
        }

        /// <summary>
        /// Overworld generator: set it again right before generation starts.
        /// (GenerateMap is an iterator - this prefix runs before any generation logic.)
        /// Also seeds the global RNG so direct Random.Range calls match across players.
        /// </summary>
        [HarmonyPatch(typeof(MapGenerationController), "GenerateMap")]
        class MapGenGeneratePatch
        {
            static void Prefix(MapGenerationController __instance)
            {
                if (!LobbyPatchFlags.InMultiplayer)
                    return;

                ApplyToMapGenerator(__instance, "GenerateMap");

                try
                {
                    UnityEngine.Random.InitState(EffectiveSeed());
                    DebugLogger.Log($"[MapSeed] Random.InitState({EffectiveSeed()}) before overworld generation");
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"[MapSeed] InitState failed: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Crypt/dungeon generator: use the developer custom-seed hook.
        /// </summary>
        [HarmonyPatch(typeof(RsgController), "Awake")]
        class RsgAwakePatch
        {
            static void Prefix()
            {
                ApplyCryptSeed("RsgController.Awake");
            }
        }

        [HarmonyPatch(typeof(RsgController), nameof(RsgController.Generate))]
        class RsgGeneratePatch
        {
            static void Prefix()
            {
                if (!LobbyPatchFlags.InMultiplayer)
                    return;

                ApplyCryptSeed("RsgController.Generate");

                try
                {
                    UnityEngine.Random.InitState(EffectiveSeed());
                }
                catch { }
            }
        }

        private static void ApplyCryptSeed(string source)
        {
            if (!LobbyPatchFlags.InMultiplayer)
                return;

            try
            {
                int seed = EffectiveSeed();
                RsgController.SetCustomSeed(seed);
                DebugLogger.Log($"[MapSeed] ({source}) Applied shared crypt seed {seed}");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[MapSeed] Failed to apply crypt seed in {source}: {ex.Message}");
            }
        }

        // ------------------------------------------------------------------
        // Deterministic placement re-seeding.
        //
        // Shrines, chests, trees and props are placed with UnityEngine.Random.
        // Even with the same map seed, the global RNG state diverges between
        // host and client (the host rolls RNG for enemy spawning, clients don't).
        // Re-seeding with a fixed salt at the START of each placement routine
        // guarantees both sides run the identical roll sequence inside it.
        // ------------------------------------------------------------------

        private static void Reseed(int salt, string label)
        {
            if (!LobbyPatchFlags.InMultiplayer)
                return;

            try
            {
                int seed = EffectiveSeed() ^ salt;
                UnityEngine.Random.InitState(seed);
                DebugLogger.Log($"[MapSeed] Reseeded RNG for {label} -> {seed}");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[MapSeed] Reseed failed for {label}: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(SpawnInteractables), nameof(SpawnInteractables.SpawnChests))]
        class SpawnChestsPatch { static void Prefix() => Reseed(0x1A2B3C, "SpawnChests"); }

        [HarmonyPatch(typeof(SpawnInteractables), nameof(SpawnInteractables.SpawnShrines))]
        class SpawnShrinesPatch { static void Prefix() => Reseed(0x2B3C4D, "SpawnShrines"); }

        [HarmonyPatch(typeof(SpawnInteractables), nameof(SpawnInteractables.SpawnShit))]
        class SpawnShitPatch { static void Prefix() => Reseed(0x3C4D5E, "SpawnShit"); }

        [HarmonyPatch(typeof(SpawnInteractables), "SpawnOther")]
        class SpawnOtherPatch { static void Prefix() => Reseed(0x4D5E6F, "SpawnOther"); }

        [HarmonyPatch(typeof(SpawnInteractables), "SpawnRails")]
        class SpawnRailsPatch { static void Prefix() => Reseed(0x5E6F70, "SpawnRails"); }

        [HarmonyPatch(typeof(RandomObjectPlacer), nameof(RandomObjectPlacer.GenerateInteractables))]
        class GenerateInteractablesPatch { static void Prefix() => Reseed(0x6F7081, "RandomObjectPlacer.GenerateInteractables"); }

        [HarmonyPatch(typeof(RandomObjectPlacer), nameof(RandomObjectPlacer.Generate))]
        class RandomObjectGeneratePatch { static void Prefix() => Reseed(0x708192, "RandomObjectPlacer.Generate"); }
    }
}
