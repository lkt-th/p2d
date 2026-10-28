using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace P2D.Domain
{
    /// <summary>
    /// Deterministic semi-auto battle: fixed 30 ticks per second, every roll from one seeded
    /// IRandom. The same setup + seed + input log always produces the same result, so the
    /// server can replay a client battle and sweeps can run it without rendering.
    /// The view layer only reads state and <see cref="Events"/>; it never changes the battle.
    /// </summary>
    public sealed class BattleSimulation
    {
        public const int TicksPerSecond = 30;
        public const int TelegraphTicks = 24;      // 0.8 s warning before a heavy attack
        public const int DodgeCooldownTicks = 30;  // one manual dodge per second
        public const double MaxGauge = 100;

        public sealed class EnemyRuntime
        {
            public EnemySpec spec;
            public double hp;
            public bool alive = true;
            public bool escaped;
            public bool enraged;
            public int attackTimer;
            public int heavyTimer;
            public int windupLeft;
            public bool dodgedWindup;
            public int escapeTicksLeft;
        }

        readonly BattleSetup setup;
        readonly bool replaying;
        readonly IRandom random;
        readonly List<BattleInput> pending = new List<BattleInput>();
        readonly List<BattleInput> applied = new List<BattleInput>();
        readonly List<BattleEvent> events = new List<BattleEvent>();
        readonly int[] skillCooldowns;
        readonly int timeLimitTicks;

        int dragonAttackTimer;
        int dodgeReadyTick;
        int buffTicksLeft;
        int shieldTicksLeft;
        int frozenTicksLeft;
        double attackBuff;

        public int Tick { get; private set; }
        public BattleOutcome Outcome { get; private set; }
        public double DragonHp { get; private set; }
        public double DragonMaxHp { get; private set; }
        public double Shield { get; private set; }
        public double Gauge { get; private set; }
        public int Target { get; private set; }
        public bool Auto { get; private set; }
        public List<EnemyRuntime> Enemies { get; private set; }
        public IReadOnlyList<BattleEvent> Events => events;

        /// <summary>Inputs in the order they were applied, with their ticks — the replay log.</summary>
        public IReadOnlyList<BattleInput> AppliedInputs => applied;

        public double ElapsedSeconds => Tick / (double)TicksPerSecond;

        /// <param name="replaying">True when re-running a recorded log: autopilot decisions come from the log.</param>
        public BattleSimulation(BattleSetup setup, bool replaying = false)
        {
            if (setup.enemies.Count == 0) throw new ArgumentException("A battle needs at least one enemy.");
            this.setup = setup;
            this.replaying = replaying;
            random = new SeededRandom(setup.seed);
            Auto = setup.auto;
            DragonMaxHp = setup.dragon.hp;
            DragonHp = DragonMaxHp;
            Gauge = Math.Max(0, Math.Min(MaxGauge, setup.startingGauge));
            skillCooldowns = new int[setup.skills.Count];
            timeLimitTicks = ToTicks(setup.timeLimitSeconds);
            dragonAttackTimer = AttackIntervalTicks(setup.dragon.speed);

            Enemies = new List<EnemyRuntime>();
            foreach (var spec in setup.enemies)
            {
                Enemies.Add(new EnemyRuntime
                {
                    spec = spec,
                    hp = spec.hp,
                    attackTimer = spec.attackIntervalSeconds > 0 ? ToTicks(spec.attackIntervalSeconds) : 0,
                    heavyTimer = spec.heavyIntervalSeconds > 0 ? ToTicks(spec.heavyIntervalSeconds) : 0,
                    escapeTicksLeft = spec.escapeSeconds > 0 ? ToTicks(spec.escapeSeconds + setup.escapeDelaySeconds) : 0,
                });
            }
        }

        public static int ToTicks(double seconds) => (int)Math.Round(seconds * TicksPerSecond);

        static int AttackIntervalTicks(double speed) => Math.Max(1, ToTicks(CombatFormulas.AttackInterval(speed)));

        /// <summary>Queues an input for the next tick.</summary>
        public void Enqueue(BattleInputType type, int index = 0) => pending.Add(new BattleInput(Tick + 1, type, index));

        /// <summary>Queues an input recorded at a specific tick (replays).</summary>
        public void Enqueue(BattleInput input) => pending.Add(input);

        public int SkillCooldownTicks(int slot) => skillCooldowns[slot];

        public bool IsSkillReady(int slot) => slot >= 0 && slot < skillCooldowns.Length && skillCooldowns[slot] == 0;

        public bool IsCommandReady => setup.command != null && Gauge >= MaxGauge;

        public bool IsTelegraphActive
        {
            get
            {
                foreach (var e in Enemies) if (e.alive && e.windupLeft > 0) return true;
                return false;
            }
        }

        /// <summary>Advances one tick. Returns false once the battle has ended.</summary>
        public bool Step()
        {
            if (Outcome != BattleOutcome.Running) return false;
            Tick++;

            ApplyPendingInputs();
            if (Auto && !replaying) RunAutoPilot();
            TickTimers();
            if (Ended()) return false;

            DragonBasicAttack();
            if (Ended()) return false;

            EnemiesAct();
            Ended();
            return Outcome == BattleOutcome.Running;
        }

        /// <summary>Runs until the battle ends (sweeps, auto mode, server replays).</summary>
        public BattleOutcome RunToEnd()
        {
            while (Step()) { }
            return Outcome;
        }

        // ---- inputs ---------------------------------------------------------------------

        void ApplyPendingInputs()
        {
            for (int i = 0; i < pending.Count; i++)
            {
                var input = pending[i];
                if (input.tick > Tick) continue;
                pending.RemoveAt(i);
                i--;
                if (Apply(input))
                    applied.Add(new BattleInput(Tick, input.type, input.index));
            }
        }

        bool Apply(BattleInput input)
        {
            switch (input.type)
            {
                case BattleInputType.UseSkill: return UseSkill(input.index);
                case BattleInputType.UseCommand: return UseCommand();
                case BattleInputType.Dodge: return Dodge();
                case BattleInputType.SetTarget:
                    if (input.index < 0 || input.index >= Enemies.Count || !Enemies[input.index].alive) return false;
                    Target = input.index;
                    return true;
                case BattleInputType.SetAuto:
                    Auto = input.index != 0;
                    return true;
            }
            return false;
        }

        bool UseSkill(int slot)
        {
            if (!IsSkillReady(slot)) return false;
            var skill = setup.skills[slot];
            switch (skill.target)
            {
                case SkillTarget.Single:
                    HitEnemy(CurrentTarget(), skill.multiplier);
                    break;
                case SkillTarget.All:
                    for (int i = 0; i < Enemies.Count; i++)
                        if (Enemies[i].alive) HitEnemy(i, skill.multiplier);
                    break;
                case SkillTarget.Self:
                    GainShield(skill.shieldPercent * DragonMaxHp, skill.shieldSeconds);
                    break;
            }
            skillCooldowns[slot] = Math.Max(1, ToTicks(CombatFormulas.Cooldown(skill.cooldownSeconds, setup.dragon.skillHaste)));
            Emit(BattleEventType.SkillUsed, -1, slot);
            return true;
        }

        bool UseCommand()
        {
            if (!IsCommandReady) return false;
            var command = setup.command;
            Gauge = 0;
            Emit(BattleEventType.CommandUsed, -1, (int)command.kind);
            switch (command.kind)
            {
                case CommandKind.AttackBuff:
                    attackBuff = command.value;
                    buffTicksLeft = ToTicks(command.durationSeconds);
                    break;
                case CommandKind.Shield:
                    GainShield(command.value * DragonMaxHp, command.durationSeconds);
                    break;
                case CommandKind.DamageAll:
                    for (int i = 0; i < Enemies.Count; i++)
                        if (Enemies[i].alive) HitEnemy(i, command.value);
                    break;
                case CommandKind.Freeze:
                    frozenTicksLeft = ToTicks(command.durationSeconds);
                    Emit(BattleEventType.Frozen, -1, command.durationSeconds);
                    break;
            }
            return true;
        }

        bool Dodge()
        {
            if (Tick < dodgeReadyTick || !IsTelegraphActive) return false;
            dodgeReadyTick = Tick + DodgeCooldownTicks;
            foreach (var e in Enemies)
                if (e.alive && e.windupLeft > 0) e.dodgedWindup = true;
            return true;
        }

        /// <summary>Auto mode: command when full, shields when hurt, otherwise the first ready attack skill.</summary>
        void RunAutoPilot()
        {
            if (IsCommandReady && Apply(new BattleInput(Tick, BattleInputType.UseCommand)))
                applied.Add(new BattleInput(Tick, BattleInputType.UseCommand));

            for (int slot = 0; slot < skillCooldowns.Length; slot++)
            {
                if (!IsSkillReady(slot)) continue;
                bool isShield = setup.skills[slot].target == SkillTarget.Self;
                if (isShield && (DragonHp > DragonMaxHp * 0.6 || Shield > 0)) continue;
                if (Apply(new BattleInput(Tick, BattleInputType.UseSkill, slot)))
                    applied.Add(new BattleInput(Tick, BattleInputType.UseSkill, slot));
                break;
            }
        }

        // ---- per-tick simulation -------------------------------------------------------

        void TickTimers()
        {
            for (int i = 0; i < skillCooldowns.Length; i++)
                if (skillCooldowns[i] > 0) skillCooldowns[i]--;

            if (buffTicksLeft > 0 && --buffTicksLeft == 0) attackBuff = 0;
            if (shieldTicksLeft > 0 && --shieldTicksLeft == 0) Shield = 0;

            if (timeLimitTicks > 0 && Tick >= timeLimitTicks)
                Finish(BattleOutcome.Timeout);
        }

        void DragonBasicAttack()
        {
            if (--dragonAttackTimer > 0) return;
            dragonAttackTimer = AttackIntervalTicks(setup.dragon.speed);
            HitEnemy(CurrentTarget(), 1.0);
        }

        void EnemiesAct()
        {
            bool frozen = frozenTicksLeft > 0;
            if (frozen) frozenTicksLeft--;

            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (!e.alive) continue;

                if (!e.enraged && e.spec.enrageSeconds > 0 && Tick >= ToTicks(e.spec.enrageSeconds))
                {
                    e.enraged = true;
                    Emit(BattleEventType.Enraged, i, 0);
                }

                if (frozen) continue;

                if (e.escapeTicksLeft > 0 && --e.escapeTicksLeft == 0)
                {
                    e.alive = false;
                    e.escaped = true;
                    Emit(BattleEventType.EnemyEscaped, i, 0);
                    Finish(BattleOutcome.Escaped);
                    return;
                }

                if (e.windupLeft > 0)
                {
                    if (--e.windupLeft == 0) ResolveHeavy(i, e);
                    if (DragonHp <= 0) return;
                    continue;
                }

                if (e.spec.heavyIntervalSeconds > 0 && --e.heavyTimer <= 0)
                {
                    e.heavyTimer = ToTicks(e.spec.heavyIntervalSeconds);
                    e.windupLeft = TelegraphTicks;
                    e.dodgedWindup = false;
                    Emit(BattleEventType.Telegraph, i, TelegraphTicks);
                    continue;
                }

                if (e.spec.attackIntervalSeconds > 0 && --e.attackTimer <= 0)
                {
                    e.attackTimer = ToTicks(e.spec.attackIntervalSeconds);
                    if (random.NextDouble() < PassiveDodgeChance(e)) Emit(BattleEventType.Dodged, i, 0);
                    else HitDragon(i, e, 1.0);
                    if (DragonHp <= 0) return;
                }
            }
        }

        void ResolveHeavy(int index, EnemyRuntime e)
        {
            // Manual dodge always works; auto mode falls back to the SPD-based chance.
            bool dodged = e.dodgedWindup || (Auto && random.NextDouble() < PassiveDodgeChance(e));
            if (dodged)
            {
                Gauge = Math.Min(MaxGauge, Gauge + 10);
                Emit(BattleEventType.Dodged, index, 0);
            }
            else
            {
                HitDragon(index, e, e.spec.heavyMultiplier);
            }
        }

        double PassiveDodgeChance(EnemyRuntime e) =>
            CombatFormulas.AutoDodgeChance(setup.dragon.speed, e.spec.speed, setup.dragon.dodgeBonus);

        // ---- damage ---------------------------------------------------------------------

        int CurrentTarget()
        {
            if (Target >= 0 && Target < Enemies.Count && Enemies[Target].alive) return Target;
            for (int i = 0; i < Enemies.Count; i++)
                if (Enemies[i].alive) { Target = i; return i; }
            return -1;
        }

        void HitEnemy(int index, double multiplier)
        {
            if (index < 0) return;
            var e = Enemies[index];
            if (!e.alive) return;

            var input = new DamageInput
            {
                attack = setup.dragon.attack * (1 + attackBuff),
                skillMultiplier = multiplier,
                attackerLevel = setup.dragonLevel,
                targetDefense = e.spec.defense,
                elementDamageBonus = setup.dragon.elementDamage,
                attackerElement = setup.dragonElement,
                targetElement = e.spec.element,
                critRate = setup.dragon.critRate,
                critDamage = setup.dragon.critDamage,
            };
            bool crit;
            double damage = CombatFormulas.RollDamage(input, random, out crit);
            e.hp = Math.Max(0, e.hp - damage);
            Gauge = Math.Min(MaxGauge, Gauge + 2);
            events.Add(new BattleEvent { tick = Tick, type = BattleEventType.EnemyHit, enemy = index, amount = damage, crit = crit });

            if (e.hp <= 0)
            {
                e.alive = false;
                Emit(BattleEventType.EnemyKilled, index, 0);
                CurrentTarget();
            }
        }

        void HitDragon(int index, EnemyRuntime e, double multiplier)
        {
            var input = new DamageInput
            {
                attack = e.spec.attack * (e.enraged ? 2 : 1),
                skillMultiplier = multiplier,
                attackerLevel = e.spec.level,
                targetDefense = setup.dragon.defense,
                attackerElement = e.spec.element,
                targetElement = setup.dragonElement,
                critRate = 0,
                critDamage = 1,
            };
            bool crit;
            double damage = CombatFormulas.RollDamage(input, random, out crit);

            if (Shield > 0)
            {
                double absorbed = Math.Min(Shield, damage);
                Shield -= absorbed;
                damage -= absorbed;
                Emit(BattleEventType.ShieldAbsorbed, index, absorbed);
            }

            DragonHp = Math.Max(0, DragonHp - damage);
            Gauge = Math.Min(MaxGauge, Gauge + damage / DragonMaxHp * 100);
            events.Add(new BattleEvent { tick = Tick, type = BattleEventType.DragonHit, enemy = index, amount = damage });
        }

        void GainShield(double amount, double seconds)
        {
            Shield += amount;
            shieldTicksLeft = Math.Max(shieldTicksLeft, ToTicks(seconds));
            Emit(BattleEventType.ShieldGained, -1, amount);
        }

        // ---- outcome --------------------------------------------------------------------

        bool Ended()
        {
            if (Outcome != BattleOutcome.Running) return true;
            if (DragonHp <= 0) { Finish(BattleOutcome.Defeat); return true; }

            bool anyAlive = false;
            foreach (var e in Enemies) if (e.alive) { anyAlive = true; break; }
            if (!anyAlive) { Finish(BattleOutcome.Victory); return true; }
            return false;
        }

        void Finish(BattleOutcome outcome)
        {
            if (Outcome != BattleOutcome.Running) return;
            Outcome = outcome;
            Emit(BattleEventType.Ended, -1, (int)outcome);
        }

        void Emit(BattleEventType type, int enemy, double amount) =>
            events.Add(new BattleEvent { tick = Tick, type = type, enemy = enemy, amount = amount });

        /// <summary>Compact fingerprint of the final state, compared between client and server replays.</summary>
        public string Digest()
        {
            var sb = new StringBuilder();
            sb.Append(Outcome).Append('|').Append(Tick).Append('|')
              .Append(DragonHp.ToString("R", CultureInfo.InvariantCulture));
            foreach (var e in Enemies)
                sb.Append('|').Append(e.hp.ToString("R", CultureInfo.InvariantCulture));
            return sb.ToString();
        }
    }

    public static class BattleReplay
    {
        /// <summary>Re-runs a battle from its setup and recorded inputs (server-side validation).</summary>
        public static BattleSimulation Run(BattleSetup setup, IEnumerable<BattleInput> inputs)
        {
            var sim = new BattleSimulation(setup, replaying: true);
            foreach (var input in inputs)
                sim.Enqueue(input);
            sim.RunToEnd();
            return sim;
        }
    }
}
