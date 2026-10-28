using System.Collections.Generic;
using NUnit.Framework;

namespace P2D.Domain.Tests
{
    public class BattleSimulationTests
    {
        static BattleSetup Setup(int seed, params EnemySpec[] enemies)
        {
            var setup = new BattleSetup
            {
                seed = seed,
                dragonLevel = 34,
                dragonElement = Element.Water,
                dragon = new DragonCombatStats
                {
                    hp = 8500, attack = 1015, defense = 251, speed = 106,
                    critRate = 0.08, critDamage = 1.86,
                },
                command = new CommandData { id = "burn_order", kind = CommandKind.AttackBuff, value = 0.5, durationSeconds = 6 },
                timeLimitSeconds = 90,
            };
            setup.skills.Add(new SkillData { id = "flame_claw", target = SkillTarget.Single, multiplier = 2.0, cooldownSeconds = 4 });
            setup.skills.Add(new SkillData { id = "tide_breath", target = SkillTarget.All, multiplier = 1.2, cooldownSeconds = 8 });
            setup.skills.Add(new SkillData { id = "scale_guard", target = SkillTarget.Self, shieldPercent = 0.2, shieldSeconds = 6, cooldownSeconds = 12 });
            setup.enemies.AddRange(enemies);
            return setup;
        }

        static EnemySpec Imp(double hp = 3000) => new EnemySpec
        {
            id = "lava_imp", level = 34, element = Element.Fire, hp = hp, attack = 300, defense = 150,
            attackIntervalSeconds = 2,
        };

        [Test]
        public void AutoBattle_WinsAgainstFloor17Pack()
        {
            var sim = new BattleSimulation(Setup(1, Imp(), Imp(5400), Imp()));
            sim.Enqueue(BattleInputType.SetAuto, 1);
            Assert.AreEqual(BattleOutcome.Victory, sim.RunToEnd());
            Assert.Greater(sim.DragonHp, 0);
        }

        [Test]
        public void OverwhelmingEnemy_Defeats()
        {
            var brute = Imp(1e9);
            brute.attack = 20000;
            var sim = new BattleSimulation(Setup(2, brute));
            Assert.AreEqual(BattleOutcome.Defeat, sim.RunToEnd());
            Assert.AreEqual(0, sim.DragonHp);
        }

        [Test]
        public void SameSeedAndInputs_SameResult()
        {
            var a = new BattleSimulation(Setup(42, Imp(), Imp(5400), Imp()));
            a.Enqueue(BattleInputType.SetAuto, 1);
            a.RunToEnd();

            var b = new BattleSimulation(Setup(42, Imp(), Imp(5400), Imp()));
            b.Enqueue(BattleInputType.SetAuto, 1);
            b.RunToEnd();

            Assert.AreEqual(a.Digest(), b.Digest());
            Assert.AreEqual(a.Events.Count, b.Events.Count);
        }

        [Test]
        public void Replay_FromRecordedInputs_MatchesClient()
        {
            var client = new BattleSimulation(Setup(7, Imp(), Imp(5400), Imp()));
            // A manual player: taps skills when ready and switches to auto halfway.
            while (client.Outcome == BattleOutcome.Running)
            {
                if (client.Tick == 600) client.Enqueue(BattleInputType.SetAuto, 1);
                if (client.IsSkillReady(0)) client.Enqueue(BattleInputType.UseSkill, 0);
                if (client.IsCommandReady) client.Enqueue(BattleInputType.UseCommand);
                if (client.Tick == 90) client.Enqueue(BattleInputType.SetTarget, 2);
                client.Step();
            }

            var server = BattleReplay.Run(Setup(7, Imp(), Imp(5400), Imp()), new List<BattleInput>(client.AppliedInputs));
            Assert.AreEqual(client.Digest(), server.Digest());
        }

        static EnemySpec Slammer() => new EnemySpec
        {
            id = "fire_soldier", level = 34, hp = 1e9, attack = 400, defense = 150, speed = 100,
            attackIntervalSeconds = 0, heavyIntervalSeconds = 3, heavyMultiplier = 4,
        };

        [Test]
        public void Dodge_DuringTelegraph_AvoidsHeavyHit()
        {
            var hit = new BattleSimulation(Setup(5, Slammer()));
            for (int i = 0; i < BattleSimulation.ToTicks(4); i++) hit.Step();

            var dodger = new BattleSimulation(Setup(5, Slammer()));
            for (int i = 0; i < BattleSimulation.ToTicks(4); i++)
            {
                if (dodger.IsTelegraphActive) dodger.Enqueue(BattleInputType.Dodge);
                dodger.Step();
            }

            Assert.Less(hit.DragonHp, hit.DragonMaxHp);
            Assert.AreEqual(dodger.DragonMaxHp, dodger.DragonHp);
            Assert.IsTrue(dodger.Events.Exists(BattleEventType.Dodged));
        }

        [Test]
        public void RareEnemy_EscapesWhenTooSlow()
        {
            var lizard = Imp(1e9);
            lizard.escapeSeconds = 5;
            var sim = new BattleSimulation(Setup(3, lizard));
            Assert.AreEqual(BattleOutcome.Escaped, sim.RunToEnd());
            Assert.AreEqual(BattleSimulation.ToTicks(5), sim.Tick);
        }

        [Test]
        public void EscapeDelayAndFreeze_BuyTime()
        {
            var lizard = Imp(1e9);
            lizard.escapeSeconds = 5;
            var setup = Setup(3, lizard);
            setup.escapeDelaySeconds = 5;
            setup.startingGauge = 100;
            setup.command = new CommandData { id = "spirit_net", kind = CommandKind.Freeze, durationSeconds = 4 };
            var sim = new BattleSimulation(setup);
            sim.Enqueue(BattleInputType.UseCommand);
            Assert.AreEqual(BattleOutcome.Escaped, sim.RunToEnd());
            // 5 s base + 5 s rider delay + 4 s frozen
            Assert.AreEqual(BattleSimulation.ToTicks(14), sim.Tick);
        }

        [Test]
        public void ShieldSkill_AbsorbsDamage()
        {
            var sim = new BattleSimulation(Setup(9, Imp(1e9)));
            sim.Enqueue(BattleInputType.UseSkill, 2);
            for (int i = 0; i < BattleSimulation.ToTicks(5); i++) sim.Step();
            Assert.AreEqual(sim.DragonMaxHp, sim.DragonHp);
            Assert.IsTrue(sim.Events.Exists(BattleEventType.ShieldAbsorbed));
        }

        [Test]
        public void SkillCooldown_BlocksReuse()
        {
            var sim = new BattleSimulation(Setup(4, Imp(1e9)));
            sim.Enqueue(BattleInputType.UseSkill, 0);
            sim.Step();
            Assert.IsFalse(sim.IsSkillReady(0));
            sim.Enqueue(BattleInputType.UseSkill, 0);
            sim.Step();
            Assert.AreEqual(1, sim.AppliedInputs.Count);
        }

        [Test]
        public void BossEnrages_AndTimeLimitEnds()
        {
            var boss = Imp(1e9);
            boss.enrageSeconds = 10;
            var setup = Setup(6, boss);
            setup.timeLimitSeconds = 20;
            var sim = new BattleSimulation(setup);
            Assert.AreEqual(BattleOutcome.Timeout, sim.RunToEnd());
            Assert.IsTrue(sim.Events.Exists(BattleEventType.Enraged));
        }

        [Test]
        public void Gauge_FillsAndCommandBuffsAttack()
        {
            var sim = new BattleSimulation(Setup(8, Imp(1e9)));
            while (!sim.IsCommandReady && sim.Step()) { }
            Assert.IsTrue(sim.IsCommandReady);
            sim.Enqueue(BattleInputType.UseCommand);
            sim.Step();
            Assert.AreEqual(0, sim.Gauge, 2.0);
            Assert.IsTrue(sim.Events.Exists(BattleEventType.CommandUsed));
        }
    }

    static class BattleEventListExtensions
    {
        public static bool Exists(this IReadOnlyList<BattleEvent> events, BattleEventType type)
        {
            foreach (var e in events) if (e.type == type) return true;
            return false;
        }
    }
}
