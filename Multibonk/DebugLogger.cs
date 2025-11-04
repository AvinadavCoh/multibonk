using MelonLoader;
using System;
using System.IO;

namespace Multibonk
{
    /// <summary>
    /// Enhanced logger that writes to both console and file, with spam filtering
    /// </summary>
    public static class DebugLogger
    {
        private static string logFilePath;
        private static StreamWriter logWriter;
        private static DateTime lastMovementLog = DateTime.MinValue;
        private static DateTime lastRotationLog = DateTime.MinValue;
        private static readonly TimeSpan spamFilterDelay = TimeSpan.FromSeconds(1); // Only log movement/rotation once per second

        static DebugLogger()
        {
            // Create logs directory in game folder
            var gameFolder = Directory.GetCurrentDirectory();
            var logsFolder = Path.Combine(gameFolder, "MultibonkLogs");
            Directory.CreateDirectory(logsFolder);

            // Create log file with timestamp
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            logFilePath = Path.Combine(logsFolder, $"Multibonk_{timestamp}.log");

            // Open file for writing
            logWriter = new StreamWriter(logFilePath, append: true) { AutoFlush = true };

            Log("=================================================");
            Log($"Multibonk Multiplayer Mod - Debug Log Started");
            Log($"Time: {DateTime.Now}");
            Log("=================================================");
        }

        /// <summary>
        /// Log general messages (always logged)
        /// </summary>
        public static void Log(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logMessage = $"[{timestamp}] {message}";
            
            MelonLogger.Msg(logMessage);
            logWriter?.WriteLine(logMessage);
        }

        /// <summary>
        /// Log errors (always logged)
        /// </summary>
        public static void Error(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logMessage = $"[{timestamp}] [ERROR] {message}";
            
            MelonLogger.Error(logMessage);
            logWriter?.WriteLine(logMessage);
        }

        /// <summary>
        /// Log warnings (always logged)
        /// </summary>
        public static void Warning(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logMessage = $"[{timestamp}] [WARNING] {message}";
            
            MelonLogger.Warning(logMessage);
            logWriter?.WriteLine(logMessage);
        }

        /// <summary>
        /// Log movement packets (spam filtered - max once per second)
        /// </summary>
        public static void LogMovement(string message)
        {
            var now = DateTime.Now;
            if (now - lastMovementLog > spamFilterDelay)
            {
                Log($"[MOVEMENT] {message}");
                lastMovementLog = now;
            }
            // Still write to file but not console
            else
            {
                var timestamp = now.ToString("HH:mm:ss.fff");
                logWriter?.WriteLine($"[{timestamp}] [MOVEMENT] {message}");
            }
        }

        /// <summary>
        /// Log rotation packets (spam filtered - max once per second)
        /// </summary>
        public static void LogRotation(string message)
        {
            var now = DateTime.Now;
            if (now - lastRotationLog > spamFilterDelay)
            {
                Log($"[ROTATION] {message}");
                lastRotationLog = now;
            }
            // Still write to file but not console
            else
            {
                var timestamp = now.ToString("HH:mm:ss.fff");
                logWriter?.WriteLine($"[{timestamp}] [ROTATION] {message}");
            }
        }

        /// <summary>
        /// Log spawn-related messages (always logged and highlighted)
        /// </summary>
        public static void LogSpawn(string message)
        {
            Log($"[SPAWN] *** {message} ***");
        }

        /// <summary>
        /// Log connection-related messages (always logged and highlighted)
        /// </summary>
        public static void LogConnection(string message)
        {
            Log($"[CONNECTION] *** {message} ***");
        }

        /// <summary>
        /// Close the log file
        /// </summary>
        public static void Close()
        {
            Log("=================================================");
            Log("Debug Log Ended");
            Log("=================================================");
            logWriter?.Close();
            logWriter?.Dispose();
        }
    }
}
