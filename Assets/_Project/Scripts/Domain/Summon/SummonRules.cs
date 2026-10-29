using System;
using System.Collections.Generic;

namespace P2D.Domain
{
    [Serializable]
    public sealed class SummonPityState
    {
        /// <summary>Pulls since the last SSR or UR.</summary>
        public int sinceSsr;

        /// <summary>Pulls since the last UR. Carries over to the next banner of the same kind.</summary>
        public int sinceUr;

        /// <summary>True after losing a rate-up 50/50: the next UR is the featured rider.</summary>
        public bool featuredGuaranteed;

        /// <summary>Server day of the last free daily pull (GDD §2 daily loop); 0 = never.</summary>
        public int freeSummonDay;
    }

    public struct SummonResult
    {
        public RiderRank rank;
        public bool featured;
    }

    /// <summary>Pact Summon rates and pity — docs/Balance.md §6.</summary>
    public static class SummonRules
    {
        public const double UrRate = 0.01;
        public const double SsrRate = 0.07;
        public const double SrRate = 0.32;
        public const int SsrPity = 40;
        public const int UrSoftPityStart = 90;
        public const double UrSoftPityStep = 0.02;
        public const int UrHardPity = 120;

        /// <summary>UR chance for the upcoming pull given pulls already made since the last UR.</summary>
        public static double UrChance(int sinceUr)
        {
            int pullNumber = sinceUr + 1;
            if (pullNumber >= UrHardPity) return 1.0;
            if (pullNumber < UrSoftPityStart) return UrRate;
            return Math.Min(1.0, UrRate + (pullNumber - UrSoftPityStart + 1) * UrSoftPityStep);
        }

        public static SummonResult Pull(IRandom random, SummonPityState pity, bool forceSrOrBetter = false)
        {
            double roll = random.NextDouble();
            double ur = UrChance(pity.sinceUr);
            RiderRank rank;
            if (roll < ur) rank = RiderRank.UR;
            else if (pity.sinceSsr + 1 >= SsrPity || roll < ur + SsrRate) rank = RiderRank.SSR;
            else if (forceSrOrBetter || roll < ur + SsrRate + SrRate) rank = RiderRank.SR;
            else rank = RiderRank.R;

            var result = new SummonResult { rank = rank };
            if (rank == RiderRank.UR)
            {
                result.featured = pity.featuredGuaranteed || random.NextDouble() < 0.5;
                pity.featuredGuaranteed = !result.featured;
                pity.sinceUr = 0;
                pity.sinceSsr = 0;
            }
            else if (rank == RiderRank.SSR)
            {
                pity.sinceSsr = 0;
                pity.sinceUr++;
            }
            else
            {
                pity.sinceSsr++;
                pity.sinceUr++;
            }
            return result;
        }

        /// <summary>Ten pulls; the last one is at least SR if the first nine were all R.</summary>
        public static List<SummonResult> PullTen(IRandom random, SummonPityState pity)
        {
            var results = new List<SummonResult>(10);
            bool gotSrPlus = false;
            for (int i = 0; i < 10; i++)
            {
                var r = Pull(random, pity, i == 9 && !gotSrPlus);
                if (r.rank != RiderRank.R) gotSrPlus = true;
                results.Add(r);
            }
            return results;
        }
    }
}
