using System;

namespace P2D.Domain
{
    public struct HuntRewards
    {
        public long gold;
        public int forgeStones;
        public int dragonEssence;
        public int elementCrystals;
        public int refineStones;
        public bool dropsGear;
    }

    /// <summary>
    /// Hunting Grounds (repeatable dungeon) — docs/Balance.md §9.
    /// Encounters are independent rolls: there is deliberately no pity.
    /// </summary>
    public static class HuntRules
    {
        public const int StaminaCost = 6;
        public const int MaxStamina = 120;
        public const int MinutesPerStamina = 5;
        public const double BaseRareRate = 0.12;
        public const double BaseEpicRate = 0.03;
        public const double RareEscapeSeconds = 20;
        public const double EpicEscapeSeconds = 30;

        public static double RareRate(double huntBonus) => BaseRareRate * (1 + ClampBonus(huntBonus));

        public static double EpicRate(double huntBonus) => BaseEpicRate * (1 + ClampBonus(huntBonus));

        public static EncounterKind RollEncounter(IRandom random, double huntBonus)
        {
            double roll = random.NextDouble();
            double epic = EpicRate(huntBonus);
            if (roll < epic) return EncounterKind.Epic;
            if (roll < epic + RareRate(huntBonus)) return EncounterKind.Rare;
            return EncounterKind.Normal;
        }

        public static double EscapeSeconds(EncounterKind kind, double riderDelay)
        {
            switch (kind)
            {
                case EncounterKind.Rare: return RareEscapeSeconds + riderDelay;
                case EncounterKind.Epic: return EpicEscapeSeconds + riderDelay;
                default: return double.PositiveInfinity;
            }
        }

        public static double MonsterHpMultiplier(EncounterKind kind)
        {
            switch (kind)
            {
                case EncounterKind.Rare: return 3;
                case EncounterKind.Epic: return 5;
                default: return 1;
            }
        }

        /// <summary>Rewards for a run in zone <paramref name="zone"/> (1-based). Escaped rares give a consolation.</summary>
        public static HuntRewards Rewards(EncounterKind kind, int zone, bool defeated)
        {
            if (kind != EncounterKind.Normal && !defeated)
                return new HuntRewards { gold = 500L * zone, forgeStones = 2 * zone };

            switch (kind)
            {
                case EncounterKind.Rare:
                    return new HuntRewards
                    {
                        gold = 3000L * zone, forgeStones = 10 * zone, dragonEssence = 3 * zone,
                        elementCrystals = 2 * zone, refineStones = 1, dropsGear = true,
                    };
                case EncounterKind.Epic:
                    return new HuntRewards
                    {
                        gold = 7500L * zone, forgeStones = 22 * zone, dragonEssence = 6 * zone,
                        elementCrystals = 5 * zone, dropsGear = true,
                    };
                default:
                    return new HuntRewards { gold = 1000L * zone, forgeStones = 3 * zone, dragonEssence = zone };
            }
        }

        /// <summary>Gear rarity for a defeated rare/epic: rare 70/30 Rare/Epic, epic 80/20 Epic/Legendary.</summary>
        public static Rarity RollGearRarity(EncounterKind kind, IRandom random)
        {
            double roll = random.NextDouble();
            if (kind == EncounterKind.Epic) return roll < 0.8 ? Rarity.Epic : Rarity.Legendary;
            return roll < 0.7 ? Rarity.Rare : Rarity.Epic;
        }

        public static int RegenerateStamina(int current, int minutesElapsed) =>
            current >= MaxStamina ? current : Math.Min(MaxStamina, current + Math.Max(0, minutesElapsed) / MinutesPerStamina);

        static double ClampBonus(double bonus) => Math.Max(0, Math.Min(RiderRules.MaxHuntBonus, bonus));
    }
}
