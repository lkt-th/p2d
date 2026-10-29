using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using P2D.Presentation.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace P2D.PlayMode.Tests
{
    public class ThaiFontTests
    {
        GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.Destroy(root);
        }

        [UnityTest]
        public IEnumerator LabelWithThaiUsesTheThaiFont()
        {
            root = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            Text label = UiKit.Label(root.transform, "มังกร Lv 10");
            yield return null;

            Font thai = Resources.Load<Font>(UiKit.ThaiFontResource);
            Assert.IsNotNull(thai, "font asset in Resources");
            Assert.AreSame(thai, label.font);
            Assert.IsTrue(thai.HasCharacter('ม'));
            Assert.IsTrue(thai.HasCharacter('ั'));
            Assert.IsTrue(thai.HasCharacter('A'), "Latin is in the same font");
            Assert.Greater(label.preferredWidth, 0f);
        }

        /// <summary>Quads of <paramref name="text"/> as the label's generator builds them, then shaped.</summary>
        List<UIVertex> Shape(string text, out List<UIVertex> raw)
        {
            if (root == null) root = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            Text label = UiKit.Label(root.transform, text, 40);
            Canvas.ForceUpdateCanvases();
            raw = new List<UIVertex>(label.cachedTextGenerator.verts);
            var shaped = new List<UIVertex>(raw);
            Assert.IsTrue(ThaiMarkPositioner.Apply(text, true, 40, label.pixelsPerUnit, shaped), "one quad per visible character: " + text);
            return shaped;
        }

        static float Top(List<UIVertex> v, int quad) => Mathf.Max(v[quad * 4].position.y, v[quad * 4 + 2].position.y);
        static float Bottom(List<UIVertex> v, int quad) => Mathf.Min(v[quad * 4].position.y, v[quad * 4 + 2].position.y);
        static float Left(List<UIVertex> v, int quad) => Mathf.Min(v[quad * 4].position.x, v[quad * 4 + 2].position.x);

        [Test]
        public void ToneMarkStacksAboveTheUpperVowel()
        {
            var v = Shape("ที่", out var raw); // ท ี ่
            Assert.Less(Bottom(raw, 2), Top(raw, 1), "unshaped: the tone overlaps the vowel");
            Assert.GreaterOrEqual(Bottom(v, 2), Top(v, 1));
            Assert.AreEqual(Top(raw, 1), Top(v, 1), 0.001f, "the vowel stays");
        }

        [Test]
        public void ToneMarkStacksAboveTheRingOfSaraAm()
        {
            var v = Shape("น้ำ", out _); // น ้ ำ
            Assert.GreaterOrEqual(Bottom(v, 1), Top(v, 2));
        }

        [Test]
        public void UpperMarksMoveLeftOfAnAscender()
        {
            var v = Shape("ปี่", out var raw); // ป ี ่
            Assert.Less(Left(v, 1), Left(raw, 1));
            Assert.Less(Left(v, 2), Left(raw, 2));
            Assert.GreaterOrEqual(Bottom(v, 2), Top(v, 1));
        }

        [Test]
        public void LowerVowelDropsBelowTheTailOfYoYing()
        {
            var v = Shape("ญู", out var raw); // ญ ู
            Assert.Greater(Top(raw, 1), Bottom(raw, 0), "unshaped: the vowel sits inside the tail");
            Assert.Less(Top(v, 1), Bottom(v, 0));
            var plain = Shape("กุ", out var plainRaw);
            Assert.AreEqual(Top(plainRaw, 1), Top(plain, 1), 0.001f, "no move under a plain consonant");
        }

        [Test]
        public void RichTextAndSpacesKeepQuadsInStep()
        {
            Shape("<b>มังกร</b> ของ <color=#ff0>ญี่ปุ่น</color>\n<size=30>น้ำ ที่</size>", out _);
        }
    }
}
