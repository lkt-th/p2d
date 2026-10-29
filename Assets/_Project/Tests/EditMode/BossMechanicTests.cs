using NUnit.Framework;

namespace P2D.Domain.Tests
{
    public class BossMechanicTests
    {
        static BattleSetup Setup(params EnemySpec[] enemies)
        {
            var setup = new BattleSetup
            {
                seed = 3,
                dragonLevel = 34,
                dragonElement = Element.Water,
                dragon = new DragonCombatStats { hp = 8500, attack = 1015, defense = 251, speed = 106, critRate = 0, critDamage = 1.5 },
                timeLimitSeconds = 90,
            };
            setup.skills.Add(new SkillData { id = "flame_claw", target = SkillTarget.Single, multiplier = 2.0, cooldownSeconds = 4 });
            setup.enemies.AddRange(enemies);
            return setup;
        }

        static EnemySpec Boss() => new EnemySpec
        {
            id = "boss", level = 34, element = Element.Fire, hp = 1e7, attack = 400, defense = 150,
            attackIntervalSeconds = 0, heavyIntervalSeconds = 3, heavyMultiplier = 3,
        };

        /// <summary>Runs until the first heavy resolves, dodging every telegraph by hand.</summary>
        static BattleSimulation FirstHeavyWithManualDodge(EnemySpec boss)
        {
            var sim = new BattleSimulation(Setup(boss));
            while (sim.Step())
            {
                if (sim.IsTelegraphActive) sim.Enqueue(BattleInputType.Dodge);
                if (sim.ElapsedSeconds > 4) break;
            }
            return sim;
        }

        [Test]
        public void Wave_CannotBeDodged()
        {
            var normal = FirstHeavyWithManualDodge(Boss());
            Assert.AreEqual(normal.DragonMaxHp, normal.DragonHp, "a telegraphed heavy is dodged by hand");

            var wave = Boss();
            wave.heavyUndodgeable = true;
            var sim = FirstHeavyWithManualDodge(wave);
            Assert.Less(sim.DragonHp, sim.DragonMaxHp, "the wave lands anyway");
        }

        [Test]
        public void CrystalArmour_OnlySkillsBreakIt_OtherDamageIsChipped()
        {
            var golem = Boss();
            golem.heavyIntervalSeconds = 0;
            golem.skillArmor = 0.002; // 20,000 armour on 10M HP
            golem.armorChip = 0.25;
            var sim = new BattleSimulation(Setup(golem));
            var plain = new BattleSimulation(Setup(Boss()));
            for (int i = 0; i < 30 * 3; i++) { sim.Step(); plain.Step(); } // basic attacks only (no skill input)
            double dealt = sim.Enemies[0].spec.hp - sim.Enemies[0].hp;
            double plainDealt = plain.Enemies[0].spec.hp - plain.Enemies[0].hp;
            Assert.Greater(plainDealt, 0);
            Assert.AreEqual(plainDealt * 0.25, dealt, plainDealt * 0.05, "basic hits are chipped while the armour holds");
            Assert.AreEqual(golem.skillArmor * golem.hp, sim.Enemies[0].armor, 1e-6, "basic hits never wear the armour");

            for (int cast = 0; cast < 200 && sim.Enemies[0].armor > 0; cast++)
            {
                sim.Enqueue(BattleInputType.UseSkill, 0);
                for (int t = 0; t < 30 * 4; t++) sim.Step();
            }
            Assert.AreEqual(0, sim.Enemies[0].armor, 1e-6);
            bool broken = false;
            foreach (var e in sim.Events) if (e.type == BattleEventType.ArmorBroken) broken = true;
            Assert.IsTrue(broken, "the break is announced");
        }

        [Test]
        public void ThemeBosses_FightTheirOwnWay_FireStaysTheSame()
        {
            var tuning = new SpireTuning();
            EnemySpec Boss30(Element theme) => SpireEnemyFactory.Floor(2, SpireRules.TopFloor, theme, tuning)[0];

            var fire = Boss30(Element.Fire);
            Assert.AreEqual(tuning.bossHeavyEvery, fire.heavyIntervalSeconds);
            Assert.IsFalse(fire.heavyUndodgeable);
            Assert.AreEqual(0, fire.skillArmor);

            var water = Boss30(Element.Water);
            Assert.IsTrue(water.heavyUndodgeable);
            Assert.AreEqual(tuning.waveEvery, water.heavyIntervalSeconds);

            Assert.AreEqual(tuning.lightningEvery, Boss30(Element.Wind).heavyIntervalSeconds);
            Assert.AreEqual(tuning.golemArmor, Boss30(Element.Earth).skillArmor);
            Assert.AreEqual(0, SpireEnemyFactory.Floor(2, 29, Element.Earth, tuning)[0].skillArmor, "only the boss");
        }
    }
}
