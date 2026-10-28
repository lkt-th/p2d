using System;
using NUnit.Framework;

namespace P2D.Domain.Tests
{
    public class DungeonAndSummonTests
    {
        [Test]
        public void Spire_Floor17Bracket2Rewards()
        {
            var r = SpireRules.FirstClearRewards(17, 2);
            Assert.AreEqual(5400, r.gold);
            Assert.AreEqual(44, r.forgeStones);
            Assert.AreEqual(8, r.dragonEssence);
            Assert.AreEqual(5, r.elementCrystals);
            Assert.AreEqual(0, r.pactSigils);
            Assert.AreEqual(1, SpireRules.FirstClearRewards(20, 2).pactSigils);
        }

        [Test]
        public void Spire_FloorTypesAndCatchUp()
        {
            Assert.AreEqual(FloorType.Guardian, SpireRules.TypeOf(10));
            Assert.AreEqual(FloorType.Boss, SpireRules.TypeOf(30));
            Assert.AreEqual(FloorType.Endless, SpireRules.TypeOf(31));
            Assert.AreEqual(12, SpireRules.CatchUpStartFloor(25));
            Assert.AreEqual(9, SpireRules.RefillTorches(6, 5));
        }

        [Test]
        public void WeeklyReset_IsMondayFiveAmBangkok()
        {
            var sundayBeforeReset = new DateTime(2026, 10, 25, 21, 59, 59, DateTimeKind.Utc);
            var atReset = new DateTime(2026, 10, 25, 22, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual(WeeklyCalendar.WeekIndex(sundayBeforeReset) + 1, WeeklyCalendar.WeekIndex(atReset));
            Assert.AreEqual(atReset, WeeklyCalendar.NextResetUtc(sundayBeforeReset));
            Assert.AreEqual(DayOfWeek.Monday, WeeklyCalendar.NextResetUtc(atReset).AddHours(7).DayOfWeek);
            Assert.AreEqual(3, WeeklyCalendar.FallbackThemeIndex(-1, 4));
        }

        [Test]
        public void Combat_DefenseAndElements()
        {
            Assert.AreEqual(1500.0 / 2570.0, CombatFormulas.DefenseFactor(100, 1070), 1e-9);
            Assert.AreEqual(1.3, CombatFormulas.ElementMultiplier(Element.Water, Element.Fire));
            Assert.AreEqual(0.8, CombatFormulas.ElementMultiplier(Element.Fire, Element.Water));
            Assert.AreEqual(1.3, CombatFormulas.ElementMultiplier(Element.Light, Element.Dark));
            Assert.AreEqual(1.3, CombatFormulas.ElementMultiplier(Element.Dark, Element.Light));
            Assert.AreEqual(1.0, CombatFormulas.ElementMultiplier(Element.Fire, Element.Earth));
            Assert.AreEqual(1.6 * 100 / 130, CombatFormulas.AttackInterval(130), 1e-9);
            Assert.AreEqual(0.40, CombatFormulas.AutoDodgeChance(400, 100, 0.3), 1e-9);
        }

        [Test]
        public void Hunt_RatesUseRiderBonusAndCap()
        {
            Assert.AreEqual(0.12, HuntRules.RareRate(0), 1e-9);
            Assert.AreEqual(0.1728, HuntRules.RareRate(0.44), 1e-9);
            Assert.AreEqual(0.045, HuntRules.EpicRate(2.0), 1e-9);
        }

        [Test]
        public void Hunt_EncounterFrequencyIsClose()
        {
            var random = new SeededRandom(1234);
            int rare = 0, epic = 0;
            const int runs = 200000;
            for (int i = 0; i < runs; i++)
            {
                var kind = HuntRules.RollEncounter(random, 0);
                if (kind == EncounterKind.Rare) rare++;
                else if (kind == EncounterKind.Epic) epic++;
            }
            Assert.AreEqual(0.12, rare / (double)runs, 0.005);
            Assert.AreEqual(0.03, epic / (double)runs, 0.003);
        }

        [Test]
        public void Hunt_RewardsByEncounter()
        {
            Assert.AreEqual(2000, HuntRules.Rewards(EncounterKind.Normal, 2, true).gold);
            Assert.IsFalse(HuntRules.Rewards(EncounterKind.Normal, 2, true).dropsGear);
            Assert.AreEqual(44, HuntRules.Rewards(EncounterKind.Epic, 2, true).forgeStones);
            Assert.AreEqual(1000, HuntRules.Rewards(EncounterKind.Rare, 2, false).gold);
            Assert.AreEqual(25, HuntRules.EscapeSeconds(EncounterKind.Rare, 5));
            Assert.AreEqual(120, HuntRules.RegenerateStamina(118, 60));
        }

        [Test]
        public void Summon_HardPityGivesUr()
        {
            var pity = new SummonPityState { sinceUr = SummonRules.UrHardPity - 1 };
            var result = SummonRules.Pull(new SeededRandom(1), pity);
            Assert.AreEqual(RiderRank.UR, result.rank);
            Assert.AreEqual(0, pity.sinceUr);
        }

        [Test]
        public void Summon_SsrPityAtForty()
        {
            var pity = new SummonPityState { sinceSsr = SummonRules.SsrPity - 1 };
            var result = SummonRules.Pull(new SeededRandom(7), pity);
            Assert.IsTrue(result.rank == RiderRank.SSR || result.rank == RiderRank.UR);
        }

        [Test]
        public void Summon_TenPullAlwaysHasSrOrBetter()
        {
            var random = new SeededRandom(99);
            for (int batch = 0; batch < 500; batch++)
            {
                var results = SummonRules.PullTen(random, new SummonPityState());
                Assert.IsTrue(results.Exists(r => r.rank != RiderRank.R));
            }
        }

        [Test]
        public void Summon_LosingFiftyFiftyGuaranteesNextFeatured()
        {
            var pity = new SummonPityState { featuredGuaranteed = true, sinceUr = SummonRules.UrHardPity - 1 };
            var result = SummonRules.Pull(new SeededRandom(3), pity);
            Assert.IsTrue(result.featured);
            Assert.IsFalse(pity.featuredGuaranteed);
        }

        [Test]
        public void SoftPity_RaisesUrChance()
        {
            Assert.AreEqual(0.01, SummonRules.UrChance(0), 1e-9);
            Assert.AreEqual(0.03, SummonRules.UrChance(89), 1e-9);
            Assert.AreEqual(1.0, SummonRules.UrChance(119), 1e-9);
        }
    }
}
