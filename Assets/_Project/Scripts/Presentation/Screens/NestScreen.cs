using P2D.Domain;
using P2D.Presentation.UI;
using P2D.Services;
using UnityEngine;

namespace P2D.Presentation.Screens
{
    /// <summary>Home: the dragon at a glance, this week's spire and what happened while away.</summary>
    public sealed class NestScreen : GameScreen
    {
        RectTransform list;

        protected override void Build() => list = UiKit.ScrollList(Root);

        public override void Refresh()
        {
            UiKit.Clear(list);
            var d = Save.dragon;
            var loadout = GameActions.ToLoadout(Save);
            double power = LoadoutCalculator.Compute(loadout).PowerScore();

            // Name on top, dragon placeholder in the middle band, stage/power underneath — no overlap.
            var hero = UiKit.Row(list, 420, UiKit.Surface);
            var name = UiKit.Rect(hero, "Name", new Vector2(0, 0.80f), Vector2.one, Vector2.zero, Vector2.zero);
            UiKit.Label(name, d.name, 56, TextAnchor.MiddleCenter);
            var art = UiKit.Rect(hero, "Dragon", new Vector2(0.36f, 0.30f), new Vector2(0.64f, 0.78f), Vector2.zero, Vector2.zero);
            art.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0.3f, 0.55f, 0.9f);
            var info = UiKit.Rect(hero, "Info", Vector2.zero, new Vector2(1, 0.28f), Vector2.zero, Vector2.zero);
            UiKit.Label(info, StageName(d.stage) + "  ·  Lv " + d.level + " / " + DragonRules.LevelCap(d.stage)
                + "\nPower " + power.ToString("N0"), 32, TextAnchor.MiddleCenter);

            var rider = Save.FindRider(d.mainRiderId);
            var riderProfile = RiderCatalog.Find(d.mainRiderId);
            var riderRow = UiKit.Row(list, 90, UiKit.Surface);
            Cell(riderRow, "Riding: <b>" + d.mainRiderId + "</b>  " + (riderProfile != null ? riderProfile.rank.ToString() : "")
                + "  " + (rider != null ? Fmt.Stars(rider.star) : ""), 0.02f, 0.7f);
            CellButton(riderRow, "RIDERS", 0.72f, 1, () => Menu.Show(ScreenId.Riders));

            var now = ServerClock.UtcNow;
            var theme = SpireThemes.ForTime(now);
            var spire = UiKit.Row(list, 200, UiKit.Surface);
            Cell(spire, "WEEKLY SPIRE · " + theme.name + "\nBest floor " + Save.spire.highestFloor + " / " + SpireRules.TopFloor
                + "   ·   Torches " + Save.spire.torches + " / " + SpireRules.MaxTorches
                + "\nResets in " + Fmt.Duration(WeeklyCalendar.NextResetUtc(now) - now), 0.02f, 0.7f, 30);
            CellButton(spire, "ENTER", 0.72f, 1, () => Menu.Show(ScreenId.Spire), true, UiKit.Accent);

            var upgrade = UiKit.Row(list, 90, UiKit.Surface);
            Cell(upgrade, "Next goal: " + NextGoal(), 0.02f, 0.7f);
            CellButton(upgrade, "DRAGON", 0.72f, 1, () => Menu.Show(ScreenId.Dragon));

            var load = SaveManager.Instance.LastLoad;
            var offline = load.offline;
            if (offline.staminaGained > 0 || offline.torchesGained > 0 || offline.weekRolledOver)
            {
                Header(list, "While you were away");
                var row = UiKit.Row(list, 120, UiKit.Surface);
                Cell(row, "Hunt stamina +" + offline.staminaGained + "   Torches +" + offline.torchesGained
                    + (offline.weekRolledOver ? "\nA new spire week began (chest from floor " + offline.weeklyChestFloor + ")" : ""), 0.02f, 1);
            }

            NestIncomeRows(now);
        }

        void NestIncomeRows(System.DateTime now)
        {
            var save = Save;
            var income = NestActions.Pending(save, now);
            int level = save.nest.level;
            Header(list, "Nest  ·  Lv " + level + " / " + NestActions.LevelCap(save.dragon.stage));

            var stored = UiKit.Row(list, 150, UiKit.Surface);
            Cell(stored, "Stored: <b>" + Fmt.Num(income.gold) + "</b> gold, <b>" + income.essence + "</b> essence"
                + "\n<color=#9AA3B5>" + income.hours.ToString("0.0") + " / " + NestActions.CapHours + " h"
                + (income.full ? "  ·  FULL, collect to keep earning" : "") + "</color>", 0.02f, 0.7f, 28);
            bool any = income.gold > 0 || income.essence > 0;
            CellButton(stored, "COLLECT", 0.72f, 1, () => Menu.Commit(NestActions.Collect(save, ServerClock.UtcNow)),
                any, any ? UiKit.Accent : UiKit.SurfaceAlt);

            var upgrade = UiKit.Row(list, 150, UiKit.Surface);
            int cap = NestActions.LevelCap(save.dragon.stage);
            string rate = Fmt.Num(NestActions.GoldPerHour(level)) + " gold, " + NestActions.EssencePerHour(level).ToString("0.#") + " essence / h";
            if (level >= cap)
            {
                Cell(upgrade, rate + "\n<color=#9AA3B5>" + (level >= NestActions.MaxLevel ? "Max level" : "Evolve the dragon to raise the cap")
                    + "</color>", 0.02f, 1, 28);
                return;
            }
            long cost = NestActions.UpgradeCost(level);
            Cell(upgrade, rate + "\n<color=#9AA3B5>Lv " + (level + 1) + ": " + Fmt.Num(NestActions.GoldPerHour(level + 1)) + " gold, "
                + NestActions.EssencePerHour(level + 1).ToString("0.#") + " essence / h</color>", 0.02f, 0.7f, 28);
            CellButton(upgrade, "UPGRADE\n" + Fmt.Num(cost), 0.72f, 1, () => Menu.Commit(NestActions.Upgrade(save, ServerClock.UtcNow)),
                save.wallet.gold >= cost);
        }

        string NextGoal()
        {
            var d = Save.dragon;
            if (d.stage >= DragonRules.MaxStage) return "Max stage reached";
            if (d.level < DragonRules.LevelCap(d.stage)) return "Reach Lv " + DragonRules.LevelCap(d.stage);
            var cost = DragonRules.EvolveCostFrom(d.stage);
            return "Evolve: scales " + Save.wallet.ancientScales + " / " + cost.ancientScales;
        }

        public static string StageName(int stage)
        {
            switch (stage)
            {
                case 1: return "Hatchling";
                case 2: return "Drake";
                case 3: return "Wyrm";
                case 4: return "Elder";
                default: return "Ancient";
            }
        }
    }
}
