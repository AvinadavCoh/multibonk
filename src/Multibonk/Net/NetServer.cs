using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Multibonk.Net
{
    /// <summary>
    /// Listens for incoming connections and tracks them. All framing/read/send
    /// threading is delegated to <see cref="Connection"/>; this class only owns
    /// the accept loop and the live connection set.
    ///
    /// THREADING: the accept loop runs on a background Task. OnClientConnected /
    /// OnClientDisconnected fire on background threads (the accept loop, or a
    /// Connection's own read loop) - do not touch Unity/IL2CPP state from these
    /// handlers directly. The <see cref="Net"/> facade marshals them via
    /// <see cref="MainThread"/>.
    /// </summary>
    public sealed class NetServer
    {
        private TcpListener _listener;
        private readonly ConcurrentDictionary<int, Connection> _connections = new ConcurrentDictionary<int, Connection>();
        private volatile bool _running;

        public event Action<Connection> OnClientConnected;
        public event Action<Connection> OnClientDisconnected;

        public bool IsRunning => _running;

        public IEnumerable<Connection> Connections => _connections.Values;

        public void Start(int port)
        {
            if (_running) return;

            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            _running = true;

            _ = Task.Run(AcceptLoop);
            Log.Info($"NetServer listening on port {port}.");
        }

        public void Stop()
        {
            if (!_running) return;
            _running = false;

            try { _listener?.Stop(); } catch { /* best effort */ }

            foreach (var conn in _connections.Values)
                conn.Close();
            _connections.Clear();
        }

        private async Task AcceptLoop()
        {
            while (_running)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    if (_running) Log.Warn($"NetServer: accept loop ended ({e.Message})");
                    break;
                }

                var conn = new Connection(client);
                _connections[conn.Id] = conn;
                conn.OnClosed += HandleConnectionClosed;
                conn.Start();

                try { OnClientConnected?.Invoke(conn); }
                catch (Exception e) { Log.Error($"NetServer: OnClientConnected handler threw: {e}"); }
            }
        }

        private void HandleConnectionClosed(Connection conn)
        {
            _connections.TryRemove(conn.Id, out _);
            try { OnClientDisconnected?.Invoke(conn); }
            catch (Exception e) { Log.Error($"NetServer: OnClientDisconnected handler threw: {e}"); }
        }

        /// <summary>Sends a packet to every currently connected client.</summary>
        public void Broadcast(IPacket packet)
        {
            foreach (var conn in _connections.Values)
                conn.Send(packet);
        }

        /// <summary>Sends a packet to every currently connected client except one.</summary>
        public void BroadcastExcept(Connection except, IPacket packet)
        {
            foreach (var conn in _connections.Values)
            {
                if (ReferenceEquals(conn, except)) continue;
                conn.Send(packet);
            }
        }
    }
}
