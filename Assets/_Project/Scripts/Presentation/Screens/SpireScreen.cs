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

            var head = UiKit.Row(list, 200, new Color(0.17f, 0.11f, 0.09f));
            Cell(head, "<b>" + theme.name + "</b>   enemies: " + theme.element + "\nBracket B" + save.spire.bracket
                + "   ·   resets in " + Fmt.Duration(WeeklyCalendar.NextResetUtc(now) - now)
                + "\nTip: a " + SpireThemes.Counter(theme.element) + " heart core has the edge", 0.02f, 1, 30);

            var progress = UiKit.Row(list, 60);
            var barCell = UiKit.Slice(UiKit.Fill(progress, "Bar"), 0, 0.75f, 14);
            UiKit.Bar(barCell, UiKit.SurfaceAlt, UiKit.Accent, save.spire.highestFloor / (float)SpireRules.TopFloor);
            Cell(progress, "Floor " + save.spire.highestFloor + " / " + SpireRules.TopFloor, 0.75f, 1, 28);

            if (next > SpireRules.TopFloor + 10)
            {
                Cell(UiKit.Row(list, 100, UiKit.Surface), "All floors cleared this week.", 0.02f, 1);
                return;
            }

            var type = SpireRules.TypeOf(next);
            var enemies = SpireEnemyFactory.Floor(save.spire.bracket, next, theme.element, Menu.Tuning);
            var rewards = SpireRules.FirstClearRewards(next, save.spire.bracket);
            double power = LoadoutCalculator.Compute(GameActions.ToLoadout(save)).PowerScore();

            Header(list, "Next: floor " + next + (type == FloorType.Normal ? "" : "  ·  " + type + (type == FloorType.Boss ? " — " + theme.boss : "")));
            var preview = UiKit.Row(list, 210, UiKit.Surface);
            var e = enemies[0];
            Cell(preview, enemies.Count + " × " + e.id + "  Lv " + e.level + "   HP " + e.hp.ToString("N0") + "   ATK " + e.attack.ToString("N0")
                + (e.heavyIntervalSeconds > 0 ? "\nTelegraphed heavy attack every " + e.heavyIntervalSeconds + " s — swipe to dodge" : "")
                + (e.enrageSeconds > 0 ? "\nEnrages at " + e.enrageSeconds + " s" : "")
                + "\nYour power " + power.ToString("N0"), 0.02f, 1, 28);

            var reward = UiKit.Row(list, 110, UiKit.Surface);
            Cell(reward, "First clear: " + Fmt.Num(rewards.gold) + " gold · " + rewards.forgeStones + " forge stones · "
                + rewards.dragonEssence + " essence · " + rewards.elementCrystals + " " + theme.element + " crystals"
                + (rewards.pactSigils > 0 ? " · " + rewards.pactSigils + " sigil" : "") + " · 1 item", 0.02f, 1, 28);

            var go = UiKit.Row(list, 150);
            CellButton(go, "CHALLENGE FLOOR " + next, 0.1f, 0.9f, () => Menu.StartSpireBattle(next), true, UiKit.Accent, 40);
        }
    }
}
