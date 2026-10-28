using P2D.Domain;
using P2D.Presentation.UI;
using P2D.Services;
using UnityEngine;

namespace P2D.Presentation.Screens
{
    /// <summary>Shop: weekly soul-shard shop and the crystal stamina refill. Real-money items need IAP.</summary>
    public sealed class ShopScreen : GameScreen
    {
        RectTransform list;

        protected override void Build() => list = UiKit.ScrollList(Root);

        public override void Refresh()
        {
            UiKit.Clear(list);
            var save = Save;
            var now = ServerClock.UtcNow;
            int week = WeeklyCalendar.WeekIndex(now);

            var head = UiKit.Row(list, 120, UiKit.Surface);
            Cell(head, "Soul shards <b>" + save.wallet.soulShards + "</b>   Crystals <b>" + Fmt.Num(save.wallet.crystals) + "</b>"
                + "\n<color=#a3a8ba>Shards come from rider copies past 5*. Items change in "
                + Fmt.Duration(WeeklyCalendar.NextResetUtc(now) - now) + ".</color>", 0.02f, 1, 28);

            Header(list, "Soul shard shop");
            foreach (var item in ShopActions.ShardShop(week))
            {
                int left = item.weeklyLimit - ShopActions.Bought(save, now, item.id);
                var row = UiKit.Row(list, 100, UiKit.Surface);
                Cell(row, "<b>" + item.name + "</b>\n<color=#a3a8ba>" + left + " / " + item.weeklyLimit + " left this week</color>", 0.02f, 0.68f, 28);
                string id = item.id;
                bool can = left > 0 && save.wallet.soulShards >= item.price;
                CellButton(row, left > 0 ? item.price + " shards" : "SOLD OUT", 0.7f, 1,
                    () => Menu.Commit(ShopActions.Buy(save, ServerClock.UtcNow, id)), can, can ? new Color(0.61f, 0.55f, 0.96f) : UiKit.SurfaceAlt);
            }

            Header(list, "Hunt stamina");
            int refills = ShopActions.StaminaRefillsLeft(save, now);
            var stamina = UiKit.Row(list, 100, UiKit.Surface);
            Cell(stamina, "<b>+" + ShopActions.StaminaRefillAmount + " stamina</b>   (now " + save.hunt.stamina + ")\n<color=#a3a8ba>"
                + refills + " / " + ShopActions.StaminaRefillsPerDay + " left today</color>", 0.02f, 0.68f, 28);
            CellButton(stamina, ShopActions.StaminaRefillCrystals + " crystals", 0.7f, 1,
                () => Menu.Commit(ShopActions.RefillStamina(save, ServerClock.UtcNow)),
                refills > 0 && save.wallet.crystals >= ShopActions.StaminaRefillCrystals);

            Header(list, "Packs & Dragon Pass");
            Cell(UiKit.Row(list, 130, UiKit.Surface),
                "Starter pack, monthly crystals, dragon skins and the Dragon Pass need store integration (Unity IAP) and are not in the greybox yet."
                + "\nThe shop never sells extra spire torches or unlimited ancient scales.", 0.02f, 1, 26, TextAnchor.MiddleLeft, UiKit.Muted);
        }
    }
}
