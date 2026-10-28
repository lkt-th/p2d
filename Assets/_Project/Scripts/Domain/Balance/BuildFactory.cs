using System;
using System.Collections.Generic;

namespace P2D.Domain
{
    /// <summary>A dragon setup to test: level/stage, gear quality and path per slot, rider and skills.</summary>
    [Serializable]
    public sealed class BuildPreset
    {
        public string name = "";
        public int level = 1;
        public int stage = 1;
        public Element element = Element.Water;
        public Rarity rarity = Rarity.Rare;
        public int enhance;

        /// <summary>Path of every gear slot, index = (int)EquipSlot (locked slots are ignored).</summary>
        public GrowthPath[] slotPaths = new GrowthPath[6];

        public string riderId = "";
        public int riderStar = 1;
        public string[] skillIds = new string[0];

        /// <summary>Equipment set of every gear slot (SetCatalog id, empty = none), index = (int)EquipSlot.</summary>
        public string[] slotSets = { "", "", "", "", "", "" };

        public BuildPreset WithAllSlots(GrowthPath path)
        {
            for (int i = 0; i < slotPaths.Length; i++) slotPaths[i] = path;
            return this;
        }

        /// <summary>Puts <paramref name="setId"/> on the given slots (e.g. four of them for the 4-piece bonus).</summary>
        public BuildPreset WithSet(string setId, params EquipSlot[] slots)
        {
            foreach (var slot in slots) slotSets[(int)slot] = setId;
            return this;
        }
    }

    /// <summary>Turns a BuildPreset into the loadout and battle setup the real game would use.</summary>
    public static class BuildFactory
    {
        public static Loadout ToLoadout(BuildPreset build)
        {
            var loadout = new Loadout { level = build.level, stage = build.stage };
            foreach (EquipSlot slot in Enum.GetValues(typeof(EquipSlot)))
            {
                if (!DragonRules.IsSlotUnlocked(slot, build.stage)) continue;
                var path = build.slotPaths[(int)slot];
                StatType stat;
                ModType type;
                MainStatFor(slot, path, out stat, out type);
                loadout.equipped.Add(new EquipmentState
                {
                    uid = build.name + "-" + slot,
                    slot = slot,
                    rarity = build.rarity,
                    path = path,
                    enhance = build.enhance,
                    main = new StatModifier(stat, type, EquipmentRules.MainBase(stat, type, build.rarity)),
                    subs = AverageSubs(path, build.rarity, build.enhance),
                    setId = build.slotSets != null ? build.slotSets[(int)slot] : "",
                });
            }

            var rider = RiderCatalog.Find(build.riderId);
            if (rider != null)
            {
                loadout.mainRider = rider;
                loadout.mainRiderState = new RiderState { riderId = rider.id, star = build.riderStar };
            }
            return loadout;
        }

        static readonly int[] StartingSubs = { 0, 1, 2, 3, 4, 4 };

        /// <summary>
        /// Average-roll sub-stats a path-focused player keeps: the rarity's starting rolls plus one
        /// per milestone (+3/+6/…), spread over the four sub-stats that path wants.
        /// Values are the midpoints of docs/Balance.md §7.2.
        /// </summary>
        public static StatModifier[] AverageSubs(GrowthPath path, Rarity rarity, int enhance)
        {
            StatModifier[] wanted;
            switch (path)
            {
                case GrowthPath.Fury:
                    wanted = new[] { Pct(StatType.CritRate, 0.0225), Pct(StatType.CritDmg, 0.045), Pct(StatType.ATK, 0.03), Flat(StatType.SPD, 2) };
                    break;
                case GrowthPath.Bastion:
                    wanted = new[] { Pct(StatType.HP, 0.03), Pct(StatType.DEF, 0.04), Pct(StatType.ATK, 0.03), Pct(StatType.CritRate, 0.0225) };
                    break;
                case GrowthPath.Tempest:
                    wanted = new[] { Flat(StatType.SPD, 2), Pct(StatType.CritRate, 0.0225), Pct(StatType.ATK, 0.03), Pct(StatType.CritDmg, 0.045) };
                    break;
                default:
                    wanted = new[] { Pct(StatType.ElementDmg, 0.03), Flat(StatType.SkillHaste, 2), Pct(StatType.ATK, 0.03), Pct(StatType.CritRate, 0.0225) };
                    break;
            }

            int rolls = StartingSubs[(int)rarity] + enhance / 3;
            int lines = Math.Min(EquipmentRules.MaxSubStats, rolls);
            var subs = new StatModifier[lines];
            for (int i = 0; i < lines; i++) subs[i] = wanted[i].Scaled(0);
            for (int r = 0; r < rolls; r++)
            {
                int i = r % Math.Max(1, lines);
                subs[i] = new StatModifier(wanted[i].stat, wanted[i].type, subs[i].value + wanted[i].value);
            }
            return subs;
        }

        static StatModifier Pct(StatType s, double v) => new StatModifier(s, ModType.Percent, v);
        static StatModifier Flat(StatType s, double v) => new StatModifier(s, ModType.Flat, v);

        /// <summary>Main stat each slot rolls for a path-focused player.</summary>
        public static void MainStatFor(EquipSlot slot, GrowthPath path, out StatType stat, out ModType type)
        {
            type = ModType.Percent;
            switch (slot)
            {
                case EquipSlot.Claw: stat = StatType.ATK; type = ModType.Flat; return;
                case EquipSlot.Chest: stat = StatType.HP; type = ModType.Flat; return;
                case EquipSlot.Tail: stat = StatType.DEF; type = ModType.Flat; return;
                case EquipSlot.HeartCore:
                    stat = path == GrowthPath.Arcane ? StatType.ElementDmg : path == GrowthPath.Bastion ? StatType.HP : StatType.ATK;
                    return;
                case EquipSlot.Horn:
                    stat = path == GrowthPath.Fury ? StatType.CritDmg
                        : path == GrowthPath.Bastion ? StatType.HP
                        : path == GrowthPath.Tempest ? StatType.CritRate
                        : StatType.ATK;
                    return;
                default: // Wing
                    if (path == GrowthPath.Tempest) { stat = StatType.SPD; type = ModType.Flat; return; }
                    stat = path == GrowthPath.Bastion ? StatType.DEF : StatType.ATK;
                    return;
            }
        }

        public static BattleSetup ToBattle(BuildPreset build, List<EnemySpec> enemies, int seed, double timeLimitSeconds)
        {
            var loadout = ToLoadout(build);
            var affinity = LoadoutCalculator.Affinity(loadout);
            string error = SkillLoadoutRules.Validate(build.skillIds, affinity, build.stage);
            if (error != null) throw new ArgumentException("Build '" + build.name + "': " + error);

            var setup = new BattleSetup
            {
                seed = seed,
                dragonLevel = build.level,
                dragonElement = build.element,
                dragon = DragonCombatStats.FromSheet(LoadoutCalculator.Compute(loadout)),
                traits = LoadoutCalculator.BattleTraits(loadout),
                command = loadout.mainRider != null ? RiderCatalog.Command(loadout.mainRider.commandSkillId) : null,
                auto = true,
                timeLimitSeconds = timeLimitSeconds,
                escapeDelaySeconds = loadout.mainRider != null ? loadout.mainRider.huntEscapeDelaySeconds : 0,
            };
            setup.skills.AddRange(SkillLoadoutRules.Resolve(build.skillIds));
            setup.enemies.AddRange(enemies);
            return setup;
        }
    }
}
