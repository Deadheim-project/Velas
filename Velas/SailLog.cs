namespace Velas
{
    /// <summary>Tiny wrapper so call sites read "SailLog.Debug(...)" instead of repeating the
    /// DebugMode check everywhere. Info/Warning/Error always print (they are rare and useful
    /// to admins); Debug is gated behind SailConfig.DebugMode and is where the noisy
    /// step-by-step tracing from section 17 of the spec lives.</summary>
    internal static class SailLog
    {
        public static void Debug(string message)
        {
            if (SailConfig.DebugMode != null && SailConfig.DebugMode.Value)
                Plugin.Log.LogInfo("[Sails] " + message);
        }

        public static void Info(string message) => Plugin.Log.LogInfo("[Sails] " + message);
        public static void Warn(string message) => Plugin.Log.LogWarning("[Sails] " + message);
        public static void Error(string message) => Plugin.Log.LogError("[Sails] " + message);
    }
}
