using NUnit.Framework;

namespace P2D.Domain.Tests
{
    public class DragonRulesTests
    {
        // Values from docs/Balance.md §1.2
        [TestCase(1, 1, 1200, 120, 60, 100)]
        [TestCase(20, 1, 3024, 302, 151, 103)]
        [TestCase(21, 2, 3588, 359, 179, 104)]
        [TestCase(40, 2, 5686, 569, 284, 107)]
        [TestCase(60, 3, 9266, 927, 463, 111)]
        [TestCase(80, 4, 14054, 1405, 703, 115)]
        [TestCase(100, 5, 21408, 2141, 1070, 119)]
        public void BaseStats_MatchBalanceTable(int level, int stage, double hp, double atk, double def, double spd)
        {
            Assert.AreEqual(hp, DragonRules.BaseStat(StatType.HP, level, stage));
            Assert.AreEqual(atk, DragonRules.BaseStat(StatType.ATK, level, stage));
            Assert.AreEqual(def, DragonRules.BaseStat(StatType.DEF, level, stage));
            Assert.AreEqual(spd, DragonRules.BaseStat(StatType.SPD, level, stage));
        }

        [TestCase(1, 100)]
        [TestCase(20, 12068)]
        public void ExpToNext_FollowsCurve(int level, long expected)
        {
            Assert.AreEqual(expected, DragonRules.ExpToNext(level));
        }

        [Test]
        public void Evolve_OnlyAtLevelCap()
        {
            Assert.IsFalse(DragonRules.CanEvolve(19, 1));
            Assert.IsTrue(DragonRules.CanEvolve(20, 1));
            Assert.IsFalse(DragonRules.CanEvolve(100, 5));
        }

        [Test]
        public void EvolveCosts_TotalFiftyOneScales()
        {
            int total = 0;
            for (int stage = 1; stage < DragonRules.MaxStage; stage++)
                total += DragonRules.EvolveCostFrom(stage).ancientScales;
            Assert.AreEqual(51, total);
        }

        [Test]
        public void Slots_UnlockByStage()
        {
            Assert.IsTrue(DragonRules.IsSlotUnlocked(EquipSlot.HeartCore, 1));
            Assert.IsFalse(DragonRules.IsSlotUnlocked(EquipSlot.Horn, 1));
            Assert.IsTrue(DragonRules.IsSlotUnlocked(EquipSlot.Horn, 2));
            Assert.IsFalse(DragonRules.IsSlotUnlocked(EquipSlot.Tail, 3));
            Assert.AreEqual(2, DragonRules.SupportRiderSlots(4));
        }
    }
}
