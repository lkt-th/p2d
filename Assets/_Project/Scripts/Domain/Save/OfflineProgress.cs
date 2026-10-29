using System;

namespace P2D.Domain
{
    public struct OfflineReport
    {
        public int staminaGained;
        public int torchesGained;
        public bool weekRolledOver;
        public int weeklyChestFloor;
    }

    /// <summary>
    /// Brings a loaded save up to <paramref name="utcNow"/>: hunt stamina, daily torches and
    /// the weekly spire reset. Pass server time; in production the weekly reset itself is
    /// confirmed by Cloud Code and this keeps the offline/dev client consistent with it.
    /// </summary>
    public static class OfflineProgress
    {
        public static OfflineReport Apply(PlayerSave save, DateTime utcNow)
        {
            var report = new OfflineReport();
            ApplyStamina(save.hunt, utcNow, ref report);
            ApplyTorches(save.spire, utcNow, ref report);
            ApplyWeeklyReset(save, utcNow, ref report);
            return report;
        }

        /// <summary>Regenerates first, then spends one hunt's stamina. Returns false if there isn't enough.</summary>
        public static bool SpendHuntStamina(HuntSave hunt, DateTime utcNow)
        {
            var ignored = new OfflineReport();
            ApplyStamina(hunt, utcNow, ref ignored);
            if (hunt.stamina < HuntRules.StaminaCost) return false;
            // Regeneration starts counting from the moment the bar drops below max.
            if (hunt.stamina >= HuntRules.MaxStamina) hunt.staminaUpdatedUtcTicks = utcNow.Ticks;
            hunt.stamina -= HuntRules.StaminaCost;
            return true;
        }

        static void ApplyStamina(HuntSave hunt, DateTime utcNow, ref OfflineReport report)
        {
            long elapsedTicks = utcNow.Ticks - hunt.staminaUpdatedUtcTicks;
            if (elapsedTicks <= 0) return;

            int minutes = (int)Math.Min(int.MaxValue, elapsedTicks / TimeSpan.TicksPerMinute);
            int points = minutes / HuntRules.MinutesPerStamina;
            if (hunt.stamina >= HuntRules.MaxStamina)
            {
                hunt.staminaUpdatedUtcTicks = utcNow.Ticks;
                return;
            }

            int before = hunt.stamina;
            hunt.stamina = Math.Min(HuntRules.MaxStamina, hunt.stamina + points);
            report.staminaGained = hunt.stamina - before;

            // Keep the partial interval so regeneration doesn't lose minutes between sessions.
            hunt.staminaUpdatedUtcTicks = hunt.stamina >= HuntRules.MaxStamina
                ? utcNow.Ticks
                : hunt.staminaUpdatedUtcTicks + points * HuntRules.MinutesPerStamina * TimeSpan.TicksPerMinute;
        }

        static void ApplyTorches(SpireSave spire, DateTime utcNow, ref OfflineReport report)
        {
            int today = WeeklyCalendar.DayIndex(utcNow);
            if (spire.lastTorchDay == int.MinValue) spire.lastTorchDay = today;
            int days = today - spire.lastTorchDay;
            if (days <= 0) return;

            int before = spire.torches;
            spire.torches = SpireRules.RefillTorches(spire.torches, days);
            spire.lastTorchDay = today;
            report.torchesGained = spire.torches - before;
        }

        static void ApplyWeeklyReset(PlayerSave save, DateTime utcNow, ref OfflineReport report)
        {
            var spire = save.spire;
            int week = WeeklyCalendar.WeekIndex(utcNow);
            if (spire.weekIndex == week) return;

            bool playedLastWeek = spire.weekIndex == week - 1;
            int best = playedLastWeek ? spire.highestFloor : 0;

            spire.pendingChestFloor = playedLastWeek ? spire.highestFloor : 0;
            spire.lastWeekBest = best;
            spire.highestFloor = SpireRules.CatchUpStartFloor(best);
            spire.catchUpFloors = spire.highestFloor; // their first-clear materials wait on the spire screen
            spire.bracket = SpireRules.BracketForStage(save.dragon.stage);
            spire.bossClaims = new int[3];
            spire.weekIndex = week;

            report.weekRolledOver = true;
            report.weeklyChestFloor = spire.pendingChestFloor;
        }
    }
}
