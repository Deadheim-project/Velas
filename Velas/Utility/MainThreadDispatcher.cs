using System;
using System.Collections.Concurrent;

namespace Velas.Utility
{
    /// <summary>Bridges async/background work (HTTP downloads, hashing) back to Unity's main
    /// thread. Anything that touches a Texture2D, Material or GameObject must run through
    /// here -- Unity's API is not thread-safe and will throw or silently corrupt state if
    /// called off the main thread. Plugin.Update() drains this every frame.</summary>
    internal static class MainThreadDispatcher
    {
        private static readonly ConcurrentQueue<Action> Queue = new ConcurrentQueue<Action>();

        public static void Enqueue(Action action)
        {
            if (action != null) Queue.Enqueue(action);
        }

        /// <summary>Drains a bounded number of queued actions per call so a burst of
        /// downloads finishing at once cannot spike a single frame.</summary>
        public static void Pump(int maxPerTick = 8)
        {
            int n = 0;
            while (n < maxPerTick && Queue.TryDequeue(out var action))
            {
                n++;
                try { action(); }
                catch (Exception e) { SailLog.Warn($"main-thread task threw: {e.Message}"); }
            }
        }
    }
}
