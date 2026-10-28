using System;

namespace P2D.Domain
{
    /// <summary>
    /// Weekly reset on Monday 05:00 ICT (= Sunday 22:00 UTC). Always pass server time:
    /// the device clock must never decide a reset.
    /// </summary>
    public static class WeeklyCalendar
    {
        // 4 Jan 2026 is a Sunday
        public static readonly DateTime Epoch = new DateTime(2026, 1, 4, 22, 0, 0, DateTimeKind.Utc);

        public static int WeekIndex(DateTime utcNow) =>
            (int)Math.Floor((utcNow - Epoch).TotalDays / 7.0);

        public static DateTime WeekStartUtc(int weekIndex) => Epoch.AddDays(7.0 * weekIndex);

        /// <summary>Daily reset uses the same 05:00 ICT boundary.</summary>
        public static int DayIndex(DateTime utcNow) => (int)Math.Floor((utcNow - Epoch).TotalDays);

        public static DateTime NextResetUtc(DateTime utcNow) => WeekStartUtc(WeekIndex(utcNow) + 1);

        /// <summary>Fallback only — the live theme comes from Remote Config by week index.</summary>
        public static int FallbackThemeIndex(int weekIndex, int themeCount) =>
            ((weekIndex % themeCount) + themeCount) % themeCount;
    }
}
