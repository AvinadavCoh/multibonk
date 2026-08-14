using System;
using System.Collections.Concurrent;

namespace Multibonk.Net
{
    /// <summary>
    /// Thin static facade over the networking core. Holds the single active
    /// <see cref="NetServer"/> XOR <see cref="NetClient"/> plus the shared
    /// <see cref="PacketRegistry"/>; the rest of the mod talks to this class, not
    /// to NetServer/NetClient/Connection directly.
    ///
    /// THREADING MODEL:
    ///   - Connection read/send loops and NetServer's accept loop run on background
    ///     Tasks (see those classes). They never touch Unity/IL2CPP state directly.
    ///   - Every incoming payload is queued (Connection.OnMessage -> HandleMessage)
    ///     as it arrives on its background thread. <see cref="PumpReceive"/>, called
    ///     from Mod.OnUpdate every frame, drains that queue ON THE MAIN THREAD and
    ///     dispatches each payload through the PacketRegistry - so every packet
    ///     handler runs on the main thread and may call into Unity/IL2CPP directly.
    ///   - Connection lifecycle events (client connected/disconnected), which fire
    ///     on background accept/read threads, are marshaled to the main thread via
    ///     <see cref="MainThread"/> before this facade re-raises them, so subscribers
    ///     get the same main-thread guarantee. Mod.OnUpdate also calls
    ///     MainThread.Drain() every frame for this (and any other) queued work.
    /// </summary>
    public static class Net
    {
        private static NetServer _server;
        private static NetClient _client;
        private static readonly PacketRegistry Registry = new PacketRegistry();

        private static readonly ConcurrentQueue<(Connection From, byte[] Payload)> Incoming =
            new ConcurrentQueue<(Connection, byte[])>();

        public static bool IsHost => _server != null;

        public static bool InSession =>
            _server != null || (_client != null && _client.Connection != null && _client.Connection.IsConnected);

        /// <summary>The shared id-to-handler table. Feature modules register their packet handlers here.</summary>
        public static PacketRegistry Handlers => Registry;

        /// <summary>Raised on the main thread when a client connects (host) or the host connection completes (join).</summary>
        public static event Action<Connection> OnClientConnected;

        /// <summary>Raised on the main thread when a client disconnects (host), or the host connection is lost (join).</summary>
        public static event Action<Connection> OnClientDisconnected;

        /// <summary>
        /// Raised once, synchronously, whenever <see cref="StartHost"/> or
        /// <see cref="JoinHost"/> successfully begins a new session - true for host,
        /// false for client. Always called from whatever thread invoked StartHost/
        /// JoinHost (both are main-thread-only APIs by convention - see their own docs).
        /// Feature modules (see Multibonk.Modules) subscribe here via ModuleHost to reset
        /// their session-scoped state.
        /// </summary>
        public static event Action<bool> OnSessionStarted;

        /// <summary>
        /// Raised exactly once when a session that was active actually ends: an
        /// explicit <see cref="StopAll"/> (including the implicit one at the start of a
        /// fresh StartHost/JoinHost), or - client role only - the host connection
        /// dropping on its own. Not raised when a single client disconnects from a host
        /// running with other clients still connected; the host's session only ends
        /// when the host itself stops. Guarded by an internal flag so an unexpected
        /// disconnect followed by an explicit StopAll (or vice versa) fires this only
        /// once. May be raised from a background thread when triggered by an
        /// unexpected client-side disconnect (marshaled the same way as
        /// OnClientDisconnected); StartHost/StopAll/JoinHost's own invocations run on
        /// the calling thread.
        /// </summary>
        public static event Action OnSessionEnded;

        // True from a successful StartHost/JoinHost until OnSessionEnded fires. Not the
        // same signal as InSession (which flips false the instant the socket closes,
        // before either the explicit StopAll path or the disconnect-callback path below
        // has had a chance to run) - this flag is what lets both of those paths share
        // one "did I already fire OnSessionEnded" check instead of double-firing it.
        private static bool _sessionActive;

        /// <summary>Starts listening as the host. Stops any existing session first.</summary>
        public static void StartHost(int port)
        {
            StopAll();

            _server = new NetServer();
            _server.OnClientConnected += conn =>
            {
                conn.OnMessage += HandleMessage;
                MainThread.Enqueue(() => OnClientConnected?.Invoke(conn));
            };
            _server.OnClientDisconnected += conn =>
                MainThread.Enqueue(() => OnClientDisconnected?.Invoke(conn));

            _server.Start(port);
            _sessionActive = true;
            OnSessionStarted?.Invoke(true);
        }

        /// <summary>
        /// Connects to a host. Stops any existing session first. Blocks the calling
        /// thread until the TCP handshake completes (or throws) - call it off the
        /// main thread if that would stall a frame.
        /// </summary>
        public static void JoinHost(string host, int port)
        {
            StopAll();

            _client = new NetClient();
            _client.OnConnected += conn =>
            {
                conn.OnMessage += HandleMessage;
                MainThread.Enqueue(() => OnClientConnected?.Invoke(conn));
            };
            _client.OnDisconnected += conn =>
                MainThread.Enqueue(() =>
                {
                    OnClientDisconnected?.Invoke(conn);
                    // Client role only: losing the host connection ends the whole
                    // session, even without an explicit StopAll() call. EndSession()
                    // no-ops if StopAll() already fired this (e.g. the maintainer
                    // pressed F6/F7 in the same frame the socket happened to drop).
                    EndSession();
                });

            _client.ConnectAsync(host, port).GetAwaiter().GetResult();
            _sessionActive = true;
            OnSessionStarted?.Invoke(false);
        }

        /// <summary>Tears down whatever session is active (host or join). Safe to call when idle.</summary>
        public static void StopAll()
        {
            _server?.Stop();
            _server = null;

            _client?.Disconnect();
            _client = null;

            while (Incoming.TryDequeue(out _)) { }

            EndSession();
        }

        private static void EndSession()
        {
            if (!_sessionActive) return;
            _sessionActive = false;
            OnSessionEnded?.Invoke();
        }

        private static void HandleMessage(Connection from, byte[] payload) =>
            Incoming.Enqueue((from, payload));

        /// <summary>
        /// Drains queued incoming payloads and dispatches each through the
        /// PacketRegistry. Must be called from the main thread (Mod.OnUpdate),
        /// every frame - this is what makes packet handlers safe to touch Unity/IL2CPP.
        /// </summary>
        public static void PumpReceive()
        {
            while (Incoming.TryDequeue(out var item))
            {
                var reader = new NetReader(item.Payload);
                byte id = reader.ReadByte();
                Registry.Dispatch(id, reader, item.From);
            }
        }

        /// <summary>Sends to every connected client. No-op if not hosting.</summary>
        public static void Broadcast(IPacket packet) => _server?.Broadcast(packet);

        /// <summary>Sends to every connected client except one. No-op if not hosting.</summary>
        public static void BroadcastExcept(Connection except, IPacket packet) => _server?.BroadcastExcept(except, packet);

        /// <summary>Sends to the host. No-op if not joined to one.</summary>
        public static void SendToHost(IPacket packet) => _client?.Send(packet);

        /// <summary>Sends to a specific connection (host side, targeting one client).</summary>
        public static void Send(Connection conn, IPacket packet) => conn?.Send(packet);
    }
}
