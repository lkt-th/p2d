using System;
using System.Collections.Generic;

namespace P2D.Domain
{
    /// <summary>
    /// Two featured riders each week (GDD §6.3, Balance §5.8): SR or better, rotating with the week.
    /// Riding one as the main rider adds 20% to spire materials (first clears and sweeps; not sigils).
    /// Remote Config will pick them later; this rotation is the offline fallback.
    /// </summary>
    public static class FeaturedRiders
    {
        public const double MaterialBonus = 0.2;

        public static string[] ForWeek(int weekIndex)
        {
            var pool = new List<string>();
            foreach (var r in RiderCatalog.All) if (r.rank >= RiderRank.SR) pool.Add(r.id);
            int n = pool.Count;
            int a = ((weekIndex * 2) % n + n) % n;
            int b = (a + 1) % n;
            return new[] { pool[a], pool[b] };
        }

        public static bool IsFeatured(PlayerSave save, string riderId) =>
            !string.IsNullOrEmpty(riderId) && Array.IndexOf(ForWeek(save.spire.weekIndex), riderId) >= 0;

        public static bool RidingFeatured(PlayerSave save) => IsFeatured(save, save.dragon.mainRiderId);

        /// <summary>Spire materials with the featured-rider bonus applied when it is earned.</summary>
        public static FloorRewards Boost(PlayerSave save, FloorRewards rewards)
        {
            if (!RidingFeatured(save)) return rewards;
            double m = 1 + MaterialBonus;
            rewards.gold = (long)Math.Floor(rewards.gold * m);
            rewards.forgeStones = (int)Math.Floor(rewards.forgeStones * m);
            rewards.dragonEssence = (int)Math.Floor(rewards.dragonEssence * m);
            rewards.elementCrystals = (int)Math.Floor(rewards.elementCrystals * m);
            return rewards;
        }
    }
}
