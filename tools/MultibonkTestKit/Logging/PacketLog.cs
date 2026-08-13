using System.Text;
using Multibonk.Networking.Comms.Base;

namespace MultibonkTestKit.Logging
{
    /// <summary>
    /// Console + file logger with a running per-packet-type tally, used to produce the
    /// end-of-session summary (including the "never received" list).
    /// </summary>
    public sealed class PacketLog : IDisposable
    {
        private readonly StreamWriter _file;
        private readonly Dictionary<byte, long> _recvCounts = new();
        private readonly Dictionary<byte, long> _sentCounts = new();
        private readonly object _lock = new();

        public string LogFilePath { get; }

        public PacketLog()
        {
            LogFilePath = Path.Combine(Directory.GetCurrentDirectory(), $"testkit-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            _file = new StreamWriter(LogFilePath, append: false) { AutoFlush = true };
            Write("=== MultibonkTestKit session started ===");
        }

        private void Write(string line)
        {
            lock (_lock)
            {
                Console.WriteLine(line);
                _file.WriteLine(line);
            }
        }

        private static string Ts() => DateTime.Now.ToString("HH:mm:ss.fff");

        public void Info(string message) => Write($"{Ts()} [INFO] {message}");

        public void Warn(string message) => Write($"{Ts()} [WARN] {message}");

        public void Error(string message) => Write($"{Ts()} [ERROR] {message}");

        public void Sent(byte id, string name, string details = null)
        {
            lock (_lock)
            {
                _sentCounts.TryGetValue(id, out var c);
                _sentCounts[id] = c + 1;
            }
            Write($"{Ts()} [SEND] id={id,-3} {name}{(details != null ? " " + details : "")}");
        }

        /// <summary>
        /// Logs a decoded (or hex-fallback) received packet and bumps its tally.
        /// </summary>
        public void Received(byte id, string name, string decoded, byte[] raw)
        {
            lock (_lock)
            {
                _recvCounts.TryGetValue(id, out var c);
                _recvCounts[id] = c + 1;
            }

            if (decoded != null)
            {
                Write($"{Ts()} [RECV] id={id,-3} {name,-28} {decoded}");
            }
            else
            {
                string hex = HexPreview(raw, 32);
                Write($"{Ts()} [RECV] id={id,-3} {name,-28} len={raw.Length} hex={hex}");
            }
        }

        private static string HexPreview(byte[] raw, int maxBytes)
        {
            var sb = new StringBuilder();
            int n = Math.Min(raw.Length, maxBytes);
            for (int i = 0; i < n; i++)
                sb.Append(raw[i].ToString("X2"));
            if (raw.Length > maxBytes) sb.Append("...");
            return sb.ToString();
        }

        /// <summary>
        /// Prints the end-of-session summary.
        /// <paramref name="asHost"/> = false (default, `join` mode): this process is the
        /// fake CLIENT, so it SENT ClientSentPacketId packets and RECEIVED ServerSentPacketId
        /// packets - this is the original behavior, unchanged.
        /// <paramref name="asHost"/> = true (`host` mode): this process is the fake HOST, so
        /// the roles are swapped - it SENT ServerSentPacketId packets and RECEIVED
        /// ClientSentPacketId packets. The "never received" list then means "these
        /// client-&gt;host packet types were never seen this session", which is the
        /// direct evidence that the client-&gt;host direction of the wire actually works.
        /// </summary>
        public void PrintSummary(bool asHost = false)
        {
            Write("");
            Write("==================== SESSION SUMMARY ====================");
            Write("");

            if (!asHost)
            {
                Write("-- Packets received from host (ServerSentPacketId) --");
                Write($"{"ID",4}  {"NAME",-30}  {"COUNT",8}");

                var neverReceived = new List<string>();
                foreach (ServerSentPacketId val in Enum.GetValues(typeof(ServerSentPacketId)))
                {
                    byte id = (byte)val;
                    long count = _recvCounts.TryGetValue(id, out var c) ? c : 0;
                    Write($"{id,4}  {val,-30}  {count,8}");
                    if (count == 0) neverReceived.Add(val.ToString());
                }

                foreach (var kv in _recvCounts.OrderBy(k => k.Key))
                {
                    if (!Enum.IsDefined(typeof(ServerSentPacketId), kv.Key))
                        Write($"{kv.Key,4}  {"<UNKNOWN ID - possible protocol drift>",-30}  {kv.Value,8}");
                }

                Write("");
                Write("-- Packets sent to host (ClientSentPacketId) --");
                Write($"{"ID",4}  {"NAME",-30}  {"COUNT",8}");
                foreach (ClientSentPacketId val in Enum.GetValues(typeof(ClientSentPacketId)))
                {
                    byte id = (byte)val;
                    long count = _sentCounts.TryGetValue(id, out var c) ? c : 0;
                    Write($"{id,4}  {val,-30}  {count,8}");
                }

                Write("");
                Write("-- NEVER RECEIVED (host never broadcast these this session) --");
                if (neverReceived.Count == 0)
                {
                    Write("  (none - every known ServerSentPacketId was observed at least once)");
                }
                else
                {
                    foreach (var name in neverReceived)
                        Write($"  {name}: never received");
                }
            }
            else
            {
                Write("-- Packets sent to client (ServerSentPacketId) --");
                Write($"{"ID",4}  {"NAME",-30}  {"COUNT",8}");
                foreach (ServerSentPacketId val in Enum.GetValues(typeof(ServerSentPacketId)))
                {
                    byte id = (byte)val;
                    long count = _sentCounts.TryGetValue(id, out var c) ? c : 0;
                    Write($"{id,4}  {val,-30}  {count,8}");
                }

                Write("");
                Write("-- Packets received from client (ClientSentPacketId) --");
                Write($"{"ID",4}  {"NAME",-30}  {"COUNT",8}");

                var neverReceived = new List<string>();
                foreach (ClientSentPacketId val in Enum.GetValues(typeof(ClientSentPacketId)))
                {
                    byte id = (byte)val;
                    long count = _recvCounts.TryGetValue(id, out var c) ? c : 0;
                    Write($"{id,4}  {val,-30}  {count,8}");
                    if (count == 0) neverReceived.Add(val.ToString());
                }

                foreach (var kv in _recvCounts.OrderBy(k => k.Key))
                {
                    if (!Enum.IsDefined(typeof(ClientSentPacketId), kv.Key))
                        Write($"{kv.Key,4}  {"<UNKNOWN ID - possible protocol drift>",-30}  {kv.Value,8}");
                }

                Write("");
                Write("-- NEVER RECEIVED (client never sent these this session - client->host direction unverified for these) --");
                if (neverReceived.Count == 0)
                {
                    Write("  (none - every known ClientSentPacketId was observed at least once)");
                }
                else
                {
                    foreach (var name in neverReceived)
                        Write($"  {name}: never received");
                }
            }

            Write("");
            Write($"Full log written to: {LogFilePath}");
            Write("===========================================================");
        }

        public void Dispose()
        {
            try { _file.Flush(); _file.Dispose(); } catch { }
        }
    }
}
