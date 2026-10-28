using System;
using System.Collections.Generic;

namespace P2D.Domain
{
    public enum Resonance
    {
        None,
        FlameStorm,     // Fury + Tempest: multi-hits and double hits +15% crit rate
        BloodArmor,     // Fury + Bastion: 5% lifesteal
        RuneFlame,      // Fury + Arcane: burns deal +50%
        WindWall,       // Bastion + Tempest: dodging grants a 3% max-HP shield
        RuneVeil,       // Bastion + Arcane: shields scale with Element DMG
        StarGale,       // Tempest + Arcane: +10 Skill Haste, dodging cuts cooldowns by 1 s
    }

    /// <summary>
    /// What each Path tier grants (docs/GDD.md §4.2). Tier I–II stat traits go into the
    /// StatSheet; battle mechanics go into PathBattleTraits. Tier IV unlocks the ultimate skill.
    /// </summary>
    public static class PathTraitRules
    {
        public static IEnumerable<StatModifier> StatModifiers(Dictionary<GrowthPath, int> affinity, bool legacyLowersResonance = false)
        {
            if (Tier(affinity, GrowthPath.Fury) >= 1) yield return new StatModifier(StatType.ATK, ModType.Percent, 0.05);
            if (Tier(affinity, GrowthPath.Bastion) >= 1) yield return new StatModifier(StatType.HP, ModType.Percent, 0.06);
            if (Tier(affinity, GrowthPath.Tempest) >= 1) yield return new StatModifier(StatType.SPD, ModType.Flat, 8);
            if (Tier(affinity, GrowthPath.Arcane) >= 1) yield return new StatModifier(StatType.ElementDmg, ModType.Percent, 0.06);
            if (Tier(affinity, GrowthPath.Arcane) >= 2) yield return new StatModifier(StatType.SkillHaste, ModType.Flat, 10);

            foreach (var r in ActiveResonances(affinity, legacyLowersResonance))
                if (r == Resonance.StarGale) yield return new StatModifier(StatType.SkillHaste, ModType.Flat, 10);
        }

        public static PathBattleTraits BattleTraits(Dictionary<GrowthPath, int> affinity, bool legacyLowersResonance = false)
        {
            var t = new PathBattleTraits();

            if (Tier(affinity, GrowthPath.Fury) >= 2) { t.critBurnPerSecond = 0.20; t.critBurnSeconds = 3; }
            if (Tier(affinity, GrowthPath.Fury) >= 3) t.lowHpDamageBonus = 0.20;

            if (Tier(affinity, GrowthPath.Bastion) >= 2) { t.startShieldPercent = 0.10; t.basicMaxHpDamage = 0.03; }
            if (Tier(affinity, GrowthPath.Bastion) >= 3) t.reflectPercent = 0.20;

            if (Tier(affinity, GrowthPath.Tempest) >= 2) t.dodgeBonus = 0.08;
            if (Tier(affinity, GrowthPath.Tempest) >= 3) t.doubleHitEvery = 4;

            if (Tier(affinity, GrowthPath.Arcane) >= 3) t.echoAfterSkills = 3;

            foreach (var r in ActiveResonances(affinity, legacyLowersResonance))
            {
                switch (r)
                {
                    case Resonance.FlameStorm: t.multiHitCritBonus = 0.15; break;
                    case Resonance.BloodArmor: t.lifesteal = 0.05; break;
                    case Resonance.RuneFlame: t.burnDamageBonus = 0.50; break;
                    case Resonance.WindWall: t.dodgeShieldPercent = 0.03; break;
                    case Resonance.RuneVeil: t.shieldScalesWithElement = true; break;
                    case Resonance.StarGale: t.dodgeCooldownReduceSeconds = 1; break;
                }
            }
            return t;
        }

        public static Resonance ResonanceFor(GrowthPath a, GrowthPath b)
        {
            if (a == b) return Resonance.None;
            if (a > b) { var tmp = a; a = b; b = tmp; }
            // enum order: Fury, Bastion, Tempest, Arcane
            if (a == GrowthPath.Fury && b == GrowthPath.Bastion) return Resonance.BloodArmor;
            if (a == GrowthPath.Fury && b == GrowthPath.Tempest) return Resonance.FlameStorm;
            if (a == GrowthPath.Fury && b == GrowthPath.Arcane) return Resonance.RuneFlame;
            if (a == GrowthPath.Bastion && b == GrowthPath.Tempest) return Resonance.WindWall;
            if (a == GrowthPath.Bastion && b == GrowthPath.Arcane) return Resonance.RuneVeil;
            return Resonance.StarGale;
        }

        /// <summary>Every pair of paths at or above the resonance threshold resonates.</summary>
        public static List<Resonance> ActiveResonances(Dictionary<GrowthPath, int> affinity, bool legacyLowersResonance)
        {
            var paths = AffinityRules.ResonantPaths(affinity, legacyLowersResonance);
            paths.Sort();
            var result = new List<Resonance>();
            for (int i = 0; i < paths.Count; i++)
                for (int j = i + 1; j < paths.Count; j++)
                    result.Add(ResonanceFor(paths[i], paths[j]));
            return result;
        }

        static int Tier(Dictionary<GrowthPath, int> affinity, GrowthPath path)
        {
            int value;
            return affinity.TryGetValue(path, out value) ? AffinityRules.Tier(value) : 0;
        }
    }

    /// <summary>Which skills a dragon may equip.</summary>
    public static class SkillLoadoutRules
    {
        public const int MaxUltimates = 1;

        public static bool IsUnlocked(SkillData skill, Dictionary<GrowthPath, int> affinity)
        {
            int value;
            return affinity.TryGetValue(skill.path, out value) && AffinityRules.Tier(value) >= skill.tier;
        }

        public static List<SkillData> Available(Dictionary<GrowthPath, int> affinity)
        {
            var list = new List<SkillData>();
            foreach (var s in SkillCatalog.All)
                if (IsUnlocked(s, affinity)) list.Add(s);
            return list;
        }

        /// <summary>Returns null when the selection is valid, otherwise a reason (for logs and UI).</summary>
        public static string Validate(IList<string> skillIds, Dictionary<GrowthPath, int> affinity, int stage)
        {
            int slots = DragonRules.SkillSlotCount(stage);
            int used = 0;
            int ultimates = 0;
            var seen = new HashSet<string>();

            foreach (var id in skillIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                used++;
                if (!seen.Add(id)) return "Skill equipped twice: " + id;
                var skill = SkillCatalog.Find(id);
                if (skill == null) return "Unknown skill: " + id;
                if (!IsUnlocked(skill, affinity)) return "Skill locked: " + id;
                if (skill.IsUltimate) ultimates++;
            }

            if (used > slots) return "Only " + slots + " skill slot(s) at stage " + stage;
            if (ultimates > MaxUltimates) return "Only one ultimate skill can be equipped";
            return null;
        }

        /// <summary>Equipped skill data in slot order, skipping empty slots.</summary>
        public static List<SkillData> Resolve(IList<string> skillIds)
        {
            var list = new List<SkillData>();
            foreach (var id in skillIds)
            {
                var skill = string.IsNullOrEmpty(id) ? null : SkillCatalog.Find(id);
                if (skill != null) list.Add(skill);
            }
            return list;
        }
    }
}
