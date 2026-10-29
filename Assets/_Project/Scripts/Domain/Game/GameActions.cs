using System;
using System.Collections.Generic;

namespace P2D.Domain
{
    public struct ActionResult
    {
        public bool ok;
        public string message;

        public static ActionResult Ok(string message = "") => new ActionResult { ok = true, message = message };
        public static ActionResult Fail(string message) => new ActionResult { ok = false, message = message };
    }

    public sealed class SpireReward
    {
        public bool firstClear;
        public FloorRewards resources;
        public EquipmentState gear;
    }

    public struct SummonOutcome
    {
        public string riderId;
        public RiderRank rank;
        public bool isNew;
        public int shards;
    }

    /// <summary>
    /// Everything a player can do from the menus, applied to a PlayerSave. Pure rules: the
    /// client calls these for instant feedback and Cloud Code re-runs the same calls to stay
    /// authoritative. Each returns why it failed instead of throwing.
    /// </summary>
    public static class GameActions
    {
        public const int ExpPerEssence = 100;
        public const int GoldPerExp = 10;
        public const double SetDropChanceHighFloors = 0.30;
        static readonly long[] StarUpGoldR = { 5000, 10000, 20000, 40000 };
        static readonly int[] RankGoldMultiplier = { 1, 2, 5, 10 };

        // ---- setup ---------------------------------------------------------------------

        /// <summary>First-launch resources and three common items so the loop can start.</summary>
        public static void EnsureStarterKit(PlayerSave save, IRandom random)
        {
            if (save.equipment.Count > 0) return;
            var starter = new[] { EquipSlot.HeartCore, EquipSlot.Claw, EquipSlot.Chest };
            foreach (var slot in starter)
            {
                var item = EquipmentGenerator.Create(random, Rarity.Common, slot, GrowthPath.Fury, NewUid(save));
                save.equipment.Add(item);
                save.dragon.equipped[(int)slot] = item.uid;
            }
            save.wallet.gold += 20000;
            save.wallet.forgeStones += 100;
            save.wallet.dragonEssence += 20;
            save.wallet.pactSigils += 10;
            save.wallet.crystals += 300;
            // Stand-ins for story chapters 1–2 until the story mode exists: the spire unlocks after
            // chapter 2, by which point the dragon is around Lv 10 (Balance §8) and has 3 scales (§1.4).
            save.wallet.ancientScales += 3;
            if (save.dragon.level < 10) save.dragon.level = 10;
            AutoSkills(save);
        }

        // ---- hatching ------------------------------------------------------------------

        /// <summary>Taps on the egg before it cracks open (the "bonding" moment, GDD §11).</summary>
        public const int HatchTaps = 3;
        public const int MaxNameLength = 12;

        /// <summary>Opens the egg and names the dragon. The name is trimmed; empty keeps the default.</summary>
        public static ActionResult Hatch(PlayerSave save, string name)
        {
            if (save.dragon.hatched) return ActionResult.Fail(Loc.T("The egg has already hatched"));
            name = (name ?? "").Trim();
            if (name.Length > MaxNameLength) return ActionResult.Fail(Loc.F("Names are up to {0} characters", MaxNameLength));
            foreach (char c in name)
                if (char.IsControl(c)) return ActionResult.Fail(Loc.T("That name has a character we can't show"));
            if (name.Length > 0) save.dragon.name = name;
            save.dragon.hatched = true;
            return ActionResult.Ok(Loc.F("{0} hatched!", save.dragon.name));
        }

        static string NewUid(PlayerSave save) => "eq" + (++save.nextItemId).ToString("D5");

        // ---- loadout -------------------------------------------------------------------

        public static Loadout ToLoadout(PlayerSave save)
        {
            var loadout = new Loadout { level = save.dragon.level, stage = save.dragon.stage };
            foreach (var uid in save.dragon.equipped)
            {
                var item = save.FindEquipment(uid);
                if (item != null) loadout.equipped.Add(item);
            }
            var main = RiderCatalog.Find(save.dragon.mainRiderId);
            var mainState = save.FindRider(save.dragon.mainRiderId);
            if (main != null && mainState != null)
            {
                loadout.mainRider = main;
                loadout.mainRiderState = mainState;
            }
            foreach (var id in save.dragon.supportRiderIds)
            {
                var profile = RiderCatalog.Find(id);
                var state = save.FindRider(id);
                if (profile == null || state == null) continue;
                loadout.supportRiders.Add(profile);
                loadout.supportRiderStates.Add(state);
            }
            return loadout;
        }

        public static ActionResult Equip(PlayerSave save, string uid)
        {
            var item = save.FindEquipment(uid);
            if (item == null) return ActionResult.Fail(Loc.T("Item not found"));
            if (!DragonRules.IsSlotUnlocked(item.slot, save.dragon.stage))
                return ActionResult.Fail(Loc.F("{0} unlocks at stage {1}", Loc.Enum(item.slot), DragonRules.UnlockStage(item.slot)));
            save.dragon.equipped[(int)item.slot] = uid;
            AutoSkillsIfInvalid(save);
            return ActionResult.Ok();
        }

        /// <summary>Fills the skill slots with the strongest unlocked skills (highest tier first, one ultimate).</summary>
        public static void AutoSkills(PlayerSave save)
        {
            var affinity = LoadoutCalculator.Affinity(ToLoadout(save));
            var available = SkillLoadoutRules.Available(affinity);
            available.Sort((a, b) => b.tier != a.tier ? b.tier.CompareTo(a.tier) : string.CompareOrdinal(a.id, b.id));
            int slots = DragonRules.SkillSlotCount(save.dragon.stage);
            var chosen = new List<string>();
            bool hasUltimate = false;
            foreach (var s in available)
            {
                if (chosen.Count >= slots) break;
                if (s.IsUltimate && hasUltimate) continue;
                hasUltimate |= s.IsUltimate;
                chosen.Add(s.id);
            }
            for (int i = 0; i < save.dragon.skillIds.Length; i++)
                save.dragon.skillIds[i] = i < chosen.Count ? chosen[i] : "";
        }

        static void AutoSkillsIfInvalid(PlayerSave save)
        {
            var affinity = LoadoutCalculator.Affinity(ToLoadout(save));
            bool empty = true;
            foreach (var id in save.dragon.skillIds) if (!string.IsNullOrEmpty(id)) empty = false;
            if (empty || SkillLoadoutRules.Validate(save.dragon.skillIds, affinity, save.dragon.stage) != null)
                AutoSkills(save);
        }

        // ---- upgrades ------------------------------------------------------------------

        public static ActionResult Enhance(PlayerSave save, string uid, IRandom random)
        {
            if (!FeatureUnlocks.IsUnlocked(save, Feature.Enhance)) return FeatureUnlocks.Locked(Feature.Enhance);
            var item = save.FindEquipment(uid);
            if (item == null) return ActionResult.Fail(Loc.T("Item not found"));
            if (item.enhance >= EquipmentRules.MaxEnhance) return ActionResult.Fail(Loc.T("Already +15"));
            int stones = EquipmentRules.StoneCost(item.enhance, item.rarity);
            long gold = EquipmentRules.GoldCost(item.enhance, item.rarity);
            if (save.wallet.forgeStones < stones) return ActionResult.Fail(Loc.F("Need {0} forge stones", stones));
            if (save.wallet.gold < gold) return ActionResult.Fail(Loc.F("Need {0} gold", gold));

            save.wallet.forgeStones -= stones;
            save.wallet.gold -= gold;
            item.enhance++;
            if (EquipmentRules.IsMilestone(item.enhance)) EquipmentGenerator.ApplyMilestone(random, item);
            AutoSkillsIfInvalid(save);
            return ActionResult.Ok("+" + item.enhance);
        }

        /// <summary>Crystals needed to reforge: ceil(0.3 × invested forge stones / 5), at least 1.</summary>
        public static int ReforgeCrystalCost(EquipmentState item) => Math.Max(1, EquipmentRules.ReforgeCrystalCost(item.enhance, item.rarity));

        static int TotalCrystals(WalletSave w)
        {
            int total = 0;
            foreach (int c in w.elementCrystals) total += c;
            return total;
        }

        /// <summary>
        /// Path Reforge: moves the item to another Growth Path and keeps its enhance level.
        /// Costs element crystals (any element, largest piles first) and essence of the target path.
        /// </summary>
        public static ActionResult Reforge(PlayerSave save, string uid, GrowthPath target)
        {
            if (!FeatureUnlocks.IsUnlocked(save, Feature.Workshop)) return FeatureUnlocks.Locked(Feature.Workshop);
            var item = save.FindEquipment(uid);
            if (item == null) return ActionResult.Fail(Loc.T("Item not found"));
            if (item.path == target) return ActionResult.Fail(Loc.F("Already {0}", Loc.Enum(target)));
            int crystals = ReforgeCrystalCost(item);
            int essence = EquipmentRules.ReforgePathEssenceCost(item.rarity);
            var w = save.wallet;
            if (TotalCrystals(w) < crystals) return ActionResult.Fail(Loc.F("Need {0} element crystals", crystals));
            if (w.pathEssence[(int)target] < essence) return ActionResult.Fail(Loc.F("Need {0} {1} essence", essence, Loc.Enum(target)));

            int left = crystals;
            while (left > 0)
            {
                int biggest = 0;
                for (int i = 1; i < w.elementCrystals.Length; i++)
                    if (w.elementCrystals[i] > w.elementCrystals[biggest]) biggest = i;
                int take = Math.Min(left, w.elementCrystals[biggest]);
                w.elementCrystals[biggest] -= take;
                left -= take;
            }
            w.pathEssence[(int)target] -= essence;
            item.path = target;
            AutoSkillsIfInvalid(save);
            return ActionResult.Ok(Loc.F("Reforged to {0} (+{1} kept)", Loc.Enum(target), item.enhance));
        }

        /// <summary>Refine: rerolls one sub-stat; the others are kept.</summary>
        public static ActionResult Refine(PlayerSave save, string uid, int subIndex, IRandom random)
        {
            if (!FeatureUnlocks.IsUnlocked(save, Feature.Workshop)) return FeatureUnlocks.Locked(Feature.Workshop);
            var item = save.FindEquipment(uid);
            if (item == null) return ActionResult.Fail(Loc.T("Item not found"));
            if (item.subs == null || subIndex < 0 || subIndex >= item.subs.Length) return ActionResult.Fail(Loc.T("No such sub-stat"));
            int stones = EquipmentRules.RefineCost(item.rarity);
            long gold = EquipmentRules.RefineGold(item.rarity);
            if (save.wallet.refineStones < stones) return ActionResult.Fail(Loc.F("Need {0} refine stones", stones));
            if (save.wallet.gold < gold) return ActionResult.Fail(Loc.F("Need {0} gold", gold));

            save.wallet.refineStones -= stones;
            save.wallet.gold -= gold;
            EquipmentGenerator.RerollSub(random, item, subIndex);
            return ActionResult.Ok(Loc.T("Refined"));
        }

        public static ActionResult Salvage(PlayerSave save, string uid)
        {
            var item = save.FindEquipment(uid);
            if (item == null) return ActionResult.Fail(Loc.T("Item not found"));
            if (Array.IndexOf(save.dragon.equipped, uid) >= 0) return ActionResult.Fail(Loc.T("Unequip it first"));
            save.wallet.forgeStones += EquipmentRules.SalvageStones(item.enhance, item.rarity);
            save.wallet.pathEssence[(int)item.path] += EquipmentRules.SalvageEssence(item.rarity);
            save.equipment.Remove(item);
            return ActionResult.Ok();
        }

        /// <summary>Feeds Dragon Essence (100 EXP and 1,000 gold each) up to the stage's level cap.</summary>
        public static ActionResult FeedEssence(PlayerSave save, int count)
        {
            var d = save.dragon;
            int cap = DragonRules.LevelCap(d.stage);
            if (d.level >= cap) return ActionResult.Fail(d.stage < DragonRules.MaxStage ? Loc.T("Level cap — evolve first") : Loc.T("Max level"));
            int fed = 0;
            while (fed < count && save.wallet.dragonEssence > 0 && save.wallet.gold >= ExpPerEssence * GoldPerExp && d.level < cap)
            {
                save.wallet.dragonEssence--;
                save.wallet.gold -= ExpPerEssence * GoldPerExp;
                d.exp += ExpPerEssence;
                fed++;
                while (d.level < cap && d.exp >= DragonRules.ExpToNext(d.level))
                {
                    d.exp -= DragonRules.ExpToNext(d.level);
                    d.level++;
                }
            }
            if (fed == 0) return ActionResult.Fail(Loc.T("Need dragon essence and gold"));
            if (d.level >= cap) d.exp = 0;
            return ActionResult.Ok(Loc.F("Lv {0}", d.level));
        }

        public static ActionResult Evolve(PlayerSave save)
        {
            var d = save.dragon;
            if (!DragonRules.CanEvolve(d.level, d.stage)) return ActionResult.Fail(Loc.F("Reach Lv {0} first", DragonRules.LevelCap(d.stage)));
            var cost = DragonRules.EvolveCostFrom(d.stage);
            var w = save.wallet;
            var heart = save.FindEquipment(d.equipped[(int)EquipSlot.HeartCore]);
            int crystalIndex = (int)HeartElement(heart);
            if (w.ancientScales < cost.ancientScales) return ActionResult.Fail(Loc.F("Need {0} ancient scales", cost.ancientScales));
            if (w.gold < cost.gold) return ActionResult.Fail(Loc.F("Need {0} gold", cost.gold));
            if (w.elementCrystals[crystalIndex] < cost.elementCrystals) return ActionResult.Fail(Loc.F("Need {0} element crystals", cost.elementCrystals));
            if (w.pathEssence[(int)GrowthPath.Fury] + w.pathEssence[1] + w.pathEssence[2] + w.pathEssence[3] < cost.pathEssence)
                return ActionResult.Fail(Loc.F("Need {0} path essence", cost.pathEssence));

            w.ancientScales -= cost.ancientScales;
            w.gold -= cost.gold;
            w.elementCrystals[crystalIndex] -= cost.elementCrystals;
            int essence = cost.pathEssence;
            for (int i = 0; i < w.pathEssence.Length && essence > 0; i++)
            {
                int take = Math.Min(essence, w.pathEssence[i]);
                w.pathEssence[i] -= take;
                essence -= take;
            }
            d.stage++;
            d.level++;
            d.exp = 0;
            AutoSkills(save);
            return ActionResult.Ok(Loc.F("Stage {0}", d.stage));
        }

        /// <summary>The breath element; the Heart Core decides it (Water until heart elements are authored).</summary>
        public static Element HeartElement(EquipmentState heart) => Element.Water;

        // ---- riders --------------------------------------------------------------------

        public static ActionResult SetMainRider(PlayerSave save, string riderId)
        {
            if (!FeatureUnlocks.IsUnlocked(save, Feature.Riders)) return FeatureUnlocks.Locked(Feature.Riders);
            if (save.FindRider(riderId) == null) return ActionResult.Fail(Loc.T("Rider not owned"));
            for (int i = 0; i < save.dragon.supportRiderIds.Length; i++)
                if (save.dragon.supportRiderIds[i] == riderId) save.dragon.supportRiderIds[i] = "";
            save.dragon.mainRiderId = riderId;
            AutoSkillsIfInvalid(save);
            return ActionResult.Ok();
        }

        public static long StarUpGold(RiderRank rank, int targetStar) =>
            StarUpGoldR[Math.Max(2, Math.Min(5, targetStar)) - 2] * RankGoldMultiplier[(int)rank];

        public static ActionResult StarUp(PlayerSave save, string riderId)
        {
            if (!FeatureUnlocks.IsUnlocked(save, Feature.Riders)) return FeatureUnlocks.Locked(Feature.Riders);
            var state = save.FindRider(riderId);
            var profile = RiderCatalog.Find(riderId);
            if (state == null || profile == null) return ActionResult.Fail(Loc.T("Rider not owned"));
            if (state.star >= RiderRules.MaxStar) return ActionResult.Fail(Loc.T("Already 5★"));
            int seals = save.wallet.soulSeals[(int)profile.rank];
            if (!RiderRules.CanStarUp(state, seals))
                return ActionResult.Fail(Loc.F("Need {0} copies (seals cover half)", RiderRules.CopiesNeeded(state.star + 1)));
            long gold = StarUpGold(profile.rank, state.star + 1);
            if (save.wallet.gold < gold) return ActionResult.Fail(Loc.F("Need {0} gold", gold));

            save.wallet.gold -= gold;
            save.wallet.soulSeals[(int)profile.rank] -= RiderRules.StarUp(state, seals);
            return ActionResult.Ok(state.star + "★");
        }

        public static List<SummonOutcome> Summon(PlayerSave save, int count, IRandom random)
        {
            var results = new List<SummonOutcome>();
            if (count != 1 && count != 10) throw new ArgumentException("Summon 1 or 10");
            if (!FeatureUnlocks.IsUnlocked(save, Feature.Riders)) return results;
            if (save.wallet.pactSigils < count) return results;
            save.wallet.pactSigils -= count;

            var pulls = count == 10
                ? SummonRules.PullTen(random, save.summonPity)
                : new List<SummonResult> { SummonRules.Pull(random, save.summonPity) };

            foreach (var pull in pulls)
            {
                var pool = new List<RiderProfile>();
                foreach (var r in RiderCatalog.All) if (r.rank == pull.rank) pool.Add(r);
                var rider = pool[random.Next(pool.Count)];
                var state = save.FindRider(rider.id);
                var outcome = new SummonOutcome { riderId = rider.id, rank = rider.rank, isNew = state == null };
                if (state == null)
                {
                    save.riders.Add(new RiderState { riderId = rider.id, star = 1 });
                }
                else if (state.star >= RiderRules.MaxStar)
                {
                    outcome.shards = RiderRules.ShardsForOverflow(rider.rank);
                    save.wallet.soulShards += outcome.shards;
                }
                else
                {
                    state.copies++;
                }
                results.Add(outcome);
            }
            return results;
        }

        // ---- spire ---------------------------------------------------------------------

        public static int NextSpireFloor(PlayerSave save) => save.spire.highestFloor + 1;

        public static BattleSetup BuildSpireBattle(PlayerSave save, int floor, Element themeElement, int seed, SpireTuning tuning)
        {
            var loadout = ToLoadout(save);
            var heart = save.FindEquipment(save.dragon.equipped[(int)EquipSlot.HeartCore]);
            var setup = new BattleSetup
            {
                seed = seed,
                dragonLevel = save.dragon.level,
                dragonElement = HeartElement(heart),
                dragon = DragonCombatStats.FromSheet(LoadoutCalculator.Compute(loadout)),
                traits = LoadoutCalculator.BattleTraits(loadout),
                command = loadout.mainRider != null ? RiderCatalog.Command(loadout.mainRider.commandSkillId) : null,
                timeLimitSeconds = SpireEnemyFactory.TimeLimit(floor, tuning),
                escapeDelaySeconds = loadout.mainRider != null ? loadout.mainRider.huntEscapeDelaySeconds : 0,
            };
            setup.skills.AddRange(SkillLoadoutRules.Resolve(save.dragon.skillIds));
            setup.enemies.AddRange(SpireEnemyFactory.Floor(save.spire.bracket, floor, themeElement, tuning));
            return setup;
        }

        /// <summary>
        /// Applies a finished spire battle. Only the next unclimbed floor pays first-clear rewards
        /// (resources + one item); replays and losses give nothing (sweeps are a separate action).
        /// </summary>
        public static SpireReward ApplySpireResult(PlayerSave save, int floor, BattleOutcome outcome, Element themeElement, IRandom random)
        {
            var reward = new SpireReward();
            if (outcome != BattleOutcome.Victory || floor != NextSpireFloor(save) || !FeatureUnlocks.IsUnlocked(save, Feature.Spire)) return reward;

            reward.firstClear = true;
            reward.resources = SpireRules.FirstClearRewards(floor, save.spire.bracket);
            var w = save.wallet;
            w.gold += reward.resources.gold;
            w.forgeStones += reward.resources.forgeStones;
            w.dragonEssence += reward.resources.dragonEssence;
            w.elementCrystals[(int)themeElement] += reward.resources.elementCrystals;
            w.pactSigils += reward.resources.pactSigils;
            // Boss-tier extras (Balance §5.4), paid once per week on the first clear.
            if (floor == 10) w.refineStones += 3;
            if (floor == 20) { w.ancientScales += 1; w.refineStones += 5; }
            if (floor == SpireRules.TopFloor) { w.ancientScales += 2; w.pathEssence[random.Next(4)] += 10; }
            SpireActions.CountClaim(save, floor); // the first clear is one of the week's three boss claims

            var rarity = EquipmentGenerator.RollSpireRarity(random, floor, save.spire.bracket);
            reward.gear = EquipmentGenerator.CreateRandom(random, rarity, save.dragon.stage, NewUid(save));
            // This week's set: always from guardians and the boss, 30% from floors 21–29 (Balance §7.5).
            var set = SetCatalog.ForElement(themeElement);
            bool setFloor = SpireRules.TypeOf(floor) != FloorType.Normal || (floor > 20 && random.NextDouble() < SetDropChanceHighFloors);
            if (set != null && setFloor) reward.gear.setId = set.id;
            save.equipment.Add(reward.gear);
            save.spire.highestFloor = floor;
            return reward;
        }
    }
}
