using P2D.Domain;
using P2D.Presentation.UI;
using P2D.Services;
using UnityEngine;

namespace P2D.Presentation.Screens
{
    /// <summary>Home: the dragon at a glance, this week's spire, what happened while away, nest income and the language switch.</summary>
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

            // Name on top, the dragon (stage + dominant path look) in the middle band, stage/power underneath — no overlap.
            var hero = UiKit.Row(list, 420, UiKit.Surface);
            var name = UiKit.Rect(hero, "Name", new Vector2(0, 0.80f), Vector2.one, Vector2.zero, Vector2.zero);
            UiKit.Label(name, d.name, 56, TextAnchor.MiddleCenter);
            var art = UiKit.Rect(hero, "Dragon", new Vector2(0.3f, 0.28f), new Vector2(0.7f, 0.80f), Vector2.zero, Vector2.zero);
            DragonPortrait.Build(art, P2D.Presentation.Battle.DragonLook.For(Save));
            var info = UiKit.Rect(hero, "Info", Vector2.zero, new Vector2(1, 0.28f), Vector2.zero, Vector2.zero);
            UiKit.Label(info, StageName(d.stage) + "  ·  " + Loc.F("Lv {0} / {1}", d.level, DragonRules.LevelCap(d.stage))
                + "\n" + Loc.F("Power {0}", power.ToString("N0")), 32, TextAnchor.MiddleCenter);

            var rider = Save.FindRider(d.mainRiderId);
            var riderProfile = RiderCatalog.Find(d.mainRiderId);
            var riderRow = UiKit.Row(list, 90, UiKit.Surface);
            Cell(riderRow, Loc.F("Riding: <b>{0}</b>", Loc.Rider(d.mainRiderId)) + "  " + (riderProfile != null ? riderProfile.rank.ToString() : "")
                + "  " + (rider != null ? Fmt.Stars(rider.star) : ""), 0.02f, 0.7f);
            CellButton(riderRow, Loc.T("RIDERS"), 0.72f, 1, () => Menu.Show(ScreenId.Riders), FeatureUnlocks.IsUnlocked(Save, Feature.Riders));
            MissionsRow();

            var now = ServerClock.UtcNow;
            var theme = SpireThemes.ForTime(now);
            var story = UiKit.Row(list, 120, UiKit.Surface);
            int nextStage = StoryActions.NextStage(Save);
            Cell(story, nextStage > 0
                ? Loc.F("STORY · next stage {0}  {1}", StoryActions.Label(nextStage), Loc.T(StoryActions.Chapter(StoryActions.ChapterOf(nextStage)).name))
                : Loc.T("STORY · every chapter cleared"), 0.02f, 0.7f, 30);
            CellButton(story, Loc.T("PLAY"), 0.72f, 1, () => Menu.Show(ScreenId.Story), true,
                nextStage > 0 && !FeatureUnlocks.IsUnlocked(Save, Feature.Spire) ? UiKit.Accent : UiKit.SurfaceAlt);

            var spire = UiKit.Row(list, 200, UiKit.Surface);
            Cell(spire, Loc.F("WEEKLY SPIRE · {0}", Loc.T(theme.name))
                + "\n" + Loc.F("Best floor {0} / {1}   ·   Torches {2} / {3}", Save.spire.highestFloor, SpireRules.TopFloor, Save.spire.torches, SpireRules.MaxTorches)
                + "\n" + Loc.F("Resets in {0}", Fmt.Duration(WeeklyCalendar.NextResetUtc(now) - now)), 0.02f, 0.7f, 30);
            bool spireOpen = FeatureUnlocks.IsUnlocked(Save, Feature.Spire);
            CellButton(spire, spireOpen ? Loc.T("ENTER") : Loc.T("LOCKED") + "\n" + StoryActions.Label(FeatureUnlocks.UnlockStage(Feature.Spire)), 0.72f, 1,
                () => Menu.Show(ScreenId.Spire), spireOpen, spireOpen ? UiKit.Accent : UiKit.SurfaceAlt);

            var upgrade = UiKit.Row(list, 90, UiKit.Surface);
            Cell(upgrade, Loc.F("Next goal: {0}", NextGoal()), 0.02f, 0.7f);
            CellButton(upgrade, Loc.T("DRAGON"), 0.72f, 1, () => Menu.Show(ScreenId.Dragon));

            var load = SaveManager.Instance.LastLoad;
            var offline = load.offline;
            if (offline.staminaGained > 0 || offline.torchesGained > 0 || offline.weekRolledOver)
            {
                Header(list, Loc.T("While you were away"));
                var row = UiKit.Row(list, 120, UiKit.Surface);
                Cell(row, Loc.F("Hunt stamina +{0}   Torches +{1}", offline.staminaGained, offline.torchesGained)
                    + (offline.weekRolledOver ? "\n" + Loc.F("A new spire week began (chest from floor {0})", offline.weeklyChestFloor) : ""), 0.02f, 1);
            }

            NestIncomeRows(now);
            LanguageRow();
        }

        void NestIncomeRows(System.DateTime now)
        {
            var save = Save;
            var income = NestActions.Pending(save, now);
            int level = save.nest.level;
            Header(list, Loc.F("Nest  ·  Lv {0} / {1}", level, NestActions.LevelCap(save.dragon.stage)));

            var stored = UiKit.Row(list, 150, UiKit.Surface);
            Cell(stored, Loc.F("Stored: <b>{0}</b> gold, <b>{1}</b> essence", Fmt.Num(income.gold), income.essence)
                + "\n<color=#9AA3B5>" + Loc.F("{0} / {1} h", income.hours.ToString("0.0"), NestActions.CapHours)
                + (income.full ? "  ·  " + Loc.T("FULL, collect to keep earning") : "") + "</color>", 0.02f, 0.7f, 28);
            bool any = income.gold > 0 || income.essence > 0;
            CellButton(stored, Loc.T("COLLECT"), 0.72f, 1, () => Menu.Commit(NestActions.Collect(save, ServerClock.UtcNow)),
                any, any ? UiKit.Accent : UiKit.SurfaceAlt);

            var upgrade = UiKit.Row(list, 150, UiKit.Surface);
            int cap = NestActions.LevelCap(save.dragon.stage);
            string rate = Loc.F("{0} gold, {1} essence / h", Fmt.Num(NestActions.GoldPerHour(level)), NestActions.EssencePerHour(level).ToString("0.#"));
            if (level >= cap)
            {
                Cell(upgrade, rate + "\n<color=#9AA3B5>" + (level >= NestActions.MaxLevel ? Loc.T("Max level") : Loc.T("Evolve the dragon to raise the cap"))
                    + "</color>", 0.02f, 1, 28);
                return;
            }
            long cost = NestActions.UpgradeCost(level);
            Cell(upgrade, rate + "\n<color=#9AA3B5>" + Loc.F("Lv {0}: {1} gold, {2} essence / h", level + 1, Fmt.Num(NestActions.GoldPerHour(level + 1)),
                NestActions.EssencePerHour(level + 1).ToString("0.#")) + "</color>", 0.02f, 0.7f, 28);
            CellButton(upgrade, Loc.T("UPGRADE") + "\n" + Fmt.Num(cost), 0.72f, 1, () => Menu.Commit(NestActions.Upgrade(save, ServerClock.UtcNow)),
                save.wallet.gold >= cost);
        }

        /// <summary>ไทย | English. Each label is in its own language so either can be found.</summary>
        void MissionsRow()
        {
            var row = UiKit.Row(list, 90, UiKit.Surface);
            bool open = FeatureUnlocks.IsUnlocked(Save, Feature.Missions);
            int ready = open ? MissionActions.Claimable(Save, ServerClock.UtcNow) : 0;
            Cell(row, Loc.T("Daily & weekly missions") + (ready > 0 ? "  <color=#e9c46a>" + Loc.F("{0} to claim", ready) + "</color>" : ""), 0.02f, 0.7f);
            CellButton(row, open ? Loc.T("MISSIONS") : Loc.T("LOCKED") + "\n" + StoryActions.Label(FeatureUnlocks.UnlockStage(Feature.Missions)), 0.72f, 1,
                () => Menu.Show(ScreenId.Missions), open, ready > 0 ? UiKit.Accent : UiKit.SurfaceAlt);
        }

        void LanguageRow()
        {
            Header(list, Loc.T("Language"));
            var row = UiKit.Row(list, 100);
            CellButton(row, "ไทย", 0, 0.5f, () => Menu.SetLanguage(Language.Thai), Loc.Current != Language.Thai,
                Loc.Current == Language.Thai ? UiKit.Accent : UiKit.SurfaceAlt);
            CellButton(row, "English", 0.5f, 1, () => Menu.SetLanguage(Language.English), Loc.Current != Language.English,
                Loc.Current == Language.English ? UiKit.Accent : UiKit.SurfaceAlt);
        }

        string NextGoal()
        {
            var d = Save.dragon;
            if (d.stage >= DragonRules.MaxStage) return Loc.T("Max stage reached");
            if (d.level < DragonRules.LevelCap(d.stage)) return Loc.F("Reach Lv {0}", DragonRules.LevelCap(d.stage));
            var cost = DragonRules.EvolveCostFrom(d.stage);
            return Loc.F("Evolve: scales {0} / {1}", Save.wallet.ancientScales, cost.ancientScales);
        }

        public static string StageName(int stage)
        {
            switch (stage)
            {
                case 1: return Loc.T("Hatchling");
                case 2: return Loc.T("Drake");
                case 3: return Loc.T("Wyrm");
                case 4: return Loc.T("Elder");
                default: return Loc.T("Ancient");
            }
        }
    }
}
