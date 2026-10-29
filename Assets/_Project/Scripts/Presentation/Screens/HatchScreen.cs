using P2D.Domain;
using P2D.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace P2D.Presentation.Screens
{
    /// <summary>
    /// First launch (GDD §11 step 1): tap the egg until it cracks, then name the dragon. The menu
    /// hides its bars until this is done, then sends the player to story 1-1.
    /// </summary>
    public sealed class HatchScreen : GameScreen
    {
        static readonly string[] TapLines = { "An egg, warm to the touch.\nTap it.", "Something moves inside…", "Cracks race across the shell!" };

        RectTransform panel;
        int taps;

        protected override void Build() => panel = UiKit.Fill(Root, "Hatch");

        public override void Refresh()
        {
            UiKit.Clear(panel);
            bool cracked = taps >= GameActions.HatchTaps;

            var title = UiKit.Rect(panel, "Title", new Vector2(0, 0.78f), new Vector2(1, 0.95f), new Vector2(40, 0), new Vector2(-40, 0));
            UiKit.Label(title, cracked ? "Name your dragon" : TapLines[Mathf.Min(taps, TapLines.Length - 1)], 44, TextAnchor.MiddleCenter);

            // The egg: brighter with every tap, a hatchling-blue block once it opens.
            var eggRect = UiKit.Rect(panel, "Egg", new Vector2(0.28f, 0.38f), new Vector2(0.72f, 0.76f), Vector2.zero, Vector2.zero);
            float glow = taps / (float)GameActions.HatchTaps;
            var eggColor = cracked ? new Color(0.3f, 0.55f, 0.9f) : Color.Lerp(new Color(0.78f, 0.70f, 0.55f), new Color(0.98f, 0.80f, 0.45f), glow);
            var egg = UiKit.TextButton(eggRect, cracked ? "" : new string('*', taps), () => { taps++; Refresh(); }, eggColor, 60);
            egg.interactable = !cracked;
            var tint = egg.colors;
            tint.disabledColor = Color.white; // the hatchling shows in full colour, not greyed out
            egg.colors = tint;

            if (!cracked)
            {
                var hint = UiKit.Rect(panel, "Hint", new Vector2(0, 0.28f), new Vector2(1, 0.36f), Vector2.zero, Vector2.zero);
                UiKit.Label(hint, "Tap " + (GameActions.HatchTaps - taps) + " more", 32, TextAnchor.MiddleCenter, UiKit.Muted);
                return;
            }

            var field = UiKit.Rect(panel, "Name", new Vector2(0.15f, 0.26f), new Vector2(0.85f, 0.34f), Vector2.zero, Vector2.zero);
            var input = NameField(field, Save.dragon.name);

            var go = UiKit.Rect(panel, "Hatch Button", new Vector2(0.2f, 0.12f), new Vector2(0.8f, 0.22f), Vector2.zero, Vector2.zero);
            UiKit.TextButton(go, "HATCH", () =>
            {
                var result = GameActions.Hatch(Save, input.text);
                Menu.Commit(result);
                if (result.ok) Menu.Show(ScreenId.Story);
            }, UiKit.Accent, 44);
        }

        static InputField NameField(RectTransform parent, string value)
        {
            parent.gameObject.AddComponent<Image>().color = UiKit.SurfaceAlt;
            var input = parent.gameObject.AddComponent<InputField>();
            var textRect = UiKit.Rect(parent, "Text", Vector2.zero, Vector2.one, new Vector2(24, 0), new Vector2(-24, 0));
            input.textComponent = UiKit.Label(textRect, "", 40, TextAnchor.MiddleCenter);
            input.textComponent.supportRichText = false;
            var placeholderRect = UiKit.Rect(parent, "Placeholder", Vector2.zero, Vector2.one, new Vector2(24, 0), new Vector2(-24, 0));
            input.placeholder = UiKit.Label(placeholderRect, "Dragon name", 40, TextAnchor.MiddleCenter, UiKit.Muted);
            input.characterLimit = GameActions.MaxNameLength;
            input.text = value;
            return input;
        }
    }
}
