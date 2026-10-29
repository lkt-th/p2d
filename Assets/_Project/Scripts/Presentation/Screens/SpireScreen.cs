using System.Collections.Generic;
using P2D.Domain;
using P2D.Presentation.UI;
using P2D.Services;
using UnityEngine;

namespace P2D.Presentation.Screens
{
    /// <summary>Weekly spire: theme, progress, a preview of the next floor and the challenge button.</summary>
    public sealed class SpireScreen : GameScreen
    {
        RectTransform list;

        protected override void Build() => list = UiKit.ScrollList(Root);

        public override void Refresh()
        {
            UiKit.Clear(list);
            var save = Save;
            var now = ServerClock.UtcNow;
            var theme = SpireThemes.ForTime(now);
            int next = GameActions.NextSpireFloor(save);

            AdventureTabs(list, ScreenId.Spire);
            if (LockedCard(list, Feature.Spire)) return;

            var head = UiKit.Row(list, 200, new Color(0.17f, 0.11f, 0.09f));
            Cell(head, "<b>" + Loc.T(theme.name) + "</b>   " + Loc.F("enemies: {0}", Loc.Enum(theme.element))
                + "\n" + Loc.F("Bracket B{0}   ·   resets in {1}", save.spire.bracket, Fmt.Duration(WeeklyCalendar.NextResetUtc(now) - now))
                + "\n" + Loc.F("Tip: a {0} heart core has the edge", Loc.Enum(SpireThemes.Counter(theme.element))), 0.02f, 1, 30);

            var featured = FeaturedRiders.ForWeek(save.spire.weekIndex);
            var feature = UiKit.Row(list, 90, FeaturedRiders.RidingFeatured(save) ? new Color(0.13f, 0.2f, 0.13f) : UiKit.Surface);
            Cell(feature, Loc.F("Featured riders: {0}, {1}", Loc.Rider(featured[0]), Loc.Rider(featured[1])) + "\n<color=#a3a8ba>"
                + (FeaturedRiders.RidingFeatured(save) ? Loc.T("Riding one: +20% spire materials") : Loc.T("Ride one for +20% spire materials"))
                + "</color>", 0.02f, 1, 26);

            var progress = UiKit.Row(list, 60);
            var barCell = UiKit.Slice(UiKit.Fill(progress, "Bar"), 0, 0.75f, 14);
            UiKit.Bar(barCell, UiKit.SurfaceAlt, UiKit.Accent, save.spire.highestFloor / (float)SpireRules.TopFloor);
            Cell(progress, Loc.F("Floor {0} / {1}", save.spire.highestFloor, SpireRules.TopFloor), 0.75f, 1, 28);

            ChestRow(save);

            if (next > SpireRules.TopFloor + 10)
            {
                Cell(UiKit.Row(list, 100, UiKit.Surface), Loc.T("All floors cleared this week."), 0.02f, 1);
                SweepRows(save, theme.element);
                return;
            }

            var type = SpireRules.TypeOf(next);
            var enemies = SpireEnemyFactory.Floor(save.spire.bracket, next, theme.element, Menu.Tuning);
            var rewards = SpireRules.FirstClearRewards(next, save.spire.bracket);
            double power = LoadoutCalculator.Compute(GameActions.ToLoadout(save)).PowerScore();

            Header(list, Loc.F("Next: floor {0}", next) + (type == FloorType.Normal ? "" : "  ·  " + Loc.Enum(type) + (type == FloorType.Boss ? " — " + Loc.T(theme.boss) : "")));
            var preview = UiKit.Row(list, 210, UiKit.Surface);
            var e = enemies[0];
            Cell(preview, enemies.Count + " × " + Loc.T(e.id) + "  " + Loc.F("Lv {0}", e.level) + "   HP " + e.hp.ToString("N0") + "   ATK " + e.attack.ToString("N0")
                + (e.heavyIntervalSeconds > 0 ? "\n" + Loc.F("Telegraphed heavy attack every {0} s — swipe to dodge", e.heavyIntervalSeconds) : "")
                + (e.enrageSeconds > 0 ? "\n" + Loc.F("Enrages at {0} s", e.enrageSeconds) : "")
                + "\n" + Loc.F("Your power {0}", power.ToString("N0")), 0.02f, 1, 28);

            var reward = UiKit.Row(list, 110, UiKit.Surface);
            Cell(reward, Loc.F("First clear: {0} gold · {1} forge stones · {2} essence · {3} {4} crystals", Fmt.Num(rewards.gold), rewards.forgeStones,
                rewards.dragonEssence, rewards.elementCrystals, Loc.Enum(theme.element))
                + (rewards.pactSigils > 0 ? " · " + Loc.F("{0} sigil", rewards.pactSigils) : "") + " · " + Loc.T("1 item"), 0.02f, 1, 28);

            var go = UiKit.Row(list, 150);
            CellButton(go, Loc.F("CHALLENGE FLOOR {0}", next), 0.1f, 0.9f, () => Menu.StartSpireBattle(next), true, UiKit.Accent, 40);

            SweepRows(save, theme.element);
        }

        /// <summary>Last week's chest, waiting to be opened (it is earned at the weekly reset).</summary>
        void ChestRow(PlayerSave save)
        {
            if (save.spire.catchUpFloors > 0)
            {
                var catchUp = UiKit.Row(list, 120, new Color(0.12f, 0.17f, 0.12f));
                Cell(catchUp, "<b>" + Loc.T("Catch-up start") + "</b>\n" + Loc.F("This week starts at floor {0}: claim floors 1–{0}", save.spire.catchUpFloors),
                    0.02f, 0.7f, 28);
                var theme = SpireThemes.ForTime(ServerClock.UtcNow).element;
                CellButton(catchUp, Loc.T("CLAIM"), 0.72f, 1, () => Menu.Commit(Result(SpireActions.ClaimCatchUp(save, theme))), true, UiKit.Accent);
            }

            int floor = save.spire.pendingChestFloor;
            if (floor <= 0) return;
            var row = UiKit.Row(list, 120, new Color(0.22f, 0.17f, 0.08f));
            Cell(row, "<b>" + Loc.T(SpireActions.ChestTier(floor)) + "</b>\n" + Loc.F("Last week you reached floor {0}", floor), 0.02f, 0.7f, 28);
            var lastWeek = SpireThemes.ForWeek(save.spire.weekIndex - 1).element;
            CellButton(row, Loc.T("OPEN"), 0.72f, 1, () => Menu.Commit(Result(SpireActions.OpenWeeklyChest(save, lastWeek, Menu.Random))), true, UiKit.Accent);
        }

        /// <summary>Sweep the highest cleared floor, plus guardian/boss floors that still have weekly claims.</summary>
        void SweepRows(PlayerSave save, Element themeElement)
        {
            if (save.spire.highestFloor <= 0) return;
            Header(list, Loc.F("Sweep  ·  soul torches {0} / {1}", save.spire.torches, SpireRules.MaxTorches));
            var floors = new List<int> { save.spire.highestFloor };
            foreach (int boss in new[] { SpireRules.TopFloor, 20, 10 })
                if (boss <= save.spire.highestFloor && !floors.Contains(boss) && SpireActions.ClaimsLeft(save, boss) > 0) floors.Add(boss);

            foreach (int floor in floors)
            {
                int f = floor;
                int claims = SpireActions.ClaimsLeft(save, f);
                var row = UiKit.Row(list, 100, UiKit.Surface);
                Cell(row, Loc.F("Floor {0}", f) + (SpireActions.BossIndex(f) >= 0
                    ? "   <color=#e9c46a>" + Loc.F("boss rewards {0} / {1} left this week", claims, SpireRules.BossClaimsPerWeek) + "</color>"
                    : "   <color=#a3a8ba>" + Loc.T("half the materials, 20% item chance") + "</color>"), 0.02f, 0.7f, 26);
                CellButton(row, Loc.T("SWEEP"), 0.72f, 1, () => Menu.Commit(Result(SpireActions.Sweep(save, f, themeElement, Menu.Random))),
                    SpireActions.CanSweep(save, f));
            }
        }

        static ActionResult Result(SweepResult r) => r.ok ? ActionResult.Ok(r.message) : ActionResult.Fail(r.message);

        static ActionResult Result(ChestReward r) => r.ok ? ActionResult.Ok(r.message) : ActionResult.Fail(r.message);
    }
}
