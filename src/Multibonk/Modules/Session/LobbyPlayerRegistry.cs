using System.Collections.Generic;
using System.Linq;
using Multibonk.Net;

namespace Multibonk.Modules.Session
{
    /// <summary>
    /// Thread-safe registry of the current session's lobby roster. Packet handlers all
    /// run on the main thread (see the Net facade's threading model), so the lock here
    /// is mostly defensive against a future caller reading the roster off-thread.
    /// </summary>
    public sealed class LobbyPlayerRegistry
    {
        private readonly object _lock = new object();
        private readonly Dictionary<ushort, LobbyPlayer> _players = new Dictionary<ushort, LobbyPlayer>();

        public void Add(LobbyPlayer player)
        {
            lock (_lock) _players[player.Uuid] = player;
        }

        public LobbyPlayer Get(ushort uuid)
        {
            lock (_lock) return _players.TryGetValue(uuid, out var p) ? p : null;
        }

        public LobbyPlayer GetByConnection(Connection conn)
        {
            lock (_lock) return _players.Values.FirstOrDefault(p => ReferenceEquals(p.Connection, conn));
        }

        public List<LobbyPlayer> All()
        {
            lock (_lock) return _players.Values.ToList();
        }

        public void Clear()
        {
            lock (_lock) _players.Clear();
        }
    }
}
