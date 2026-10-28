using System.Collections.Generic;

namespace P2D.Domain
{
    /// <summary>
    /// Launch riders from docs/GDD.md §5.4 as plain data, shared by the balance simulator,
    /// tests and the server. In Unity the RiderDefinition assets are the authoring source;
    /// keep both in step until the assets are generated from this list.
    /// </summary>
    public static class RiderCatalog
    {
        static StatModifier Pct(StatType s, double v) => new StatModifier(s, ModType.Percent, v);
        static StatModifier Flat(StatType s, double v) => new StatModifier(s, ModType.Flat, v);

        static readonly List<RiderProfile> all = new List<RiderProfile>
        {
            new RiderProfile { id = "tim", rank = RiderRank.R, pactBuffs = new[] { Pct(StatType.HP, 0.04) } },
            new RiderProfile { id = "mina", rank = RiderRank.R, pactBuffs = new[] { Pct(StatType.ATK, 0.04) } },
            new RiderProfile { id = "lira", rank = RiderRank.SR, hasPath = true, path = GrowthPath.Tempest, baseAffinity = 6,
                pactBuffs = new[] { Flat(StatType.SPD, 5) } },
            new RiderProfile { id = "gorth", rank = RiderRank.SR, hasPath = true, path = GrowthPath.Bastion, baseAffinity = 6,
                pactBuffs = new[] { Pct(StatType.DEF, 0.06) } },
            new RiderProfile { id = "finn", rank = RiderRank.SR, hasPath = true, path = GrowthPath.Tempest, baseAffinity = 6,
                pactBuffs = new[] { Flat(StatType.SPD, 4) }, huntBase = 0.10 },
            new RiderProfile { id = "kyren", rank = RiderRank.SSR, hasPath = true, path = GrowthPath.Fury, baseAffinity = 12,
                pactBuffs = new[] { Pct(StatType.ATK, 0.10), Pct(StatType.CritDmg, 0.15) }, commandSkillId = "burn_order" },
            new RiderProfile { id = "selena", rank = RiderRank.SSR, hasPath = true, path = GrowthPath.Arcane, baseAffinity = 12,
                pactBuffs = new[] { Pct(StatType.ElementDmg, 0.10), Flat(StatType.SkillHaste, 6) }, commandSkillId = "moon_rune" },
            new RiderProfile { id = "brogan", rank = RiderRank.SSR, hasPath = true, path = GrowthPath.Bastion, baseAffinity = 12,
                pactBuffs = new[] { Pct(StatType.HP, 0.10), Pct(StatType.DEF, 0.10) }, commandSkillId = "stand_firm" },
            new RiderProfile { id = "zephyr", rank = RiderRank.SSR, hasPath = true, path = GrowthPath.Tempest, baseAffinity = 12,
                pactBuffs = new[] { Flat(StatType.SPD, 7), Pct(StatType.CritRate, 0.08) }, commandSkillId = "gale_surge" },
            new RiderProfile { id = "ravena", rank = RiderRank.SSR, hasPath = true, path = GrowthPath.Fury, baseAffinity = 12,
                pactBuffs = new[] { Pct(StatType.ATK, 0.08), Pct(StatType.CritRate, 0.05) }, huntBase = 0.15,
                huntEscapeDelaySeconds = 5, commandSkillId = "stalk_prey" },
            new RiderProfile { id = "orion", rank = RiderRank.UR, hasPath = true, path = GrowthPath.Fury, baseAffinity = 15,
                pactBuffs = new[] { Pct(StatType.ATK, 0.12), Pct(StatType.HP, 0.12), Pct(StatType.CritRate, 0.06) },
                legacyLowersResonance = true, commandSkillId = "ancestral_call" },
            new RiderProfile { id = "lycan", rank = RiderRank.UR, hasPath = true, path = GrowthPath.Tempest, baseAffinity = 15,
                pactBuffs = new[] { Pct(StatType.ATK, 0.12), Flat(StatType.SPD, 8), Pct(StatType.ElementDmg, 0.10) },
                huntBase = 0.20, legacyHuntRefundChance = 0.25, commandSkillId = "spirit_net" },
        };

        static readonly List<CommandData> commands = new List<CommandData>
        {
            new CommandData { id = "burn_order", kind = CommandKind.AttackBuff, value = 0.50, durationSeconds = 6 },
            new CommandData { id = "moon_rune", kind = CommandKind.ResetCooldown },
            new CommandData { id = "stand_firm", kind = CommandKind.Shield, value = 0.25, durationSeconds = 8 },
            new CommandData { id = "gale_surge", kind = CommandKind.SpeedBuff, value = 0.60, durationSeconds = 6 },
            new CommandData { id = "stalk_prey", kind = CommandKind.AttackBuff, value = 0.35, durationSeconds = 8 },
            new CommandData { id = "ancestral_call", kind = CommandKind.DamageAll, value = 3.0 },
            new CommandData { id = "spirit_net", kind = CommandKind.Freeze, durationSeconds = 4 },
        };

        public static IReadOnlyList<RiderProfile> All => all;

        public static RiderProfile Find(string id)
        {
            foreach (var r in all) if (r.id == id) return r;
            return null;
        }

        public static CommandData Command(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var c in commands) if (c.id == id) return c;
            return null;
        }
    }
}
