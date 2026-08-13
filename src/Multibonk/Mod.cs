using MelonLoader;
// "Net" collides with the Multibonk.Net *namespace* itself when used unqualified
// from inside the Multibonk namespace, so alias the facade class explicitly.
using NetFacade = Multibonk.Net.Net;

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
    ///
    /// THREADING MODEL: socket I/O (Multibonk.Net.Connection's read/send loops,
    /// NetServer's accept loop) runs entirely on background Tasks and never touches
    /// Unity/IL2CPP state. Every frame, OnUpdate() first calls Net.PumpReceive(),
    /// which drains queued incoming packet payloads and dispatches them through the
    /// PacketRegistry ON THIS (the main) THREAD — so packet handlers may call into
    /// Unity/IL2CPP directly. It then calls MainThread.Drain(), which runs any other
    /// work background threads marshaled over with MainThread.Enqueue (e.g. connection
    /// lifecycle events). Nothing under Multibonk.Net ever calls into the game from a
    /// background thread; everything funnels through this pump.
    /// </summary>
    public sealed class Mod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Multibonk (rebuild) initializing…");
            // Composition root is wired here as the rebuild progresses:
            //   - networking core (done: Multibonk.Net)
            //   - packet registry (done: Net.Handlers, populated by feature modules)
            //   - feature modules (players, enemies, pickups, …)
            LoggerInstance.Msg("Multibonk initialized.");
        }

        public override void OnUpdate()
        {
            // See the threading-model comment above: both calls must happen on the
            // main thread, in this order, every frame.
            NetFacade.PumpReceive();
            Multibonk.Net.MainThread.Drain();
        }

        public override void OnApplicationQuit()
        {
            NetFacade.StopAll();
        }
    }
}
