using Multibonk.Net;

namespace Multibonk.Modules
{
    /// <summary>
    /// One self-contained feature module (session/lobby, players, enemies, ...). Each
    /// module owns its own packets and in-memory state behind this common lifecycle.
    ///
    /// Harmony patches are NOT applied here - they live in separate [HarmonyPatch]
    /// classes that MelonLoader auto-applies at load. A module only wires packet
    /// handlers and resets its own state around a session; it never calls
    /// Harmony.PatchAll() itself (that on top of MelonLoader's auto-apply was the
    /// double-patch bug in the legacy codebase).
    /// </summary>
    public interface IModule
    {
        /// <summary>
        /// Called once, at mod load, before any session exists. Register this module's
        /// packet handlers on <paramref name="registry"/> here (and do any other
        /// non-Harmony one-time setup). Must not throw - an exception here is caught
        /// and logged by the composition root, not left to crash the loader.
        /// </summary>
        void Install(PacketRegistry registry);

        /// <summary>
        /// Called every time a new session begins (host starts listening, or a join
        /// completes), after any previous session has already been torn down via
        /// <see cref="OnSessionEnd"/>. <paramref name="isHost"/> is true when this mod
        /// instance is acting as the host for the new session, false when it is the client.
        /// </summary>
        void OnSessionStart(bool isHost);

        /// <summary>
        /// Called once a session ends, for any reason (explicit stop, lost connection,
        /// or immediately before a new session replaces this one). Modules should reset
        /// all session-scoped state here (player registries, in-flight counters, ...).
        /// </summary>
        void OnSessionEnd();

        /// <summary>
        /// Called once per frame from Mod.OnUpdate (via ModuleHost.TickAll), after
        /// Net.PumpReceive/MainThread.Drain have run - so this is always on the main
        /// thread, safe to touch Unity/IL2CPP state, and sees state already updated by
        /// this frame's incoming packets. Default no-op; a module only overrides this if
        /// it has genuine per-frame work (e.g. reading a live game transform and sending
        /// throttled network updates), unlike Install/OnSessionStart/OnSessionEnd which
        /// every module implements.
        /// </summary>
        void Tick() { }
    }
}
