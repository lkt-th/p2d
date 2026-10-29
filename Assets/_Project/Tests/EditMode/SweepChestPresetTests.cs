using System;
using NUnit.Framework;

namespace P2D.Domain.Tests
{
    public class SweepChestPresetTests
    {
        static readonly DateTime Now = new DateTime(2026, 10, 30, 3, 0, 0, DateTimeKind.Utc);

        static PlayerSave NewGame()
        {
            var save = PlayerSave.CreateNew("p", "Ignis", Now, "tim");
            save.story.cleared = StoryActions.TotalStages; // every feature open
            GameActions.EnsureStarterKit(save, new SeededRandom(7));
            return save;
        }

        [Test]
        public void Sweep_NeedsAClearedFloorAndATorch_AndPaysHalf()
        {
            var save = NewGame();
            var random = new SeededRandom(3);
            Assert.IsFalse(SpireActions.Sweep(save, 1, Element.Fire, random).ok, "floor 1 not cleared yet");

            save.spire.highestFloor = 5;
            int torches = save.spire.torches;
            long gold = save.wallet.gold;
            var full = SpireRules.FirstClearRewards(5, save.spire.bracket);
            var result = SpireActions.Sweep(save, 5, Element.Fire, random);
            Assert.IsTrue(result.ok);
            Assert.AreEqual(torches - 1, save.spire.torches);
            Assert.AreEqual(full.gold / 2, result.resources.gold);
            Assert.AreEqual(gold + full.gold / 2, save.wallet.gold);
            Assert.AreEqual(full.forgeStones / 2, result.resources.forgeStones);
            Assert.IsFalse(result.bossClaim);

            save.spire.torches = 0;
            Assert.IsFalse(SpireActions.Sweep(save, 5, Element.Fire, random).ok, "no torches");
        }

        [Test]
        public void Sweep_GearChance_IsAboutTwentyPercent()
        {
            var save = NewGame();
            save.spire.highestFloor = 5;
            var random = new SeededRandom(11);
            int gear = 0;
            for (int i = 0; i < 500; i++)
            {
                save.spire.torches = 1;
                if (SpireActions.Sweep(save, 5, Element.Fire, random).gear != null) gear++;
            }
            Assert.That(gear, Is.InRange(75, 125));
        }

        [Test]
        public void BossFloors_PayThreeClaimsAWeek_FirstClearIncluded()
        {
            var save = NewGame();
            var random = new SeededRandom(5);
            save.spire.highestFloor = 19;
            var first = GameActions.ApplySpireResult(save, 20, BattleOutcome.Victory, Element.Wind, random);
            Assert.IsTrue(first.firstClear);
            Assert.AreEqual(2, SpireActions.ClaimsLeft(save, 20));

            int scales = save.wallet.ancientScales;
            for (int i = 0; i < 2; i++)
            {
                save.spire.torches = 1;
                var sweep = SpireActions.Sweep(save, 20, Element.Wind, random);
                Assert.IsTrue(sweep.bossClaim);
                Assert.AreEqual(SetCatalog.ForElement(Element.Wind).id, sweep.gear.setId, "boss claims give a set item");
            }
            Assert.AreEqual(scales + 2, save.wallet.ancientScales);
            Assert.AreEqual(0, SpireActions.ClaimsLeft(save, 20));

            save.spire.torches = 1;
            var plain = SpireActions.Sweep(save, 20, Element.Wind, random);
            Assert.IsTrue(plain.ok);
            Assert.IsFalse(plain.bossClaim, "after three claims it is a plain sweep");
            Assert.AreEqual(scales + 2, save.wallet.ancientScales);
        }

        [TestCase(5, "Bronze chest", 0, 0)]
        [TestCase(15, "Silver chest", 0, 0)]
        [TestCase(25, "Gold chest", 1, 0)]
        [TestCase(30, "Dragon chest", 3, 1)]
        public void WeeklyChest_FollowsTheTiers(int floor, string tier, int scales, int ssrSeals)
        {
            var save = NewGame();
            save.spire.pendingChestFloor = floor;
            int before = save.wallet.ancientScales;
            var chest = SpireActions.OpenWeeklyChest(save, Element.Earth, new SeededRandom(2));
            Assert.IsTrue(chest.ok);
            Assert.AreEqual(tier, chest.tier);
            Assert.AreEqual(300L * floor * save.spire.bracket, chest.gold);
            Assert.AreEqual(before + scales, save.wallet.ancientScales);
            Assert.AreEqual(ssrSeals, save.wallet.soulSeals[(int)RiderRank.SSR]);
            Assert.AreEqual(floor >= 20, chest.gear != null);
            if (chest.gear != null) Assert.AreEqual(SetCatalog.ForElement(Element.Earth).id, chest.gear.setId);
            Assert.IsFalse(SpireActions.OpenWeeklyChest(save, Element.Earth, new SeededRandom(2)).ok, "opens once");
        }

        [Test]
        public void CatchUp_PaysTheSkippedFloorsOnce()
        {
            var save = NewGame();
            save.spire.highestFloor = 20;
            OfflineProgress.Apply(save, Now.AddDays(7));
            Assert.AreEqual(10, save.spire.highestFloor, "the new week starts at half of last week's best");
            Assert.AreEqual(10, save.spire.catchUpFloors);

            long expectedGold = 0;
            int expectedSigils = 0;
            for (int f = 1; f <= 10; f++)
            {
                var r = SpireRules.FirstClearRewards(f, save.spire.bracket);
                expectedGold += r.gold;
                expectedSigils += r.pactSigils;
            }
            long gold = save.wallet.gold;
            int sigils = save.wallet.pactSigils;
            Assert.IsTrue(SpireActions.ClaimCatchUp(save, Element.Fire).ok);
            Assert.AreEqual(gold + expectedGold, save.wallet.gold);
            Assert.AreEqual(sigils + expectedSigils, save.wallet.pactSigils);
            Assert.IsFalse(SpireActions.ClaimCatchUp(save, Element.Fire).ok, "claims once");
        }

        [Test]
        public void Preset_StoresAndRestoresTheLoadout()
        {
            var save = NewGame();
            var claw = save.dragon.equipped[(int)EquipSlot.Claw];
            Assert.IsTrue(PresetActions.Store(save, 0).ok);

            var other = EquipmentGenerator.Create(new SeededRandom(1), Rarity.Rare, EquipSlot.Claw, GrowthPath.Tempest, "eq90001");
            save.equipment.Add(other);
            GameActions.Equip(save, other.uid);
            save.riders.Add(new RiderState { riderId = "lira", star = 1 });
            GameActions.SetMainRider(save, "lira");

            var result = PresetActions.Apply(save, 0);
            Assert.IsTrue(result.ok, result.message);
            Assert.AreEqual(claw, save.dragon.equipped[(int)EquipSlot.Claw]);
            Assert.AreEqual("tim", save.dragon.mainRiderId);
            Assert.IsFalse(PresetActions.Apply(save, 1).ok, "slot 2 is empty");
        }

        [Test]
        public void Preset_SkipsSalvagedItems_AndIsLockedEarly()
        {
            var save = NewGame();
            PresetActions.Store(save, 0);
            var claw = save.dragon.equipped[(int)EquipSlot.Claw];
            var spare = EquipmentGenerator.Create(new SeededRandom(1), Rarity.Rare, EquipSlot.Claw, GrowthPath.Fury, "eq90002");
            save.equipment.Add(spare);
            GameActions.Equip(save, spare.uid);
            Assert.IsTrue(GameActions.Salvage(save, claw).ok);

            Assert.IsTrue(PresetActions.Apply(save, 0).ok);
            Assert.AreEqual(spare.uid, save.dragon.equipped[(int)EquipSlot.Claw], "the salvaged claw is skipped, the current one stays");

            save.story.cleared = 0;
            Assert.IsFalse(PresetActions.Store(save, 1).ok, "presets open with the workshop at 2-10");
        }
    }
}
