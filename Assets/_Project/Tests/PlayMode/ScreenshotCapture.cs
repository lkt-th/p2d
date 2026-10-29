using System.Collections;
using System.IO;
using NUnit.Framework;
using P2D.Presentation.Battle;
using P2D.Presentation.Screens;
using P2D.Services;
using UnityEngine;
using UnityEngine.TestTools;

namespace P2D.PlayMode.Tests
{
    /// <summary>
    /// Renders every menu tab and the battle at 1080×1920 into PNGs for visual review.
    /// Explicit: run it on purpose, e.g.
    ///   Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode
    ///             -testFilter P2D.PlayMode.Tests.ScreenshotCapture -testResults out.xml
    /// Output: Logs/Screenshots (override with the P2D_SCREENSHOT_DIR environment variable). Thai unless P2D_SCREENSHOT_LANG=en.
    /// </summary>
    [Explicit, Category("Screenshots")]
    public class ScreenshotCapture
    {
        const int Width = 1080;
        const int Height = 1920;

        static string OutputDir
        {
            get
            {
                string dir = System.Environment.GetEnvironmentVariable("P2D_SCREENSHOT_DIR");
                if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "Screenshots");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        [SetUp]
        public void SetUp()
        {
            SaveManager.StorageOverride = new MemorySaveStorage();
            // Thai by default (the review language); P2D_SCREENSHOT_LANG=en captures the English UI.
            BattleHandoff.Pending = null;
            BattleHandoff.LastMessage = null;
        }

        [TearDown]
        public void TearDown()
        {
            if (SaveManager.Instance != null) Object.DestroyImmediate(SaveManager.Instance.gameObject);
            SaveManager.StorageOverride = null;
            P2D.Services.LanguageSetting.Override = null;
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (go.transform.parent == null && go.name != "Code-based tests runner") Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator CaptureMenuScreens()
        {
            var menu = new GameObject("Main Menu").AddComponent<MainMenu>();
            yield return null;
            // First launch: the egg, then the name field once it cracks.
            var save = menu.Save;
            yield return null;
            Capture("hatch-egg");
            foreach (var b in Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
                if (b.transform.parent != null && b.transform.parent.name == "Egg")
                {
                    for (int i = 0; i < P2D.Domain.GameActions.HatchTaps; i++) b.onClick.Invoke();
                    break;
                }
            yield return null;
            yield return null;
            Capture("hatch-name");
            save.dragon.hatched = true;

            // A little progress so lists are not empty.
            save.wallet.soulShards = 180;
            save.wallet.pactSigils = 30;
            save.spire.highestFloor = 7;
            save.story.cleared = 20; // every feature open, chapter 3 next
            save.riders.Add(new P2D.Domain.RiderState { riderId = "kyren", star = 4, copies = 2 });

            foreach (ScreenId id in System.Enum.GetValues(typeof(ScreenId)))
            {
                menu.Show(id);
                yield return null;
                yield return null;
                Capture("menu-" + id.ToString().ToLowerInvariant());
            }

            // A fresh game: locked features and the opening scene of 1-1.
            save.story.cleared = 0;
            menu.Show(ScreenId.Spire);
            yield return null;
            Capture("locked-spire");
            menu.Show(ScreenId.Story);
            yield return null;
            foreach (var b in Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
                if (b.gameObject.activeInHierarchy && b.GetComponentInChildren<UnityEngine.UI.Text>().text.StartsWith(P2D.Domain.Loc.F("FIGHT STAGE {0}", "1-1"))) b.onClick.Invoke();
            yield return null;
            yield return null;
            Capture("story-dialogue");
        }

        [UnityTest]
        public IEnumerator CaptureBattle()
        {
            var root = new GameObject("Greybox Battle").AddComponent<GreyboxBattleBootstrap>();
            yield return null;
            root.Runner.SetSpeed(1);
            for (int i = 0; i < 90; i++) yield return null; // ~1.5 s of fighting: damage numbers on screen
            Capture("battle");
            root.Runner.Skip();
            yield return null;
            yield return null;
            Capture("battle-result");
        }

        /// <summary>Renders all cameras and overlay canvases into one 1080×1920 PNG.</summary>
        static void Capture(string name)
        {
            var rt = new RenderTexture(Width, Height, 24);
            var cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var cam = cameras.Length > 0 ? cameras[0] : new GameObject("Shot Camera").AddComponent<Camera>();

            var modes = new RenderMode[canvases.Length];
            for (int i = 0; i < canvases.Length; i++)
            {
                modes[i] = canvases[i].renderMode;
                if (canvases[i].isRootCanvas)
                {
                    canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                    canvases[i].worldCamera = cam;
                    canvases[i].planeDistance = 1;
                }
            }

            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            cam.targetTexture = null;

            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(OutputDir, name + ".png"), tex.EncodeToPNG());

            for (int i = 0; i < canvases.Length; i++)
                if (canvases[i].isRootCanvas) canvases[i].renderMode = modes[i];
            Object.Destroy(tex);
            rt.Release();
        }
    }
}
