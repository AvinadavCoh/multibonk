using System;
using System.Collections.Concurrent;

namespace Multibonk.Net
{
    /// <summary>
    /// Static main-thread dispatcher. IL2CPP/Unity calls are only safe on the main
    /// (game) thread; code running on a background thread (Connection's read/send
    /// loops, NetServer's accept loop) that needs to touch game state must
    /// <see cref="Enqueue"/> an Action here instead of calling it directly.
    /// <c>Mod.OnUpdate()</c> calls <see cref="Drain"/> every frame.
    /// </summary>
    public static class MainThread
    {
        private static readonly ConcurrentQueue<Action> Queue = new ConcurrentQueue<Action>();

        /// <summary>Queues an action to run on a future Drain() call. Safe to call from any thread.</summary>
        public static void Enqueue(Action action)
        {
            if (action == null) return;
            Queue.Enqueue(action);
        }

        /// <summary>
        /// Runs every action queued as of the start of this call. Must be called from
        /// the main thread. Each action is wrapped in try/catch so one bad action can't
        /// kill the pump; actions enqueued *during* this drain (e.g. a handler
        /// scheduling follow-up work) run on the next Drain() rather than this one, so
        /// a self-re-enqueueing action can't spin this call forever.
        /// </summary>
        public static void Drain()
        {
            int count = Queue.Count;
            for (int i = 0; i < count; i++)
            {
                if (!Queue.TryDequeue(out var action)) break;

                try { action(); }
                catch (Exception e) { Log.Error($"MainThread: queued action threw: {e}"); }
            }
        }
    }
}
