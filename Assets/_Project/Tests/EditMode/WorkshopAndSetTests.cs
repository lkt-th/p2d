using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace P2D.Domain.Tests
{
    public class WorkshopAndSetTests
    {
        static readonly DateTime Now = new DateTime(2026, 10, 29, 3, 0, 0, DateTimeKind.Utc);

        static PlayerSave NewGame()
        {
            var save = PlayerSave.CreateNew("p", "Ignis", Now, "tim");
            save.story.cleared = StoryActions.TotalStages; // every feature open
            GameActions.EnsureStarterKit(save, new SeededRandom(1));
            return save;
        }

        static EquipmentState Item(PlayerSave save, EquipSlot slot, Rarity rarity, int enhance, string setId = null)
        {
            var item = EquipmentGenerator.Create(new SeededRandom((int)slot + 10), rarity, slot, GrowthPath.Fury, "t" + slot);
            item.enhance = enhance;
            item.setId = setId;
            save.equipment.Add(item);
            return item;
        }

        // ---- reforge ----

        [Test]
        public void Reforge_ChangesPathKeepsEnhance_AndCostsCrystalsAndEssence()
        {
            var save = NewGame();
            var item = Item(save, EquipSlot.Claw, Rarity.Legendary, 9);
            int crystals = GameActions.ReforgeCrystalCost(item);
            Assert.AreEqual((int)Math.Ceiling(0.3 * EquipmentRules.TotalStonesInvested(9, Rarity.Legendary) / 5.0), crystals);

            Assert.IsFalse(GameActions.Reforge(save, item.uid, GrowthPath.Tempest).ok, "no crystals yet");
            save.wallet.elementCrystals[(int)Element.Fire] = crystals - 1;
            save.wallet.elementCrystals[(int)Element.Water] = 5;
            save.wallet.pathEssence[(int)GrowthPath.Tempest] = 20;

            Assert.IsTrue(GameActions.Reforge(save, item.uid, GrowthPath.Tempest).ok);
            Assert.AreEqual(GrowthPath.Tempest, item.path);
            Assert.AreEqual(9, item.enhance);
            Assert.AreEqual(0, save.wallet.pathEssence[(int)GrowthPath.Tempest]);
            Assert.AreEqual(crystals - 1 + 5 - crystals, save.wallet.elementCrystals[0] + save.wallet.elementCrystals[1]);
            Assert.IsFalse(GameActions.Reforge(save, item.uid, GrowthPath.Tempest).ok, "already that path");
        }

        [Test]
        public void Reforge_MovesAffinity()
        {
            var save = NewGame();
            string claw = save.dragon.equipped[(int)EquipSlot.Claw];
            save.wallet.elementCrystals[0] = 10;
            save.wallet.pathEssence[(int)GrowthPath.Arcane] = 10;
            int before = LoadoutCalculator.Affinity(GameActions.ToLoadout(save))[GrowthPath.Arcane];
            Assert.IsTrue(GameActions.Reforge(save, claw, GrowthPath.Arcane).ok);
            Assert.AreEqual(before + 4, LoadoutCalculator.Affinity(GameActions.ToLoadout(save))[GrowthPath.Arcane]);
        }

        // ---- refine ----

        [Test]
        public void Refine_RerollsOneSub_KeepsOthersAndRollCount()
        {
            var save = NewGame();
            var item = Item(save, EquipSlot.Chest, Rarity.Legendary, 0);
            // make sub 0 look like it absorbed 3 rolls
            var subs = item.subs;
            subs[0] = new StatModifier(StatType.CritDmg, ModType.Percent, 0.135);
            var others = new List<StatModifier>(subs).GetRange(1, subs.Length - 1);
            save.wallet.refineStones = 10;
            save.wallet.gold = 100000;

            Assert.IsTrue(GameActions.Refine(save, item.uid, 0, new SeededRandom(4)).ok);
            Assert.AreEqual(4, item.subs.Length);
            for (int i = 1; i < item.subs.Length; i++) Assert.AreEqual(others[i - 1].stat, item.subs[i].stat, "other subs locked");
            Assert.AreEqual(10 - EquipmentRules.RefineCost(Rarity.Legendary), save.wallet.refineStones);
            Assert.AreEqual(100000 - EquipmentRules.RefineGold(Rarity.Legendary), save.wallet.gold);

            // three rolls of the new stat: at least 3 × its minimum roll
            var s = item.subs[0];
            Assert.AreNotEqual(item.main.stat, s.stat);
            Assert.Greater(s.value, 0);
        }

        [Test]
        public void Refine_FailsWithoutStones()
        {
            var save = NewGame();
            var item = Item(save, EquipSlot.Chest, Rarity.Rare, 0);
            save.wallet.refineStones = 0;
            Assert.IsFalse(GameActions.Refine(save, item.uid, 0, new SeededRandom(5)).ok);
            Assert.IsFalse(GameActions.Refine(save, item.uid, 9, new SeededRandom(5)).ok);
        }

        // ---- sets ----

        [Test]
        public void TwoPiece_AddsStats_FourPiece_AddsBattleEffect()
        {
            var save = NewGame();
            save.dragon.stage = 3;
            var loadout = GameActions.ToLoadout(save);
            double atk = LoadoutCalculator.Compute(loadout).Get(StatType.ATK);

            foreach (var uid in save.dragon.equipped)
            {
                var item = save.FindEquipment(uid);
                if (item != null && item.slot != EquipSlot.HeartCore) item.setId = "magma";
            }
            loadout = GameActions.ToLoadout(save);
            Assert.AreEqual(2, LoadoutCalculator.SetPieces(loadout)["magma"]);
            double withTwo = LoadoutCalculator.Compute(loadout).Get(StatType.ATK);
            Assert.Greater(withTwo, atk);
            Assert.AreEqual(0, LoadoutCalculator.BattleTraits(loadout).critBurstMultiplier);

            var horn = Item(save, EquipSlot.Horn, Rarity.Rare, 0, "magma");
            var wing = Item(save, EquipSlot.Wing, Rarity.Rare, 0, "magma");
            GameActions.Equip(save, horn.uid);
            GameActions.Equip(save, wing.uid);
            Assert.AreEqual(0.40, LoadoutCalculator.BattleTraits(GameActions.ToLoadout(save)).critBurstMultiplier); // Balance §7.5
        }

        [Test]
        public void LockedSlots_DoNotCountForSets()
        {
            var save = NewGame();
            var wing = Item(save, EquipSlot.Wing, Rarity.Rare, 0, "storm");
            save.dragon.equipped[(int)EquipSlot.Wing] = wing.uid; // stage 1: wing locked
            Assert.IsFalse(LoadoutCalculator.SetPieces(GameActions.ToLoadout(save)).ContainsKey("storm"));
        }

        static BattleSetup Setup(PathBattleTraits traits, double critRate, EnemySpec enemy, double shield = 0)
        {
            var setup = new BattleSetup
            {
                seed = 3, dragonLevel = 30, dragonElement = Element.Water, traits = traits,
                dragon = new DragonCombatStats { hp = 5000, attack = 500, defense = 100, speed = 100, critRate = critRate, critDamage = 1.5 },
            };
            setup.enemies.Add(enemy);
            if (shield > 0) setup.traits.startShieldPercent = shield;
            return setup;
        }

        static int Count(BattleSimulation sim, BattleEventType type)
        {
            int n = 0;
            foreach (var e in sim.Events) if (e.type == type) n++;
            return n;
        }

        static EnemySpec Dummy(double attack = 0, double interval = 0, double speed = 100) =>
            new EnemySpec { id = "dummy", level = 30, hp = 1e9, attack = attack, attackIntervalSeconds = interval, speed = speed };

        [Test]
        public void MagmaFourPiece_CritsAddABurst()
        {
            var plain = new BattleSimulation(Setup(new PathBattleTraits(), 1.0, Dummy()));
            var burst = new BattleSimulation(Setup(new PathBattleTraits { critBurstMultiplier = 0.6, critBurstCooldownSeconds = 1 }, 1.0, Dummy()));
            for (int i = 0; i < 300; i++) { plain.Step(); burst.Step(); }
            Assert.AreEqual(Count(plain, BattleEventType.EnemyHit) * 2, Count(burst, BattleEventType.EnemyHit), "each crit adds one burst, bursts don't chain");
        }

        [Test]
        public void MagmaFourPiece_BurstHasOneSecondCooldown()
        {
            var setup = Setup(new PathBattleTraits { critBurstMultiplier = 0.6, critBurstCooldownSeconds = 1 }, 1.0, Dummy());
            setup.skills.Add(SkillCatalog.Find("gale_claws")); // 4 hits, all crits at 100% crit rate
            var sim = new BattleSimulation(setup);
            sim.Enqueue(BattleInputType.UseSkill, 0);
            sim.Step();
            Assert.AreEqual(4 + 1, Count(sim, BattleEventType.EnemyHit), "4 claw hits + a single burst");

            // Fast attacks: at SPD 400 the dragon swings every ~0.4 s but bursts at most once per second.
            var fast = Setup(new PathBattleTraits { critBurstMultiplier = 0.6, critBurstCooldownSeconds = 1 }, 1.0, Dummy());
            fast.dragon.speed = 400;
            var quick = new BattleSimulation(fast);
            for (int i = 0; i < BattleSimulation.ToTicks(10); i++) quick.Step();
            int bursts = Count(quick, BattleEventType.EnemyHit) - (int)(10 / CombatFormulas.AttackInterval(400));
            Assert.LessOrEqual(bursts, 11);
        }

        [Test]
        public void GlacialFourPiece_ReflectsOnlyWhileShielded()
        {
            var traits = new PathBattleTraits { shieldedReflectPercent = 0.2 };
            var shielded = new BattleSimulation(Setup(traits, 0, Dummy(300, 1), 0.5));
            var bare = new BattleSimulation(Setup(traits, 0, Dummy(300, 1)));
            for (int i = 0; i < 90; i++) { shielded.Step(); bare.Step(); }
            Assert.Greater(Count(shielded, BattleEventType.Reflected), 0);
            Assert.AreEqual(0, Count(bare, BattleEventType.Reflected));
        }

        [Test]
        public void StormFourPiece_DodgeCounterattacks()
        {
            var setup = Setup(new PathBattleTraits { dodgeCounterMultiplier = 1.0, dodgeBonus = 0.4 }, 0, Dummy(100, 0.5, 0));
            setup.dragon.speed = 300;
            var sim = new BattleSimulation(setup);
            for (int i = 0; i < 300; i++) sim.Step();
            int dodges = Count(sim, BattleEventType.Dodged);
            Assert.Greater(dodges, 0);

            var noSet = Setup(new PathBattleTraits { dodgeBonus = 0.4 }, 0, Dummy(100, 0.5, 0));
            noSet.dragon.speed = 300;
            var baseline = new BattleSimulation(noSet);
            for (int i = 0; i < 300; i++) baseline.Step();
            Assert.Greater(Count(sim, BattleEventType.EnemyHit), Count(baseline, BattleEventType.EnemyHit));
        }

        [Test]
        public void StarFourPiece_SkillBoostsElementDamage()
        {
            var skill = new SkillData { id = "s", multiplier = 1, cooldownSeconds = 30 };
            var with = Setup(new PathBattleTraits { skillElementBuff = 0.25, skillElementBuffSeconds = 5 }, 0, Dummy());
            var without = Setup(new PathBattleTraits(), 0, Dummy());
            with.skills.Add(skill);
            without.skills.Add(skill);
            var a = new BattleSimulation(with);
            var b = new BattleSimulation(without);
            a.Enqueue(BattleInputType.UseSkill, 0);
            b.Enqueue(BattleInputType.UseSkill, 0);
            for (int i = 0; i < 60; i++) { a.Step(); b.Step(); }
            Assert.Greater(a.Enemies[0].spec.hp - a.Enemies[0].hp, b.Enemies[0].spec.hp - b.Enemies[0].hp);
        }

        [Test]
        public void SpireGuardian_DropsThisWeeksSet()
        {
            var save = NewGame();
            save.spire.highestFloor = 9;
            var reward = GameActions.ApplySpireResult(save, 10, BattleOutcome.Victory, Element.Water, new SeededRandom(6));
            Assert.AreEqual("glacial", reward.gear.setId);
            var normal = GameActions.ApplySpireResult(save, 11, BattleOutcome.Victory, Element.Water, new SeededRandom(6));
            Assert.IsTrue(string.IsNullOrEmpty(normal.gear.setId));
        }
    }
}
