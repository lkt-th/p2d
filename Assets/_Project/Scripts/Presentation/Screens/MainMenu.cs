using System;
using System.Collections.Generic;
using P2D.Domain;
using P2D.Presentation.Battle;
using P2D.Presentation.UI;
using P2D.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace P2D.Presentation.Screens
{
    public enum ScreenId { Nest, Dragon, Riders, Spire, Summon, Hunt, Shop, Story, Hatch, Missions }

    /// <summary>
    /// The menu scene: loads the save, builds the portrait shell (resource bar, content area,
    /// bottom navigation, toast) and switches between the greybox screens. Every player action
    /// goes through GameActions on the live PlayerSave, then <see cref="Commit"/> saves and redraws.
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        // Bottom bar order from the mockup; Story and Hunt share ADVENTURE with the spire, Summon opens from RIDERS.
        static readonly string[] NavLabels = { "DRAGON", "RIDERS", "NEST", "ADVENTURE", "SHOP" };
        static readonly ScreenId[] NavTargets = { ScreenId.Dragon, ScreenId.Riders, ScreenId.Nest, ScreenId.Story, ScreenId.Shop };

        /// <summary>Where to land when the menu reloads after a battle.</summary>
        static ScreenId returnScreen = ScreenId.Spire;

        readonly Dictionary<ScreenId, GameScreen> screens = new Dictionary<ScreenId, GameScreen>();
        readonly List<Image> navImages = new List<Image>();
        readonly SpireTuning tuning = new SpireTuning();
        Text resourceText;
        GameObject resourceBar;
        GameObject navBar;
        Text toastText;
        float toastUntil;
        ScreenId current;

        public PlayerSave Save => SaveManager.Instance != null ? SaveManager.Instance.Current : null;
        public SpireTuning Tuning => tuning;
        public ScreenId Current => current;

        /// <summary>Client-side randomness for drops and rolls (the server re-rolls authoritatively).</summary>
        public IRandom Random { get; } = new SeededRandom(Environment.TickCount);

        void Awake()
        {
            if (SaveManager.Instance == null) new GameObject("SaveManager").AddComponent<SaveManager>();
            LanguageSetting.Apply();
            BuildShell();

            if (Save == null)
            {
                Toast(Loc.F("Save could not be loaded: {0}", SaveManager.Instance.LastLoad.error), 30);
                return;
            }

            GameActions.EnsureStarterKit(Save, Random);
            SaveManager.Instance.MarkDirty();

            AddScreen(ScreenId.Nest, new NestScreen());
            AddScreen(ScreenId.Dragon, new DragonScreen());
            AddScreen(ScreenId.Riders, new RidersScreen());
            AddScreen(ScreenId.Spire, new SpireScreen());
            AddScreen(ScreenId.Summon, new SummonScreen());
            AddScreen(ScreenId.Hunt, new HuntScreen());
            AddScreen(ScreenId.Shop, new ShopScreen());
            AddScreen(ScreenId.Story, new StoryScreen());
            AddScreen(ScreenId.Hatch, new HatchScreen());
            AddScreen(ScreenId.Missions, new MissionsScreen());

            bool returningFromBattle = !string.IsNullOrEmpty(BattleHandoff.LastMessage);
            Show(!Save.dragon.hatched ? ScreenId.Hatch : returningFromBattle ? returnScreen : ScreenId.Nest);
            if (returningFromBattle)
            {
                Toast(BattleHandoff.LastMessage, 5);
                BattleHandoff.LastMessage = null;
            }
        }

        void AddScreen(ScreenId id, GameScreen screen)
        {
            screen.Create(this, content, id.ToString());
            screens[id] = screen;
        }

        public void Show(ScreenId id)
        {
            current = id;
            // The egg screen stands alone: no resources or tabs until the dragon has a name.
            bool hatched = Save == null || Save.dragon.hatched;
            if (resourceBar != null) resourceBar.SetActive(hatched);
            if (navBar != null) navBar.SetActive(hatched);
            foreach (var pair in screens) pair.Value.SetVisible(pair.Key == id);
            for (int i = 0; i < navImages.Count; i++) navImages[i].color = NavTargets[i] == NavTab(id) ? UiKit.Accent : UiKit.Surface;
            RefreshResources();
        }

        static ScreenId NavTab(ScreenId id)
        {
            switch (id)
            {
                case ScreenId.Hunt:
                case ScreenId.Spire: return ScreenId.Story;
                case ScreenId.Summon: return ScreenId.Riders;
                case ScreenId.Missions: return ScreenId.Nest;
                default: return id;
            }
        }

        /// <summary>
        /// Fights an already-rolled hunting encounter in the battle scene (stamina is spent by the
        /// caller via HuntActions.BeginRun); rewards are applied on return.
        /// </summary>
        public void StartHuntBattle(int zone, EncounterKind kind)
        {
            var setup = HuntActions.BuildBattle(Save, zone, kind, Environment.TickCount);
            SaveManager.Instance.MarkDirty();
            SaveManager.Instance.SaveNow();
            returnScreen = ScreenId.Hunt;
            var zoneInfo = HuntActions.Zone(zone);

            BattleHandoff.Pending = new BattleHandoff.Request
            {
                setup = setup,
                title = Loc.T(zoneInfo.name) + "  ·  " + Loc.Enum(kind),
                onFinished = (outcome, inputs) =>
                {
                    var live = SaveManager.Instance.Current;
                    var run = HuntActions.ApplyResult(live, zone, kind, outcome, new SeededRandom(Environment.TickCount));
                    SaveManager.Instance.MarkDirty();
                    SaveManager.Instance.SaveNow();
                    BattleHandoff.LastMessage = HuntScreen.Describe(run);
                },
            };
            SceneManager.LoadScene(BattleHandoff.BattleScene);
        }

        /// <summary>Saves after a successful action, shows its message and redraws the screen.</summary>
        public void Commit(ActionResult result, string successText = null)
        {
            if (result.ok)
            {
                SaveManager.Instance.MarkDirty();
                if (!string.IsNullOrEmpty(successText ?? result.message)) Toast(successText ?? result.message);
            }
            else
            {
                Toast(result.message);
            }
            Redraw();
        }

        public void Redraw()
        {
            if (screens.ContainsKey(current)) screens[current].Refresh();
            RefreshResources();
        }

        public void Toast(string message, float seconds = 2.5f)
        {
            toastText.text = message;
            toastText.transform.parent.gameObject.SetActive(true);
            toastUntil = Time.unscaledTime + seconds;
        }

        /// <summary>Switches the UI language and reloads the menu so every label is rebuilt in it.</summary>
        public void SetLanguage(Language language)
        {
            LanguageSetting.Set(language);
            SaveManager.Instance.SaveNow();
            SceneManager.LoadScene(BattleHandoff.MenuScene);
        }

        /// <summary>Fights the next story stage in the battle scene; the first clear is paid on return.</summary>
        public void StartStoryBattle(int index)
        {
            var setup = StoryActions.BuildBattle(Save, index, Environment.TickCount, tuning);
            SaveManager.Instance.MarkDirty();
            SaveManager.Instance.SaveNow();
            returnScreen = ScreenId.Story;
            var chapter = StoryActions.Chapter(StoryActions.ChapterOf(index));

            BattleHandoff.Pending = new BattleHandoff.Request
            {
                setup = setup,
                title = Loc.F("Story {0}", StoryActions.Label(index)) + "  ·  " + Loc.T(chapter.name),
                onFinished = (outcome, inputs) =>
                {
                    var live = SaveManager.Instance.Current;
                    var reward = StoryActions.ApplyResult(live, index, outcome, new SeededRandom(Environment.TickCount));
                    SaveManager.Instance.MarkDirty();
                    SaveManager.Instance.SaveNow();
                    BattleHandoff.LastMessage = reward.firstClear
                        ? Loc.F("Stage {0} cleared! +{1} gold, +{2} essence", StoryActions.Label(index), Fmt.Num(reward.gold), reward.essence)
                          + (reward.gear != null ? Loc.F(", new {0} {1}", Loc.Enum(reward.gear.rarity), Loc.Enum(reward.gear.slot)) : "")
                          + (reward.riderId.Length > 0 ? Loc.F(reward.riderIsNew ? ", {0} joins you" : ", rider {0} (copy)", Loc.Rider(reward.riderId)) : "")
                          + (reward.unlocked.Count > 0 ? "\n" + Loc.F("Unlocked: {0}", string.Join(", ", reward.unlocked.ConvertAll(FeatureUnlocks.Name).ToArray())) : "")
                        : Loc.F("Stage {0}: {1}", StoryActions.Label(index), Loc.Enum(outcome));
                },
            };
            SceneManager.LoadScene(BattleHandoff.BattleScene);
        }

        /// <summary>Starts a spire battle in the battle scene; the result is applied to the save on return.</summary>
        public void StartSpireBattle(int floor)
        {
            var save = Save;
            var theme = SpireThemes.ForTime(ServerClock.UtcNow);
            var setup = GameActions.BuildSpireBattle(save, floor, theme.element, Environment.TickCount, tuning);
            SaveManager.Instance.SaveNow();
            returnScreen = ScreenId.Spire;

            BattleHandoff.Pending = new BattleHandoff.Request
            {
                setup = setup,
                title = Loc.F("Floor {0}", floor) + "  ·  " + Loc.T(theme.name),
                onFinished = (outcome, inputs) =>
                {
                    // Runs in the battle scene: use the singleton, not this destroyed menu.
                    var live = SaveManager.Instance.Current;
                    var reward = GameActions.ApplySpireResult(live, floor, outcome, theme.element, new SeededRandom(Environment.TickCount));
                    SaveManager.Instance.MarkDirty();
                    SaveManager.Instance.SaveNow();
                    BattleHandoff.LastMessage = reward.firstClear
                        ? Loc.F("Floor {0} cleared! +{1} gold, +{2} forge stones, new {3} {4}", floor, Fmt.Num(reward.resources.gold),
                            reward.resources.forgeStones, Loc.Enum(reward.gear.rarity), Loc.Enum(reward.gear.slot))
                        : outcome == BattleOutcome.Victory ? Loc.T("Victory (no first-clear reward)") : Loc.F("Floor {0}: {1}", floor, Loc.Enum(outcome));
                },
            };
            SceneManager.LoadScene(BattleHandoff.BattleScene);
        }

        void Update()
        {
            if (toastText != null && toastText.transform.parent.gameObject.activeSelf && Time.unscaledTime > toastUntil)
                toastText.transform.parent.gameObject.SetActive(false);
        }

        // ---- shell ---------------------------------------------------------------------

        RectTransform content;

        void BuildShell()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            var cam = new GameObject("Menu Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = UiKit.Ground;
            cam.tag = "MainCamera";

            var canvasGo = new GameObject("Menu");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<PortraitCanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
            var canvasRect = (RectTransform)canvasGo.transform;
            UiKit.Panel(canvasRect, "Background", UiKit.Ground);

            var safe = UiKit.Fill(canvasRect, "Safe Area");
            safe.gameObject.AddComponent<SafeAreaFitter>();

            var top = UiKit.Rect(safe, "Resources", new Vector2(0, 1), Vector2.one, new Vector2(0, -110), Vector2.zero);
            top.gameObject.AddComponent<Image>().color = UiKit.Surface;
            resourceBar = top.gameObject;
            resourceText = UiKit.Label(UiKit.Slice(UiKit.Fill(top, "Text"), 0, 1, 24), "", 28);

            content = UiKit.Rect(safe, "Content", Vector2.zero, Vector2.one, new Vector2(0, 150), new Vector2(0, -110));

            var nav = UiKit.Rect(safe, "Navigation", Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 150));
            nav.gameObject.AddComponent<Image>().color = UiKit.Surface;
            navBar = nav.gameObject;
            for (int i = 0; i < NavLabels.Length; i++)
            {
                var id = NavTargets[i];
                var cell = UiKit.Slice(UiKit.Fill(nav, "Nav " + id), i / (float)NavLabels.Length, (i + 1) / (float)NavLabels.Length, 10);
                var button = UiKit.TextButton(cell, Loc.T(NavLabels[i]), () => Show(id), UiKit.Surface, 28);
                navImages.Add(button.GetComponent<Image>());
            }

            var toast = UiKit.Rect(safe, "Toast", new Vector2(0, 0), new Vector2(1, 0), new Vector2(40, 170), new Vector2(-40, 290));
            toast.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.85f);
            toastText = UiKit.Label(UiKit.Slice(UiKit.Fill(toast, "Text"), 0, 1, 16), "", 30, TextAnchor.MiddleCenter);
            toast.gameObject.SetActive(false);
        }

        void RefreshResources()
        {
            var save = Save;
            if (save == null || resourceText == null) return;
            MissionActions.Sync(save, ServerClock.UtcNow); // events from actions without a clock (battles, enhance) land in today
            var w = save.wallet;
            resourceText.text = Loc.F("Gold {0}   Gems {1}   Essence {2}   Forge {3}   Sigils {4}   Scales {5}", Fmt.Num(w.gold),
                Fmt.Num(w.crystals), w.dragonEssence, w.forgeStones, w.pactSigils, w.ancientScales);
        }
    }

    /// <summary>One tab of the menu. Screens rebuild their content in Refresh (cheap at greybox scale).</summary>
    public abstract class GameScreen
    {
        protected MainMenu Menu { get; private set; }
        protected RectTransform Root { get; private set; }
        protected PlayerSave Save => Menu.Save;

        public void Create(MainMenu menu, RectTransform parent, string name)
        {
            Menu = menu;
            Root = UiKit.Fill(parent, name);
            Build();
        }

        public void SetVisible(bool visible)
        {
            Root.gameObject.SetActive(visible);
            if (visible) Refresh();
        }

        protected abstract void Build();
        public abstract void Refresh();

        protected static Text Cell(RectTransform row, string text, float x0, float x1, int size = 30,
            TextAnchor anchor = TextAnchor.MiddleLeft, Color? color = null)
        {
            var cell = UiKit.Slice(UiKit.Fill(row, "Cell"), x0, x1);
            return UiKit.Label(cell, text, size, anchor, color);
        }

        protected static Button CellButton(RectTransform row, string text, float x0, float x1, UnityEngine.Events.UnityAction onClick,
            bool interactable = true, Color? color = null, int size = 28)
        {
            var cell = UiKit.Slice(UiKit.Fill(row, "Cell"), x0, x1);
            var button = UiKit.TextButton(cell, text, onClick, color ?? UiKit.SurfaceAlt, size);
            button.interactable = interactable;
            return button;
        }

        /// <summary>STORY | SPIRE | HUNTING switch at the top of the adventure screens.</summary>
        protected void AdventureTabs(RectTransform list, ScreenId active)
        {
            var row = UiKit.Row(list, 100);
            CellButton(row, Loc.T("STORY"), 0, 1 / 3f, () => Menu.Show(ScreenId.Story), true,
                active == ScreenId.Story ? UiKit.Accent : UiKit.SurfaceAlt);
            CellButton(row, Loc.T("WEEKLY SPIRE"), 1 / 3f, 2 / 3f, () => Menu.Show(ScreenId.Spire), true,
                active == ScreenId.Spire ? UiKit.Accent : UiKit.SurfaceAlt);
            CellButton(row, Loc.T("HUNTING"), 2 / 3f, 1, () => Menu.Show(ScreenId.Hunt), true,
                active == ScreenId.Hunt ? UiKit.Accent : UiKit.SurfaceAlt);
        }

        /// <summary>Draws a "locked" card with a shortcut to the story and returns true when the feature is still locked.</summary>
        protected bool LockedCard(RectTransform list, Feature feature)
        {
            if (FeatureUnlocks.IsUnlocked(Save, feature)) return false;
            var row = UiKit.Row(list, 170, UiKit.Surface);
            Cell(row, Loc.F("<b>{0}</b> is locked", FeatureUnlocks.Name(feature)) + "\n<color=#9AA3B5>"
                + Loc.F("{0} to open it", FeatureUnlocks.Hint(feature)) + "</color>", 0.02f, 0.7f, 30);
            CellButton(row, Loc.T("STORY"), 0.72f, 1, () => Menu.Show(ScreenId.Story), true, UiKit.Accent);
            return true;
        }

        protected static void Header(RectTransform list, string text)
        {
            var row = UiKit.Row(list, 64);
            Cell(row, text, 0, 1, 34, TextAnchor.LowerLeft, UiKit.Accent);
        }
    }

    /// <summary>Display formatting shared by the greybox screens.</summary>
    public static class Fmt
    {
        public static string Num(long v)
        {
            if (v >= 1000000) return (v / 1000000.0).ToString("0.##") + "M";
            if (v >= 10000) return (v / 1000.0).ToString("0.#") + "K";
            return v.ToString("N0");
        }

        public static string Stars(int star) => new string('*', star) + new string('-', RiderRules.MaxStar - star);

        public static string Stat(StatModifier m)
        {
            bool percent = m.type == ModType.Percent;
            string name = Loc.Enum(m.stat) + (percent && !StatSheet.IsRateStat(m.stat) ? "%" : "");
            string value = percent ? (m.value * 100).ToString("0.#") + "%" : Math.Round(m.value).ToString("N0");
            return name + " +" + value;
        }

        public static string Item(EquipmentState item)
        {
            var main = EquipmentRules.MainModifier(item);
            var parts = new List<string>();
            foreach (var s in item.subs ?? new StatModifier[0]) parts.Add(Stat(s));
            var set = SetCatalog.Find(item.setId);
            return "<b>" + Loc.Enum(item.slot) + "</b>  " + RarityTag(item.rarity) + " +" + item.enhance + "   " + Loc.Enum(item.path) + " "
                + AffinityRules.ForEquipment(item.rarity, item.enhance) + (set != null ? "   <color=#e9c46a>" + Loc.T(set.name) + "</color>" : "") + "\n" + Stat(main)
                + (parts.Count > 0 ? "   <color=#a3a8ba>" + string.Join(", ", parts.ToArray()) + "</color>" : "");
        }

        public static string RarityTag(Rarity r)
        {
            string color;
            switch (r)
            {
                case Rarity.Uncommon: color = "#6cc27a"; break;
                case Rarity.Rare: color = "#5aa9e6"; break;
                case Rarity.Epic: color = "#b07ceb"; break;
                case Rarity.Legendary: color = "#f0a04b"; break;
                case Rarity.Mythic: color = "#e5534b"; break;
                default: color = "#c8ccd6"; break;
            }
            return "<color=" + color + ">" + Loc.Enum(r) + "</color>";
        }

        public static string Duration(TimeSpan t) =>
            t.TotalDays >= 1 ? Loc.F("{0}d {1}h", (int)t.TotalDays, t.Hours) : Loc.F("{0}h {1}m", t.Hours, t.Minutes);
    }
}
