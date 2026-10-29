using System;

namespace P2D.Domain
{
    public sealed class SweepResult
    {
        public bool ok;
        public string message = "";
        public FloorRewards resources;
        public int refineStones;
        public int ancientScales;
        public int pathEssence;
        public EquipmentState gear;
        /// <summary>True when a guardian/boss floor paid its weekly claim (Balance §5.4).</summary>
        public bool bossClaim;
    }

    public sealed class ChestReward
    {
        public bool ok;
        public string message = "";
        public string tier = "";
        public long gold;
        public int forgeStones;
        public int essence;
        public int elementCrystals;
        public int pactSigils;
        public int ancientScales;
        public int ssrSeals;
        public EquipmentState gear;
    }

    /// <summary>
    /// Weekly spire actions besides fighting (GDD §6, Balance §5.4–5.6): sweeping a cleared floor with
    /// a soul torch, the guardian/boss weekly claims, and opening last week's chest.
    /// </summary>
    public static class SpireActions
    {
        public const double SweepShare = 0.5;
        public const double SweepGearChance = 0.2;

        /// <summary>Index into <see cref="SpireSave.bossClaims"/> for floors 10 / 20 / 30, or -1.</summary>
        public static int BossIndex(int floor) => floor == 10 ? 0 : floor == 20 ? 1 : floor == SpireRules.TopFloor ? 2 : -1;

        public static int ClaimsLeft(PlayerSave save, int floor)
        {
            int i = BossIndex(floor);
            return i < 0 ? 0 : Math.Max(0, SpireRules.BossClaimsPerWeek - save.spire.bossClaims[i]);
        }

        /// <summary>Counts a guardian/boss reward against the week's three claims (first clears count too).</summary>
        public static void CountClaim(PlayerSave save, int floor)
        {
            int i = BossIndex(floor);
            if (i >= 0) save.spire.bossClaims[i]++;
        }

        public static bool CanSweep(PlayerSave save, int floor) =>
            FeatureUnlocks.IsUnlocked(save, Feature.Spire) && floor >= 1 && floor <= save.spire.highestFloor && save.spire.torches > 0;

        /// <summary>
        /// Spends a torch on a cleared floor: half the first-clear materials (no sigils) and a 20% item
        /// chance. A guardian/boss floor with a weekly claim left pays its boss rewards and a set item instead.
        /// </summary>
        public static SweepResult Sweep(PlayerSave save, int floor, Element themeElement, IRandom random)
        {
            var result = new SweepResult();
            if (!FeatureUnlocks.IsUnlocked(save, Feature.Spire)) { result.message = FeatureUnlocks.Locked(Feature.Spire).message; return result; }
            if (floor < 1 || floor > save.spire.highestFloor) { result.message = Loc.T("Clear the floor first"); return result; }
            if (save.spire.torches <= 0) { result.message = Loc.T("No soul torches left (3 more tomorrow)"); return result; }

            save.spire.torches--;
            var full = FeaturedRiders.Boost(save, SpireRules.FirstClearRewards(floor, save.spire.bracket));
            result.resources = new FloorRewards
            {
                gold = (long)Math.Floor(full.gold * SweepShare),
                forgeStones = (int)Math.Floor(full.forgeStones * SweepShare),
                dragonEssence = (int)Math.Floor(full.dragonEssence * SweepShare),
                elementCrystals = (int)Math.Floor(full.elementCrystals * SweepShare),
            };
            var w = save.wallet;
            w.gold += result.resources.gold;
            w.forgeStones += result.resources.forgeStones;
            w.dragonEssence += result.resources.dragonEssence;
            w.elementCrystals[(int)themeElement] += result.resources.elementCrystals;

            bool claim = ClaimsLeft(save, floor) > 0;
            if (claim)
            {
                result.bossClaim = true;
                CountClaim(save, floor);
                if (floor == 10) result.refineStones = 3;
                if (floor == 20) { result.ancientScales = 1; result.refineStones = 5; }
                if (floor == SpireRules.TopFloor) { result.ancientScales = 2; result.pathEssence = 10; }
                w.refineStones += result.refineStones;
                w.ancientScales += result.ancientScales;
                if (result.pathEssence > 0) w.pathEssence[random.Next(4)] += result.pathEssence;
            }

            if (claim || random.NextDouble() < SweepGearChance)
            {
                var rarity = EquipmentGenerator.RollSpireRarity(random, floor, save.spire.bracket);
                result.gear = EquipmentGenerator.CreateRandom(random, rarity, save.dragon.stage, "eq" + (++save.nextItemId).ToString("D5"));
                var set = SetCatalog.ForElement(themeElement);
                if (claim && set != null) result.gear.setId = set.id;
                save.equipment.Add(result.gear);
            }

            result.ok = true;
            result.message = Loc.F("Swept floor {0}: +{1} gold, +{2} forge stones", floor, result.resources.gold, result.resources.forgeStones)
                + (result.ancientScales > 0 ? Loc.F(", +{0} ancient scales", result.ancientScales) : "")
                + (result.gear != null ? Loc.F(", new {0} {1}", Loc.Enum(result.gear.rarity), Loc.Enum(result.gear.slot)) : "");
            return result;
        }

        /// <summary>
        /// Pays the first-clear materials of the floors this week's catch-up start skipped (GDD §6.4,
        /// Balance §5.7): gold, forge stones, essence, theme crystals and sigils for floors 1..N, no items.
        /// </summary>
        public static ChestReward ClaimCatchUp(PlayerSave save, Element themeElement)
        {
            var reward = new ChestReward();
            int floors = Math.Min(save.spire.catchUpFloors, SpireRules.TopFloor);
            if (floors <= 0) { reward.message = Loc.T("No catch-up rewards waiting"); return reward; }
            for (int f = 1; f <= floors; f++)
            {
                var r = SpireRules.FirstClearRewards(f, save.spire.bracket);
                reward.gold += r.gold;
                reward.forgeStones += r.forgeStones;
                reward.elementCrystals += r.elementCrystals;
                reward.pactSigils += r.pactSigils;
                reward.essence += r.dragonEssence;
            }
            var w = save.wallet;
            w.gold += reward.gold;
            w.forgeStones += reward.forgeStones;
            w.dragonEssence += reward.essence;
            w.elementCrystals[(int)themeElement] += reward.elementCrystals;
            w.pactSigils += reward.pactSigils;
            save.spire.catchUpFloors = 0;
            reward.ok = true;
            reward.message = Loc.F("Catch-up floors 1–{0}: +{1} gold, +{2} forge stones, +{3} sigils", floors, reward.gold, reward.forgeStones, reward.pactSigils);
            return reward;
        }

        /// <summary>Chest tier name for last week's best floor (GDD §6 table).</summary>
        public static string ChestTier(int floor) =>
            floor >= SpireRules.TopFloor ? "Dragon chest" : floor >= 20 ? "Gold chest" : floor >= 10 ? "Silver chest" : "Bronze chest";

        /// <summary>
        /// Opens the chest earned at the last weekly reset (Balance §5.6). Materials scale with the floor
        /// reached; silver adds crystals and sigils, gold a scale and an Epic set item, the dragon chest
        /// three scales, a Legendary set item and an SSR soul seal.
        /// </summary>
        public static ChestReward OpenWeeklyChest(PlayerSave save, Element lastWeekElement, IRandom random)
        {
            var chest = new ChestReward();
            int floor = save.spire.pendingChestFloor;
            if (floor <= 0) { chest.message = Loc.T("No weekly chest to open"); return chest; }

            int b = Math.Max(1, save.spire.bracket);
            chest.tier = ChestTier(floor);
            chest.gold = 300L * floor * b;
            chest.forgeStones = floor * b;
            if (floor >= 10)
            {
                chest.elementCrystals = 5 + floor / 2;
                chest.pactSigils = 2;
            }
            if (floor >= 20) chest.ancientScales = floor >= SpireRules.TopFloor ? 3 : 1;
            if (floor >= SpireRules.TopFloor) chest.ssrSeals = 1;

            var w = save.wallet;
            w.gold += chest.gold;
            w.forgeStones += chest.forgeStones;
            w.elementCrystals[(int)lastWeekElement] += chest.elementCrystals;
            w.pactSigils += chest.pactSigils;
            w.ancientScales += chest.ancientScales;
            w.soulSeals[(int)RiderRank.SSR] += chest.ssrSeals;

            if (floor >= 20)
            {
                var rarity = floor >= SpireRules.TopFloor ? Rarity.Legendary : Rarity.Epic;
                chest.gear = EquipmentGenerator.CreateRandom(random, rarity, save.dragon.stage, "eq" + (++save.nextItemId).ToString("D5"));
                var set = SetCatalog.ForElement(lastWeekElement);
                if (set != null) chest.gear.setId = set.id;
                save.equipment.Add(chest.gear);
            }

            save.spire.pendingChestFloor = 0;
            chest.ok = true;
            chest.message = Loc.F("{0}: +{1} gold, +{2} forge stones", Loc.T(chest.tier), chest.gold, chest.forgeStones)
                + (chest.ancientScales > 0 ? Loc.F(", +{0} ancient scales", chest.ancientScales) : "")
                + (chest.gear != null ? Loc.F(", new {0} {1}", Loc.Enum(chest.gear.rarity), Loc.Enum(chest.gear.slot)) : "");
            return chest;
        }
    }
}
