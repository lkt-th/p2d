using System.Collections.Generic;
using NUnit.Framework;

namespace P2D.Domain.Tests
{
    public class PathSkillTests
    {
        static Dictionary<GrowthPath, int> Affinity(int fury = 0, int bastion = 0, int tempest = 0, int arcane = 0) =>
            new Dictionary<GrowthPath, int>
            {
                [GrowthPath.Fury] = fury, [GrowthPath.Bastion] = bastion,
                [GrowthPath.Tempest] = tempest, [GrowthPath.Arcane] = arcane,
            };

        static BattleSetup Setup(int seed, IEnumerable<SkillData> skills, PathBattleTraits traits, params EnemySpec[] enemies)
        {
            var setup = new BattleSetup
            {
                seed = seed,
                dragonLevel = 34,
                dragonElement = Element.Water,
                dragon = new DragonCombatStats { hp = 8500, attack = 1000, defense = 250, speed = 106, critRate = 0.05, critDamage = 1.5 },
                traits = traits,
            };
            setup.skills.AddRange(skills);
            setup.enemies.AddRange(enemies);
            return setup;
        }

        static EnemySpec Dummy(double hp = 1e9, double attack = 0, double interval = 0) => new EnemySpec
        {
            id = "dummy", level = 34, element = Element.Earth, hp = hp, attack = attack, defense = 0,
            attackIntervalSeconds = interval,
        };

        static int Count(BattleSimulation sim, BattleEventType type)
        {
            int n = 0;
            foreach (var e in sim.Events) if (e.type == type) n++;
            return n;
        }

        static double Sum(BattleSimulation sim, BattleEventType type)
        {
            double total = 0;
            foreach (var e in sim.Events) if (e.type == type) total += e.amount;
            return total;
        }

        // ---- catalog and equip rules ----

        [Test]
        public void Catalog_HasFourSkillsPerPathWithOneUltimate()
        {
            Assert.AreEqual(16, SkillCatalog.All.Count);
            foreach (GrowthPath path in System.Enum.GetValues(typeof(GrowthPath)))
            {
                var skills = SkillCatalog.ForPath(path);
                Assert.AreEqual(4, skills.Count);
                for (int tier = 1; tier <= 4; tier++)
                    Assert.AreEqual(1, skills.FindAll(s => s.tier == tier).Count);
                Assert.IsTrue(skills.Find(s => s.tier == 4).IsUltimate);
            }
        }

        [Test]
        public void Skills_UnlockByTier()
        {
            var available = SkillLoadoutRules.Available(Affinity(fury: 91, bastion: 22));
            var ids = available.ConvertAll(s => s.id);
            CollectionAssert.AreEquivalent(new[] { "flame_claw", "searing_breath", "scale_guard" }, ids);
        }

        [Test]
        public void Validate_EnforcesSlotsLocksAndOneUltimate()
        {
            var affinity = Affinity(fury: 200, tempest: 200);
            Assert.IsNull(SkillLoadoutRules.Validate(new[] { "flame_claw", "inferno_cataclysm", "gale_claws" }, affinity, 3));
            Assert.IsNotNull(SkillLoadoutRules.Validate(new[] { "flame_claw", "searing_breath" }, affinity, 1));
            Assert.IsNotNull(SkillLoadoutRules.Validate(new[] { "inferno_cataclysm", "thousand_wings" }, affinity, 3));
            Assert.IsNotNull(SkillLoadoutRules.Validate(new[] { "rune_orb" }, affinity, 3));
            Assert.IsNotNull(SkillLoadoutRules.Validate(new[] { "flame_claw", "flame_claw" }, affinity, 3));
            Assert.IsNotNull(SkillLoadoutRules.Validate(new[] { "nope" }, affinity, 3));
        }

        // ---- traits and resonances ----

        [Test]
        public void BastionTierTwo_StartShieldAndHpScaledBasics()
        {
            var traits = PathTraitRules.BattleTraits(Affinity(bastion: 60));
            Assert.AreEqual(0.10, traits.startShieldPercent, 1e-9);
            Assert.AreEqual(0.07, traits.basicMaxHpDamage, 1e-9, "Balance 10 round 10");
            Assert.AreEqual(0, PathTraitRules.BattleTraits(Affinity(bastion: 20)).basicMaxHpDamage, "tier I has no HP-scaled basics");
        }

        [Test]
        public void TraitStats_FollowTiers()
        {
            var sheet = new StatSheet();
            sheet.AddRange(PathTraitRules.StatModifiers(Affinity(fury: 20, arcane: 60)));
            sheet.SetBase(StatType.ATK, 100);
            Assert.AreEqual(105, sheet.Get(StatType.ATK), 1e-9);
            Assert.AreEqual(0.06, sheet.Get(StatType.ElementDmg), 1e-9);
            Assert.AreEqual(10, sheet.Get(StatType.SkillHaste), 1e-9);
        }

        [Test]
        public void Resonance_NeedsTwoPathsAtSixty_OrFortyWithLegacy()
        {
            Assert.AreEqual(0, PathTraitRules.ActiveResonances(Affinity(fury: 93, tempest: 59), false).Count);
            CollectionAssert.AreEqual(new[] { Resonance.FlameStorm },
                PathTraitRules.ActiveResonances(Affinity(fury: 93, tempest: 106), false));
            CollectionAssert.AreEqual(new[] { Resonance.RuneVeil },
                PathTraitRules.ActiveResonances(Affinity(bastion: 45, arcane: 41), true));
            Assert.AreEqual(Resonance.StarGale, PathTraitRules.ResonanceFor(GrowthPath.Arcane, GrowthPath.Tempest));
        }

        [Test]
        public void BattleTraits_MatchTiers()
        {
            var t = PathTraitRules.BattleTraits(Affinity(fury: 120, bastion: 60, tempest: 120, arcane: 120));
            Assert.AreEqual(0.20, t.lowHpDamageBonus);
            Assert.AreEqual(0.10, t.startShieldPercent);
            Assert.AreEqual(0, t.reflectPercent);
            Assert.AreEqual(4, t.doubleHitEvery);
            Assert.AreEqual(3, t.echoAfterSkills);
            Assert.AreEqual(0.05, t.lifesteal);          // Fury + Bastion
            Assert.IsTrue(t.shieldScalesWithElement);     // Bastion + Arcane
        }

        // ---- skills in battle ----

        [Test]
        public void GaleClaws_HitsFourTimes()
        {
            var sim = new BattleSimulation(Setup(1, new[] { SkillCatalog.Find("gale_claws") }, new PathBattleTraits(), Dummy()));
            sim.Enqueue(BattleInputType.UseSkill, 0);
            sim.Step();
            Assert.AreEqual(4, Count(sim, BattleEventType.EnemyHit));
        }

        [Test]
        public void SearingBreath_BurnsForFourSeconds()
        {
            var sim = new BattleSimulation(Setup(2, new[] { SkillCatalog.Find("searing_breath") }, new PathBattleTraits(), Dummy()));
            sim.Enqueue(BattleInputType.UseSkill, 0);
            for (int i = 0; i < BattleSimulation.ToTicks(6); i++) sim.Step();
            Assert.AreEqual(4, Count(sim, BattleEventType.BurnTick));
            Assert.AreEqual(4 * 0.30 * 1000, Sum(sim, BattleEventType.BurnTick), 1e-6);
        }

        [Test]
        public void RuneFlame_RaisesBurnDamage()
        {
            var traits = new PathBattleTraits { burnDamageBonus = 0.5 };
            var sim = new BattleSimulation(Setup(2, new[] { SkillCatalog.Find("searing_breath") }, traits, Dummy()));
            sim.Enqueue(BattleInputType.UseSkill, 0);
            for (int i = 0; i < BattleSimulation.ToTicks(6); i++) sim.Step();
            Assert.AreEqual(4 * 0.30 * 1000 * 1.5, Sum(sim, BattleEventType.BurnTick), 1e-6);
        }

        [Test]
        public void ExecutionerBite_DoublesDamageOnLowHpTargets()
        {
            var bite = new[] { SkillCatalog.Find("executioner_bite") };
            var full = new BattleSimulation(Setup(3, bite, new PathBattleTraits(), Dummy()));
            full.Enqueue(BattleInputType.UseSkill, 0);
            full.Step();

            var low = Dummy();
            low.startHpFraction = 0.2;
            var wounded = new BattleSimulation(Setup(3, bite, new PathBattleTraits(), low));
            wounded.Enqueue(BattleInputType.UseSkill, 0);
            wounded.Step();

            Assert.AreEqual(2.0, Sum(wounded, BattleEventType.EnemyHit) / Sum(full, BattleEventType.EnemyHit), 1e-9);
        }

        [Test]
        public void StoneMend_Heals()
        {
            var sim = new BattleSimulation(Setup(4, new[] { SkillCatalog.Find("stone_mend") }, new PathBattleTraits(),
                Dummy(attack: 3000, interval: 0.5)));
            while (sim.DragonHp > sim.DragonMaxHp * 0.7) sim.Step();
            double before = sim.DragonHp;
            sim.Enqueue(BattleInputType.UseSkill, 0);
            sim.Step();
            Assert.Greater(sim.DragonHp, before);
            Assert.AreEqual(1, Count(sim, BattleEventType.Healed));
        }

        [Test]
        public void EternalBastion_ShieldsAndReflects()
        {
            var sim = new BattleSimulation(Setup(5, new[] { SkillCatalog.Find("eternal_bastion") }, new PathBattleTraits(),
                Dummy(attack: 500, interval: 1)));
            sim.Enqueue(BattleInputType.UseSkill, 0);
            for (int i = 0; i < BattleSimulation.ToTicks(3); i++) sim.Step();
            Assert.AreEqual(sim.DragonMaxHp, sim.DragonHp);
            Assert.Greater(Count(sim, BattleEventType.Reflected), 0);
        }

        [Test]
        public void TimeMirror_CutsOtherCooldowns()
        {
            var sim = new BattleSimulation(Setup(6, new[] { SkillCatalog.Find("rune_orb"), SkillCatalog.Find("time_mirror") },
                new PathBattleTraits(), Dummy()));
            sim.Enqueue(BattleInputType.UseSkill, 0);
            sim.Step();
            int before = sim.SkillCooldownTicks(0);
            sim.Enqueue(BattleInputType.UseSkill, 1);
            sim.Step();
            Assert.AreEqual(before - 1 - BattleSimulation.ToTicks(4), sim.SkillCooldownTicks(0));
        }

        [Test]
        public void ArcaneEcho_FourthSkillHasNoCooldown()
        {
            var skills = new[]
            {
                new SkillData { id = "a", multiplier = 1, cooldownSeconds = 1 },
                new SkillData { id = "b", multiplier = 1, cooldownSeconds = 1 },
                new SkillData { id = "c", multiplier = 1, cooldownSeconds = 1 },
            };
            var sim = new BattleSimulation(Setup(7, skills, new PathBattleTraits { echoAfterSkills = 3 }, Dummy()));
            for (int slot = 0; slot < 3; slot++) { sim.Enqueue(BattleInputType.UseSkill, slot); sim.Step(); }
            Assert.IsTrue(sim.EchoReady);
            while (!sim.IsSkillReady(0)) sim.Step();
            sim.Enqueue(BattleInputType.UseSkill, 0);
            sim.Step();
            Assert.IsTrue(sim.IsSkillReady(0));
            Assert.IsFalse(sim.EchoReady);
        }

        [Test]
        public void BastionTraits_StartShieldAndReflect()
        {
            var traits = PathTraitRules.BattleTraits(Affinity(bastion: 120));
            var sim = new BattleSimulation(Setup(8, new SkillData[0], traits, Dummy(attack: 400, interval: 1)));
            Assert.AreEqual(0.10 * sim.DragonMaxHp, sim.Shield, 1e-9);
            for (int i = 0; i < BattleSimulation.ToTicks(3); i++) sim.Step();
            Assert.Greater(Count(sim, BattleEventType.Reflected), 0);
        }

        static int BasicHitsIn30Seconds(PathBattleTraits traits)
        {
            var sim = new BattleSimulation(Setup(9, new SkillData[0], traits, Dummy()));
            for (int i = 0; i < BattleSimulation.ToTicks(30); i++) sim.Step();
            return Count(sim, BattleEventType.EnemyHit);
        }

        [Test]
        public void TempestTrait_EveryFourthBasicAttackHitsTwice()
        {
            int plain = BasicHitsIn30Seconds(new PathBattleTraits());
            int doubled = BasicHitsIn30Seconds(new PathBattleTraits { doubleHitEvery = 4 });
            Assert.AreEqual(plain + plain / 4, doubled);
        }

        [Test]
        public void ZephyrCommand_SpeedsUpBasicAttacks()
        {
            var setup = Setup(11, new SkillData[0], new PathBattleTraits(), Dummy());
            var plain = new BattleSimulation(setup);
            for (int i = 0; i < BattleSimulation.ToTicks(6); i++) plain.Step();

            setup.command = RiderCatalog.Command(RiderCatalog.Find("zephyr").commandSkillId);
            setup.startingGauge = 100;
            var hasted = new BattleSimulation(setup);
            hasted.Enqueue(BattleInputType.UseCommand);
            for (int i = 0; i < BattleSimulation.ToTicks(6); i++) hasted.Step();

            Assert.Greater(Count(hasted, BattleEventType.EnemyHit), Count(plain, BattleEventType.EnemyHit));
        }

        static EnemySpec Imp() => new EnemySpec
        {
            id = "imp", level = 34, element = Element.Fire, hp = 6000, attack = 300, defense = 150, attackIntervalSeconds = 2,
        };

        [Test]
        public void FullFuryBuild_ReplaysDeterministically()
        {
            var skills = SkillLoadoutRules.Resolve(new[] { "flame_claw", "searing_breath", "inferno_cataclysm" });
            var traits = PathTraitRules.BattleTraits(Affinity(fury: 212, tempest: 60));
            var client = new BattleSimulation(Setup(10, skills, traits, Imp(), Imp(), Imp()));
            client.Enqueue(BattleInputType.SetAuto, 1);
            Assert.AreEqual(BattleOutcome.Victory, client.RunToEnd());

            var server = BattleReplay.Run(Setup(10, skills, traits, Imp(), Imp(), Imp()), new List<BattleInput>(client.AppliedInputs));
            Assert.AreEqual(client.Digest(), server.Digest());
        }
    }
}
