using MelonLoader;

namespace Multibonk
{
    /// <summary>
    /// Static wrapper over <see cref="MelonLogger"/>. Everything in the mod logs
    /// through this instead of calling MelonLogger directly, so the logging surface
    /// (prefixing, filtering, etc.) can change in one place later.
    /// </summary>
    public static class Log
    {
        public static void Info(string message) => MelonLogger.Msg(message);

        public static void Warn(string message) => MelonLogger.Warning(message);

        public static void Error(string message) => MelonLogger.Error(message);
    }
}
