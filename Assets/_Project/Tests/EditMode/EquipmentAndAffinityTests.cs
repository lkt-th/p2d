using System.Collections.Generic;
using NUnit.Framework;

namespace P2D.Domain.Tests
{
    public class EquipmentAndAffinityTests
    {
        // docs/Balance.md §3.2
        [TestCase(Rarity.Common, 0, 4)]
        [TestCase(Rarity.Common, 15, 7)]
        [TestCase(Rarity.Rare, 9, 13)]
        [TestCase(Rarity.Epic, 15, 22)]
        [TestCase(Rarity.Legendary, 9, 26)]
        [TestCase(Rarity.Legendary, 15, 31)]
        [TestCase(Rarity.Mythic, 15, 43)]
        public void EquipmentAffinity_MatchesTable(Rarity rarity, int enhance, int expected)
        {
            Assert.AreEqual(expected, AffinityRules.ForEquipment(rarity, enhance));
        }

        [TestCase(19, 0)]
        [TestCase(20, 1)]
        [TestCase(128, 3)]
        [TestCase(200, 4)]
        public void Tier_UsesThresholds(int affinity, int tier)
        {
            Assert.AreEqual(tier, AffinityRules.Tier(affinity));
        }

        [Test]
        public void PureFuryBuild_ReachesTierFour()
        {
            var gear = new List<EquipmentState>();
            for (int i = 0; i < 6; i++)
                gear.Add(new EquipmentState { rarity = Rarity.Legendary, enhance = 15, path = GrowthPath.Fury });
            var kyren = TestRiders.Kyren();
            var affinity = AffinityRules.Compute(gear, kyren, new RiderState { star = 5 });
            Assert.AreEqual(212, affinity[GrowthPath.Fury]);
            Assert.AreEqual(4, AffinityRules.Tier(affinity[GrowthPath.Fury]));
        }

        [Test]
        public void MainStat_LegendaryClaw()
        {
            double claw = EquipmentRules.MainBase(StatType.ATK, ModType.Flat, Rarity.Legendary);
            Assert.AreEqual(323, EquipmentRules.MainStatAt(claw, 9), 1e-9);
            Assert.AreEqual(340, EquipmentRules.MainStatAt(claw, 10), 1e-9);
        }

        [TestCase(Rarity.Rare, 555)]
        [TestCase(Rarity.Legendary, 1110)]
        [TestCase(Rarity.Mythic, 1665)]
        public void EnhanceCost_TotalToFifteen(Rarity rarity, int expected)
        {
            Assert.AreEqual(expected, EquipmentRules.TotalStonesInvested(15, rarity));
        }

        [Test]
        public void EnhanceTowards_StopsWhenStonesRunOut()
        {
            long stones = 100;
            int reached = EquipmentRules.EnhanceTowards(9, 15, Rarity.Legendary, ref stones);
            Assert.AreEqual(10, reached);
            Assert.AreEqual(24, stones);
        }

        [Test]
        public void NextMilestone_IsNextMultipleOfThree()
        {
            Assert.AreEqual(12, EquipmentRules.NextMilestone(9));
            Assert.AreEqual(15, EquipmentRules.NextMilestone(14));
            Assert.AreEqual(15, EquipmentRules.NextMilestone(15));
        }

        [Test]
        public void Salvage_RefundsEightyPercent()
        {
            Assert.AreEqual(888, EquipmentRules.SalvageStones(15, Rarity.Legendary));
        }
    }
}
