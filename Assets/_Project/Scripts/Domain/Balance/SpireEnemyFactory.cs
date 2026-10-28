using System;
using System.Collections.Generic;

namespace P2D.Domain
{
    /// <summary>
    /// Enemy strength knobs for spire floors (docs/Balance.md §11). Enemy stats are a fraction
    /// of an un-geared dragon at the bracket's first enemy level, then ×1.06 per floor (§5.2).
    /// </summary>
    [Serializable]
    public sealed class SpireTuning
    {
        public double normalHp = 1.4;
        public double normalAtk = 0.30;
        public double defense = 0.6;
        public int packSize = 3;
        public double guardianHp = 3.0;
        public double guardianAtk = 1.5;
        public double guardianHeavyEvery = 6;
        public double bossHp = 4.0;
        public double bossAtk = 1.0;
        public double bossHeavyEvery = 8;
        public double heavyMultiplier = 3;
        public double bossEnrageSeconds = 90;
        public double floorTimeLimit = 90;
        public double bossTimeLimit = 120;
    }

    public static class SpireEnemyFactory
    {
        static readonly int[] FirstLevel = { 5, 20, 40, 60, 80 };
        static readonly int[] LastLevel = { 25, 45, 65, 85, 100 };

        public static int EnemyLevel(int bracket, int floor)
        {
            int b = Math.Max(1, Math.Min(5, bracket)) - 1;
            if (floor > SpireRules.TopFloor) return Math.Min(DragonRules.MaxLevel, LastLevel[b] + floor - SpireRules.TopFloor);
            return FirstLevel[b] + (int)Math.Round((LastLevel[b] - FirstLevel[b]) * (floor - 1) / 29.0);
        }

        static double Reference(StatType stat, int bracket)
        {
            int level = FirstLevel[Math.Max(1, Math.Min(5, bracket)) - 1];
            int stage = Math.Min(DragonRules.MaxStage, (level - 1) / 20 + 1);
            return DragonRules.BaseStat(stat, level, stage);
        }

        public static double TimeLimit(int floor, SpireTuning tuning) =>
            SpireRules.TypeOf(floor) == FloorType.Boss ? tuning.bossTimeLimit : tuning.floorTimeLimit;

        public static List<EnemySpec> Floor(int bracket, int floor, Element themeElement, SpireTuning tuning)
        {
            double scale = SpireRules.EnemyStatMultiplier(floor);
            int level = EnemyLevel(bracket, floor);
            double hp = Reference(StatType.HP, bracket) * tuning.normalHp * scale;
            double atk = Reference(StatType.ATK, bracket) * tuning.normalAtk * scale;
            double def = Reference(StatType.DEF, bracket) * tuning.defense * scale;

            var list = new List<EnemySpec>();
            switch (SpireRules.TypeOf(floor))
            {
                case FloorType.Guardian:
                    list.Add(new EnemySpec
                    {
                        id = "guardian", level = level, element = themeElement, defense = def,
                        hp = hp * tuning.guardianHp, attack = atk * tuning.guardianAtk,
                        attackIntervalSeconds = 2, heavyIntervalSeconds = tuning.guardianHeavyEvery,
                        heavyMultiplier = tuning.heavyMultiplier,
                    });
                    break;
                case FloorType.Boss:
                    list.Add(new EnemySpec
                    {
                        id = "boss", level = level, element = themeElement, defense = def,
                        hp = hp * tuning.bossHp, attack = atk * tuning.bossAtk,
                        attackIntervalSeconds = 2, heavyIntervalSeconds = tuning.bossHeavyEvery,
                        heavyMultiplier = tuning.heavyMultiplier + 1, enrageSeconds = tuning.bossEnrageSeconds,
                    });
                    break;
                default:
                    for (int i = 0; i < tuning.packSize; i++)
                        list.Add(new EnemySpec
                        {
                            id = "mob", level = level, element = themeElement, defense = def, hp = hp, attack = atk,
                            attackIntervalSeconds = 2,
                        });
                    break;
            }
            return list;
        }
    }
}
