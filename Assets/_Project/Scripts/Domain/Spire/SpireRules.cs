using System;

namespace P2D.Domain
{
    public struct FloorRewards
    {
        public long gold;
        public int forgeStones;
        public int dragonEssence;
        public int elementCrystals;
        public int pactSigils;
    }

    /// <summary>Spire of the Dragon Path (weekly dungeon) — docs/Balance.md §5.</summary>
    public static class SpireRules
    {
        public const int TopFloor = 30;
        public const int TorchesPerDay = 3;
        public const int MaxTorches = 9;
        public const int BossClaimsPerWeek = 3;

        /// <summary>Bracket is the dragon stage at reset time, locked for the week.</summary>
        public static int BracketForStage(int stageAtReset) => Math.Max(1, Math.Min(DragonRules.MaxStage, stageAtReset));

        public static FloorType TypeOf(int floor)
        {
            if (floor > TopFloor) return FloorType.Endless;
            if (floor == TopFloor) return FloorType.Boss;
            return floor % 10 == 0 ? FloorType.Guardian : FloorType.Normal;
        }

        /// <summary>Multiplier on the bracket's base enemy stats.</summary>
        public static double EnemyStatMultiplier(int floor)
        {
            if (floor <= TopFloor) return Math.Pow(1.06, floor - 1);
            return Math.Pow(1.06, TopFloor - 1) * Math.Pow(1.08, floor - TopFloor);
        }

        public static double EnemyHpMultiplier(int floor)
        {
            switch (TypeOf(floor))
            {
                case FloorType.Guardian: return 2.5;
                case FloorType.Boss: return 6.0;
                default: return 1.0;
            }
        }

        public static FloorRewards FirstClearRewards(int floor, int bracket)
        {
            return new FloorRewards
            {
                gold = (long)Math.Round(1000.0 * bracket * (1 + floor / 10.0)),
                forgeStones = (5 + floor) * bracket,
                dragonEssence = 3 + floor / 3,
                elementCrystals = 2 + floor / 5,
                pactSigils = floor % 5 == 0 ? 1 : 0,
            };
        }

        /// <summary>Next week starts at half of last week's best floor; skipped floors grant first-clear rewards.</summary>
        public static int CatchUpStartFloor(int lastWeekBest) => Math.Max(0, Math.Min(TopFloor, lastWeekBest) / 2);

        public static int RefillTorches(int current, int days) => Math.Min(MaxTorches, current + TorchesPerDay * Math.Max(0, days));
    }
}
