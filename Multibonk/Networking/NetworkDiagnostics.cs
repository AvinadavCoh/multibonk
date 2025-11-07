using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Multibonk.Networking
{
    public static class NetworkDiagnostics
    {
        /// <summary>
        /// Get the local IP address of this computer
        /// </summary>
        public static string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
                return "127.0.0.1";
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Failed to get local IP: {ex.Message}");
                return "127.0.0.1";
            }
        }

        /// <summary>
        /// Test if a host is reachable using ping
        /// </summary>
        public static bool PingHost(string host, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                // Remove port if present
                if (host.Contains(":"))
                {
                    host = host.Split(':')[0];
                }

                DebugLogger.Log($"Pinging {host}...");
                
                using (Ping ping = new Ping())
                {
                    PingReply reply = ping.Send(host, 3000); // 3 second timeout
                    
                    if (reply.Status == IPStatus.Success)
                    {
                        DebugLogger.Log($"Ping successful! Round trip: {reply.RoundtripTime}ms");
                        return true;
                    }
                    else
                    {
                        errorMessage = $"Ping failed: {reply.Status}";
                        DebugLogger.Warning(errorMessage);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Ping error: {ex.Message}";
                DebugLogger.Error(errorMessage);
                return false;
            }
        }

        /// <summary>
        /// Test if a specific port is open on a host
        /// </summary>
        public static bool TestConnection(string host, int port, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                DebugLogger.Log($"Testing connection to {host}:{port}...");
                
                using (TcpClient client = new TcpClient())
                {
                    var result = client.BeginConnect(host, port, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(3));
                    
                    if (success)
                    {
                        try
                        {
                            client.EndConnect(result);
                            DebugLogger.Log($"Connection test successful to {host}:{port}");
                            return true;
                        }
                        catch (Exception ex)
                        {
                            errorMessage = $"Connection failed: {ex.Message}";
                            DebugLogger.Warning(errorMessage);
                            return false;
                        }
                    }
                    else
                    {
                        errorMessage = "Connection timeout - host not responding";
                        DebugLogger.Warning(errorMessage);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Connection error: {ex.Message}";
                DebugLogger.Error(errorMessage);
                return false;
            }
        }

        /// <summary>
        /// Run full diagnostics and log results
        /// </summary>
        public static string RunDiagnostics(string targetHost, int targetPort)
        {
            DebugLogger.Log("=== NETWORK DIAGNOSTICS ===");
            
            // Get local IP
            var localIP = GetLocalIPAddress();
            DebugLogger.Log($"Your IP: {localIP}");
            
            // Test ping
            string pingError;
            bool pingOk = PingHost(targetHost, out pingError);
            
            // Test port
            string portError;
            bool portOk = TestConnection(targetHost, targetPort, out portError);
            
            DebugLogger.Log("=== DIAGNOSTICS COMPLETE ===");
            
            if (pingOk && portOk)
            {
                return "✓ Connection OK";
            }
            else if (pingOk && !portOk)
            {
                return $"✗ Host reachable but port {targetPort} blocked/closed";
            }
            else if (!pingOk && !portOk)
            {
                return "✗ Host not reachable - check IP or firewall";
            }
            else
            {
                return "⚠ Unexpected network state";
            }
        }
    }
}
