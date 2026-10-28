using NUnit.Framework;

namespace P2D.Domain.Tests
{
    public class BalanceSimulatorTests
    {
        [Test]
        public void DefaultScenarios_AreValidBuilds()
        {
            foreach (var scenario in BalanceScenarios.Default())
                foreach (var build in scenario.builds)
                {
                    var enemies = SpireEnemyFactory.Floor(scenario.bracket, 1, Element.Fire, new SpireTuning());
                    Assert.DoesNotThrow(() => BuildFactory.ToBattle(build, enemies, 1, 90), build.name);
                }
        }

        [Test]
        public void SameInputs_SameReport()
        {
            var build = BalanceScenarios.Default()[0].builds[0];
            var a = BalanceSimulator.RunSpire(build, 2, Element.Fire, 18, 20, 3, new SpireTuning(), 0.8);
            var b = BalanceSimulator.RunSpire(build, 2, Element.Fire, 18, 20, 3, new SpireTuning(), 0.8);
            Assert.AreEqual(BalanceSimulator.ToCsv(new[] { a }), BalanceSimulator.ToCsv(new[] { b }));
        }

        [Test]
        public void HigherFloors_AreNotEasier()
        {
            var build = BalanceScenarios.Default()[0].builds[0];
            var report = BalanceSimulator.RunSpire(build, 2, Element.Fire, 1, 30, 4, new SpireTuning(), 0.8);
            Assert.AreEqual(1.0, report.At(1).WinRate);
            Assert.LessOrEqual(report.At(30).WinRate, report.At(1).WinRate);
            Assert.Greater(report.HighestReliableFloor, 10);
        }

        [Test]
        public void BetterGear_ClimbsHigher()
        {
            var scenario = BalanceScenarios.Default()[0];
            var good = scenario.builds.Find(b => b.name == "B2 Fury");
            var weak = scenario.builds.Find(b => b.name == "B2 Fury ของต่ำ");
            var tuning = new SpireTuning();
            int goodFloor = BalanceSimulator.RunSpire(good, 2, Element.Fire, 15, 30, 4, tuning, 0.8).HighestReliableFloor;
            int weakFloor = BalanceSimulator.RunSpire(weak, 2, Element.Fire, 15, 30, 4, tuning, 0.8).HighestReliableFloor;
            Assert.Greater(goodFloor, weakFloor);
        }

        [Test]
        public void AverageSubs_FollowRarityAndMilestones()
        {
            Assert.AreEqual(0, BuildFactory.AverageSubs(GrowthPath.Fury, Rarity.Common, 0).Length);
            var legendary = BuildFactory.AverageSubs(GrowthPath.Fury, Rarity.Legendary, 12);
            Assert.AreEqual(4, legendary.Length);
            // 4 starting rolls + 4 milestones = 8 rolls over 4 lines → 2 rolls each
            Assert.AreEqual(StatType.CritRate, legendary[0].stat);
            Assert.AreEqual(0.045, legendary[0].value, 1e-9);
        }

        [Test]
        public void EnemyLevels_FollowBracketTable()
        {
            Assert.AreEqual(20, SpireEnemyFactory.EnemyLevel(2, 1));
            Assert.AreEqual(45, SpireEnemyFactory.EnemyLevel(2, 30));
            Assert.AreEqual(100, SpireEnemyFactory.EnemyLevel(5, 30));
            Assert.AreEqual(1, SpireEnemyFactory.Floor(2, 30, Element.Fire, new SpireTuning()).Count);
            Assert.AreEqual(3, SpireEnemyFactory.Floor(2, 29, Element.Fire, new SpireTuning()).Count);
        }
    }
}
