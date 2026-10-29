using P2D.Domain;
using P2D.Presentation.Battle;
using P2D.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace P2D.Presentation.Screens
{
    /// <summary>
    /// Evolution cutscene (GDD §3.2: 5–8 s, skippable). A full-screen overlay on its own canvas:
    /// the old look glows and swells, a flash, then the new stage grows in with its name and any
    /// newly unlocked gear slot. Closes by itself at the end or on SKIP. Visual only — the
    /// evolution has already been applied by GameActions.Evolve.
    /// </summary>
    public sealed class EvolutionCutscene : MonoBehaviour
    {
        public const float Duration = 6f;
        const float SwapAt = 2.6f;

        public static EvolutionCutscene Current { get; private set; }

        DragonLook from;
        DragonLook to;
        RectTransform stageFrame;
        RectTransform figure;
        Image glow;
        Image flash;
        Text caption;
        Text unlocked;
        Button skip;
        string newSlots;
        bool swapped;
        float time;

        public Button SkipButton => skip;
        public bool Swapped => swapped;

        /// <summary>Shows the cutscene for the dragon in <paramref name="menu"/>'s save, which has just evolved from <paramref name="fromStage"/>.</summary>
        public static EvolutionCutscene Play(MainMenu menu, int fromStage, int toStage)
        {
            if (Current != null) Current.Close();
            var look = DragonLook.For(menu.Save);
            var go = new GameObject("Evolution Cutscene");
            var cutscene = go.AddComponent<EvolutionCutscene>();
            cutscene.Build(menu.Save.dragon.name, look.AtStage(fromStage), look.AtStage(toStage));
            Current = cutscene;
            return cutscene;
        }

        void Build(string dragonName, DragonLook oldLook, DragonLook newLook)
        {
            from = oldLook;
            to = newLook;

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50; // above the menu, its bars and the toast
            gameObject.AddComponent<CanvasScaler>();
            gameObject.AddComponent<PortraitCanvasScaler>();
            gameObject.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)transform;

            UiKit.Panel(root, "Backdrop", new Color(0.03f, 0.03f, 0.06f, 1f)).GetComponent<Image>().raycastTarget = true;

            var title = UiKit.Rect(root, "Title", new Vector2(0, 0.82f), new Vector2(1, 0.9f), Vector2.zero, Vector2.zero);
            UiKit.Label(title, Loc.T("EVOLUTION"), 72, TextAnchor.MiddleCenter, UiKit.Accent);

            var stages = UiKit.Rect(root, "Stages", new Vector2(0, 0.75f), new Vector2(1, 0.82f), Vector2.zero, Vector2.zero);
            UiKit.Label(stages, NestScreen.StageName(from.stage) + "  →  " + NestScreen.StageName(to.stage), 48, TextAnchor.MiddleCenter);

            stageFrame = UiKit.Rect(root, "Stage", new Vector2(0.08f, 0.3f), new Vector2(0.92f, 0.74f), Vector2.zero, Vector2.zero);
            var glowRect = UiKit.Rect(stageFrame, "Glow", new Vector2(0.1f, 0.05f), new Vector2(0.9f, 0.95f), Vector2.zero, Vector2.zero);
            glow = glowRect.gameObject.AddComponent<Image>();
            glow.sprite = DragonPortrait.Circle;
            glow.preserveAspect = true;
            glow.raycastTarget = false;
            glow.color = Fade(to.accent, 0);
            figure = DragonPortrait.Build(stageFrame, from);

            var captionRect = UiKit.Rect(root, "Caption", new Vector2(0.05f, 0.2f), new Vector2(0.95f, 0.29f), Vector2.zero, Vector2.zero);
            caption = UiKit.Label(captionRect, Loc.F("{0} is now a {1}!", dragonName, NestScreen.StageName(to.stage)), 44, TextAnchor.MiddleCenter);
            caption.gameObject.SetActive(false);

            newSlots = NewSlots(from.stage, to.stage);
            var unlockedRect = UiKit.Rect(root, "Unlocked", new Vector2(0.05f, 0.14f), new Vector2(0.95f, 0.2f), Vector2.zero, Vector2.zero);
            unlocked = UiKit.Label(unlockedRect, newSlots.Length > 0 ? Loc.F("New gear slot: {0}", newSlots) : "", 34, TextAnchor.MiddleCenter, UiKit.Good);
            unlocked.gameObject.SetActive(false);

            var skipRect = UiKit.Rect(root, "Skip", new Vector2(0.7f, 0.04f), new Vector2(0.95f, 0.1f), Vector2.zero, Vector2.zero);
            skip = UiKit.TextButton(skipRect, Loc.T("SKIP"), Close, UiKit.SurfaceAlt, 36);

            flash = UiKit.Panel(root, "Flash", new Color(1, 1, 1, 0)).GetComponent<Image>();
            flash.raycastTarget = false;
        }

        static string NewSlots(int fromStage, int toStage)
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (EquipSlot slot in System.Enum.GetValues(typeof(EquipSlot)))
                if (!DragonRules.IsSlotUnlocked(slot, fromStage) && DragonRules.IsSlotUnlocked(slot, toStage)) names.Add(Loc.Enum(slot));
            return string.Join(", ", names.ToArray());
        }

        void Update()
        {
            time += Time.unscaledDeltaTime;
            float fromSize = DragonPortrait.SizeOf(from.stage);
            float toSize = DragonPortrait.SizeOf(to.stage);

            if (time < SwapAt)
            {
                // Charging: the old form pulses and swells, the glow builds up behind it.
                float k = time / SwapAt;
                float pulse = 1 + Mathf.Sin(time * (6 + 14 * k)) * 0.04f * k;
                figure.localScale = Vector3.one * fromSize * (1 + 0.12f * k) * pulse;
                glow.color = Fade(to.AccentShown, 0.6f * k);
                glowRect.localScale = Vector3.one * (0.6f + 0.5f * k);
                flash.color = new Color(1, 1, 1, Mathf.Clamp01((k - 0.85f) / 0.15f));
            }
            else
            {
                if (!swapped) Swap();
                // Revealed: a white flash fades, the new form overshoots to its size and settles.
                float t = time - SwapAt;
                flash.color = new Color(1, 1, 1, Mathf.Clamp01(1 - t / 0.6f));
                float grow = Mathf.Clamp01(t / 0.9f);
                float overshoot = 1 + Mathf.Sin(grow * Mathf.PI) * 0.15f;
                figure.localScale = Vector3.one * Mathf.Lerp(fromSize, toSize, grow) * overshoot;
                glow.color = Fade(to.AccentShown, Mathf.Lerp(0.6f, 0.25f, Mathf.Clamp01(t / 1.5f)));
                glowRect.localScale = Vector3.one * (1.1f + Mathf.Sin(time * 2f) * 0.04f);
            }

            if (time >= Duration) Close();
        }

        RectTransform glowRect => (RectTransform)glow.transform;

        void Swap()
        {
            swapped = true;
            Destroy(figure.parent.gameObject);
            figure = DragonPortrait.Build(stageFrame, to);
            caption.gameObject.SetActive(true);
            unlocked.gameObject.SetActive(newSlots.Length > 0);
        }

        public void Close()
        {
            if (Current == this) Current = null;
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        static Color Fade(Color c, float alpha) => new Color(c.r, c.g, c.b, alpha);
    }
}
