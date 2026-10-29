using P2D.Domain;
using P2D.Presentation.UI;
using UnityEngine;

namespace P2D.Presentation.Screens
{
    /// <summary>Story mode: the current chapter's 10 stages, a preview of the next one and the fight button.</summary>
    public sealed class StoryScreen : GameScreen
    {
        RectTransform list;

        protected override void Build() => list = UiKit.ScrollList(Root);

        public override void Refresh()
        {
            UiKit.Clear(list);
            var save = Save;
            int next = StoryActions.NextStage(save);
            int chapterNo = next > 0 ? StoryActions.ChapterOf(next) : StoryActions.Chapters.Length;
            var chapter = StoryActions.Chapter(chapterNo);

            AdventureTabs(list, ScreenId.Story);

            var head = UiKit.Row(list, 150, new Color(0.10f, 0.13f, 0.18f));
            Cell(head, "<b>Chapter " + chapterNo + " · " + chapter.name + "</b>   enemies: " + chapter.element
                + "\nCleared " + save.story.cleared + " / " + StoryActions.TotalStages
                + "   ·   boss gives rider <b>" + chapter.riderReward + "</b>", 0.02f, 1, 30);

            // Ten stage chips for this chapter: done / next / locked.
            var chips = UiKit.Row(list, 90);
            for (int s = 1; s <= StoryActions.StagesPerChapter; s++)
            {
                int index = StoryActions.Index(chapterNo, s);
                bool done = index <= save.story.cleared;
                var color = index == next ? UiKit.Accent : done ? new Color(0.25f, 0.45f, 0.30f) : UiKit.SurfaceAlt;
                var kind = StoryActions.KindOf(index);
                string label = kind == StoryStageKind.Boss ? "B" : kind == StoryStageKind.Guardian ? "G" : s.ToString();
                var cell = UiKit.Slice(UiKit.Fill(chips, "Stage " + s), (s - 1) / 10f, s / 10f, 4);
                cell.gameObject.AddComponent<UnityEngine.UI.Image>().color = color;
                UiKit.Label(cell, label, 30, TextAnchor.MiddleCenter);
            }

            if (next == 0)
            {
                Cell(UiKit.Row(list, 100, UiKit.Surface), "Every chapter is cleared. More chapters are on the way.", 0.02f, 1);
                return;
            }

            var kindNext = StoryActions.KindOf(next);
            var enemies = StoryActions.Enemies(next, Menu.Tuning);
            var e = enemies[0];
            double power = LoadoutCalculator.Compute(GameActions.ToLoadout(save)).PowerScore();
            Header(list, "Next: stage " + StoryActions.Label(next) + (kindNext == StoryStageKind.Normal ? "" : "  ·  " + kindNext));
            var preview = UiKit.Row(list, 170, UiKit.Surface);
            Cell(preview, enemies.Count + " × " + e.id + "  Lv " + e.level + "   HP " + e.hp.ToString("N0") + "   ATK " + e.attack.ToString("N0")
                + (e.heavyIntervalSeconds > 0 ? "\nTelegraphed heavy attack every " + e.heavyIntervalSeconds + " s — swipe to dodge" : "")
                + "\nYour dragon Lv " + save.dragon.level + "   ·   power " + power.ToString("N0"), 0.02f, 1, 28);

            var r = StoryActions.Rewards(next);
            var reward = UiKit.Row(list, 110, UiKit.Surface);
            Cell(reward, "First clear: " + Fmt.Num(r.gold) + " gold · " + r.essence + " essence · " + r.forgeStones + " forge stones"
                + (kindNext != StoryStageKind.Normal ? " · 1 " + StoryActions.GearRarity(next) + " item" : "")
                + (r.pactSigils > 0 ? " · " + r.pactSigils + " sigils · " + r.crystals + " gems" : "")
                + (r.riderId.Length > 0 ? " · rider " + r.riderId : ""), 0.02f, 1, 28);

            var go = UiKit.Row(list, 150);
            CellButton(go, "FIGHT STAGE " + StoryActions.Label(next), 0.1f, 0.9f, () => Menu.StartStoryBattle(next), true, UiKit.Accent, 40);
        }
    }
}
