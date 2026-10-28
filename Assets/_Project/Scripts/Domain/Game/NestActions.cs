using System;

namespace P2D.Domain
{
    [Serializable]
    public sealed class NestSave
    {
        public int level = 1;

        /// <summary>When income was last collected (UTC ticks); 0 = not set yet (older saves).</summary>
        public long lastCollectUtcTicks;
    }

    public struct NestIncome
    {
        public long gold;
        public int essence;
        public double hours;
        public bool full;
    }

    /// <summary>
    /// Dragon's Nest idle income (docs/GDD.md §8, numbers in Balance §13): gold and dragon essence
    /// accrue per hour up to a 12-hour cap. The nest levels up with gold; its max level is 2 × dragon stage.
    /// Uses server time only (pass ServerClock.UtcNow).
    /// </summary>
    public static class NestActions
    {
        public const double CapHours = 12;
        public const int MaxLevel = 10;
        public const long BaseGoldPerHour = 1500;
        public const long GoldPerHourPerLevel = 500;
        public const double BaseEssencePerHour = 1.0;
        public const double EssencePerHourPerLevel = 0.5;
        public const long UpgradeGoldPerLevelSquared = 20000;

        public static long GoldPerHour(int level) => BaseGoldPerHour + GoldPerHourPerLevel * (Math.Max(1, level) - 1);

        public static double EssencePerHour(int level) => BaseEssencePerHour + EssencePerHourPerLevel * (Math.Max(1, level) - 1);

        public static int LevelCap(int dragonStage) => Math.Min(MaxLevel, 2 * Math.Max(1, dragonStage));

        public static long UpgradeCost(int level) => UpgradeGoldPerLevelSquared * level * level;

        static NestSave Nest(PlayerSave save, DateTime utcNow)
        {
            if (save.nest == null) save.nest = new NestSave();
            if (save.nest.lastCollectUtcTicks <= 0) save.nest.lastCollectUtcTicks = utcNow.Ticks; // older saves start now
            return save.nest;
        }

        /// <summary>What is waiting in the nest right now (nothing is changed).</summary>
        public static NestIncome Pending(PlayerSave save, DateTime utcNow)
        {
            var nest = Nest(save, utcNow);
            double hours = Math.Max(0, (utcNow.Ticks - nest.lastCollectUtcTicks) / (double)TimeSpan.TicksPerHour);
            bool full = hours >= CapHours;
            hours = Math.Min(CapHours, hours);
            return new NestIncome
            {
                hours = hours,
                full = full,
                gold = (long)Math.Floor(GoldPerHour(nest.level) * hours),
                essence = (int)Math.Floor(EssencePerHour(nest.level) * hours),
            };
        }

        public static ActionResult Collect(PlayerSave save, DateTime utcNow)
        {
            var income = Pending(save, utcNow);
            if (income.gold <= 0 && income.essence <= 0) return ActionResult.Fail("Nothing to collect yet");
            save.wallet.gold += income.gold;
            save.wallet.dragonEssence += income.essence;
            save.nest.lastCollectUtcTicks = utcNow.Ticks;
            return ActionResult.Ok("+" + income.gold + " gold, +" + income.essence + " essence");
        }

        /// <summary>Upgrading collects first, so the higher rate never applies to time already stored.</summary>
        public static ActionResult Upgrade(PlayerSave save, DateTime utcNow)
        {
            var nest = Nest(save, utcNow);
            int cap = LevelCap(save.dragon.stage);
            if (nest.level >= cap)
                return ActionResult.Fail(nest.level >= MaxLevel ? "Nest is max level" : "Evolve the dragon to raise the nest cap (" + cap + ")");
            long cost = UpgradeCost(nest.level);
            if (save.wallet.gold < cost) return ActionResult.Fail("Need " + cost + " gold");

            Collect(save, utcNow);
            save.wallet.gold -= cost;
            nest.level++;
            return ActionResult.Ok("Nest Lv " + nest.level);
        }
    }
}
