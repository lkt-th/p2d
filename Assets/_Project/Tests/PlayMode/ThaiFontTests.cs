using System.Collections;
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
    }
}
