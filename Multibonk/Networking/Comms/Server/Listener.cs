using System.Net;
using System.Net.Sockets;
using Multibonk.Networking.Comms.Base;

namespace Multibonk.Networking.Comms.Server
{
    public class Listener
    {
        private readonly IServerProtocol protocol;

        private TcpListener tcpListener;
        private bool running = false;
        private int port;

        public Listener(int port, IServerProtocol protocol)
        {
            this.port = port;
            this.protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
        }

        public void InternalStart()
        {
            try
            {
                DebugLogger.Log($"Starting server on port {port}...");
                tcpListener = new TcpListener(IPAddress.Any, port);

                tcpListener.Start();
                DebugLogger.Log($"Server started successfully on port {port}");

                protocol.ServerStarted();

                _ = Task.Run(AcceptLoop);
            }
            catch (SocketException ex)
            {
                DebugLogger.Error($"Failed to start server on port {port}");
                DebugLogger.Error($"Error code: {ex.ErrorCode} - {ex.Message}");
                DebugLogger.Error($"Port may already be in use or blocked by firewall");
                throw new Exception($"Server start failed: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Unexpected error starting server: {ex.Message}");
                DebugLogger.Error($"Stack: {ex.StackTrace}");
                throw;
            }
        }

        public void Start()
        {
            if (running) return;
            running = true;

            new Thread(() => {
                try
                {
                    InternalStart();
                }
                catch (Exception ex)
                {
                    DebugLogger.Error($"Server start failed: {ex.Message}");
                    running = false;
                    // Don't crash - just log the error
                }
            }).Start();
        }

        public void Stop()
        {
            if (!running)
                return;

            running = false;
            protocol.ServerClosed();
            tcpListener.Stop();
        }

        private Connection CreateConnection(TcpClient client)
        {
            try
            {
                var connection = new Connection(client);

                protocol.HandleConnect(connection);

                connection.OnMessageReceived += (conn, packet) =>
                {
                    try
                    {
                        protocol.HandleMessage(conn, packet, 0, packet.Length);
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Error($"Error handling message from client: {ex.Message}");
                        DebugLogger.Error($"Stack: {ex.StackTrace}");
                    }
                };

                connection.OnClose += conn =>
                {
                    try
                    {
                        protocol.HandleClose(conn);
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Error($"Error handling client disconnect: {ex.Message}");
                    }
                };

                return connection;
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Error creating connection: {ex.Message}");
                DebugLogger.Error($"Stack: {ex.StackTrace}");
                throw;
            }
        }

        private async Task AcceptLoop()
        {
            while (running)
            {
                try
                {
                    var client = await tcpListener.AcceptTcpClientAsync();
                    DebugLogger.Log($"Client connected from {client.Client.RemoteEndPoint}");
                    
                    var connection = CreateConnection(client);
                    connection.Start();
                }
                catch (ObjectDisposedException)
                {
                    DebugLogger.Log("Server stopped");
                    break;
                }
                catch (Exception ex)
                {
                    if (running) // Only log if we're still supposed to be running
                    {
                        DebugLogger.Error($"Error accepting client: {ex.Message}");
                        DebugLogger.Error($"Stack: {ex.StackTrace}");
                        // Don't break - keep accepting new connections
                    }
                }
            }
        }
    }
}
