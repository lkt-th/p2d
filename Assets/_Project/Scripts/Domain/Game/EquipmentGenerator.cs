using System;
using System.Collections.Generic;

namespace P2D.Domain
{
    /// <summary>Rolls new equipment: main stat by slot and path, sub-stats from docs/Balance.md §7.2.</summary>
    public static class EquipmentGenerator
    {
        struct SubRoll
        {
            public StatType stat;
            public ModType type;
            public double min;
            public double max;

            public SubRoll(StatType stat, ModType type, double min, double max)
            {
                this.stat = stat;
                this.type = type;
                this.min = min;
                this.max = max;
            }
        }

        static readonly SubRoll[] SubTable =
        {
            new SubRoll(StatType.ATK, ModType.Percent, 0.02, 0.04),
            new SubRoll(StatType.HP, ModType.Percent, 0.02, 0.04),
            new SubRoll(StatType.DEF, ModType.Percent, 0.03, 0.05),
            new SubRoll(StatType.CritRate, ModType.Percent, 0.015, 0.03),
            new SubRoll(StatType.CritDmg, ModType.Percent, 0.03, 0.06),
            new SubRoll(StatType.SPD, ModType.Flat, 1, 3),
            new SubRoll(StatType.ElementDmg, ModType.Percent, 0.02, 0.04),
            new SubRoll(StatType.SkillHaste, ModType.Flat, 1, 3),
        };

        static readonly int[] StartingSubs = { 0, 1, 2, 3, 4, 4 };

        public static EquipmentState Create(IRandom random, Rarity rarity, EquipSlot slot, GrowthPath path, string uid)
        {
            StatType stat;
            ModType type;
            BuildFactory.MainStatFor(slot, path, out stat, out type);
            var item = new EquipmentState
            {
                uid = uid,
                definitionId = slot.ToString().ToLowerInvariant(),
                slot = slot,
                rarity = rarity,
                path = path,
                main = new StatModifier(stat, type, EquipmentRules.MainBase(stat, type, rarity)),
            };
            var subs = new List<StatModifier>();
            for (int i = 0; i < StartingSubs[(int)rarity]; i++)
                AddNewSub(random, item, subs);
            item.subs = subs.ToArray();
            return item;
        }

        /// <summary>A random slot (unlocked at <paramref name="stage"/>) and path.</summary>
        public static EquipmentState CreateRandom(IRandom random, Rarity rarity, int stage, string uid)
        {
            var slots = new List<EquipSlot>();
            foreach (EquipSlot s in Enum.GetValues(typeof(EquipSlot)))
                if (DragonRules.IsSlotUnlocked(s, stage)) slots.Add(s);
            var slot = slots[random.Next(slots.Count)];
            var path = (GrowthPath)random.Next(4);
            return Create(random, rarity, slot, path, uid);
        }

        /// <summary>At +3/+6/…: a new sub-stat while there are fewer than 4, otherwise one existing sub improves.</summary>
        public static void ApplyMilestone(IRandom random, EquipmentState item)
        {
            var subs = new List<StatModifier>(item.subs ?? new StatModifier[0]);
            if (subs.Count < EquipmentRules.MaxSubStats)
            {
                AddNewSub(random, item, subs);
            }
            else
            {
                int i = random.Next(subs.Count);
                var roll = RollFor(subs[i].stat, subs[i].type);
                subs[i] = new StatModifier(subs[i].stat, subs[i].type, subs[i].value + Roll(random, roll));
            }
            item.subs = subs.ToArray();
        }

        /// <summary>
        /// Refine: replaces sub-stat <paramref name="index"/> with a new random one (the others stay,
        /// i.e. they are "locked"). The new line keeps the number of rolls the old one had absorbed,
        /// so milestone upgrades are not lost.
        /// </summary>
        public static void RerollSub(IRandom random, EquipmentState item, int index)
        {
            var subs = new List<StatModifier>(item.subs);
            var old = subs[index];
            var oldRoll = RollFor(old.stat, old.type);
            int rolls = Math.Max(1, (int)Math.Round(old.value / ((oldRoll.min + oldRoll.max) / 2)));

            var options = new List<SubRoll>();
            foreach (var s in SubTable)
            {
                bool taken = s.stat == item.main.stat && s.type == item.main.type;
                for (int i = 0; i < subs.Count; i++) if (i != index && subs[i].stat == s.stat) taken = true;
                if (!taken) options.Add(s);
            }
            var pick = options[random.Next(options.Count)];
            double value = 0;
            for (int r = 0; r < rolls; r++) value += Roll(random, pick);
            subs[index] = new StatModifier(pick.stat, pick.type, value);
            item.subs = subs.ToArray();
        }

        /// <summary>Spire drop rarity by floor band (Balance §5.3), capped by the bracket (§5.1).</summary>
        public static Rarity RollSpireRarity(IRandom random, int floor, int bracket)
        {
            double r = random.NextDouble();
            Rarity rarity;
            var type = SpireRules.TypeOf(floor);
            if (type == FloorType.Boss || floor > SpireRules.TopFloor) rarity = r < 0.10 ? Rarity.Mythic : Rarity.Legendary;
            else if (type == FloorType.Guardian) rarity = r < 0.5 ? Rarity.Epic : Rarity.Legendary;
            else if (floor < 10) rarity = r < 0.60 ? Rarity.Rare : r < 0.95 ? Rarity.Epic : Rarity.Legendary;
            else if (floor < 20) rarity = r < 0.30 ? Rarity.Rare : r < 0.85 ? Rarity.Epic : Rarity.Legendary;
            else rarity = r < 0.60 ? Rarity.Epic : r < 0.98 ? Rarity.Legendary : Rarity.Mythic;

            var cap = bracket <= 1 ? Rarity.Epic : bracket <= 3 ? Rarity.Legendary : Rarity.Mythic;
            return rarity > cap ? cap : rarity;
        }

        static void AddNewSub(IRandom random, EquipmentState item, List<StatModifier> subs)
        {
            var options = new List<SubRoll>();
            foreach (var s in SubTable)
            {
                bool taken = s.stat == item.main.stat && s.type == item.main.type;
                foreach (var existing in subs) if (existing.stat == s.stat) taken = true;
                if (!taken) options.Add(s);
            }
            if (options.Count == 0) return;
            var pick = options[random.Next(options.Count)];
            subs.Add(new StatModifier(pick.stat, pick.type, Roll(random, pick)));
        }

        static SubRoll RollFor(StatType stat, ModType type)
        {
            foreach (var s in SubTable) if (s.stat == stat && s.type == type) return s;
            return SubTable[0];
        }

        static double Roll(IRandom random, SubRoll roll)
        {
            double v = roll.min + random.NextDouble() * (roll.max - roll.min);
            return roll.type == ModType.Flat ? Math.Round(v) : Math.Round(v, 4);
        }
    }
}
