using System.Collections.Generic;

namespace P2D.Domain
{
    /// <summary>
    /// The 16 dragon skills, 4 per Growth Path, unlocked by that path's affinity tier
    /// (docs/GDD.md §4.2, numbers in docs/Balance.md §10). Kept in code so the server
    /// shares the exact values; Remote Config may override numbers later.
    /// </summary>
    public static class SkillCatalog
    {
        static readonly List<SkillData> all = new List<SkillData>
        {
            // ---- Fury: burst, burn, execute ----
            new SkillData { id = "flame_claw", path = GrowthPath.Fury, tier = 1, target = SkillTarget.Single,
                multiplier = 2.0, cooldownSeconds = 5 },
            new SkillData { id = "searing_breath", path = GrowthPath.Fury, tier = 2, target = SkillTarget.All,
                multiplier = 1.0, cooldownSeconds = 9, burnPerSecond = 0.30, burnSeconds = 4 },
            new SkillData { id = "executioner_bite", path = GrowthPath.Fury, tier = 3, target = SkillTarget.Single,
                multiplier = 2.5, cooldownSeconds = 10, executeBonus = 1.0 },
            new SkillData { id = "inferno_cataclysm", path = GrowthPath.Fury, tier = 4, target = SkillTarget.All,
                multiplier = 4.0, cooldownSeconds = 30, burnPerSecond = 0.50, burnSeconds = 5 },

            // ---- Bastion: shields, sustain, reflect ----
            new SkillData { id = "scale_guard", path = GrowthPath.Bastion, tier = 1, target = SkillTarget.Self,
                multiplier = 0, cooldownSeconds = 12, shieldPercent = 0.15, shieldSeconds = 6 },
            new SkillData { id = "tail_slam", path = GrowthPath.Bastion, tier = 2, target = SkillTarget.Single,
                multiplier = 1.6, cooldownSeconds = 8, shieldPercent = 0.08, shieldSeconds = 5 },
            new SkillData { id = "stone_mend", path = GrowthPath.Bastion, tier = 3, target = SkillTarget.Self,
                multiplier = 0, cooldownSeconds = 18, healPercent = 0.20 },
            new SkillData { id = "eternal_bastion", path = GrowthPath.Bastion, tier = 4, target = SkillTarget.Self,
                multiplier = 0, cooldownSeconds = 35, shieldPercent = 0.40, shieldSeconds = 10,
                reflectPercent = 0.30, reflectSeconds = 10 },

            // ---- Tempest: many hits, evasion ----
            new SkillData { id = "gale_claws", path = GrowthPath.Tempest, tier = 1, target = SkillTarget.Single,
                multiplier = 0.7, hits = 4, cooldownSeconds = 5 },
            new SkillData { id = "cyclone_step", path = GrowthPath.Tempest, tier = 2, target = SkillTarget.Self,
                multiplier = 0, cooldownSeconds = 12, dodgeBuff = 0.25, dodgeBuffSeconds = 5 },
            new SkillData { id = "wind_shear", path = GrowthPath.Tempest, tier = 3, target = SkillTarget.All,
                multiplier = 0.6, hits = 3, cooldownSeconds = 9 },
            new SkillData { id = "thousand_wings", path = GrowthPath.Tempest, tier = 4, target = SkillTarget.Single,
                multiplier = 0.8, hits = 12, cooldownSeconds = 30 },

            // ---- Arcane: element damage, cooldown control ----
            new SkillData { id = "rune_orb", path = GrowthPath.Arcane, tier = 1, target = SkillTarget.Single,
                multiplier = 1.8, cooldownSeconds = 5, extraElementDamage = 0.30 },
            new SkillData { id = "star_bolt", path = GrowthPath.Arcane, tier = 2, target = SkillTarget.All,
                multiplier = 1.3, cooldownSeconds = 8 },
            new SkillData { id = "time_mirror", path = GrowthPath.Arcane, tier = 3, target = SkillTarget.Self,
                multiplier = 0, cooldownSeconds = 15, cooldownReduceSeconds = 4 },
            new SkillData { id = "galaxy_rain", path = GrowthPath.Arcane, tier = 4, target = SkillTarget.All,
                multiplier = 1.5, hits = 3, cooldownSeconds = 30, extraElementDamage = 0.50 },
        };

        public static IReadOnlyList<SkillData> All => all;

        public static SkillData Find(string id)
        {
            foreach (var s in all) if (s.id == id) return s;
            return null;
        }

        public static List<SkillData> ForPath(GrowthPath path)
        {
            var list = new List<SkillData>();
            foreach (var s in all) if (s.path == path) list.Add(s);
            return list;
        }
    }
}
