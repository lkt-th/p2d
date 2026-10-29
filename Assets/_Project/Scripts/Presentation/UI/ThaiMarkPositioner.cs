using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace P2D.Presentation.UI
{
    /// <summary>
    /// Legacy <see cref="Text"/> has no OpenType shaping, so every Thai mark is drawn at its default
    /// spot: a tone mark lands on top of an upper vowel (ที่), marks hit the ascender of ป ฝ ฟ ฬ, and
    /// a lower vowel hides in the tail of ญ ฐ ฎ ฏ. This effect moves those glyph quads after the text
    /// mesh is built (the "Thai Font Adjuster" idea, without needing a PUA font).
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class ThaiMarkPositioner : BaseMeshEffect
    {
        const float Gap = 0.04f;           // em, space between stacked marks
        const float AscenderShift = 0.12f; // em, upper marks move left of a tall consonant

        static readonly List<UIVertex> Verts = new List<UIVertex>();

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            var text = (Text)graphic;
            if (!NeedsShaping(text.text)) return;

            Verts.Clear();
            var v = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                Verts.Add(v);
            }
            // Vertices are in local units, where one em equals the font size.
            float scale = text.resizeTextForBestFit ? text.cachedTextGenerator.fontSizeUsedForBestFit / text.pixelsPerUnit / text.fontSize : 1f;
            if (!Apply(text.text, text.supportRichText, text.fontSize, scale, Verts)) return;
            for (int i = 0; i < Verts.Count; i++) vh.SetUIVertex(Verts[i], i);
        }

        public static bool NeedsShaping(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s)
                if (IsAbove(c) || IsBelow(c)) return true;
            return false;
        }

        /// <summary>
        /// Moves the quads (4 vertices per visible glyph, in text order) of Thai marks in
        /// <paramref name="verts"/>. Returns false, leaving them untouched, when the quad count does
        /// not match the visible characters of <paramref name="s"/>.
        /// </summary>
        public static bool Apply(string s, bool richText, float fontSize, float scale, List<UIVertex> verts)
        {
            var glyphs = new List<int>();
            var ems = new List<float>();
            VisibleChars(s, richText, fontSize, glyphs, ems);
            if (glyphs.Count * 4 != verts.Count) return false;

            int baseQuad = -1;
            float aboveTop = 0;
            bool hasAbove = false;
            for (int k = 0; k < glyphs.Count; k++)
            {
                char c = s[glyphs[k]];
                float em = ems[k] * scale;
                if (IsAbove(c))
                {
                    if (baseQuad < 0) continue;
                    float dx = IsAscender(s[glyphs[baseQuad]]) ? -AscenderShift * em : 0;
                    float dy = 0;
                    float bottom = Bottom(verts, k);
                    float floor = hasAbove ? aboveTop
                        : k + 1 < glyphs.Count && s[glyphs[k + 1]] == 'ำ' ? Top(verts, k + 1) // tone before ำ: over its ring
                        : float.NegativeInfinity;
                    if (bottom < floor + Gap * em) dy = floor + Gap * em - bottom;
                    Move(verts, k, dx, dy);
                    aboveTop = Top(verts, k);
                    hasAbove = true;
                }
                else if (IsBelow(c))
                {
                    if (baseQuad < 0) continue;
                    float baseBottom = Bottom(verts, baseQuad);
                    float top = Top(verts, k);
                    if (top > baseBottom) Move(verts, k, 0, baseBottom - Gap * em - top); // only into a tail
                }
                else
                {
                    baseQuad = k;
                    hasAbove = false;
                }
            }
            return true;
        }

        /// <summary>Indexes of characters that get a quad: not whitespace, not a rich-text tag.</summary>
        static void VisibleChars(string s, bool richText, float fontSize, List<int> glyphs, List<float> ems)
        {
            var sizes = new Stack<float>();
            float size = fontSize;
            for (int i = 0; i < s.Length; i++)
            {
                if (richText && s[i] == '<')
                {
                    int end = s.IndexOf('>', i);
                    if (end > i)
                    {
                        string tag = s.Substring(i + 1, end - i - 1);
                        if (tag == "b" || tag == "/b" || tag == "i" || tag == "/i" || tag == "/color" || tag == "/material"
                            || tag.StartsWith("color=") || tag.StartsWith("material="))
                        {
                            i = end;
                            continue;
                        }
                        if (tag.StartsWith("size=") && float.TryParse(tag.Substring(5), System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out float n))
                        {
                            sizes.Push(size);
                            size = n;
                            i = end;
                            continue;
                        }
                        if (tag == "/size")
                        {
                            if (sizes.Count > 0) size = sizes.Pop();
                            i = end;
                            continue;
                        }
                    }
                }
                if (char.IsWhiteSpace(s[i])) continue;
                glyphs.Add(i);
                ems.Add(size);
            }
        }

        // ั ิ ี ึ ื ็ ่ ้ ๊ ๋ ์ ํ ๎
        static bool IsAbove(char c) => c == 'ั' || (c >= 'ิ' && c <= 'ื') || (c >= '็' && c <= '๎');

        // ุ ู ฺ
        static bool IsBelow(char c) => c >= 'ุ' && c <= 'ฺ';

        // ป ฝ ฟ ฬ
        static bool IsAscender(char c) => c == 'ป' || c == 'ฝ' || c == 'ฟ' || c == 'ฬ';

        static float Top(List<UIVertex> v, int quad)
        {
            float y = float.NegativeInfinity;
            for (int i = quad * 4; i < quad * 4 + 4; i++) y = Mathf.Max(y, v[i].position.y);
            return y;
        }

        static float Bottom(List<UIVertex> v, int quad)
        {
            float y = float.PositiveInfinity;
            for (int i = quad * 4; i < quad * 4 + 4; i++) y = Mathf.Min(y, v[i].position.y);
            return y;
        }

        static void Move(List<UIVertex> v, int quad, float dx, float dy)
        {
            if (dx == 0 && dy == 0) return;
            for (int i = quad * 4; i < quad * 4 + 4; i++)
            {
                var vert = v[i];
                vert.position += new Vector3(dx, dy, 0);
                v[i] = vert;
            }
        }
    }
}
