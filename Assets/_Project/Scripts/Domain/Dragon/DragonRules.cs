using System;

namespace P2D.Domain
{
    public struct EvolveCost
    {
        public int ancientScales;
        public long gold;
        public int elementCrystals;
        public int pathEssence;
    }

    /// <summary>Dragon level, stage and base stats — docs/Balance.md §1.</summary>
    public static class DragonRules
    {
        public const int MaxStage = 5;
        public const int MaxLevel = 100;
        public const double BaseHP = 1200;
        public const double BaseATK = 120;
        public const double BaseDEF = 60;
        public const double GrowthPerLevel = 0.08;

        static readonly int[] LevelCaps = { 20, 40, 60, 80, 100 };
        static readonly double[] StageMultipliers = { 1.00, 1.15, 1.35, 1.60, 2.00 };
        static readonly int[] SkillSlots = { 1, 2, 3, 3, 3 };

        static readonly EvolveCost[] EvolveCosts =
        {
            new EvolveCost { ancientScales = 3, gold = 50000 },
            new EvolveCost { ancientScales = 8, gold = 200000, elementCrystals = 20 },
            new EvolveCost { ancientScales = 15, gold = 600000, elementCrystals = 50 },
            new EvolveCost { ancientScales = 25, gold = 1500000, elementCrystals = 100, pathEssence = 50 },
        };

        public static int LevelCap(int stage) => LevelCaps[ClampStage(stage) - 1];

        public static double StageMultiplier(int stage) => StageMultipliers[ClampStage(stage) - 1];

        public static int SkillSlotCount(int stage) => SkillSlots[ClampStage(stage) - 1];

        public static int SupportRiderSlots(int stage) => stage >= 4 ? 2 : 0;

        public static int UnlockStage(EquipSlot slot)
        {
            switch (slot)
            {
                case EquipSlot.Horn: return 2;
                case EquipSlot.Wing: return 3;
                case EquipSlot.Tail: return 4;
                default: return 1;
            }
        }

        public static bool IsSlotUnlocked(EquipSlot slot, int stage) => stage >= UnlockStage(slot);

        public static double BaseStat(StatType stat, int level, int stage)
        {
            double growth = (1 + GrowthPerLevel * (level - 1)) * StageMultiplier(stage);
            switch (stat)
            {
                case StatType.HP: return Math.Round(BaseHP * growth, MidpointRounding.AwayFromZero);
                case StatType.ATK: return Math.Round(BaseATK * growth, MidpointRounding.AwayFromZero);
                case StatType.DEF: return Math.Round(BaseDEF * growth, MidpointRounding.AwayFromZero);
                case StatType.SPD: return 100 + (level - 1) / 5;
                case StatType.CritRate: return 0.05;
                case StatType.CritDmg: return 1.5;
                default: return 0;
            }
        }

        public static StatSheet BaseSheet(int level, int stage)
        {
            var sheet = new StatSheet();
            foreach (StatType stat in Enum.GetValues(typeof(StatType)))
                sheet.SetBase(stat, BaseStat(stat, level, stage));
            return sheet;
        }

        public static long ExpToNext(int level) =>
            (long)Math.Round(100 * Math.Pow(level, 1.6), MidpointRounding.AwayFromZero);

        public static bool CanEvolve(int level, int stage) => stage < MaxStage && level >= LevelCap(stage);

        /// <summary>Cost to evolve from <paramref name="fromStage"/> to the next stage.</summary>
        public static EvolveCost EvolveCostFrom(int fromStage)
        {
            if (fromStage < 1 || fromStage >= MaxStage)
                throw new ArgumentOutOfRangeException(nameof(fromStage));
            return EvolveCosts[fromStage - 1];
        }

        static int ClampStage(int stage) => Math.Max(1, Math.Min(MaxStage, stage));
    }
}
