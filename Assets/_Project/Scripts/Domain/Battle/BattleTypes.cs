using System;
using System.Collections.Generic;

namespace P2D.Domain
{
    public enum BattleOutcome { Running, Victory, Defeat, Escaped, Timeout }

    public enum BattleInputType { UseSkill, UseCommand, Dodge, SetTarget, SetAuto }

    public enum SkillTarget { Single, All, Self }

    public enum CommandKind { AttackBuff, Shield, DamageAll, Freeze }

    public enum BattleEventType
    {
        DragonHit, EnemyHit, Dodged, Telegraph, ShieldGained, ShieldAbsorbed,
        SkillUsed, CommandUsed, EnemyKilled, EnemyEscaped, Enraged, Frozen, Ended
    }

    [Serializable]
    public struct BattleInput
    {
        /// <summary>Tick at which the input is applied (recorded for replays).</summary>
        public int tick;
        public BattleInputType type;

        /// <summary>Skill slot, enemy index, or 1/0 for SetAuto.</summary>
        public int index;

        public BattleInput(int tick, BattleInputType type, int index = 0)
        {
            this.tick = tick;
            this.type = type;
            this.index = index;
        }
    }

    public struct BattleEvent
    {
        public int tick;
        public BattleEventType type;

        /// <summary>Enemy index (-1 = the dragon or not applicable).</summary>
        public int enemy;
        public double amount;
        public bool crit;
    }

    [Serializable]
    public sealed class SkillData
    {
        public string id;
        public SkillTarget target = SkillTarget.Single;
        public double multiplier = 1.5;
        public double cooldownSeconds = 6;

        /// <summary>Self skills: shield as a fraction of max HP.</summary>
        public double shieldPercent;
        public double shieldSeconds;
    }

    [Serializable]
    public sealed class CommandData
    {
        public string id;
        public CommandKind kind;

        /// <summary>AttackBuff: +ATK fraction. Shield: fraction of max HP. DamageAll: skill multiplier.</summary>
        public double value;
        public double durationSeconds;
    }

    [Serializable]
    public sealed class EnemySpec
    {
        public string id;
        public int level = 1;
        public Element element;
        public double hp = 100;
        public double attack = 10;
        public double defense;
        public double speed = 100;

        /// <summary>0 = no basic attacks.</summary>
        public double attackIntervalSeconds = 2;

        /// <summary>0 = no telegraphed heavy attack.</summary>
        public double heavyIntervalSeconds;
        public double heavyMultiplier = 3;

        /// <summary>Hunting Grounds rares: flee after this many seconds. 0 = never.</summary>
        public double escapeSeconds;

        /// <summary>Bosses: attack doubles after this many seconds. 0 = never.</summary>
        public double enrageSeconds;
    }

    /// <summary>Dragon numbers the battle needs, usually built from a computed StatSheet.</summary>
    [Serializable]
    public struct DragonCombatStats
    {
        public double hp;
        public double attack;
        public double defense;
        public double speed;
        public double critRate;
        public double critDamage;
        public double elementDamage;
        public double skillHaste;
        public double dodgeBonus;

        public static DragonCombatStats FromSheet(StatSheet sheet, double dodgeBonus = 0) => new DragonCombatStats
        {
            hp = sheet.Get(StatType.HP),
            attack = sheet.Get(StatType.ATK),
            defense = sheet.Get(StatType.DEF),
            speed = sheet.Get(StatType.SPD),
            critRate = sheet.Get(StatType.CritRate),
            critDamage = sheet.Get(StatType.CritDmg),
            elementDamage = sheet.Get(StatType.ElementDmg),
            skillHaste = sheet.Get(StatType.SkillHaste),
            dodgeBonus = dodgeBonus,
        };
    }

    [Serializable]
    public sealed class BattleSetup
    {
        public int seed;
        public int dragonLevel = 1;
        public Element dragonElement;
        public DragonCombatStats dragon;
        public List<SkillData> skills = new List<SkillData>();
        public CommandData command;
        public List<EnemySpec> enemies = new List<EnemySpec>();
        public bool auto;
        public double timeLimitSeconds = 90;

        /// <summary>Command gauge at the start (0–100), e.g. from a rider trait or a carried-over floor.</summary>
        public double startingGauge;

        /// <summary>Extra seconds before Hunting Grounds rares flee (rider trait).</summary>
        public double escapeDelaySeconds;
    }
}
