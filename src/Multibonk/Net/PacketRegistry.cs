using System;
using System.Collections.Generic;

namespace Multibonk.Net
{
    /// <summary>
    /// Maps a wire packet id to a handler - the whole protocol lives in one table
    /// instead of the legacy mod's hand-wired DI handler lists.
    /// </summary>
    public sealed class PacketRegistry
    {
        public delegate void Handler(NetReader reader, Connection from);

        private readonly Dictionary<byte, Handler> _handlers = new Dictionary<byte, Handler>();

        /// <summary>Registers (or replaces, with a warning) the handler for a packet id.</summary>
        public void Register(byte id, Handler handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (_handlers.ContainsKey(id))
                Log.Warn($"PacketRegistry: overwriting existing handler for id {id}.");
            _handlers[id] = handler;
        }

        /// <summary>
        /// Looks up and invokes the handler for <paramref name="id"/>. Never throws:
        /// an unknown id is logged and skipped, and a handler that throws is caught
        /// and logged so one bad packet can't take down the caller's loop.
        /// </summary>
        public void Dispatch(byte id, NetReader reader, Connection from)
        {
            if (!_handlers.TryGetValue(id, out var handler))
            {
                Log.Warn($"PacketRegistry: no handler for packet id {id}, skipping.");
                return;
            }

            try
            {
                handler(reader, from);
            }
            catch (Exception e)
            {
                Log.Error($"PacketRegistry: handler for id {id} threw: {e}");
            }
        }
    }
}
