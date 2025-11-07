using System.Net.Sockets;
using Multibonk.Networking.Comms.Base;

namespace Multibonk.Networking.Comms.Client
{
    public class NetworkClient
    {
        private TcpClient tcpClient;
        private Connection connection;
        private IClientProtocol protocol; 


        public bool IsConnected => tcpClient?.Connected ?? false;

        public NetworkClient(IClientProtocol protocol)
        {
            tcpClient = new TcpClient();
            this.protocol = protocol;

            connection = new Connection(tcpClient);
        } 

        private void InternalConnect(string ip, int port)
        {
            try
            {
                DebugLogger.Log($"Attempting to connect to {ip}:{port}...");
                tcpClient.Connect(ip, port);
                DebugLogger.Log($"TCP connection established!");

                connection.Start();

                protocol.OnConnect(connection);
                connection.OnMessageReceived += (conn, packet) =>
                {
                    try
                    {
                        protocol.HandleMessage(conn, packet, 0, packet.Length);
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Error($"Error handling message: {ex.Message}");
                        DebugLogger.Error($"Stack: {ex.StackTrace}");
                    }
                };
                connection.OnClose += conn =>
                {
                    try
                    {
                        protocol.OnDisconnect(conn);
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Error($"Error during disconnect: {ex.Message}");
                    }
                };
            }
            catch (SocketException ex)
            {
                DebugLogger.Error($"Network error connecting to {ip}:{port}");
                DebugLogger.Error($"Error code: {ex.ErrorCode} - {ex.Message}");
                DebugLogger.Error($"Make sure the host has started the server and is reachable");
                throw new Exception($"Failed to connect: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Unexpected error connecting to {ip}:{port}");
                DebugLogger.Error($"Error: {ex.Message}");
                DebugLogger.Error($"Stack: {ex.StackTrace}");
                throw;
            }
        }
        public void Connect(string ip, int port)
        {
            if (IsConnected) throw new InvalidOperationException("Client already connected.");

            new Thread(() => {
                try
                {
                    InternalConnect(ip, port);
                }
                catch (Exception ex)
                {
                    DebugLogger.Error($"Connection failed: {ex.Message}");
                    // Don't crash - just log the error
                }
            }).Start();
        }

        public Connection GetConnection()
        {
            return connection;
        }

        public void Disconnect()
        {
            connection?.Close();
            tcpClient = new TcpClient();
            connection = null;
        }

    }
}
