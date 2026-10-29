using System;
using NUnit.Framework;

namespace P2D.Domain.Tests
{
    public class GameActionsTests
    {
        static readonly DateTime Now = new DateTime(2026, 10, 29, 3, 0, 0, DateTimeKind.Utc);

        static PlayerSave NewGame()
        {
            var save = PlayerSave.CreateNew("p", "Ignis", Now, "tim");
            save.story.cleared = StoryActions.TotalStages; // every feature open
            GameActions.EnsureStarterKit(save, new SeededRandom(1));
            return save;
        }

        [Test]
        public void StarterKit_EquipsThreeItemsAndPicksSkills()
        {
            var save = NewGame();
            Assert.AreEqual(3, save.equipment.Count);
            Assert.IsNotNull(save.FindEquipment(save.dragon.equipped[(int)EquipSlot.Claw]));
            // 3 common Fury items = 12 affinity → no skill unlocked yet at Tier 0
            Assert.AreEqual("", save.dragon.skillIds[0]);
            GameActions.EnsureStarterKit(save, new SeededRandom(2));
            Assert.AreEqual(3, save.equipment.Count, "starter kit is given once");
        }

        [Test]
        public void Enhance_SpendsStonesAndGold_AndAddsSubAtMilestone()
        {
            var save = NewGame();
            string uid = save.dragon.equipped[(int)EquipSlot.Claw];
            var item = save.FindEquipment(uid);
            long stones = save.wallet.forgeStones;
            for (int i = 0; i < 3; i++) Assert.IsTrue(GameActions.Enhance(save, uid, new SeededRandom(i)).ok);
            Assert.AreEqual(3, item.enhance);
            Assert.AreEqual(stones - EquipmentRules.TotalStonesInvested(3, Rarity.Common), save.wallet.forgeStones);
            Assert.AreEqual(1, item.subs.Length, "common starts with 0 subs, +3 adds one");

            save.wallet.forgeStones = 0;
            Assert.IsFalse(GameActions.Enhance(save, uid, new SeededRandom(9)).ok);
        }

        [Test]
        public void Enhancing_ToTierOne_UnlocksFirstSkill()
        {
            var save = NewGame();
            save.wallet.forgeStones = 100000;
            save.wallet.gold = 10000000;
            foreach (var uid in save.dragon.equipped)
                if (!string.IsNullOrEmpty(uid))
                    while (save.FindEquipment(uid).enhance < 15) GameActions.Enhance(save, uid, new SeededRandom(3));
            // 3 × common +15 = 21 Fury → Tier I
            Assert.AreEqual("flame_claw", save.dragon.skillIds[0]);
        }

        [Test]
        public void FeedEssence_LevelsUpToCap_ThenEvolveNeedsScales()
        {
            var save = NewGame();
            save.wallet.dragonEssence = 100000;
            save.wallet.gold = 100000000;
            Assert.IsTrue(GameActions.FeedEssence(save, 100000).ok);
            Assert.AreEqual(20, save.dragon.level);
            Assert.AreEqual(0, save.dragon.exp);
            Assert.IsFalse(GameActions.FeedEssence(save, 1).ok);

            Assert.IsTrue(GameActions.Evolve(save).ok);
            Assert.AreEqual(2, save.dragon.stage);
            Assert.AreEqual(21, save.dragon.level);
            Assert.AreEqual(0, save.wallet.ancientScales);
        }

        [Test]
        public void Equip_RespectsLockedSlots()
        {
            var save = NewGame();
            var wing = EquipmentGenerator.Create(new SeededRandom(4), Rarity.Rare, EquipSlot.Wing, GrowthPath.Tempest, "w1");
            save.equipment.Add(wing);
            Assert.IsFalse(GameActions.Equip(save, "w1").ok);
            save.dragon.stage = 3;
            Assert.IsTrue(GameActions.Equip(save, "w1").ok);
            Assert.AreEqual("w1", save.dragon.equipped[(int)EquipSlot.Wing]);
        }

        [Test]
        public void Summon_AddsRidersAndCopies_AndSpendsSigils()
        {
            var save = NewGame();
            save.wallet.pactSigils = 200;
            int before = save.riders.Count;
            var random = new SeededRandom(5);
            int copies = 0;
            for (int i = 0; i < 15; i++)
                foreach (var r in GameActions.Summon(save, 10, random))
                    if (!r.isNew) copies++;
            Assert.AreEqual(50, save.wallet.pactSigils);
            Assert.Greater(save.riders.Count, before);
            Assert.Greater(copies, 0);
            save.wallet.pactSigils = 5;
            Assert.AreEqual(0, GameActions.Summon(save, 10, new SeededRandom(6)).Count, "not enough sigils");
            Assert.AreEqual(5, save.wallet.pactSigils);
        }

        [Test]
        public void StarUp_UsesCopiesSealsAndGold()
        {
            var save = NewGame();
            save.riders.Add(new RiderState { riderId = "kyren", star = 1, copies = 1 });
            save.wallet.gold = 0;
            Assert.IsFalse(GameActions.StarUp(save, "kyren").ok);
            save.wallet.gold = 1000000;
            Assert.IsTrue(GameActions.StarUp(save, "kyren").ok);
            Assert.AreEqual(2, save.FindRider("kyren").star);
            Assert.AreEqual(1000000 - 25000, save.wallet.gold);
            Assert.IsFalse(GameActions.StarUp(save, "kyren").ok, "3★ needs 2 copies");
            save.wallet.soulSeals[(int)RiderRank.SSR] = 1;
            save.FindRider("kyren").copies = 1;
            Assert.IsTrue(GameActions.StarUp(save, "kyren").ok);
            Assert.AreEqual(0, save.wallet.soulSeals[(int)RiderRank.SSR]);
        }

        [Test]
        public void SetMainRider_RequiresOwnership()
        {
            var save = NewGame();
            Assert.IsFalse(GameActions.SetMainRider(save, "kyren").ok);
            save.riders.Add(new RiderState { riderId = "kyren", star = 1 });
            Assert.IsTrue(GameActions.SetMainRider(save, "kyren").ok);
            Assert.AreEqual("kyren", save.dragon.mainRiderId);
        }

        [Test]
        public void SpireResult_PaysFirstClearOnce()
        {
            var save = NewGame();
            long gold = save.wallet.gold;
            int items = save.equipment.Count;

            var reward = GameActions.ApplySpireResult(save, 1, BattleOutcome.Victory, Element.Fire, new SeededRandom(7));
            Assert.IsTrue(reward.firstClear);
            Assert.AreEqual(1, save.spire.highestFloor);
            Assert.AreEqual(gold + reward.resources.gold, save.wallet.gold);
            Assert.AreEqual(items + 1, save.equipment.Count);

            Assert.IsFalse(GameActions.ApplySpireResult(save, 1, BattleOutcome.Victory, Element.Fire, new SeededRandom(8)).firstClear);
            Assert.IsFalse(GameActions.ApplySpireResult(save, 2, BattleOutcome.Defeat, Element.Fire, new SeededRandom(8)).firstClear);
            Assert.IsFalse(GameActions.ApplySpireResult(save, 5, BattleOutcome.Victory, Element.Fire, new SeededRandom(8)).firstClear);
        }

        [Test]
        public void SpireBattle_UsesTheSavedLoadout()
        {
            var save = NewGame();
            var setup = GameActions.BuildSpireBattle(save, 1, Element.Fire, 42, new SpireTuning());
            Assert.AreEqual(3, setup.enemies.Count);
            Assert.AreEqual(LoadoutCalculator.Compute(GameActions.ToLoadout(save)).Get(StatType.ATK), setup.dragon.attack, 1e-9);
            setup.auto = true;
            Assert.AreEqual(BattleOutcome.Victory, new BattleSimulation(setup).RunToEnd(), "a new player clears floor 1 in auto");
        }

        [Test]
        public void SpireDrops_RespectBracketCap()
        {
            var random = new SeededRandom(11);
            for (int i = 0; i < 2000; i++)
                Assert.LessOrEqual(EquipmentGenerator.RollSpireRarity(random, 30, 1), Rarity.Epic);
        }
    }
}
