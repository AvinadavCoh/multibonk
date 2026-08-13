using MelonLoader;

[assembly: MelonInfo(typeof(Multibonk.Mod), "Multibonk", "0.1.0", "AvinadavCoh")]
[assembly: MelonGame("Megabonk", "Megabonk")]

namespace Multibonk
{
    /// <summary>
    /// Entry point for the clean rebuild.
    ///
    /// Deliberately thin: it owns the mod lifecycle and pumps the per-frame loop.
    /// Game hooks live in feature modules as [HarmonyPatch] classes with TYPED
    /// Il2Cpp references — MelonLoader auto-applies them once at load. We never call
    /// Harmony.PatchAll() (doing so on top of MelonLoader's auto-apply was the
    /// double-patch bug in the old codebase).
    /// </summary>
    public sealed class Mod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Multibonk (rebuild) initializing…");
            // Composition root is wired here as the rebuild progresses:
            //   - networking core
            //   - packet registry
            //   - feature modules (players, enemies, pickups, …)
            LoggerInstance.Msg("Multibonk initialized.");
        }
    }
}
