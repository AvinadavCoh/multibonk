using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Multibonk.Net
{
    /// <summary>
    /// Connects to a host's <see cref="NetServer"/> and owns the resulting
    /// single <see cref="Connection"/>.
    ///
    /// THREADING: ConnectAsync performs the TCP handshake asynchronously but does
    /// not itself touch game state. OnConnected / OnDisconnected fire on
    /// background threads (see Connection) - the <see cref="Net"/> facade
    /// marshals them via <see cref="MainThread"/>.
    /// </summary>
    public sealed class NetClient
    {
        public Connection Connection { get; private set; }

        public event Action<Connection> OnConnected;
        public event Action<Connection> OnDisconnected;

        public async Task ConnectAsync(string host, int port)
        {
            var tcp = new TcpClient();
            await tcp.ConnectAsync(host, port).ConfigureAwait(false);

            var conn = new Connection(tcp);
            Connection = conn;
            conn.OnClosed += HandleClosed;
            conn.Start();

            try { OnConnected?.Invoke(conn); }
            catch (Exception e) { Log.Error($"NetClient: OnConnected handler threw: {e}"); }
        }

        private void HandleClosed(Connection conn)
        {
            try { OnDisconnected?.Invoke(conn); }
            catch (Exception e) { Log.Error($"NetClient: OnDisconnected handler threw: {e}"); }
        }

        public void Send(IPacket packet) => Connection?.Send(packet);

        public void Disconnect() => Connection?.Close();
    }
}
