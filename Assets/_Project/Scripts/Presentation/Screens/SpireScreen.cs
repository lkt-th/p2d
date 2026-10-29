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

            var progress = UiKit.Row(list, 60);
            var barCell = UiKit.Slice(UiKit.Fill(progress, "Bar"), 0, 0.75f, 14);
            UiKit.Bar(barCell, UiKit.SurfaceAlt, UiKit.Accent, save.spire.highestFloor / (float)SpireRules.TopFloor);
            Cell(progress, Loc.F("Floor {0} / {1}", save.spire.highestFloor, SpireRules.TopFloor), 0.75f, 1, 28);

            if (next > SpireRules.TopFloor + 10)
            {
                Cell(UiKit.Row(list, 100, UiKit.Surface), Loc.T("All floors cleared this week."), 0.02f, 1);
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
        }
    }
}
