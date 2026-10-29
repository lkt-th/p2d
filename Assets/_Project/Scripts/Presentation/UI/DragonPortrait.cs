using P2D.Domain;
using P2D.Presentation.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace P2D.Presentation.UI
{
    /// <summary>
    /// A front view of a <see cref="DragonLook"/> made of flat uGUI shapes (circles, slabs and
    /// diamonds), for menus: the Nest hero and the evolution cutscene. Square, grows from the
    /// bottom centre with the stage; no camera or render texture, so it is stable in batch mode.
    /// </summary>
    public static class DragonPortrait
    {
        static Sprite circle;

        /// <summary>A soft-edged white circle sprite, made once in code.</summary>
        public static Sprite Circle
        {
            get
            {
                if (circle != null) return circle;
                const int size = 128;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Dragon Portrait Circle" };
                var pixels = new Color32[size * size];
                float r = size / 2f;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                        byte a = (byte)(Mathf.Clamp01(r - d) * 255);
                        pixels[y * size + x] = new Color32(255, 255, 255, a);
                    }
                tex.SetPixels32(pixels);
                tex.Apply();
                circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
                return circle;
            }
        }

        /// <summary>
        /// Builds the portrait inside <paramref name="parent"/> (fitted to a centred square) and
        /// returns the figure, whose scale is the stage size (1 = the biggest stage).
        /// </summary>
        public static RectTransform Build(RectTransform parent, DragonLook look)
        {
            var frame = UiKit.Fill(parent, "Dragon Portrait");
            var fitter = frame.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1;

            var figure = UiKit.Fill(frame, "Figure");
            figure.pivot = new Vector2(0.5f, 0.05f);
            figure.localScale = Vector3.one * SizeOf(look.stage);
            Draw(figure, look);
            return figure;
        }

        /// <summary>Portrait scale for a stage: the Ancient fills the frame.</summary>
        public static float SizeOf(int stage) => DragonLook.StageScale(stage) / DragonLook.StageScale(DragonRules.MaxStage);

        static void Draw(RectTransform f, DragonLook look)
        {
            float w = look.bulk;
            Color accent = look.AccentShown;
            Color horn = look.glow ? accent : Color.Lerp(look.belly, Color.white, 0.2f);
            Color dark = look.body * 0.8f;
            dark.a = 1;

            if (look.glow) Shape(f, "Aura", 0.5f, 0.45f, 1.0f, 0.95f, 0, Fade(accent, 0.2f), true);

            if (look.tail)
            {
                Shape(f, "Tail", 0.74f, 0.17f, 0.4f, 0.09f, 22, dark, false);
                Shape(f, "Tail Blade", 0.93f, 0.25f, 0.1f, 0.1f, 45, horn, false);
            }

            if (look.wings)
            {
                float len = 0.32f * look.wingLength;
                for (int side = -1; side <= 1; side += 2)
                {
                    float cx = 0.5f + side * (0.17f * w + len / 2);
                    Shape(f, "Wing", cx, 0.54f, len, 0.2f, -22 * side, look.wing, false);
                    Shape(f, "Wing Bone", cx, 0.62f, len * 1.02f, 0.035f, -28 * side, dark, false);
                }
            }

            if (look.backPlate)
            {
                Shape(f, "Back Plate", 0.5f, 0.47f, 0.5f * w, 0.32f, 0, accent, false);
                for (int i = -1; i <= 1; i++) Shape(f, "Plate Ridge", 0.5f + i * 0.12f * w, 0.62f, 0.08f, 0.08f, 45, accent * 0.85f, false);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                float fx = 0.5f + side * 0.14f * w;
                Shape(f, "Foot", fx, 0.14f, 0.14f, 0.1f, 0, dark, true);
                for (int c = -1; c <= 1; c += 2)
                    Shape(f, "Claw", fx + c * 0.03f, 0.09f, 0.04f * look.clawSize, 0.04f * look.clawSize, 45, horn, false);
            }

            Shape(f, "Body", 0.5f, 0.36f, 0.42f * w, 0.44f, 0, look.body, true);
            Shape(f, "Belly", 0.5f, 0.33f, 0.26f * w, 0.3f, 0, look.belly, true);

            if (look.horns)
                for (int side = -1; side <= 1; side += 2)
                {
                    // A slab leaning outwards with a diamond tip at its top end.
                    float h = 0.08f * look.hornSize, hx = 0.5f + side * (0.1f + 0.02f * look.hornSize), hy = 0.76f + h / 2;
                    float lean = 35 * Mathf.Deg2Rad;
                    Shape(f, "Horn", hx, hy, 0.06f, h, -35 * side, horn, false);
                    Shape(f, "Horn Tip", hx + side * Mathf.Sin(lean) * h / 2, hy + Mathf.Cos(lean) * h / 2, 0.04f, 0.04f, 45, horn, false);
                }

            Shape(f, "Head", 0.5f, 0.68f, 0.3f, 0.27f, 0, look.body * 0.95f, true);
            Shape(f, "Snout", 0.5f, 0.615f, 0.17f, 0.12f, 0, look.belly, true);
            for (int side = -1; side <= 1; side += 2)
                Shape(f, "Eye", 0.5f + side * 0.065f, 0.71f, 0.05f, 0.05f, 0, accent, true);

            if (look.crest) Shape(f, "Crest", 0.5f, 0.82f, 0.07f, 0.07f, 45, accent, false);

            if (look.orb)
            {
                float size = 0.12f + 0.015f * look.tier;
                Shape(f, "Orb Halo", 0.84f, 0.6f, size * 1.8f, size * 1.8f, 0, Fade(accent, 0.3f), true);
                Shape(f, "Orb", 0.84f, 0.6f, size, size, 0, accent, true);
            }
        }

        static Color Fade(Color c, float alpha) => new Color(c.r, c.g, c.b, alpha);

        /// <summary>One flat part: centre and size in fractions of the square, rotated about its centre.</summary>
        static void Shape(RectTransform parent, string name, float cx, float cy, float w, float h, float angle, Color color, bool round)
        {
            var rt = UiKit.Rect(parent, name, new Vector2(cx - w / 2, cy - h / 2), new Vector2(cx + w / 2, cy + h / 2), Vector2.zero, Vector2.zero);
            rt.localRotation = Quaternion.Euler(0, 0, angle);
            var image = rt.gameObject.AddComponent<Image>();
            if (round) image.sprite = Circle;
            if (color.a <= 0) color.a = 1;
            image.color = color;
            image.raycastTarget = false;
        }
    }
}
