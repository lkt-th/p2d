using System;
using NUnit.Framework;

namespace P2D.Domain.Tests
{
    public class StoryTests
    {
        static PlayerSave Starter()
        {
            var save = PlayerSave.CreateNew("p", "Ignis", new DateTime(2026, 10, 30, 3, 0, 0, DateTimeKind.Utc), "tim");
            GameActions.EnsureStarterKit(save, new SeededRandom(7));
            return save;
        }

        static int AutoWins(PlayerSave save, int index, int runs)
        {
            int wins = 0;
            for (int seed = 1; seed <= runs; seed++)
            {
                var setup = StoryActions.BuildBattle(save, index, seed, new SpireTuning());
                setup.auto = true;
                if (new BattleSimulation(setup).RunToEnd() == BattleOutcome.Victory) wins++;
            }
            return wins;
        }

        [Test]
        public void Stages_AreNumberedAcrossChapters()
        {
            Assert.AreEqual(30, StoryActions.TotalStages);
            Assert.AreEqual(13, StoryActions.Index(2, 3));
            Assert.AreEqual("2-3", StoryActions.Label(13));
            Assert.AreEqual(StoryStageKind.Guardian, StoryActions.KindOf(15));
            Assert.AreEqual(StoryStageKind.Boss, StoryActions.KindOf(20));
            Assert.AreEqual(StoryStageKind.Normal, StoryActions.KindOf(21));
        }

        [TestCase(1, 5)]
        [TestCase(10, 11)]
        [TestCase(20, 18)]
        [TestCase(30, 24)]
        public void EnemyLevel_FollowsBalanceTable(int index, int level) => Assert.AreEqual(level, StoryActions.EnemyLevel(index));

        [Test]
        public void Rewards_FollowBalanceTable()
        {
            var normal = StoryActions.Rewards(1);
            Assert.AreEqual(650, normal.gold);
            Assert.AreEqual(12, normal.essence);
            Assert.AreEqual(6, normal.forgeStones);
            Assert.AreEqual(0, normal.pactSigils);

            var boss = StoryActions.Rewards(10);
            Assert.AreEqual(3 * (500 + 1500), boss.gold);
            Assert.AreEqual(10, boss.pactSigils);
            Assert.AreEqual(100, boss.crystals);
            Assert.AreEqual("mina", boss.riderId);
            Assert.AreEqual("brogan", StoryActions.Rewards(30).riderId, "the chapter 3 boss gives the SSR");
        }

        [Test]
        public void OnlyTheNextStage_PaysOnce()
        {
            var save = Starter();
            long gold = save.wallet.gold;
            var random = new SeededRandom(3);
            Assert.IsFalse(StoryActions.ApplyResult(save, 2, BattleOutcome.Victory, random).firstClear, "stage 2 is locked");
            Assert.IsFalse(StoryActions.ApplyResult(save, 1, BattleOutcome.Defeat, random).firstClear);
            Assert.IsTrue(StoryActions.ApplyResult(save, 1, BattleOutcome.Victory, random).firstClear);
            Assert.AreEqual(gold + 650, save.wallet.gold);
            Assert.AreEqual(2, StoryActions.NextStage(save));
            Assert.IsFalse(StoryActions.ApplyResult(save, 1, BattleOutcome.Victory, random).firstClear, "replays pay nothing");
        }

        [Test]
        public void Guardian_GivesGear_Boss_GivesRider()
        {
            var save = Starter();
            var random = new SeededRandom(3);
            int items = save.equipment.Count;
            save.story.cleared = 4;
            var guardian = StoryActions.ApplyResult(save, 5, BattleOutcome.Victory, random);
            Assert.AreEqual(Rarity.Uncommon, guardian.gear.rarity);
            Assert.AreEqual(items + 1, save.equipment.Count);

            save.story.cleared = 9;
            var boss = StoryActions.ApplyResult(save, 10, BattleOutcome.Victory, random);
            Assert.IsTrue(boss.riderIsNew);
            Assert.IsNotNull(save.FindRider("mina"));
            Assert.AreEqual(Rarity.Rare, boss.gear.rarity);
        }

        [Test]
        public void AllStagesCleared_NextIsZero()
        {
            var save = Starter();
            save.story.cleared = StoryActions.TotalStages;
            Assert.AreEqual(0, StoryActions.NextStage(save));
        }

        [Test]
        public void Features_OpenWithStoryStages()
        {
            var save = Starter();
            var random = new SeededRandom(3);
            var heart = save.dragon.equipped[(int)EquipSlot.HeartCore];
            Assert.IsFalse(GameActions.Enhance(save, heart, random).ok, "enhance is locked at the start");
            Assert.AreEqual(0, GameActions.Summon(save, 10, random).Count, "summon is locked at the start");
            EncounterKind kind;
            Assert.IsFalse(HuntActions.BeginRun(save, new DateTime(2026, 10, 30, 3, 0, 0, DateTimeKind.Utc), random, out kind));
            Assert.IsFalse(GameActions.ApplySpireResult(save, 1, BattleOutcome.Victory, Element.Fire, random).firstClear);

            save.story.cleared = 5;
            var opened = StoryActions.ApplyResult(save, 6, BattleOutcome.Victory, random).unlocked;
            CollectionAssert.AreEqual(new[] { Feature.Enhance }, opened);
            Assert.IsTrue(GameActions.Enhance(save, heart, random).ok);

            save.story.cleared = 9;
            CollectionAssert.AreEquivalent(new[] { Feature.Riders, Feature.Hunt },
                StoryActions.ApplyResult(save, 10, BattleOutcome.Victory, random).unlocked);
            Assert.IsFalse(FeatureUnlocks.IsUnlocked(save, Feature.Spire));
            save.story.cleared = 15;
            Assert.IsTrue(FeatureUnlocks.IsUnlocked(save, Feature.Spire));
            Assert.IsFalse(FeatureUnlocks.IsUnlocked(save, Feature.Workshop));
            Assert.AreEqual("Clear story 2-10", FeatureUnlocks.Hint(Feature.Workshop));
        }

        [Test]
        public void Dialogue_PlaysOnce_WithTheDragonName()
        {
            var save = Starter();
            save.dragon.name = "Pyra";
            var lines = StoryScript.Before(1, save.dragon.name);
            Assert.Greater(lines.Length, 0);
            Assert.IsTrue(Array.Exists(lines, l => l.text.Contains("Pyra")));
            Assert.IsFalse(Array.Exists(lines, l => l.text.Contains(StoryScript.DragonToken)));
            Assert.IsTrue(StoryScript.ShouldPlay(save, 1));
            StoryScript.MarkSeen(save, 1);
            Assert.IsFalse(StoryScript.ShouldPlay(save, 1), "a retry skips the scene");
            Assert.IsFalse(StoryScript.ShouldPlay(save, 2), "stage 1-2 has no scene");
            foreach (int boss in new[] { 10, 20, 30 })
                Assert.Greater(StoryScript.Before(boss, "x").Length, 0, "every chapter boss has a scene");
        }

        [Test]
        public void StarterDragon_ClearsTheFirstStages_ButNotTheChapterBoss()
        {
            var save = Starter(); // Lv 10, three common items
            Assert.GreaterOrEqual(AutoWins(save, 1, 20), 19, "1-1 must be safe");
            Assert.GreaterOrEqual(AutoWins(save, 5, 20), 19, "the first guardian is a check, not a wall");
            Assert.AreEqual(0, AutoWins(save, 10, 10), "the chapter boss asks for a few levels first");
            save.dragon.level = 12;
            Assert.GreaterOrEqual(AutoWins(save, 10, 20), 19, "Lv 12 beats the chapter 1 boss");
        }
    }
}
