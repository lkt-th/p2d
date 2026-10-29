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
            if (LockedCard(list, Feature.Hunt)) return;

            var zones = UiKit.Row(list, 110);
            for (int i = 0; i < HuntActions.Zones.Length; i++)
            {
                int z = i + 1;
                bool open = HuntActions.IsZoneOpen(save, z);
                float x0 = i / (float)HuntActions.Zones.Length, x1 = (i + 1) / (float)HuntActions.Zones.Length;
                CellButton(zones, open ? Loc.F("ZONE {0}", z) : Loc.F("Stage {0}", z), x0, x1, () => { zone = z; Refresh(); }, open,
                    z == zone ? UiKit.Accent : UiKit.SurfaceAlt, 26);
            }

            var info = HuntActions.Zone(zone);
            double bonus = HuntActions.HuntBonus(save);
            double rare = HuntRules.RareRate(bonus), epic = HuntRules.EpicRate(bonus);
            var head = UiKit.Row(list, 220, new Color(0.09f, 0.14f, 0.12f));
            Cell(head, "<b>" + Loc.T(info.name) + "</b>   " + Loc.F("Lv {0}", info.level) + "   " + Loc.F("enemies: {0}", Loc.Enum(info.element))
                + "\n" + Loc.F("Stamina {0} / {1}  ({2} per run, +1 every {3} min)", save.hunt.stamina, HuntRules.MaxStamina, HuntRules.StaminaCost,
                    HuntRules.MinutesPerStamina)
                + "\n" + Loc.F("Encounters: normal {0} · rare {1} · epic {2}", Pct(1 - rare - epic), Pct(rare), Pct(epic))
                + (bonus > 0 ? "   <color=#9fe0c2>" + Loc.F("(hunter +{0})", Pct(bonus)) + "</color>" : "")
                + "\n" + Loc.F("Rares flee after {0} s (epic {1} s). Random every run — no guarantee.", HuntRules.RareEscapeSeconds, HuntRules.EpicEscapeSeconds),
                0.02f, 1, 28);

            bool canRun = save.hunt.stamina >= HuntRules.StaminaCost;
            var buttons = UiKit.Row(list, 130);
            CellButton(buttons, Loc.T("AUTO HUNT x1"), 0, 0.34f, () => AutoHunt(1), canRun);
            CellButton(buttons, Loc.T("AUTO x5") + "\n" + Loc.T("stops on rare"), 0.34f, 0.67f, () => AutoHunt(5), canRun);
            CellButton(buttons, Loc.T("FIGHT") + "\n" + Loc.T("YOURSELF"), 0.67f, 1, FightYourself, canRun, UiKit.Accent);

            if (!canRun)
            {
                var refill = UiKit.Row(list, 90, UiKit.Surface);
                Cell(refill, Loc.F("Out of stamina. Refill in the shop (crystals, {0} left today).", ShopActions.StaminaRefillsLeft(save, now)), 0.02f, 0.72f, 26);
                CellButton(refill, Loc.T("SHOP"), 0.74f, 1, () => Menu.Show(ScreenId.Shop));
            }

            if (lastRuns.Count == 0) return;
            Header(list, Loc.T("Last hunt"));
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
            Menu.Commit(lastRuns.Count > 0 ? ActionResult.Ok(rare ? Loc.T("A rare monster appeared!") : Loc.F("Hunted {0}×", lastRuns.Count))
                : ActionResult.Fail(Loc.T("Not enough stamina")));
        }

        void FightYourself()
        {
            EncounterKind kind;
            if (!HuntActions.BeginRun(Save, ServerClock.UtcNow, Menu.Random, out kind))
            {
                Menu.Toast(Loc.T("Not enough stamina"));
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
                case EncounterKind.Rare: head = "<b>" + Loc.T("RARE · gold lizard") + "</b>"; break;
                case EncounterKind.Epic: head = "<b>" + Loc.T("EPIC · shadow dragon") + "</b>"; break;
                default: head = Loc.T("Ash wolves"); break;
            }
            head += " — " + (run.outcome == BattleOutcome.Victory ? Loc.T("won") : run.outcome == BattleOutcome.Escaped ? Loc.T("it fled") : Loc.Enum(run.outcome));
            var r = run.rewards;
            string loot = r.gold > 0
                ? Loc.F("+{0} gold, +{1} forge", Fmt.Num(r.gold), r.forgeStones) + (r.dragonEssence > 0 ? Loc.F(", +{0} essence", r.dragonEssence) : "")
                  + (r.elementCrystals > 0 ? Loc.F(", +{0} crystals", r.elementCrystals) : "") + (r.refineStones > 0 ? Loc.F(", +{0} refine", r.refineStones) : "")
                : Loc.T("nothing");
            if (run.gear != null) loot += ", " + Loc.Enum(run.gear.rarity) + " " + Loc.Enum(run.gear.slot);
            if (!string.IsNullOrEmpty(run.bonus)) loot += ", " + Loc.T(run.bonus);
            if (run.staminaRefunded) loot += " · " + Loc.T("stamina refunded (Legacy)");
            return head + "\n" + loot;
        }
    }
}
