using System.Collections.Generic;
using P2D.Presentation.Battle;
using P2D.Presentation.Screens;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace P2D.Editor
{
    /// <summary>
    /// Creates the greybox scenes. Each holds a single bootstrap component that builds
    /// everything else at runtime:
    ///   Main.unity          — MainMenu (menu screens, save) — first in Build Settings
    ///   GreyboxBattle.unity — GreyboxBattleBootstrap (battle)
    /// Batch mode: -executeMethod P2D.Editor.GreyboxSceneBuilder.BuildAll
    /// </summary>
    public static class GreyboxSceneBuilder
    {
        public const string MainScenePath = "Assets/_Project/Scenes/Main.unity";
        public const string BattleScenePath = "Assets/_Project/Scenes/GreyboxBattle.unity";

        [MenuItem("P2D/Create Greybox Scenes")]
        public static void BuildAll()
        {
            System.IO.Directory.CreateDirectory("Assets/_Project/Scenes");
            CreateScene<MainMenu>(MainScenePath, "Main Menu");
            CreateScene<GreyboxBattleBootstrap>(BattleScenePath, "Greybox Battle");

            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(MainScenePath, true),
                new EditorBuildSettingsScene(BattleScenePath, true),
            };
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != MainScenePath && s.path != BattleScenePath) scenes.Add(s);
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[P2D] Created " + MainScenePath + " and " + BattleScenePath);
        }

        static void CreateScene<T>(string path, string objectName) where T : Component
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject(objectName).AddComponent<T>();
            EditorSceneManager.SaveScene(scene, path);
        }
    }
}
