using System;
using System.Collections.Generic;
using P2D.Domain;
using P2D.Presentation.UI;
using UnityEngine;

namespace P2D.Presentation.Screens
{
    /// <summary>Dragon: stats, growth paths, skills, level/evolve, equipped gear and inventory.</summary>
    public sealed class DragonScreen : GameScreen
    {
        RectTransform list;

        protected override void Build() => list = UiKit.ScrollList(Root);

        public override void Refresh()
        {
            UiKit.Clear(list);
            var save = Save;
            var d = save.dragon;
            var loadout = GameActions.ToLoadout(save);
            var sheet = LoadoutCalculator.Compute(loadout);
            var affinity = LoadoutCalculator.Affinity(loadout);

            var head = UiKit.Row(list, 150, UiKit.Surface);
            Cell(head, "<b>" + d.name + "</b>  " + NestScreen.StageName(d.stage) + " (Stage " + d.stage + ")\nLv " + d.level + " / "
                + DragonRules.LevelCap(d.stage) + "   EXP " + d.exp + " / " + DragonRules.ExpToNext(d.level)
                + "   Power " + sheet.PowerScore().ToString("N0"), 0.02f, 1, 32);

            var stats = UiKit.Row(list, 110, UiKit.Surface);
            Cell(stats, "HP " + sheet.Get(StatType.HP).ToString("N0") + "   ATK " + sheet.Get(StatType.ATK).ToString("N0")
                + "   DEF " + sheet.Get(StatType.DEF).ToString("N0") + "   SPD " + sheet.Get(StatType.SPD).ToString("N0")
                + "\nCRIT " + (sheet.Get(StatType.CritRate) * 100).ToString("0.#") + "% / " + (sheet.Get(StatType.CritDmg) * 100).ToString("0")
                + "%   Element " + (sheet.Get(StatType.ElementDmg) * 100).ToString("0.#") + "%   Haste " + sheet.Get(StatType.SkillHaste).ToString("0"),
                0.02f, 1, 30);

            Header(list, "Growth paths  (tiers at 20 / 60 / 120 / 200)");
            foreach (GrowthPath path in Enum.GetValues(typeof(GrowthPath)))
            {
                int value = affinity[path];
                var row = UiKit.Row(list, 56);
                Cell(row, path.ToString(), 0, 0.22f, 28);
                var barCell = UiKit.Slice(UiKit.Fill(row, "Bar"), 0.22f, 0.82f, 14);
                UiKit.Bar(barCell, UiKit.SurfaceAlt, PathColor(path), value / 220f);
                Cell(row, value + "  T" + AffinityRules.Tier(value), 0.82f, 1, 28);
            }

            var skills = UiKit.Row(list, 100, UiKit.Surface);
            var names = new List<string>();
            foreach (var id in d.skillIds) if (!string.IsNullOrEmpty(id)) names.Add(id);
            Cell(skills, "Skills (" + DragonRules.SkillSlotCount(d.stage) + " slots): "
                + (names.Count > 0 ? string.Join(", ", names.ToArray()) : "none unlocked — reach Tier I in a path"), 0.02f, 0.72f, 28);
            CellButton(skills, "AUTO PICK", 0.74f, 1, () =>
            {
                GameActions.AutoSkills(save);
                Menu.Commit(ActionResult.Ok("Skills updated"));
            });

            var grow = UiKit.Row(list, 110);
            CellButton(grow, "FEED x10\n(essence " + save.wallet.dragonEssence + ")", 0, 0.5f,
                () => Menu.Commit(GameActions.FeedEssence(save, 10)), true, UiKit.Accent);
            string evolveText = d.stage < DragonRules.MaxStage
                ? "EVOLVE\n(" + DragonRules.EvolveCostFrom(d.stage).ancientScales + " scales)"
                : "MAX STAGE";
            CellButton(grow, evolveText, 0.5f, 1, () => Menu.Commit(GameActions.Evolve(save)),
                DragonRules.CanEvolve(d.level, d.stage));

            Header(list, "Equipped");
            foreach (EquipSlot slot in Enum.GetValues(typeof(EquipSlot)))
            {
                var row = UiKit.Row(list, 120, UiKit.Surface);
                if (!DragonRules.IsSlotUnlocked(slot, d.stage))
                {
                    Cell(row, slot + "  —  unlocks at stage " + DragonRules.UnlockStage(slot), 0.02f, 1, 28, TextAnchor.MiddleLeft, UiKit.Muted);
                    continue;
                }
                var item = save.FindEquipment(d.equipped[(int)slot]);
                if (item == null)
                {
                    Cell(row, slot + "  —  empty", 0.02f, 1, 28, TextAnchor.MiddleLeft, UiKit.Muted);
                    continue;
                }
                Cell(row, Fmt.Item(item), 0.02f, 0.72f, 26);
                string uid = item.uid;
                bool maxed = item.enhance >= EquipmentRules.MaxEnhance;
                CellButton(row, maxed ? "MAX" : "ENHANCE\n" + EquipmentRules.StoneCost(item.enhance, item.rarity) + " stones",
                    0.74f, 1, () => Menu.Commit(GameActions.Enhance(save, uid, Menu.Random)), !maxed);
            }

            var bag = new List<EquipmentState>();
            foreach (var item in save.equipment)
                if (Array.IndexOf(d.equipped, item.uid) < 0) bag.Add(item);
            bag.Sort((a, b) => a.rarity != b.rarity ? b.rarity.CompareTo(a.rarity) : b.enhance.CompareTo(a.enhance));

            Header(list, "Inventory (" + bag.Count + ")");
            if (bag.Count == 0)
                Cell(UiKit.Row(list, 80), "Clear spire floors to find gear.", 0.02f, 1, 28, TextAnchor.MiddleLeft, UiKit.Muted);
            foreach (var item in bag)
            {
                var row = UiKit.Row(list, 120, UiKit.Surface);
                Cell(row, Fmt.Item(item), 0.02f, 0.62f, 26);
                string uid = item.uid;
                CellButton(row, "EQUIP", 0.63f, 0.82f, () => Menu.Commit(GameActions.Equip(save, uid), "Equipped"),
                    DragonRules.IsSlotUnlocked(item.slot, d.stage));
                CellButton(row, "SALVAGE", 0.82f, 1, () => Menu.Commit(GameActions.Salvage(save, uid), "Salvaged"), true, UiKit.Surface);
            }
        }

        static Color PathColor(GrowthPath path)
        {
            switch (path)
            {
                case GrowthPath.Fury: return new Color(0.94f, 0.42f, 0.3f);
                case GrowthPath.Bastion: return new Color(0.82f, 0.68f, 0.43f);
                case GrowthPath.Tempest: return new Color(0.3f, 0.79f, 0.65f);
                default: return new Color(0.61f, 0.55f, 0.96f);
            }
        }
    }
}
