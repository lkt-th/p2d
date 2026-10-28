using UnityEditor;
using UnityEngine;

namespace P2D.Editor
{
    /// <summary>
    /// Keeps Player Settings portrait-only so the setting survives a fresh clone
    /// (ProjectSettings are generated on first open). Also available from the menu.
    /// </summary>
    [InitializeOnLoad]
    static class PortraitProjectSetup
    {
        static PortraitProjectSetup()
        {
            EditorApplication.delayCall += () => Apply(false);
        }

        [MenuItem("P2D/Apply Portrait Player Settings")]
        static void ApplyFromMenu() => Apply(true);

        static void Apply(bool log)
        {
            bool changed = PlayerSettings.defaultInterfaceOrientation != UIOrientation.Portrait
                || PlayerSettings.allowedAutorotateToLandscapeLeft
                || PlayerSettings.allowedAutorotateToLandscapeRight
                || PlayerSettings.allowedAutorotateToPortraitUpsideDown;

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            if (string.IsNullOrEmpty(PlayerSettings.productName) || PlayerSettings.productName == "p2d")
                PlayerSettings.productName = "Path to Dragon";

            if (changed || log)
                Debug.Log("[P2D] Player Settings set to portrait-only.");
        }
    }
}
