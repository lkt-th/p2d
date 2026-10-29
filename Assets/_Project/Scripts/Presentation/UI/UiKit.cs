using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace P2D.Presentation.UI
{
    /// <summary>
    /// Code-built uGUI helpers for the greybox screens (reference 1080×1920). Real art will
    /// replace these with prefabs; until then every screen is built from these few pieces.
    /// </summary>
    public static class UiKit
    {
        public static readonly Color Ground = new Color(0.06f, 0.07f, 0.09f);
        public static readonly Color Surface = new Color(0.10f, 0.11f, 0.15f);
        public static readonly Color SurfaceAlt = new Color(0.14f, 0.15f, 0.21f);
        public static readonly Color Accent = new Color(0.94f, 0.54f, 0.29f);
        public static readonly Color Muted = new Color(0.64f, 0.66f, 0.73f);
        public static readonly Color Good = new Color(0.42f, 0.76f, 0.48f);
        public static readonly Color Bad = new Color(0.9f, 0.33f, 0.29f);

        /// <summary>Resources path of the UI font (Noto Sans Thai: Thai + Latin, SIL OFL).</summary>
        public const string ThaiFontResource = "NotoSansThai";

        static Font font;

        /// <summary>
        /// The font for every UI <see cref="Text"/>: Noto Sans Thai from Resources (so it ships in
        /// player builds), or the built-in legacy font if that asset is missing.
        /// </summary>
        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.Load<Font>(ThaiFontResource);
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        public static RectTransform Fill(Transform parent, string name) => Rect(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        public static RectTransform Panel(Transform parent, string name, Color color)
        {
            var rt = Fill(parent, name);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            return rt;
        }

        public static Text Label(Transform parent, string text, int size = 32, TextAnchor anchor = TextAnchor.MiddleLeft, Color? color = null)
        {
            var rt = Fill(parent, "Label");
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color ?? Color.white;
            t.text = text;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button TextButton(Transform parent, string text, UnityAction onClick, Color? color = null, int size = 30)
        {
            var rt = Fill(parent, "Button " + text);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color ?? SurfaceAlt;
            var button = rt.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.55f);
            colors.highlightedColor = new Color(1f, 0.9f, 0.82f);
            button.colors = colors;
            button.onClick.AddListener(onClick);
            Label(rt, text, size, TextAnchor.MiddleCenter);
            return button;
        }

        /// <summary>Places <paramref name="rt"/> at a fixed size from an anchor point.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            return rt;
        }

        /// <summary>Horizontal slice of a row: x0..x1 as fractions of the row width.</summary>
        public static RectTransform Slice(RectTransform rt, float x0, float x1, float padding = 8)
        {
            rt.anchorMin = new Vector2(x0, 0);
            rt.anchorMax = new Vector2(x1, 1);
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
            return rt;
        }

        /// <summary>A vertical scroll list filling <paramref name="parent"/>; returns the content to add rows to.</summary>
        public static RectTransform ScrollList(Transform parent)
        {
            var viewport = Fill(parent, "Scroll");
            viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40;

            var content = Rect(viewport, "Content", new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(0.5f, 1);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 20, 40);
            layout.spacing = 14;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            scroll.viewport = viewport;
            return content;
        }

        /// <summary>A fixed-height row in a scroll list.</summary>
        public static RectTransform Row(Transform list, float height, Color? color = null)
        {
            var rt = Rect(list, "Row", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            rt.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            if (color.HasValue) rt.gameObject.AddComponent<Image>().color = color.Value;
            return rt;
        }

        /// <summary>A bar inside <paramref name="parent"/>; returns the fill (set anchorMax.x to the fraction).</summary>
        public static RectTransform Bar(RectTransform parent, Color back, Color front, float fraction)
        {
            parent.gameObject.AddComponent<Image>().color = back;
            var fill = Fill(parent, "Fill");
            fill.gameObject.AddComponent<Image>().color = front;
            fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1);
            return fill;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var child = t.GetChild(i);
                child.SetParent(null, false); // leave the layout now, not at end of frame
                Object.Destroy(child.gameObject);
            }
        }
    }
}
