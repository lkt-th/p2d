using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using P2D.Domain;
using UnityEditor;
using UnityEngine;

namespace P2D.Editor
{
    /// <summary>
    /// P2D → Balance Simulator. Runs the default build scenarios through spire floors 1–30 and
    /// shows win rates and clear times; reports are saved under Logs/BalanceReports.
    /// </summary>
    public sealed class BalanceSimulatorWindow : EditorWindow
    {
        int seedsPerFloor = 20;
        float manualDodgeRate = 0.8f;
        int firstFloor = 1;
        int lastFloor = 30;
        Element themeElement = Element.Fire;
        bool showTuning;
        readonly SpireTuning tuning = new SpireTuning();
        bool[] enabledScenarios;
        string markdown = "";
        string csv = "";
        Vector2 scroll;

        [MenuItem("P2D/Balance Simulator")]
        static void Open() => GetWindow<BalanceSimulatorWindow>("Balance Simulator");

        void OnGUI()
        {
            var scenarios = BalanceScenarios.Default();
            if (enabledScenarios == null || enabledScenarios.Length != scenarios.Count)
            {
                enabledScenarios = new bool[scenarios.Count];
                for (int i = 0; i < enabledScenarios.Length; i++) enabledScenarios[i] = true;
            }

            EditorGUILayout.LabelField("Scenarios", EditorStyles.boldLabel);
            for (int i = 0; i < scenarios.Count; i++)
                enabledScenarios[i] = EditorGUILayout.ToggleLeft(
                    "B" + scenarios[i].bracket + " — " + scenarios[i].title + " (" + scenarios[i].builds.Count + " builds)",
                    enabledScenarios[i]);

            EditorGUILayout.Space();
            seedsPerFloor = EditorGUILayout.IntSlider("Runs per floor", seedsPerFloor, 1, 200);
            manualDodgeRate = EditorGUILayout.Slider("Player dodge rate", manualDodgeRate, 0f, 1f);
            firstFloor = EditorGUILayout.IntSlider("First floor", firstFloor, 1, 40);
            lastFloor = EditorGUILayout.IntSlider("Last floor", Math.Max(firstFloor, lastFloor), firstFloor, 40);
            themeElement = (Element)EditorGUILayout.EnumPopup("Enemy element", themeElement);

            showTuning = EditorGUILayout.Foldout(showTuning, "Enemy tuning (SpireTuning)");
            if (showTuning) DrawTuning();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Run", GUILayout.Height(28))) Run(scenarios);
                GUI.enabled = markdown.Length > 0;
                if (GUILayout.Button("Save report", GUILayout.Height(28))) Save();
                GUI.enabled = true;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(markdown, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        void DrawTuning()
        {
            EditorGUI.indentLevel++;
            foreach (FieldInfo field in typeof(SpireTuning).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType == typeof(double))
                    field.SetValue(tuning, EditorGUILayout.DoubleField(field.Name, (double)field.GetValue(tuning)));
                else if (field.FieldType == typeof(int))
                    field.SetValue(tuning, EditorGUILayout.IntField(field.Name, (int)field.GetValue(tuning)));
            }
            EditorGUI.indentLevel--;
        }

        void Run(List<BalanceScenario> scenarios)
        {
            var md = new StringBuilder();
            var csvText = new StringBuilder();
            try
            {
                for (int s = 0; s < scenarios.Count; s++)
                {
                    if (!enabledScenarios[s]) continue;
                    var scenario = scenarios[s];
                    var reports = new List<BuildReport>();
                    for (int b = 0; b < scenario.builds.Count; b++)
                    {
                        var build = scenario.builds[b];
                        if (EditorUtility.DisplayCancelableProgressBar("Balance Simulator", build.name,
                                (s + b / (float)scenario.builds.Count) / scenarios.Count))
                            return;
                        reports.Add(BalanceSimulator.RunSpire(build, scenario.bracket, themeElement,
                            firstFloor, lastFloor, seedsPerFloor, tuning, manualDodgeRate));
                    }
                    md.AppendLine("### " + scenario.title);
                    md.AppendLine(BalanceSimulator.ToMarkdown(reports, scenario.bracket, themeElement, seedsPerFloor, manualDodgeRate));
                    csvText.Append(BalanceSimulator.ToCsv(reports));
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            markdown = md.ToString();
            csv = csvText.ToString();
        }

        void Save()
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "BalanceReports");
            Directory.CreateDirectory(dir);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmm");
            File.WriteAllText(Path.Combine(dir, "balance-" + stamp + ".md"), markdown, Encoding.UTF8);
            File.WriteAllText(Path.Combine(dir, "balance-" + stamp + ".csv"), csv, Encoding.UTF8);
            EditorUtility.RevealInFinder(dir);
        }
    }
}
