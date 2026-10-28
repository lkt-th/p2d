using System;
using System.Collections.Generic;

namespace P2D.Domain
{
    /// <summary>Rider stars, pact buffs and hunter trait — docs/Balance.md §4.</summary>
    public static class RiderRules
    {
        public const int MaxStar = 5;
        public const double SupportRatio = 0.30;
        public const double MaxHuntBonus = 0.50;

        // index = star - 1, kept as integer percents so affinity rounding is exact
        static readonly int[] StarMultPct = { 100, 125, 150, 180, 220 };
        static readonly int[] CopiesToReach = { 0, 1, 2, 3, 4 };
        static readonly int[] ShardsPerOverflowCopy = { 5, 20, 100, 300 };

        public static double StarMultiplier(int star) => StarMultPct[ClampStar(star) - 1] / 100.0;

        public static int CopiesNeeded(int targetStar) => CopiesToReach[ClampStar(targetStar) - 1];

        /// <summary>Universal soul seals may replace up to half of a step's copies (rounded down).</summary>
        public static int MaxSeals(int targetStar) => CopiesNeeded(targetStar) / 2;

        public static bool CanStarUp(RiderState state, int sealsOwned)
        {
            if (state.star >= MaxStar) return false;
            int target = state.star + 1;
            int usableSeals = Math.Min(sealsOwned, MaxSeals(target));
            return state.copies + usableSeals >= CopiesNeeded(target);
        }

        /// <summary>
        /// Raises the star by one, spending copies first and seals for the rest.
        /// Returns the number of seals used.
        /// </summary>
        public static int StarUp(RiderState state, int sealsOwned)
        {
            if (!CanStarUp(state, sealsOwned)) throw new InvalidOperationException("Cannot star up " + state.riderId);
            int need = CopiesNeeded(state.star + 1);
            int fromCopies = Math.Min(state.copies, need);
            state.copies -= fromCopies;
            state.star++;
            return need - fromCopies;
        }

        /// <summary>Soul shards granted for a duplicate pulled after the rider is already 5★.</summary>
        public static int ShardsForOverflow(RiderRank rank) => ShardsPerOverflowCopy[(int)rank];

        public static int Affinity(RiderProfile profile, RiderState state) =>
            profile.hasPath ? profile.baseAffinity * StarMultPct[ClampStar(state.star) - 1] / 100 : 0;

        /// <summary>Pact buffs scaled by star; supports pass <see cref="SupportRatio"/>.</summary>
        public static IEnumerable<StatModifier> Buffs(RiderProfile profile, RiderState state, double ratio = 1.0)
        {
            double factor = StarMultiplier(state.star) * ratio;
            foreach (var m in profile.pactBuffs)
                yield return m.Scaled(factor);
            if (state.star >= 3)
                foreach (var m in profile.star3Trait)
                    yield return m.Scaled(factor);
        }

        public static double HuntBonus(RiderProfile profile, RiderState state, double ratio = 1.0) =>
            profile.huntBase * StarMultiplier(state.star) * ratio;

        /// <summary>Main rider at full strength plus supports at 30%, capped at +50%.</summary>
        public static double TotalHuntBonus(
            RiderProfile main, RiderState mainState,
            IList<RiderProfile> supports, IList<RiderState> supportStates)
        {
            double total = main != null ? HuntBonus(main, mainState) : 0;
            if (supports != null)
                for (int i = 0; i < supports.Count; i++)
                    if (supports[i] != null)
                        total += HuntBonus(supports[i], supportStates[i], SupportRatio);
            return Math.Min(total, MaxHuntBonus);
        }

        static int ClampStar(int star) => Math.Max(1, Math.Min(MaxStar, star));
    }
}
