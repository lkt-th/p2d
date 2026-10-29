using System;
using NUnit.Framework;

namespace P2D.Domain.Tests
{
    public class MissionTests
    {
        // Thursday 10:00 UTC; the daily reset is 22:00 UTC (05:00 UTC+7), the weekly one Sunday 22:00 UTC.
        static readonly DateTime Now = new DateTime(2026, 10, 29, 10, 0, 0, DateTimeKind.Utc);

        static PlayerSave NewGame(int cleared = -1)
        {
            var save = PlayerSave.CreateNew("p", "Ignis", Now, "tim");
            save.story.cleared = cleared < 0 ? StoryActions.TotalStages : cleared; // every feature open by default
            return save;
        }

        static MissionDef Mission(string id) => MissionActions.Find(id);

        [Test]
        public void Record_CountsTowardDailyAndWeekly()
        {
            var save = NewGame();
            MissionActions.Record(save, MissionEvent.Hunt, 2, Now);
            MissionActions.Record(save, MissionEvent.Hunt, 5); // no clock: waits until the next sync
            Assert.AreEqual(3, MissionActions.Progress(save, Mission("d_hunt"), Now), "capped at the target");
            Assert.AreEqual(7, MissionActions.Progress(save, Mission("w_hunt"), Now));
            Assert.AreEqual(0, MissionActions.Progress(save, Mission("d_enhance"), Now));
        }

        [Test]
        public void ActionSites_RecordEvents()
        {
            var save = NewGame();
            GameActions.EnsureStarterKit(save, new SeededRandom(1));
            save.wallet.dragonEssence = 10;
            save.wallet.gold = 1000000;
            save.wallet.forgeStones = 1000;
            Assert.IsTrue(GameActions.FeedEssence(save, 1).ok);
            Assert.IsTrue(GameActions.Enhance(save, save.equipment[0].uid, new SeededRandom(1)).ok);
            HuntActions.ApplyResult(save, 1, EncounterKind.Normal, BattleOutcome.Defeat, new SeededRandom(1));
            Assert.IsTrue(NestActions.Collect(save, Now.AddHours(1)).ok);

            Assert.AreEqual(1, MissionActions.Progress(save, Mission("d_feed"), Now.AddHours(1)));
            Assert.AreEqual(1, MissionActions.Progress(save, Mission("d_enhance"), Now.AddHours(1)));
            Assert.AreEqual(1, MissionActions.Progress(save, Mission("d_hunt"), Now.AddHours(1)), "every finished run counts");
            Assert.AreEqual(1, MissionActions.Progress(save, Mission("d_nest"), Now.AddHours(1)));
        }

        [Test]
        public void DailyResets_AtTheDailyReset_WeeklyKeeps()
        {
            var save = NewGame();
            MissionActions.Record(save, MissionEvent.Hunt, 3, Now);
            Assert.IsTrue(MissionActions.Claim(save, "d_hunt", Now).ok);

            var nextDay = Now.Date.AddHours(22); // Thursday 22:00 UTC
            Assert.AreEqual(3, MissionActions.Progress(save, Mission("d_hunt"), nextDay.AddMinutes(-1)));
            Assert.AreEqual(0, MissionActions.Progress(save, Mission("d_hunt"), nextDay));
            Assert.IsFalse(MissionActions.IsClaimed(save, Mission("d_hunt"), nextDay));
            Assert.AreEqual(3, MissionActions.Progress(save, Mission("w_hunt"), nextDay));
        }

        [Test]
        public void WeeklyResets_WithTheSpireWeek()
        {
            var save = NewGame();
            MissionActions.Record(save, MissionEvent.Summon, 10, Now);
            Assert.IsTrue(MissionActions.Claim(save, "w_summon", Now).ok);

            var reset = WeeklyCalendar.NextResetUtc(Now);
            Assert.AreEqual(10, MissionActions.Progress(save, Mission("w_summon"), reset.AddMinutes(-1)));
            Assert.AreEqual(0, MissionActions.Progress(save, Mission("w_summon"), reset));
            Assert.IsFalse(MissionActions.IsClaimed(save, Mission("w_summon"), reset));
        }

        [Test]
        public void Claim_PaysOnce_AndOnlyWhenDone()
        {
            var save = NewGame();
            long crystals = save.wallet.crystals;
            Assert.IsFalse(MissionActions.Claim(save, "d_feed", Now).ok, "not done");
            Assert.IsFalse(MissionActions.Claim(save, "nope", Now).ok);

            MissionActions.Record(save, MissionEvent.Feed, 1, Now);
            Assert.IsTrue(MissionActions.Claim(save, "d_feed", Now).ok);
            Assert.AreEqual(crystals + 10, save.wallet.crystals);
            Assert.IsFalse(MissionActions.Claim(save, "d_feed", Now).ok, "claimed already");
            Assert.AreEqual(crystals + 10, save.wallet.crystals);
        }

        [Test]
        public void DailyClaims_FillTheAllDailiesMission()
        {
            var save = NewGame();
            int sigils = save.wallet.pactSigils;
            foreach (var kind in new[] { MissionEvent.StoryClear, MissionEvent.Hunt, MissionEvent.Enhance, MissionEvent.Feed, MissionEvent.NestCollect })
                MissionActions.Record(save, kind, 3, Now);
            foreach (var id in new[] { "d_story", "d_hunt", "d_enhance", "d_feed" })
                Assert.IsTrue(MissionActions.Claim(save, id, Now).ok, id);
            Assert.IsFalse(MissionActions.Claim(save, "d_all", Now).ok, "4 of 5");
            Assert.IsTrue(MissionActions.Claim(save, "d_nest", Now).ok);
            Assert.IsTrue(MissionActions.Claim(save, "d_all", Now).ok);
            Assert.AreEqual(sigils + 1, save.wallet.pactSigils);
            Assert.AreEqual(6, MissionActions.Progress(save, Mission("w_daily"), Now));
        }

        [Test]
        public void Missions_LockedBefore1_3()
        {
            var save = NewGame(cleared: 2);
            Assert.AreEqual(3, FeatureUnlocks.UnlockStage(Feature.Missions));
            MissionActions.Record(save, MissionEvent.Feed, 1, Now);
            Assert.AreEqual(0, MissionActions.Progress(save, Mission("d_feed"), Now), "nothing counts while locked");
            Assert.IsFalse(MissionActions.Claim(save, "d_feed", Now).ok);

            save.story.cleared = 3;
            MissionActions.Record(save, MissionEvent.Feed, 1, Now);
            Assert.IsTrue(MissionActions.Claim(save, "d_feed", Now).ok);
        }

        [Test]
        public void Missions_SurviveJsonRoundTrip()
        {
            var save = NewGame();
            MissionActions.Record(save, MissionEvent.Hunt, 2, Now);
            var json = UnityEngine.JsonUtility.FromJson<PlayerSave>(UnityEngine.JsonUtility.ToJson(save));
            Assert.AreEqual(2, MissionActions.Progress(json, Mission("d_hunt"), Now));
        }
    }
}
