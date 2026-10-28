using NUnit.Framework;

namespace P2D.Domain.Tests
{
    public class LoadoutCalculatorTests
    {
        static EquipmentState Claw(int enhance) => new EquipmentState
        {
            slot = EquipSlot.Claw,
            rarity = Rarity.Legendary,
            path = GrowthPath.Fury,
            enhance = enhance,
            main = new StatModifier(StatType.ATK, ModType.Flat, EquipmentRules.MainBase(StatType.ATK, ModType.Flat, Rarity.Legendary)),
        };

        [Test]
        public void Atk_CombinesBaseGearAndRider()
        {
            var loadout = new Loadout
            {
                level = 34,
                stage = 2,
                mainRider = TestRiders.Kyren(),
                mainRiderState = new RiderState { star = 4 },
            };
            loadout.equipped.Add(Claw(9));

            double baseAtk = DragonRules.BaseStat(StatType.ATK, 34, 2);
            double expected = (baseAtk + 323) * 1.18;
            Assert.AreEqual(expected, LoadoutCalculator.Compute(loadout).Get(StatType.ATK), 1e-6);
        }

        [Test]
        public void LockedSlotsAreIgnored()
        {
            var loadout = new Loadout { level = 10, stage = 1 };
            loadout.equipped.Add(new EquipmentState
            {
                slot = EquipSlot.Wing,
                rarity = Rarity.Mythic,
                path = GrowthPath.Tempest,
                enhance = 15,
                main = new StatModifier(StatType.SPD, ModType.Flat, 12),
            });
            Assert.AreEqual(DragonRules.BaseStat(StatType.SPD, 10, 1), LoadoutCalculator.Compute(loadout).Get(StatType.SPD));
            Assert.AreEqual(0, LoadoutCalculator.Affinity(loadout)[GrowthPath.Tempest]);
        }

        [Test]
        public void SupportRidersNeedStageFour()
        {
            var loadout = new Loadout { level = 50, stage = 3 };
            loadout.supportRiders.Add(TestRiders.Kyren());
            loadout.supportRiderStates.Add(new RiderState { star = 1 });
            double withoutSupport = LoadoutCalculator.Compute(loadout).Get(StatType.ATK);
            Assert.AreEqual(DragonRules.BaseStat(StatType.ATK, 50, 3), withoutSupport);

            loadout.stage = 4;
            loadout.level = 61;
            double expected = DragonRules.BaseStat(StatType.ATK, 61, 4) * 1.03;
            Assert.AreEqual(expected, LoadoutCalculator.Compute(loadout).Get(StatType.ATK), 1e-6);
        }
    }
}
