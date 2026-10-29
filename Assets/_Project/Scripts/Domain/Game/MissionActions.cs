using System;
using System.Collections.Generic;

namespace P2D.Domain
{
    /// <summary>What a mission counts. Append new kinds at the end (saved as array indices).</summary>
    public enum MissionEvent { StoryClear, Hunt, Enhance, Feed, NestCollect, SpireClear, Summon, DailyClaim }

    public sealed class MissionDef
    {
        public string id;
        /// <summary>English source text; {0} is the target.</summary>
        public string name;
        public MissionEvent kind;
        public int target;
        public bool weekly;
        public int pactSigils;
        public int crystals;
    }

    [Serializable]
    public sealed class MissionSave
    {
        public int dayIndex = int.MinValue;
        public int weekIndex = int.MinValue;

        /// <summary>Counts per <see cref="MissionEvent"/> for today and for this week.</summary>
        public int[] daily = new int[0];
        public int[] weekly = new int[0];
        public List<string> claimedDaily = new List<string>();
        public List<string> claimedWeekly = new List<string>();

        /// <summary>
        /// Events recorded by actions that do not know the server time; <see cref="MissionActions.Sync"/>
        /// adds them to the current day and week.
        /// </summary>
        public int[] pending = new int[0];
    }

    /// <summary>
    /// Daily and weekly missions (docs/GDD.md §8, numbers in Balance §15): short goals that pay pact
    /// sigils and crystals. Daily counts reset at the daily reset (<see cref="WeeklyCalendar.DayIndex"/>),
    /// weekly counts with the spire week. Server time only (pass ServerClock.UtcNow).
    /// </summary>
    public static class MissionActions
    {
        public static readonly IReadOnlyList<MissionDef> Daily = new List<MissionDef>
        {
            new MissionDef { id = "d_story", name = "Clear {0} story stage", kind = MissionEvent.StoryClear, target = 1, crystals = 10 },
            new MissionDef { id = "d_hunt", name = "Hunt {0} times", kind = MissionEvent.Hunt, target = 3, crystals = 10 },
            new MissionDef { id = "d_enhance", name = "Enhance gear {0} times", kind = MissionEvent.Enhance, target = 3, crystals = 10 },
            new MissionDef { id = "d_feed", name = "Feed the dragon {0} time", kind = MissionEvent.Feed, target = 1, crystals = 10 },
            new MissionDef { id = "d_nest", name = "Collect nest income {0} time", kind = MissionEvent.NestCollect, target = 1, crystals = 10 },
            new MissionDef { id = "d_spire", name = "Clear {0} spire floor", kind = MissionEvent.SpireClear, target = 1, crystals = 10 },
            new MissionDef { id = "d_summon", name = "Summon {0} time", kind = MissionEvent.Summon, target = 1, crystals = 10 },
            new MissionDef { id = "d_all", name = "Claim {0} daily missions", kind = MissionEvent.DailyClaim, target = 5, pactSigils = 1, crystals = 20 },
        };

        public static readonly IReadOnlyList<MissionDef> Weekly = new List<MissionDef>
        {
            new MissionDef { id = "w_hunt", name = "Hunt {0} times", kind = MissionEvent.Hunt, target = 40, weekly = true, pactSigils = 1, crystals = 50 },
            new MissionDef { id = "w_enhance", name = "Enhance gear {0} times", kind = MissionEvent.Enhance, target = 20, weekly = true, pactSigils = 1, crystals = 50 },
            new MissionDef { id = "w_spire", name = "Clear {0} spire floors", kind = MissionEvent.SpireClear, target = 10, weekly = true, pactSigils = 1, crystals = 50 },
            new MissionDef { id = "w_summon", name = "Summon {0} times", kind = MissionEvent.Summon, target = 10, weekly = true, pactSigils = 1, crystals = 50 },
            new MissionDef { id = "w_daily", name = "Claim {0} daily missions", kind = MissionEvent.DailyClaim, target = 25, weekly = true, pactSigils = 1, crystals = 100 },
        };

        static readonly int EventCount = Enum.GetValues(typeof(MissionEvent)).Length;

        public static MissionDef Find(string id)
        {
            foreach (var m in Daily) if (m.id == id) return m;
            foreach (var m in Weekly) if (m.id == id) return m;
            return null;
        }

        public static string Title(MissionDef mission) => Loc.F(mission.name, mission.target);

        /// <summary>
        /// Counts an event for a Domain action that has no server time (enhance, summon, battles).
        /// It waits in <see cref="MissionSave.pending"/> until the next <see cref="Sync"/>.
        /// Missions locked by the story count nothing.
        /// </summary>
        public static void Record(PlayerSave save, MissionEvent kind, int count)
        {
            if (count <= 0 || !FeatureUnlocks.IsUnlocked(save, Feature.Missions)) return;
            Missions(save).pending[(int)kind] += count;
        }

        public static void Record(PlayerSave save, MissionEvent kind, int count, DateTime utcNow)
        {
            Record(save, kind, count);
            Sync(save, utcNow);
        }

        /// <summary>Resets the day and week when they rolled over, then adds pending events to both.</summary>
        public static MissionSave Sync(PlayerSave save, DateTime utcNow)
        {
            var m = Missions(save);
            int day = WeeklyCalendar.DayIndex(utcNow);
            if (m.dayIndex != day)
            {
                m.dayIndex = day;
                Array.Clear(m.daily, 0, m.daily.Length);
                m.claimedDaily.Clear();
            }
            int week = WeeklyCalendar.WeekIndex(utcNow);
            if (m.weekIndex != week)
            {
                m.weekIndex = week;
                Array.Clear(m.weekly, 0, m.weekly.Length);
                m.claimedWeekly.Clear();
            }
            for (int i = 0; i < EventCount; i++)
            {
                m.daily[i] += m.pending[i];
                m.weekly[i] += m.pending[i];
                m.pending[i] = 0;
            }
            return m;
        }

        /// <summary>Progress towards the target, capped at the target.</summary>
        public static int Progress(PlayerSave save, MissionDef mission, DateTime utcNow)
        {
            var m = Sync(save, utcNow);
            return Math.Min(mission.target, (mission.weekly ? m.weekly : m.daily)[(int)mission.kind]);
        }

        public static bool IsClaimed(PlayerSave save, MissionDef mission, DateTime utcNow)
        {
            var m = Sync(save, utcNow);
            return (mission.weekly ? m.claimedWeekly : m.claimedDaily).Contains(mission.id);
        }

        public static bool CanClaim(PlayerSave save, MissionDef mission, DateTime utcNow) =>
            FeatureUnlocks.IsUnlocked(save, Feature.Missions) && !IsClaimed(save, mission, utcNow)
            && Progress(save, mission, utcNow) >= mission.target;

        /// <summary>Missions done and not claimed yet (for a badge).</summary>
        public static int Claimable(PlayerSave save, DateTime utcNow)
        {
            int n = 0;
            foreach (var m in Daily) if (CanClaim(save, m, utcNow)) n++;
            foreach (var m in Weekly) if (CanClaim(save, m, utcNow)) n++;
            return n;
        }

        public static ActionResult Claim(PlayerSave save, string missionId, DateTime utcNow)
        {
            if (!FeatureUnlocks.IsUnlocked(save, Feature.Missions)) return FeatureUnlocks.Locked(Feature.Missions);
            var mission = Find(missionId);
            if (mission == null) return ActionResult.Fail(Loc.T("Unknown mission"));
            if (IsClaimed(save, mission, utcNow)) return ActionResult.Fail(Loc.T("Already claimed"));
            int progress = Progress(save, mission, utcNow);
            if (progress < mission.target) return ActionResult.Fail(Loc.F("Not done yet ({0} / {1})", progress, mission.target));

            var m = Missions(save);
            (mission.weekly ? m.claimedWeekly : m.claimedDaily).Add(mission.id);
            save.wallet.pactSigils += mission.pactSigils;
            save.wallet.crystals += mission.crystals;
            if (!mission.weekly) Record(save, MissionEvent.DailyClaim, 1, utcNow);
            return ActionResult.Ok(RewardText(mission));
        }

        public static string RewardText(MissionDef mission) =>
            mission.pactSigils > 0
                ? Loc.F("+{0} pact sigils, +{1} crystals", mission.pactSigils, mission.crystals)
                : Loc.F("+{0} crystals", mission.crystals);

        /// <summary>The mission state, with arrays sized for every event (older saves and new events).</summary>
        static MissionSave Missions(PlayerSave save)
        {
            if (save.missions == null) save.missions = new MissionSave();
            var m = save.missions;
            if (m.daily == null || m.daily.Length < EventCount) m.daily = Resize(m.daily);
            if (m.weekly == null || m.weekly.Length < EventCount) m.weekly = Resize(m.weekly);
            if (m.pending == null || m.pending.Length < EventCount) m.pending = Resize(m.pending);
            if (m.claimedDaily == null) m.claimedDaily = new List<string>();
            if (m.claimedWeekly == null) m.claimedWeekly = new List<string>();
            return m;
        }

        static int[] Resize(int[] counts)
        {
            var sized = new int[EventCount];
            if (counts != null) Array.Copy(counts, sized, Math.Min(counts.Length, EventCount));
            return sized;
        }
    }
}
