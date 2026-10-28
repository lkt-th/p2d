using System;
using System.Collections.Generic;

namespace P2D.Domain
{
    /// <summary>Growth Path affinity and tiers — docs/Balance.md §3.</summary>
    public static class AffinityRules
    {
        public static readonly int[] TierThresholds = { 20, 60, 120, 200 };
        public const int ResonanceThreshold = 60;
        public const int LegacyResonanceThreshold = 40;

        static readonly int[] RarityBase = { 4, 6, 9, 13, 18, 25 };

        /// <summary>floor(RarityBase × (1 + 0.05 × enhance)), in integer math to avoid float drift.</summary>
        public static int ForEquipment(Rarity rarity, int enhance) =>
            RarityBase[(int)rarity] * (100 + 5 * enhance) / 100;

        public static int Tier(int affinity)
        {
            int tier = 0;
            foreach (int threshold in TierThresholds)
                if (affinity >= threshold) tier++;
            return tier;
        }

        public static Dictionary<GrowthPath, int> Compute(
            IEnumerable<EquipmentState> equipped, RiderProfile mainRider, RiderState mainRiderState)
        {
            var result = new Dictionary<GrowthPath, int>();
            foreach (GrowthPath path in Enum.GetValues(typeof(GrowthPath)))
                result[path] = 0;

            foreach (var item in equipped)
                if (item != null)
                    result[item.path] += ForEquipment(item.rarity, item.enhance);

            if (mainRider != null && mainRider.hasPath)
                result[mainRider.path] += RiderRules.Affinity(mainRider, mainRiderState);

            return result;
        }

        /// <summary>Paths that reached the resonance threshold (a pair of them grants a resonance bonus).</summary>
        public static List<GrowthPath> ResonantPaths(Dictionary<GrowthPath, int> affinity, bool legacyLowersThreshold)
        {
            int threshold = legacyLowersThreshold ? LegacyResonanceThreshold : ResonanceThreshold;
            var paths = new List<GrowthPath>();
            foreach (var pair in affinity)
                if (pair.Value >= threshold) paths.Add(pair.Key);
            return paths;
        }

        public static GrowthPath Dominant(Dictionary<GrowthPath, int> affinity)
        {
            var best = GrowthPath.Fury;
            int bestValue = int.MinValue;
            foreach (GrowthPath path in Enum.GetValues(typeof(GrowthPath)))
            {
                if (affinity[path] > bestValue)
                {
                    best = path;
                    bestValue = affinity[path];
                }
            }
            return best;
        }
    }
}
