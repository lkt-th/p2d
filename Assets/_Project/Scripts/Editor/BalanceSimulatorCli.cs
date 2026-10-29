using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using P2D.Domain;
using UnityEditor;
using UnityEngine;

namespace P2D.Editor
{
    /// <summary>
    /// Runs every default balance scenario from the command line and writes the Markdown report:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod P2D.Editor.BalanceSimulatorCli.Run
    ///             -balanceOut Logs/balance.md [-balanceRuns 100] [-balanceDodge 0.8] [-balanceAffixes ThemeSurge,Rush,Mist]
    /// </summary>
    public static class BalanceSimulatorCli
    {
        public static void Run()
        {
            string output = Arg("-balanceOut", "Logs/BalanceReports/balance-cli.md");
            int runs = int.Parse(Arg("-balanceRuns", "100"), CultureInfo.InvariantCulture);
            double dodge = double.Parse(Arg("-balanceDodge", "0.8"), CultureInfo.InvariantCulture);
            var affixes = ParseAffixes(Arg("-balanceAffixes", "")); // empty = the affix-free baseline

            var started = DateTime.Now;
            var tuning = new SpireTuning();
            var md = new StringBuilder();
            foreach (var scenario in BalanceScenarios.Default())
            {
                var reports = new List<BuildReport>();
                foreach (var build in scenario.builds)
                    reports.Add(BalanceSimulator.RunSpire(build, scenario.bracket, scenario.themeElement, 1, 30, runs, tuning, dodge, affixes));
                md.AppendLine("### " + scenario.title + (affixes != null ? " · affixes " + string.Join(", ", affixes) : ""));
                md.AppendLine(BalanceSimulator.ToMarkdown(reports, scenario.bracket, scenario.themeElement, runs, dodge));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            File.WriteAllText(output, md.ToString(), new UTF8Encoding(false));
            Debug.Log("[P2D] Balance report written to " + output + " in " + (DateTime.Now - started).TotalSeconds.ToString("0.0") + " s");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        static SpireAffix[] ParseAffixes(string list)
        {
            if (string.IsNullOrEmpty(list)) return null;
            var result = new List<SpireAffix>();
            foreach (var name in list.Split(',')) result.Add((SpireAffix)Enum.Parse(typeof(SpireAffix), name.Trim()));
            return result.ToArray();
        }

        static string Arg(string name, string fallback)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return fallback;
        }
    }
}
