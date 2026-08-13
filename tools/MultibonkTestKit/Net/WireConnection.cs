using System.Net.Sockets;

namespace MultibonkTestKit.Net
{
    /// <summary>
    /// Byte-exact reimplementation of the wire framing used by
    /// Multibonk.Networking.Comms.Base.Connection (Multibonk/Networking/Comms/Base/Connection.cs).
    /// That file can't be linked directly because it references MelonLoader.
    ///
    /// Framing (verified from Connection.cs):
    ///   - HEADER_LENGTH = 2 bytes: a little-endian signed 16-bit length prefix
    ///     (BitConverter.GetBytes((short)data.Length) / BitConverter.ToInt16),
    ///     written on the machine's native endianness (little-endian on Windows/x64,
    ///     which is what both the game and this tool run on).
    ///   - The length prefix covers ONLY the payload that follows (the packet-id byte
    ///     plus its fields) - it does NOT include the 2 header bytes themselves.
    ///   - MAX_PACKET_SIZE = 1024 total (header + payload), so payload must be
    ///     <= 1022 bytes. The reader rejects any length that is <= 0 or > 1022.
    ///   - There is no trailer/checksum; framing is length-prefix only.
    /// </summary>
    public sealed class WireConnection : IDisposable
    {
        public const int HEADER_LENGTH = 2;
        public const int MAX_PACKET_SIZE = 1024;
        public const int MAX_PAYLOAD_SIZE = MAX_PACKET_SIZE - HEADER_LENGTH;

        private readonly TcpClient _client;
        private NetworkStream _stream;
        private readonly byte[] _headerBuf = new byte[HEADER_LENGTH];
        private readonly byte[] _bodyBuf = new byte[MAX_PACKET_SIZE];

        /// <summary>Raised on the read loop's background task for every decoded payload (id byte + fields).</summary>
        public event Action<byte[]> OnPacket;

        /// <summary>Raised once when the read loop ends, with the causing exception (null = graceful).</summary>
        public event Action<Exception> OnClosed;

        public bool IsConnected => _client?.Connected ?? false;

        public WireConnection()
        {
            _client = new TcpClient();
        }

        /// <summary>
        /// Wraps a socket already accepted by a <see cref="System.Net.Sockets.TcpListener"/>
        /// (used by host mode - the tool is the server side, the real game is the client).
        /// </summary>
        public WireConnection(TcpClient acceptedClient)
        {
            _client = acceptedClient ?? throw new ArgumentNullException(nameof(acceptedClient));
            _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            _stream = _client.GetStream();
        }

        public async Task ConnectAsync(string host, int port)
        {
            await _client.ConnectAsync(host, port).ConfigureAwait(false);
            _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            _stream = _client.GetStream();
        }

        public void StartReadLoop()
        {
            _ = Task.Run(ReadLoopAsync);
        }

        private async Task ReadLoopAsync()
        {
            Exception failure = null;
            try
            {
                while (_client.Connected)
                {
                    await ReadExactAsync(_headerBuf, 0, HEADER_LENGTH).ConfigureAwait(false);
                    short len = BitConverter.ToInt16(_headerBuf, 0);

                    if (len <= 0 || len > MAX_PAYLOAD_SIZE)
                        throw new IOException($"Invalid packet length {len}");

                    await ReadExactAsync(_bodyBuf, 0, len).ConfigureAwait(false);

                    var payload = new byte[len];
                    Array.Copy(_bodyBuf, 0, payload, 0, len);

                    try { OnPacket?.Invoke(payload); }
                    catch (Exception handlerEx)
                    {
                        // Never let a bad decode kill the read loop.
                        Console.WriteLine($"[WireConnection] Packet handler threw: {handlerEx}");
                    }
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                OnClosed?.Invoke(failure);
            }
        }

        private async Task ReadExactAsync(byte[] buffer, int offset, int count)
        {
            int total = 0;
            while (total < count)
            {
                int n = await _stream.ReadAsync(buffer.AsMemory(offset + total, count - total)).ConfigureAwait(false);
                if (n == 0) throw new EndOfStreamException("Connection closed by remote host (received 0 bytes).");
                total += n;
            }
        }

        private readonly SemaphoreSlim _sendLock = new(1, 1);

        /// <summary>Sends a raw payload (packet-id byte + fields), applying the 2-byte length-prefix framing.</summary>
        public async Task SendAsync(byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (payload.Length + HEADER_LENGTH > MAX_PACKET_SIZE)
                throw new ArgumentException($"Packet too large, max {MAX_PAYLOAD_SIZE} bytes, got {payload.Length}");

            var frame = new byte[payload.Length + HEADER_LENGTH];
            Array.Copy(BitConverter.GetBytes((short)payload.Length), 0, frame, 0, HEADER_LENGTH);
            Array.Copy(payload, 0, frame, HEADER_LENGTH, payload.Length);

            await _sendLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await _stream.WriteAsync(frame).ConfigureAwait(false);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        /// <summary>
        /// Abrupt disconnect for testing the disconnect soft-lock fix: forces a TCP RST
        /// instead of a graceful FIN close (LingerState(true,0) + hard Close), so the
        /// host sees the same kind of ungraceful drop as a crashed/alt-tabbed game client.
        /// </summary>
        public void HardDisconnect()
        {
            try
            {
                _client.Client.LingerState = new LingerOption(true, 0);
                _client.Client.Close(0);
            }
            catch { /* best effort */ }
        }

        public void Dispose()
        {
            try { _stream?.Close(); } catch { }
            try { _client?.Close(); } catch { }
        }
    }
}
