using System;
using System.Collections.Generic;

namespace P2D.Domain
{
    /// <summary>
    /// One free single pull per server day (GDD §2 daily loop: "อัญเชิญฟรี 1 ครั้ง", Balance §6).
    /// It counts toward pity like any pull. Server time only.
    /// </summary>
    public static class FreeSummon
    {
        public static bool IsAvailable(PlayerSave save, DateTime utcNow) =>
            FeatureUnlocks.IsUnlocked(save, Feature.Riders) && save.summonPity.freeSummonDay != WeeklyCalendar.DayIndex(utcNow);

        /// <summary>When the next free pull opens (the daily reset, same hour as the weekly one).</summary>
        public static DateTime NextFreeUtc(DateTime utcNow) => WeeklyCalendar.Epoch.AddDays(WeeklyCalendar.DayIndex(utcNow) + 1);

        public static List<SummonOutcome> Pull(PlayerSave save, DateTime utcNow, IRandom random)
        {
            if (!IsAvailable(save, utcNow)) return new List<SummonOutcome>();
            save.summonPity.freeSummonDay = WeeklyCalendar.DayIndex(utcNow);
            save.wallet.pactSigils += 1; // the free pull pays its own sigil
            return GameActions.Summon(save, 1, random);
        }
    }
}
