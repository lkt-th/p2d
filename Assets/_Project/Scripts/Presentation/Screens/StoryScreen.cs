using P2D.Domain;
using P2D.Presentation.UI;
using UnityEngine;

namespace P2D.Presentation.Screens
{
    /// <summary>
    /// Story mode: the current chapter's 10 stages, a preview of the next one and the fight button.
    /// A stage with an opening scene plays it (NEXT / SKIP) the first time before the battle starts.
    /// </summary>
    public sealed class StoryScreen : GameScreen
    {
        RectTransform list;
        int talkingStage;
        int line;

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
            Cell(head, "<b>" + Loc.F("Chapter {0} · {1}", chapterNo, Loc.T(chapter.name)) + "</b>   " + Loc.F("enemies: {0}", Loc.Enum(chapter.element))
                + "\n" + Loc.F("Cleared {0} / {1}", save.story.cleared, StoryActions.TotalStages)
                + "   ·   " + Loc.F("boss gives rider <b>{0}</b>", Loc.Rider(chapter.riderReward)), 0.02f, 1, 30);

            // Ten stage chips for this chapter: done / next / locked.
            var chips = UiKit.Row(list, 90);
            for (int s = 1; s <= StoryActions.StagesPerChapter; s++)
            {
                int index = StoryActions.Index(chapterNo, s);
                bool done = index <= save.story.cleared;
                var color = index == next ? UiKit.Accent : done ? new Color(0.25f, 0.45f, 0.30f) : UiKit.SurfaceAlt;
                var kind = StoryActions.KindOf(index);
                string label = kind == StoryStageKind.Boss ? Loc.T("B") : kind == StoryStageKind.Guardian ? Loc.T("G") : s.ToString();
                var cell = UiKit.Slice(UiKit.Fill(chips, "Stage " + s), (s - 1) / 10f, s / 10f, 4);
                cell.gameObject.AddComponent<UnityEngine.UI.Image>().color = color;
                UiKit.Label(cell, label, label.Length > 2 ? 24 : 30, TextAnchor.MiddleCenter);
            }

            if (talkingStage == next && next > 0)
            {
                Scene(save, next);
                return;
            }

            if (next == 0)
            {
                Cell(UiKit.Row(list, 100, UiKit.Surface), Loc.T("Every chapter is cleared. More chapters are on the way."), 0.02f, 1);
                return;
            }

            var kindNext = StoryActions.KindOf(next);
            var enemies = StoryActions.Enemies(next, Menu.Tuning);
            var e = enemies[0];
            double power = LoadoutCalculator.Compute(GameActions.ToLoadout(save)).PowerScore();
            Header(list, Loc.F("Next: stage {0}", StoryActions.Label(next)) + (kindNext == StoryStageKind.Normal ? "" : "  ·  " + Loc.Enum(kindNext)));
            var preview = UiKit.Row(list, 170, UiKit.Surface);
            Cell(preview, enemies.Count + " × " + Loc.T(e.id) + "  " + Loc.F("Lv {0}", e.level) + "   HP " + e.hp.ToString("N0") + "   ATK " + e.attack.ToString("N0")
                + (e.heavyIntervalSeconds > 0 ? "\n" + Loc.F("Telegraphed heavy attack every {0} s — swipe to dodge", e.heavyIntervalSeconds) : "")
                + "\n" + Loc.F("Your dragon Lv {0}   ·   power {1}", save.dragon.level, power.ToString("N0")), 0.02f, 1, 28);

            var r = StoryActions.Rewards(next);
            var reward = UiKit.Row(list, 110, UiKit.Surface);
            Cell(reward, Loc.F("First clear: {0} gold · {1} essence · {2} forge stones", Fmt.Num(r.gold), r.essence, r.forgeStones)
                + (kindNext != StoryStageKind.Normal ? " · " + Loc.F("1 {0} item", Loc.Enum(StoryActions.GearRarity(next))) : "")
                + (r.pactSigils > 0 ? " · " + Loc.F("{0} sigils · {1} gems", r.pactSigils, r.crystals) : "")
                + (r.riderId.Length > 0 ? " · " + Loc.F("rider {0}", Loc.Rider(r.riderId)) : ""), 0.02f, 1, 28);

            var go = UiKit.Row(list, 150);
            CellButton(go, Loc.F("FIGHT STAGE {0}", StoryActions.Label(next)), 0.1f, 0.9f, () => Fight(next), true, UiKit.Accent, 40);
        }

        void Fight(int index)
        {
            if (StoryScript.ShouldPlay(Save, index))
            {
                talkingStage = index;
                line = 0;
                Refresh();
                return;
            }
            Menu.StartStoryBattle(index);
        }

        void Scene(PlayerSave save, int index)
        {
            var lines = StoryScript.Before(index, save.dragon.name);
            var current = lines[Mathf.Clamp(line, 0, lines.Length - 1)];
            bool last = line >= lines.Length - 1;

            Header(list, Loc.F("Story {0}", StoryActions.Label(index)) + "  ·  " + (line + 1) + " / " + lines.Length);
            var box = UiKit.Row(list, 300, new Color(0.13f, 0.11f, 0.17f));
            Cell(box, "<b><color=#F08C4B>" + current.speaker + "</color></b>\n" + current.text, 0.03f, 0.97f, 32);

            var buttons = UiKit.Row(list, 130);
            CellButton(buttons, Loc.T("SKIP"), 0, 0.3f, () => EndScene(save, index), true, UiKit.SurfaceAlt);
            CellButton(buttons, last ? Loc.T("FIGHT!") : Loc.T("NEXT"), 0.35f, 1, () =>
            {
                if (last) EndScene(save, index);
                else { line++; Refresh(); }
            }, true, UiKit.Accent, 36);
        }

        void EndScene(PlayerSave save, int index)
        {
            StoryScript.MarkSeen(save, index);
            talkingStage = 0;
            Menu.StartStoryBattle(index); // saves before loading the battle scene
        }
    }
}
