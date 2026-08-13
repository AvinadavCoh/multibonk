using System.Net;
using System.Net.Sockets;

namespace MultibonkTestKit.Net
{
    /// <summary>
    /// Plain TCP accept loop for host mode - the tool acts as the server side
    /// (mirrors Multibonk.Networking.Comms.Server.Listener, which can't be linked
    /// directly because it references MelonLoader's DebugLogger). Never throws out
    /// of the accept loop; a bad accept is logged via <see cref="OnAcceptError"/> and
    /// the loop keeps going so the tool never needs to be restarted because of a
    /// single flaky connection attempt.
    /// </summary>
    public sealed class WireListener : IDisposable
    {
        private readonly TcpListener _listener;
        private CancellationTokenSource _cts;

        /// <summary>Raised on the accept loop's background task for every accepted client.</summary>
        public event Action<WireConnection> OnClientConnected;

        /// <summary>Raised if an individual accept attempt throws (loop keeps running).</summary>
        public event Action<Exception> OnAcceptError;

        public WireListener(int port)
        {
            _listener = new TcpListener(IPAddress.Any, port);
        }

        public void Start()
        {
            _listener.Start();
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => AcceptLoopAsync(_cts.Token));
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    break; // listener was stopped/disposed - normal shutdown
                }
                catch (Exception ex)
                {
                    if (!ct.IsCancellationRequested)
                        OnAcceptError?.Invoke(ex);
                    continue;
                }

                try
                {
                    var conn = new WireConnection(client);
                    OnClientConnected?.Invoke(conn);
                }
                catch (Exception ex)
                {
                    OnAcceptError?.Invoke(ex);
                    try { client.Close(); } catch { }
                }
            }
        }

        public void Dispose()
        {
            try { _cts?.Cancel(); } catch { }
            try { _listener.Stop(); } catch { }
        }
    }
}
