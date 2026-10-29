using System;
using System.Collections.Generic;
using P2D.Domain;
using P2D.Presentation.UI;
using P2D.Services;
using UnityEngine;

namespace P2D.Presentation.Screens
{
    /// <summary>Daily and weekly missions: progress, CLAIM buttons and the time to each reset (opened from the Nest).</summary>
    public sealed class MissionsScreen : GameScreen
    {
        RectTransform list;

        protected override void Build() => list = UiKit.ScrollList(Root);

        public override void Refresh()
        {
            UiKit.Clear(list);
            var back = UiKit.Row(list, 90);
            CellButton(back, Loc.T("< NEST"), 0, 0.34f, () => Menu.Show(ScreenId.Nest));
            if (LockedCard(list, Feature.Missions)) return;

            var now = ServerClock.UtcNow;
            var nextDay = WeeklyCalendar.Epoch.AddDays(WeeklyCalendar.DayIndex(now) + 1);
            Header(list, Loc.T("Daily missions") + "   <size=26><color=#a3a8ba>" + Loc.F("Resets in {0}", Fmt.Duration(nextDay - now)) + "</color></size>");
            Rows(MissionActions.Daily, now);
            Header(list, Loc.T("Weekly missions") + "   <size=26><color=#a3a8ba>"
                + Loc.F("Resets in {0}", Fmt.Duration(WeeklyCalendar.NextResetUtc(now) - now)) + "</color></size>");
            Rows(MissionActions.Weekly, now);
        }

        void Rows(IReadOnlyList<MissionDef> missions, DateTime now)
        {
            var save = Save;
            foreach (var mission in missions)
            {
                int progress = MissionActions.Progress(save, mission, now);
                bool claimed = MissionActions.IsClaimed(save, mission, now);
                bool done = progress >= mission.target;
                var row = UiKit.Row(list, 110, UiKit.Surface);
                Cell(row, "<b>" + MissionActions.Title(mission) + "</b>   " + progress + " / " + mission.target
                    + "\n<color=#a3a8ba>" + MissionActions.RewardText(mission) + "</color>", 0.02f, 0.7f, 28,
                    TextAnchor.MiddleLeft, claimed ? UiKit.Muted : (Color?)null);
                string id = mission.id;
                CellButton(row, claimed ? Loc.T("CLAIMED") : done ? Loc.T("CLAIM") : Loc.T("IN PROGRESS"), 0.72f, 1,
                    () => Menu.Commit(MissionActions.Claim(Save, id, ServerClock.UtcNow)), done && !claimed,
                    done && !claimed ? UiKit.Accent : UiKit.SurfaceAlt);
            }
        }
    }
}
