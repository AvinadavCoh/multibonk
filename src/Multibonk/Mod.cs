using System;
using MelonLoader;
using Multibonk.Modules;
using UnityEngine;
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
        // ---------------- dev triggers (scaffolding until a real UI exists) ----------------
        // No lobby UI exists yet, so these keybinds drive the session module directly for
        // manual/test-kit testing. Delete once a real host/join/start-game UI lands.
        private const int DevHostPort = 25565;
        private const string DevJoinHost = "127.0.0.1";
        private const int DevJoinPort = 25565;
        private const int DevStartGameSeed = 12345;

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Multibonk (rebuild) initializing…");

            // Composition root: networking core (Multibonk.Net) is already up; feature
            // modules (players, enemies, pickups, ... - session/lobby first) install
            // their packet handlers here.
            ModuleHost.Install();

            PrintDevTriggerLegend();
            LoggerInstance.Msg("Multibonk initialized.");
        }

        public override void OnUpdate()
        {
            // See the threading-model comment above: both calls must happen on the
            // main thread, in this order, every frame.
            NetFacade.PumpReceive();
            Multibonk.Net.MainThread.Drain();
            ModuleHost.TickAll();

            CheckDevTriggers();
        }

        public override void OnApplicationQuit()
        {
            NetFacade.StopAll();
        }

        // ---------------- dev triggers ----------------

        private void CheckDevTriggers()
        {
            try
            {
                if (Input.GetKeyDown(KeyCode.F6))
                {
                    Log.Info($"[DevTrigger] F6 - hosting a session on port {DevHostPort}...");
                    NetFacade.StartHost(DevHostPort);
                    Log.Info($"[DevTrigger] Now hosting on port {DevHostPort}. Run the test kit with: dotnet run -- join --host {DevJoinHost} --port {DevHostPort}");
                }
                else if (Input.GetKeyDown(KeyCode.F7))
                {
                    Log.Info($"[DevTrigger] F7 - joining {DevJoinHost}:{DevJoinPort}...");
                    try
                    {
                        NetFacade.JoinHost(DevJoinHost, DevJoinPort);
                        Log.Info($"[DevTrigger] Connected to {DevJoinHost}:{DevJoinPort}.");
                    }
                    catch (Exception e)
                    {
                        Log.Error($"[DevTrigger] Join failed: {e.Message}");
                    }
                }
                else if (Input.GetKeyDown(KeyCode.F8))
                {
                    Log.Info($"[DevTrigger] F8 - sending START_GAME (seed={DevStartGameSeed}) to all clients...");
                    ModuleHost.Session.SendStartGameToAll(DevStartGameSeed);
                }
            }
            catch (Exception e)
            {
                Log.Error($"[DevTrigger] threw: {e}");
            }
        }

        private void PrintDevTriggerLegend()
        {
            Log.Info("[DevTrigger] Keybind legend (scaffolding until a real UI exists):");
            Log.Info($"[DevTrigger]   F6 = host a session on port {DevHostPort}");
            Log.Info($"[DevTrigger]   F7 = join {DevJoinHost}:{DevJoinPort}");
            Log.Info($"[DevTrigger]   F8 = (host only) send START_GAME to all clients, seed={DevStartGameSeed}");
        }
    }
}
