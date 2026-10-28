using System;

namespace P2D.Domain
{
    /// <summary>Main stats, enhance, reforge and salvage — docs/Balance.md §7.</summary>
    public static class EquipmentRules
    {
        public const int MaxEnhance = 15;
        public const int MaxSubStats = 4;
        public const int GoldPerStone = 200;
        public const double SalvageRefund = 0.8;

        static readonly double[] FlatAtk = { 30, 50, 80, 120, 170, 230 };
        static readonly double[] FlatDef = { 15, 25, 40, 60, 85, 115 };
        static readonly double[] PercentStat = { 0.03, 0.04, 0.06, 0.08, 0.10, 0.13 };
        static readonly double[] Spd = { 3, 4, 6, 8, 10, 12 };
        static readonly double[] CostMultiplier = { 0.5, 0.75, 1.0, 1.5, 2.0, 3.0 };
        static readonly int[] ReforgePathEssence = { 1, 3, 5, 10, 20, 40 };
        static readonly int[] SalvagePathEssence = { 0, 0, 1, 2, 5, 10 };

        /// <summary>Base value of a main stat at +0.</summary>
        public static double MainBase(StatType stat, ModType type, Rarity rarity)
        {
            int r = (int)rarity;
            if (type == ModType.Flat)
            {
                switch (stat)
                {
                    case StatType.ATK: return FlatAtk[r];
                    case StatType.HP: return FlatAtk[r] * 10;
                    case StatType.DEF: return FlatDef[r];
                    case StatType.SPD: return Spd[r];
                }
                throw new ArgumentException("No flat main stat for " + stat);
            }
            switch (stat)
            {
                case StatType.CritRate: return PercentStat[r] * 0.5;
                case StatType.CritDmg: return PercentStat[r] * 1.5;
                default: return PercentStat[r];
            }
        }

        public static double MainStatAt(double mainBase, int enhance) => mainBase * (1 + 0.10 * enhance);

        public static StatModifier MainModifier(EquipmentState item)
        {
            var main = item.main;
            return new StatModifier(main.stat, main.type, MainStatAt(main.value, item.enhance));
        }

        public static bool IsMilestone(int enhance) => enhance > 0 && enhance % 3 == 0;

        /// <summary>Forge stones to go from +<paramref name="level"/> to the next level.</summary>
        public static int StoneCost(int level, Rarity rarity)
        {
            if (level < 0 || level >= MaxEnhance) throw new ArgumentOutOfRangeException(nameof(level));
            double baseCost = Math.Ceiling(5 * Math.Pow(1.25, level));
            return (int)Math.Ceiling(baseCost * CostMultiplier[(int)rarity]);
        }

        public static long GoldCost(int level, Rarity rarity) => (long)StoneCost(level, rarity) * GoldPerStone;

        public static int TotalStonesInvested(int enhance, Rarity rarity)
        {
            int total = 0;
            for (int n = 0; n < enhance; n++) total += StoneCost(n, rarity);
            return total;
        }

        /// <summary>
        /// Enhances up to <paramref name="target"/> while stones last (enhance never fails).
        /// Returns the reached level; <paramref name="stones"/> is reduced by what was spent.
        /// </summary>
        public static int EnhanceTowards(int current, int target, Rarity rarity, ref long stones)
        {
            int capped = Math.Min(target, MaxEnhance);
            while (current < capped)
            {
                int cost = StoneCost(current, rarity);
                if (stones < cost) break;
                stones -= cost;
                current++;
            }
            return current;
        }

        public static int NextMilestone(int enhance) => Math.Min(MaxEnhance, (enhance / 3 + 1) * 3);

        public static int ReforgeCrystalCost(int enhance, Rarity rarity) =>
            (int)Math.Ceiling(0.3 * TotalStonesInvested(enhance, rarity) / 5.0);

        public static int ReforgePathEssenceCost(Rarity rarity) => ReforgePathEssence[(int)rarity];

        public static int SalvageStones(int enhance, Rarity rarity) =>
            (int)Math.Floor(SalvageRefund * TotalStonesInvested(enhance, rarity));

        public static int SalvageEssence(Rarity rarity) => SalvagePathEssence[(int)rarity];
    }
}
