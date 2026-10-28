using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace P2D.Domain
{
    public sealed class FloorStats
    {
        public int floor;
        public int runs;
        public int wins;
        public double totalClearSeconds;
        public double totalHpLeft;

        public double WinRate => runs > 0 ? wins / (double)runs : 0;
        public double AvgClearSeconds => wins > 0 ? totalClearSeconds / wins : double.NaN;
        public double AvgHpLeft => wins > 0 ? totalHpLeft / wins : double.NaN;
    }

    public sealed class BuildReport
    {
        public BuildPreset build;
        public double power;
        public Dictionary<GrowthPath, int> affinity;
        public List<FloorStats> floors = new List<FloorStats>();

        /// <summary>Highest floor won in at least half the runs, counting up until the first failure.</summary>
        public int HighestReliableFloor
        {
            get
            {
                int best = 0;
                foreach (var f in floors)
                {
                    if (f.WinRate < 0.5) break;
                    best = f.floor;
                }
                return best;
            }
        }

        public FloorStats At(int floor) => floors.Find(f => f.floor == floor);
    }

    /// <summary>
    /// Plays builds through spire floors in auto mode with many seeds (no rendering) to check
    /// pacing and path balance before playtests. Auto mode only dodges telegraphed attacks by
    /// SPD chance; <paramref name="manualDodgeRate"/> models a player who also swipes to dodge
    /// that share of telegraphs (0 = pure auto, 0.8 = an attentive player).
    /// </summary>
    public static class BalanceSimulator
    {
        public static BuildReport RunSpire(BuildPreset build, int bracket, Element themeElement,
            int firstFloor, int lastFloor, int seedsPerFloor, SpireTuning tuning, double manualDodgeRate = 0)
        {
            var loadout = BuildFactory.ToLoadout(build);
            var report = new BuildReport
            {
                build = build,
                power = LoadoutCalculator.Compute(loadout).PowerScore(),
                affinity = LoadoutCalculator.Affinity(loadout),
            };

            for (int floor = firstFloor; floor <= lastFloor; floor++)
            {
                var stats = new FloorStats { floor = floor };
                for (int s = 0; s < seedsPerFloor; s++)
                {
                    int seed = unchecked(floor * 7919 + s * 104729 + StableHash(build.name));
                    var enemies = SpireEnemyFactory.Floor(bracket, floor, themeElement, tuning);
                    var setup = BuildFactory.ToBattle(build, enemies, seed, SpireEnemyFactory.TimeLimit(floor, tuning));
                    var sim = new BattleSimulation(setup);
                    RunWithPlayer(sim, manualDodgeRate, new SeededRandom(~seed));

                    stats.runs++;
                    if (sim.Outcome == BattleOutcome.Victory)
                    {
                        stats.wins++;
                        stats.totalClearSeconds += sim.ElapsedSeconds;
                        stats.totalHpLeft += sim.DragonHp / sim.DragonMaxHp;
                    }
                }
                report.floors.Add(stats);
            }
            return report;
        }

        /// <summary>Steps the battle, swiping to dodge each new telegraph with the given success rate.</summary>
        static void RunWithPlayer(BattleSimulation sim, double dodgeRate, IRandom player)
        {
            int telegraphsSeen = 0;
            while (sim.Step())
            {
                if (dodgeRate <= 0) continue;
                int telegraphs = 0;
                var events = sim.Events;
                for (int i = events.Count - 1; i >= 0 && events[i].tick == sim.Tick; i--)
                    if (events[i].type == BattleEventType.Telegraph) telegraphs++;
                for (int t = 0; t < telegraphs; t++)
                    if (player.NextDouble() < dodgeRate) sim.Enqueue(BattleInputType.Dodge);
                telegraphsSeen += telegraphs;
            }
        }

        /// <summary>Markdown summary: one row per build plus a floor-by-floor win-rate grid.</summary>
        public static string ToMarkdown(IList<BuildReport> reports, int bracket, Element themeElement, int seedsPerFloor,
            double manualDodgeRate = 0)
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            string mode = manualDodgeRate > 0
                ? "Auto + ผู้เล่นหลบท่าหนักได้ " + Math.Round(manualDodgeRate * 100).ToString(ci) + "%"
                : "Auto ล้วน";
            sb.AppendLine("## Bracket B" + bracket + " · ศัตรูธาตุ" + themeElement + " · " + seedsPerFloor + " รอบต่อชั้น · " + mode);
            sb.AppendLine();
            sb.AppendLine("| Build | พลัง | Affinity | ผ่านถึงชั้น | ชั้น 10 | ชั้น 20 | ชั้น 30 |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var r in reports)
            {
                sb.Append("| ").Append(r.build.name)
                  .Append(" | ").Append(r.power.ToString("N0", ci))
                  .Append(" | ").Append(AffinityText(r.affinity))
                  .Append(" | **").Append(r.HighestReliableFloor).Append("**")
                  .Append(" | ").Append(Cell(r.At(10), ci))
                  .Append(" | ").Append(Cell(r.At(20), ci))
                  .Append(" | ").Append(Cell(r.At(30), ci))
                  .AppendLine(" |");
            }
            sb.AppendLine();
            sb.AppendLine("<details><summary>อัตราชนะรายชั้น (%)</summary>");
            sb.AppendLine();
            sb.Append("| ชั้น |");
            foreach (var r in reports) sb.Append(' ').Append(r.build.name).Append(" |");
            sb.AppendLine();
            sb.Append("|---|");
            foreach (var r in reports) sb.Append("---|");
            sb.AppendLine();
            if (reports.Count > 0)
            {
                foreach (var f in reports[0].floors)
                {
                    sb.Append("| ").Append(f.floor).Append(" |");
                    foreach (var r in reports)
                    {
                        var s = r.At(f.floor);
                        sb.Append(' ').Append(s == null ? "-" : Math.Round(s.WinRate * 100).ToString(ci)).Append(" |");
                    }
                    sb.AppendLine();
                }
            }
            sb.AppendLine();
            sb.AppendLine("</details>");
            return sb.ToString();
        }

        /// <summary>CSV with one line per build and floor, for spreadsheets.</summary>
        public static string ToCsv(IList<BuildReport> reports)
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder("build,floor,runs,wins,win_rate,avg_clear_seconds,avg_hp_left\n");
            foreach (var r in reports)
                foreach (var f in r.floors)
                    sb.Append(r.build.name).Append(',').Append(f.floor).Append(',').Append(f.runs).Append(',')
                      .Append(f.wins).Append(',').Append(f.WinRate.ToString("0.###", ci)).Append(',')
                      .Append(double.IsNaN(f.AvgClearSeconds) ? "" : f.AvgClearSeconds.ToString("0.0", ci)).Append(',')
                      .Append(double.IsNaN(f.AvgHpLeft) ? "" : f.AvgHpLeft.ToString("0.###", ci)).Append('\n');
            return sb.ToString();
        }

        /// <summary>FNV-1a: string.GetHashCode differs between runtimes, seeds must not.</summary>
        static int StableHash(string text)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in text ?? "") { hash ^= c; hash *= 16777619; }
                return (int)hash;
            }
        }

        static string Cell(FloorStats s, CultureInfo ci)
        {
            if (s == null) return "-";
            string text = Math.Round(s.WinRate * 100).ToString(ci) + "%";
            if (s.wins > 0) text += " · " + s.AvgClearSeconds.ToString("0", ci) + " วิ";
            return text;
        }

        static string AffinityText(Dictionary<GrowthPath, int> affinity)
        {
            var parts = new List<string>();
            foreach (var pair in affinity)
                if (pair.Value > 0) parts.Add(pair.Key + " " + pair.Value + " (T" + AffinityRules.Tier(pair.Value) + ")");
            return parts.Count == 0 ? "—" : string.Join(", ", parts.ToArray());
        }
    }
}
