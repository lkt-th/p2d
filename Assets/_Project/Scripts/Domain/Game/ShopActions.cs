using System;
using System.Collections.Generic;

namespace P2D.Domain
{
    public enum ShopCurrency { SoulShards, Crystals }

    public sealed class ShopItem
    {
        public string id;
        public string name;
        public ShopCurrency currency;
        public int price;
        public int weeklyLimit;
    }

    [Serializable]
    public sealed class ShopPurchase
    {
        public string id = "";
        public int count;
    }

    [Serializable]
    public sealed class ShopSave
    {
        public int weekIndex = int.MinValue;
        public List<ShopPurchase> bought = new List<ShopPurchase>();
        public int staminaDay = int.MinValue;
        public int staminaRefillsToday;
    }

    /// <summary>
    /// Shop (docs/GDD.md §10): the weekly soul-shard shop and the crystal stamina refill.
    /// Real-money packs and the battle pass need store integration and are not sold here.
    /// The shop never sells spire torches or unlimited ancient scales.
    /// </summary>
    public static class ShopActions
    {
        public const int StaminaRefillAmount = 60;
        public const int StaminaRefillCrystals = 50;
        public const int StaminaRefillsPerDay = 2;

        static readonly string[] FeaturedRotation = { "kyren", "brogan", "zephyr", "selena", "ravena" };

        /// <summary>This week's shard shop; one SSR rider copy rotates weekly.</summary>
        public static List<ShopItem> ShardShop(int weekIndex)
        {
            string featured = FeaturedRotation[WeeklyCalendar.FallbackThemeIndex(weekIndex, FeaturedRotation.Length)];
            return new List<ShopItem>
            {
                new ShopItem { id = "seal_ssr", name = "SSR soul seal", price = 100, weeklyLimit = 2 },
                new ShopItem { id = "rider_" + featured, name = featured + " (rider copy)", price = 150, weeklyLimit = 1 },
                new ShopItem { id = "ancient_scale", name = "Ancient scale ×1", price = 80, weeklyLimit = 2 },
                new ShopItem { id = "pact_sigil_5", name = "Pact sigil ×5", price = 60, weeklyLimit = 3 },
                new ShopItem { id = "seal_sr", name = "SR soul seal", price = 25, weeklyLimit = 5 },
                new ShopItem { id = "refine_5", name = "Refine stone ×5", price = 30, weeklyLimit = 3 },
            };
        }

        public static int Bought(PlayerSave save, DateTime utcNow, string id)
        {
            var shop = Shop(save, utcNow);
            foreach (var p in shop.bought) if (p.id == id) return p.count;
            return 0;
        }

        /// <summary>Display name in the current language (the rider copy names the featured rider).</summary>
        public static string ShopItemName(ShopItem item) =>
            item.id.StartsWith("rider_") ? Loc.F("{0} (rider copy)", Loc.Rider(item.id.Substring(6))) : Loc.T(item.name);

        public static ActionResult Buy(PlayerSave save, DateTime utcNow, string id)
        {
            var shop = Shop(save, utcNow);
            var item = ShardShop(shop.weekIndex).Find(i => i.id == id);
            if (item == null) return ActionResult.Fail(Loc.T("Not sold this week"));
            int bought = Bought(save, utcNow, id);
            if (bought >= item.weeklyLimit) return ActionResult.Fail(Loc.T("Sold out this week"));
            if (save.wallet.soulShards < item.price) return ActionResult.Fail(Loc.F("Need {0} soul shards", item.price));

            if (!Grant(save, item.id)) return ActionResult.Fail(Loc.T("Unknown item"));
            save.wallet.soulShards -= item.price;
            var record = shop.bought.Find(p => p.id == id);
            if (record == null) shop.bought.Add(record = new ShopPurchase { id = id });
            record.count++;
            return ActionResult.Ok(Loc.F("Bought {0}", ShopItemName(item)));
        }

        public static int StaminaRefillsLeft(PlayerSave save, DateTime utcNow)
        {
            var shop = Shop(save, utcNow);
            int today = WeeklyCalendar.DayIndex(utcNow);
            return shop.staminaDay == today ? StaminaRefillsPerDay - shop.staminaRefillsToday : StaminaRefillsPerDay;
        }

        public static ActionResult RefillStamina(PlayerSave save, DateTime utcNow)
        {
            var shop = Shop(save, utcNow);
            int today = WeeklyCalendar.DayIndex(utcNow);
            if (shop.staminaDay != today) { shop.staminaDay = today; shop.staminaRefillsToday = 0; }
            if (shop.staminaRefillsToday >= StaminaRefillsPerDay) return ActionResult.Fail(Loc.T("No refills left today"));
            if (save.wallet.crystals < StaminaRefillCrystals) return ActionResult.Fail(Loc.F("Need {0} crystals", StaminaRefillCrystals));

            OfflineProgress.Apply(save, utcNow);
            save.wallet.crystals -= StaminaRefillCrystals;
            save.hunt.stamina += StaminaRefillAmount; // may go above max, like a purchased potion
            shop.staminaRefillsToday++;
            return ActionResult.Ok(Loc.F("+{0} hunt stamina", StaminaRefillAmount));
        }

        /// <summary>The shop state for the current week (purchases reset at the weekly reset).</summary>
        static ShopSave Shop(PlayerSave save, DateTime utcNow)
        {
            if (save.shop == null) save.shop = new ShopSave();
            int week = WeeklyCalendar.WeekIndex(utcNow);
            if (save.shop.weekIndex != week)
            {
                save.shop.weekIndex = week;
                save.shop.bought.Clear();
            }
            return save.shop;
        }

        static bool Grant(PlayerSave save, string id)
        {
            var w = save.wallet;
            switch (id)
            {
                case "seal_ssr": w.soulSeals[(int)RiderRank.SSR]++; return true;
                case "seal_sr": w.soulSeals[(int)RiderRank.SR]++; return true;
                case "ancient_scale": w.ancientScales++; return true;
                case "pact_sigil_5": w.pactSigils += 5; return true;
                case "refine_5": w.refineStones += 5; return true;
            }
            if (id.StartsWith("rider_"))
            {
                string riderId = id.Substring("rider_".Length);
                if (RiderCatalog.Find(riderId) == null) return false;
                var state = save.FindRider(riderId);
                if (state == null) save.riders.Add(new RiderState { riderId = riderId, star = 1 });
                else if (state.star >= RiderRules.MaxStar) w.soulShards += RiderRules.ShardsForOverflow(RiderCatalog.Find(riderId).rank);
                else state.copies++;
                return true;
            }
            return false;
        }
    }
}
