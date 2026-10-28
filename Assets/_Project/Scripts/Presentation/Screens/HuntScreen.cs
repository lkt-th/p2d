using System.Collections.Generic;
using P2D.Domain;
using P2D.Presentation.UI;
using P2D.Services;
using UnityEngine;

namespace P2D.Presentation.Screens
{
    /// <summary>Hunting Grounds: pick a zone, auto-hunt for resources or fight a run yourself.</summary>
    public sealed class HuntScreen : GameScreen
    {
        RectTransform list;
        int zone;
        List<HuntRun> lastRuns = new List<HuntRun>();

        protected override void Build() => list = UiKit.ScrollList(Root);

        public override void Refresh()
        {
            UiKit.Clear(list);
            var save = Save;
            var now = ServerClock.UtcNow;
            OfflineProgress.Apply(save, now);
            if (zone == 0 || !HuntActions.IsZoneOpen(save, zone)) zone = Mathf.Min(save.dragon.stage, HuntActions.Zones.Length);

            AdventureTabs(list, ScreenId.Hunt);

            var zones = UiKit.Row(list, 110);
            for (int i = 0; i < HuntActions.Zones.Length; i++)
            {
                int z = i + 1;
                bool open = HuntActions.IsZoneOpen(save, z);
                float x0 = i / (float)HuntActions.Zones.Length, x1 = (i + 1) / (float)HuntActions.Zones.Length;
                CellButton(zones, open ? "ZONE " + z : "Stage " + z, x0, x1, () => { zone = z; Refresh(); }, open,
                    z == zone ? UiKit.Accent : UiKit.SurfaceAlt, 26);
            }

            var info = HuntActions.Zone(zone);
            double bonus = HuntActions.HuntBonus(save);
            double rare = HuntRules.RareRate(bonus), epic = HuntRules.EpicRate(bonus);
            var head = UiKit.Row(list, 220, new Color(0.09f, 0.14f, 0.12f));
            Cell(head, "<b>" + info.name + "</b>   Lv " + info.level + "   enemies: " + info.element
                + "\nStamina " + save.hunt.stamina + " / " + HuntRules.MaxStamina + "  (" + HuntRules.StaminaCost + " per run, +1 every "
                + HuntRules.MinutesPerStamina + " min)"
                + "\nEncounters: normal " + Pct(1 - rare - epic) + " · rare " + Pct(rare) + " · epic " + Pct(epic)
                + (bonus > 0 ? "   <color=#9fe0c2>(hunter +" + Pct(bonus) + ")</color>" : "")
                + "\nRares flee after " + HuntRules.RareEscapeSeconds + " s (epic " + HuntRules.EpicEscapeSeconds + " s). Random every run — no guarantee.",
                0.02f, 1, 28);

            bool canRun = save.hunt.stamina >= HuntRules.StaminaCost;
            var buttons = UiKit.Row(list, 130);
            CellButton(buttons, "AUTO HUNT x1", 0, 0.34f, () => AutoHunt(1), canRun);
            CellButton(buttons, "AUTO x5\nstops on rare", 0.34f, 0.67f, () => AutoHunt(5), canRun);
            CellButton(buttons, "FIGHT\nYOURSELF", 0.67f, 1, FightYourself, canRun, UiKit.Accent);

            if (!canRun)
            {
                var refill = UiKit.Row(list, 90, UiKit.Surface);
                Cell(refill, "Out of stamina. Refill in the shop (crystals, " + ShopActions.StaminaRefillsLeft(save, now) + " left today).", 0.02f, 0.72f, 26);
                CellButton(refill, "SHOP", 0.74f, 1, () => Menu.Show(ScreenId.Shop));
            }

            if (lastRuns.Count == 0) return;
            Header(list, "Last hunt");
            foreach (var run in lastRuns)
            {
                var row = UiKit.Row(list, 120, run.kind == EncounterKind.Normal ? UiKit.Surface : new Color(0.2f, 0.17f, 0.08f));
                Cell(row, Describe(run), 0.02f, 1, 26);
            }
        }

        void AutoHunt(int count)
        {
            lastRuns = HuntActions.AutoHunt(Save, zone, count, ServerClock.UtcNow, Menu.Random);
            bool rare = lastRuns.Exists(r => r.kind != EncounterKind.Normal);
            Menu.Commit(lastRuns.Count > 0 ? ActionResult.Ok(rare ? "A rare monster appeared!" : "Hunted " + lastRuns.Count + "×")
                : ActionResult.Fail("Not enough stamina"));
        }

        void FightYourself()
        {
            EncounterKind kind;
            if (!HuntActions.BeginRun(Save, ServerClock.UtcNow, Menu.Random, out kind))
            {
                Menu.Toast("Not enough stamina");
                return;
            }
            Menu.StartHuntBattle(zone, kind);
        }

        static string Pct(double v) => (v * 100).ToString("0.#") + "%";

        public static string Describe(HuntRun run)
        {
            string head;
            switch (run.kind)
            {
                case EncounterKind.Rare: head = "<b>RARE · gold lizard</b>"; break;
                case EncounterKind.Epic: head = "<b>EPIC · shadow dragon</b>"; break;
                default: head = "Ash wolves"; break;
            }
            head += " — " + (run.outcome == BattleOutcome.Victory ? "won" : run.outcome == BattleOutcome.Escaped ? "it fled" : run.outcome.ToString().ToLowerInvariant());
            var r = run.rewards;
            string loot = r.gold > 0
                ? "+" + Fmt.Num(r.gold) + " gold, +" + r.forgeStones + " forge" + (r.dragonEssence > 0 ? ", +" + r.dragonEssence + " essence" : "")
                  + (r.elementCrystals > 0 ? ", +" + r.elementCrystals + " crystals" : "") + (r.refineStones > 0 ? ", +" + r.refineStones + " refine" : "")
                : "nothing";
            if (run.gear != null) loot += ", " + run.gear.rarity + " " + run.gear.slot;
            if (!string.IsNullOrEmpty(run.bonus)) loot += ", " + run.bonus;
            if (run.staminaRefunded) loot += " · stamina refunded (Legacy)";
            return head + "\n" + loot;
        }
    }
}
