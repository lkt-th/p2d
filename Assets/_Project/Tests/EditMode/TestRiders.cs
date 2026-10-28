namespace P2D.Domain.Tests
{
    /// <summary>Sample riders from docs/GDD.md §5.4.</summary>
    static class TestRiders
    {
        public static RiderProfile Kyren() => new RiderProfile
        {
            id = "kyren",
            rank = RiderRank.SSR,
            hasPath = true,
            path = GrowthPath.Fury,
            baseAffinity = 12,
            pactBuffs = new[]
            {
                new StatModifier(StatType.ATK, ModType.Percent, 0.10),
                new StatModifier(StatType.CritDmg, ModType.Percent, 0.15),
            },
        };

        public static RiderProfile Finn() => new RiderProfile
        {
            id = "finn",
            rank = RiderRank.SR,
            hasPath = true,
            path = GrowthPath.Tempest,
            baseAffinity = 6,
            pactBuffs = new[] { new StatModifier(StatType.SPD, ModType.Flat, 4) },
            huntBase = 0.10,
        };

        public static RiderProfile Lycan() => new RiderProfile
        {
            id = "lycan",
            rank = RiderRank.UR,
            hasPath = true,
            path = GrowthPath.Tempest,
            baseAffinity = 15,
            pactBuffs = new[]
            {
                new StatModifier(StatType.ATK, ModType.Percent, 0.12),
                new StatModifier(StatType.SPD, ModType.Flat, 8),
                new StatModifier(StatType.ElementDmg, ModType.Percent, 0.10),
            },
            huntBase = 0.20,
            legacyHuntRefundChance = 0.25,
        };
    }
}
