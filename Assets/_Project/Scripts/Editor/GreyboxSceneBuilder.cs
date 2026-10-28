using System.Collections.Generic;
using P2D.Presentation.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace P2D.Editor
{
    /// <summary>
    /// Creates Assets/_Project/Scenes/GreyboxBattle.unity: an empty scene with only the
    /// GreyboxBattleBootstrap (it builds camera, world and UI at runtime) and puts it first in
    /// Build Settings. Batch mode: -executeMethod P2D.Editor.GreyboxSceneBuilder.Build
    /// </summary>
    public static class GreyboxSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/GreyboxBattle.unity";

        [MenuItem("P2D/Create Greybox Battle Scene")]
        public static void Build()
        {
            System.IO.Directory.CreateDirectory("Assets/_Project/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Greybox Battle").AddComponent<GreyboxBattleBootstrap>();
            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != ScenePath) scenes.Add(s);
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[P2D] Created " + ScenePath);
        }
    }
}
