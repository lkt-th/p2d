using System;
using System.Collections.Generic;

namespace P2D.Domain
{
    public struct HuntZone
    {
        public int zone;
        public string name;
        public int level;
        public Element element;
    }

    public sealed class HuntRun
    {
        public EncounterKind kind;
        public BattleOutcome outcome;
        public HuntRewards rewards;
        public EquipmentState gear;
        public string bonus = "";
        public bool staminaRefunded;
    }

    /// <summary>
    /// Hunting Grounds (docs/GDD.md §8.1, Balance §9): repeatable runs for basic resources with a
    /// random chance of rare monsters. Encounters are independent rolls (no pity); the rider's
    /// hunter trait raises the rare rates.
    /// </summary>
    public static class HuntActions
    {
        public static readonly HuntZone[] Zones =
        {
            new HuntZone { zone = 1, name = "Ember Meadow", level = 8, element = Element.Fire },
            new HuntZone { zone = 2, name = "Ash Forest", level = 19, element = Element.Fire },
            new HuntZone { zone = 3, name = "Frost Hollow", level = 39, element = Element.Water },
            new HuntZone { zone = 4, name = "Storm Ridge", level = 59, element = Element.Wind },
            new HuntZone { zone = 5, name = "Star Ruins", level = 79, element = Element.Earth },
        };

        /// <summary>
        /// Zones open with the dragon's stage (zone N needs stage N). Enemy level sits just below the
        /// first level of that stage so a fresh dragon farms its newest zone safely (≥95% normal wins).
        /// </summary>
        public static bool IsZoneOpen(PlayerSave save, int zone) => zone >= 1 && zone <= Zones.Length && zone <= save.dragon.stage;

        public static HuntZone Zone(int zone) => Zones[Math.Max(1, Math.Min(Zones.Length, zone)) - 1];

        public static double HuntBonus(PlayerSave save)
        {
            var loadout = GameActions.ToLoadout(save);
            int supports = Math.Min(loadout.supportRiders.Count, DragonRules.SupportRiderSlots(save.dragon.stage));
            return RiderRules.TotalHuntBonus(loadout.mainRider, loadout.mainRiderState,
                loadout.supportRiders.GetRange(0, supports), loadout.supportRiderStates.GetRange(0, supports));
        }

        /// <summary>Monsters for one run. Normals come in a pack of 3; rares are alone, tougher and flee.</summary>
        public static List<EnemySpec> Enemies(int zone, EncounterKind kind)
        {
            var z = Zone(zone);
            int stage = Math.Min(DragonRules.MaxStage, (z.level - 1) / 20 + 1);
            var tuning = new SpireTuning();
            double hp = DragonRules.BaseStat(StatType.HP, z.level, stage) * tuning.normalHp;
            double atk = DragonRules.BaseStat(StatType.ATK, z.level, stage) * tuning.normalAtk;
            double def = DragonRules.BaseStat(StatType.DEF, z.level, stage) * tuning.defense;

            var list = new List<EnemySpec>();
            if (kind == EncounterKind.Normal)
            {
                for (int i = 0; i < 3; i++)
                    list.Add(new EnemySpec { id = "ash wolf", level = z.level, element = z.element, hp = hp, attack = atk, defense = def });
                return list;
            }
            bool epic = kind == EncounterKind.Epic;
            list.Add(new EnemySpec
            {
                id = epic ? "shadow dragon" : "gold lizard",
                level = z.level,
                element = z.element,
                hp = hp * 3 * HuntRules.MonsterHpMultiplier(kind),
                attack = atk * (epic ? 1.5 : 1.0),
                defense = def,
                escapeSeconds = epic ? HuntRules.EpicEscapeSeconds : HuntRules.RareEscapeSeconds,
            });
            return list;
        }

        /// <summary>Spends stamina and rolls what this run meets. Returns false without stamina.</summary>
        public static bool BeginRun(PlayerSave save, DateTime utcNow, IRandom random, out EncounterKind kind)
        {
            kind = EncounterKind.Normal;
            if (!FeatureUnlocks.IsUnlocked(save, Feature.Hunt)) return false;
            if (!OfflineProgress.SpendHuntStamina(save.hunt, utcNow)) return false;
            kind = HuntRules.RollEncounter(random, HuntBonus(save));
            return true;
        }

        public static BattleSetup BuildBattle(PlayerSave save, int zone, EncounterKind kind, int seed)
        {
            var loadout = GameActions.ToLoadout(save);
            var heart = save.FindEquipment(save.dragon.equipped[(int)EquipSlot.HeartCore]);
            var setup = new BattleSetup
            {
                seed = seed,
                dragonLevel = save.dragon.level,
                dragonElement = GameActions.HeartElement(heart),
                dragon = DragonCombatStats.FromSheet(LoadoutCalculator.Compute(loadout)),
                traits = LoadoutCalculator.BattleTraits(loadout),
                command = loadout.mainRider != null ? RiderCatalog.Command(loadout.mainRider.commandSkillId) : null,
                timeLimitSeconds = 90,
                escapeDelaySeconds = loadout.mainRider != null ? loadout.mainRider.huntEscapeDelaySeconds : 0,
            };
            setup.skills.AddRange(SkillLoadoutRules.Resolve(save.dragon.skillIds));
            setup.enemies.AddRange(Enemies(zone, kind));
            return setup;
        }

        /// <summary>Pays out a finished run. Escaped or lost rares give the consolation; losses to normals give nothing.</summary>
        public static HuntRun ApplyResult(PlayerSave save, int zone, EncounterKind kind, BattleOutcome outcome, IRandom random)
        {
            var run = new HuntRun { kind = kind, outcome = outcome };
            bool won = outcome == BattleOutcome.Victory;
            if (!won && kind == EncounterKind.Normal) return run;

            run.rewards = HuntRules.Rewards(kind, zone, won);
            var w = save.wallet;
            w.gold += run.rewards.gold;
            w.forgeStones += run.rewards.forgeStones;
            w.dragonEssence += run.rewards.dragonEssence;
            w.elementCrystals[(int)Zone(zone).element] += run.rewards.elementCrystals;
            w.refineStones += run.rewards.refineStones;

            if (won && run.rewards.dropsGear)
            {
                var rarity = HuntRules.RollGearRarity(kind, random);
                run.gear = EquipmentGenerator.CreateRandom(random, rarity, save.dragon.stage, "eq" + (++save.nextItemId).ToString("D5"));
                save.equipment.Add(run.gear);
            }
            if (won && kind == EncounterKind.Epic)
            {
                if (random.NextDouble() < 0.75) { w.pactSigils += 1; run.bonus = "1 pact sigil"; }
                else { w.soulSeals[(int)RiderRank.SR] += 1; run.bonus = "1 SR soul seal"; }
            }

            var rider = RiderCatalog.Find(save.dragon.mainRiderId);
            if (won && kind != EncounterKind.Normal && rider != null && rider.legacyHuntRefundChance > 0
                && random.NextDouble() < rider.legacyHuntRefundChance)
            {
                save.hunt.stamina = Math.Min(HuntRules.MaxStamina, save.hunt.stamina + HuntRules.StaminaCost);
                run.staminaRefunded = true;
            }
            return run;
        }

        /// <summary>
        /// Auto hunts up to <paramref name="count"/> times, fighting each run with the real battle in
        /// auto mode; stops early on a rare so the player can choose to fight it themselves next time.
        /// </summary>
        public static List<HuntRun> AutoHunt(PlayerSave save, int zone, int count, DateTime utcNow, IRandom random)
        {
            var runs = new List<HuntRun>();
            for (int i = 0; i < count; i++)
            {
                EncounterKind kind;
                if (!BeginRun(save, utcNow, random, out kind)) break;
                var setup = BuildBattle(save, zone, kind, random.Next(int.MaxValue));
                setup.auto = true;
                var outcome = new BattleSimulation(setup).RunToEnd();
                runs.Add(ApplyResult(save, zone, kind, outcome, random));
                if (kind != EncounterKind.Normal) break;
            }
            return runs;
        }
    }
}
