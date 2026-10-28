using System;
using UnityEngine;

namespace P2D.Services
{
    /// <summary>
    /// Server-anchored UTC time. Call <see cref="Sync"/> with the backend's time after login;
    /// until then it falls back to the device clock (dev/offline only). Counting forward uses
    /// realtimeSinceStartup, so changing the phone's clock doesn't move game time.
    /// </summary>
    public static class ServerClock
    {
        static DateTime anchorUtc;
        static double anchorRealtime;

        public static bool IsSynced { get; private set; }

        public static void Sync(DateTime serverUtc)
        {
            anchorUtc = DateTime.SpecifyKind(serverUtc, DateTimeKind.Utc);
            anchorRealtime = Time.realtimeSinceStartupAsDouble;
            IsSynced = true;
        }

        public static DateTime UtcNow
        {
            get
            {
                if (!IsSynced) return DateTime.UtcNow;
                return anchorUtc.AddSeconds(Time.realtimeSinceStartupAsDouble - anchorRealtime);
            }
        }
    }
}
