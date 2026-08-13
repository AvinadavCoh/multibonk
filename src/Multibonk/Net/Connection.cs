using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Multibonk.Net
{
    /// <summary>
    /// Wraps a single TCP socket with length-prefixed framing.
    ///
    /// WIRE FORMAT - byte-exact match to the legacy mod's Connection
    /// (Multibonk/Networking/Comms/Base/Connection.cs):
    ///   - HEADER_LENGTH = 2 bytes: a little-endian signed 16-bit length prefix
    ///     (BitConverter.GetBytes((short)payload.Length) / BitConverter.ToInt16),
    ///     covering ONLY the payload that follows (packet id byte + fields) -
    ///     the header itself is not counted.
    ///   - MAX_PACKET_SIZE = 1024 bytes total (header + payload), so payload must
    ///     be &lt;= 1022 bytes. A length outside (0, 1022] closes the connection.
    ///   - No trailer or checksum; framing is length-prefix only.
    ///
    /// THREADING: Start() launches a background read Task and a background send
    /// Task. OnMessage / OnClosed fire on those background threads - callers must
    /// NOT touch Unity/IL2CPP state directly from these handlers; marshal via
    /// <see cref="MainThread"/> (the <see cref="Net"/> facade does this for the
    /// event types it re-publishes).
    /// </summary>
    public sealed class Connection
    {
        private const int HeaderLength = 2;
        private const int MaxPacketSize = 1024;
        private const int MaxPayloadSize = MaxPacketSize - HeaderLength;

        private static int _nextId;

        private readonly TcpClient _client;
        private NetworkStream _stream;
        private readonly byte[] _headerBuf = new byte[HeaderLength];
        private readonly byte[] _bodyBuf = new byte[MaxPayloadSize];

        private readonly ConcurrentQueue<byte[]> _outgoing = new ConcurrentQueue<byte[]>();
        private readonly SemaphoreSlim _outgoingSignal = new SemaphoreSlim(0);

        private int _closed; // 0 = open, 1 = closed - guarded with Interlocked so Close() is idempotent.

        /// <summary>Stable per-process id, assigned on construction.</summary>
        public int Id { get; }

        /// <summary>Higher layers stamp the associated player's UUID here once known (0 = unassigned).</summary>
        public ushort PlayerUuid { get; set; }

        /// <summary>Raised on a background thread with each decoded payload (packet id byte + fields).</summary>
        public event Action<Connection, byte[]> OnMessage;

        /// <summary>Raised on a background thread exactly once when the connection ends, for any reason.</summary>
        public event Action<Connection> OnClosed;

        public bool IsConnected => _client?.Connected ?? false;

        public Connection(TcpClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            Id = Interlocked.Increment(ref _nextId);
        }

        /// <summary>Starts the background read/send loops. Call once, after construction.</summary>
        public void Start()
        {
            try
            {
                _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                _stream = _client.GetStream();
            }
            catch (Exception e)
            {
                Log.Error($"Connection {Id}: failed to start ({e.Message})");
                Close();
                return;
            }

            _ = Task.Run(ReadLoop);
            _ = Task.Run(SendLoop);
        }

        /// <summary>Frames (id byte + fields) and enqueues a packet for the send loop. Thread-safe.</summary>
        public void Send(IPacket packet)
        {
            if (packet == null) throw new ArgumentNullException(nameof(packet));

            var w = new NetWriter();
            w.WriteByte(packet.Id);
            packet.Write(w);
            byte[] payload = w.ToArray();

            if (payload.Length > MaxPayloadSize)
            {
                Log.Error($"Connection {Id}: dropped packet id {packet.Id}, payload {payload.Length} bytes exceeds max {MaxPayloadSize}.");
                return;
            }

            _outgoing.Enqueue(payload);
            _outgoingSignal.Release();
        }

        private async Task ReadLoop()
        {
            try
            {
                while (_client.Connected)
                {
                    await ReadExact(_headerBuf, 0, HeaderLength).ConfigureAwait(false);
                    short len = BitConverter.ToInt16(_headerBuf, 0);

                    if (len <= 0 || len > MaxPayloadSize)
                        throw new IOException($"Invalid packet length {len}");

                    await ReadExact(_bodyBuf, 0, len).ConfigureAwait(false);

                    byte[] payload = new byte[len];
                    Array.Copy(_bodyBuf, 0, payload, 0, len);

                    try { OnMessage?.Invoke(this, payload); }
                    catch (Exception handlerEx)
                    {
                        // A bad decode/handler must never kill the read loop.
                        Log.Error($"Connection {Id}: OnMessage handler threw: {handlerEx}");
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn($"Connection {Id}: read loop ended ({e.Message})");
            }
            finally
            {
                Close();
            }
        }

        private async Task ReadExact(byte[] buffer, int offset, int count)
        {
            int total = 0;
            while (total < count)
            {
                int n = await _stream.ReadAsync(buffer, offset + total, count - total).ConfigureAwait(false);
                if (n == 0) throw new EndOfStreamException("Connection closed by remote host (received 0 bytes).");
                total += n;
            }
        }

        private async Task SendLoop()
        {
            try
            {
                while (Volatile.Read(ref _closed) == 0)
                {
                    await _outgoingSignal.WaitAsync().ConfigureAwait(false);

                    while (_outgoing.TryDequeue(out var payload))
                    {
                        if (Volatile.Read(ref _closed) != 0 || !_client.Connected) break;
                        await InternalSend(payload).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn($"Connection {Id}: send loop ended ({e.Message})");
            }
            finally
            {
                Close();
            }
        }

        private async Task InternalSend(byte[] payload)
        {
            byte[] frame = new byte[payload.Length + HeaderLength];
            Array.Copy(BitConverter.GetBytes((short)payload.Length), 0, frame, 0, HeaderLength);
            Array.Copy(payload, 0, frame, HeaderLength, payload.Length);
            await _stream.WriteAsync(frame, 0, frame.Length).ConfigureAwait(false);
        }

        /// <summary>Idempotent. Closes the socket and raises OnClosed exactly once, on the calling thread.</summary>
        public void Close()
        {
            if (Interlocked.Exchange(ref _closed, 1) != 0) return;

            try { _outgoingSignal.Release(); } catch { /* wakes SendLoop so it can exit; best effort */ }
            try { _stream?.Close(); } catch { /* best effort */ }
            try { _client?.Close(); } catch { /* best effort */ }

            try { OnClosed?.Invoke(this); }
            catch (Exception e) { Log.Error($"Connection {Id}: OnClosed handler threw: {e}"); }
        }
    }
}
