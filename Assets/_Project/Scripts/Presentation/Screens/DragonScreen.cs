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
        string workshopUid;

        protected override void Build() => list = UiKit.ScrollList(Root);

        /// <summary>Reforge (change path, keep +N) and refine (reroll one sub-stat) for one item.</summary>
        void Workshop(PlayerSave save, EquipmentState item)
        {
            Header(list, Loc.T("Workshop"));
            var info = UiKit.Row(list, 130, new Color(0.16f, 0.13f, 0.2f));
            Cell(info, Fmt.Item(item), 0.02f, 0.78f, 26);
            CellButton(info, Loc.T("CLOSE"), 0.8f, 1, () => { workshopUid = null; Refresh(); });

            int crystals = GameActions.ReforgeCrystalCost(item);
            int essence = EquipmentRules.ReforgePathEssenceCost(item.rarity);
            int owned = 0;
            foreach (int c in save.wallet.elementCrystals) owned += c;
            Cell(UiKit.Row(list, 80), Loc.F("Reforge path — keeps +{0}.  Cost {1} element crystals (have {2}) + {3} essence of the new path",
                item.enhance, crystals, owned, essence), 0.02f, 1, 26, TextAnchor.MiddleLeft, UiKit.Muted);
            var paths = UiKit.Row(list, 100);
            var all = (GrowthPath[])Enum.GetValues(typeof(GrowthPath));
            for (int i = 0; i < all.Length; i++)
            {
                var target = all[i];
                bool current = target == item.path;
                string uid = item.uid;
                CellButton(paths, Loc.Enum(target) + "\n" + (current ? Loc.T("(current)") : Loc.F("{0} ess.", save.wallet.pathEssence[(int)target])),
                    i / 4f, (i + 1) / 4f, () => Menu.Commit(GameActions.Reforge(save, uid, target)), !current,
                    current ? UiKit.Accent : UiKit.SurfaceAlt, 26);
            }

            Cell(UiKit.Row(list, 80), Loc.F("Refine — reroll one sub-stat, the others stay.  Cost {0} refine stones (have {1}) + {2} gold",
                EquipmentRules.RefineCost(item.rarity), save.wallet.refineStones, Fmt.Num(EquipmentRules.RefineGold(item.rarity))),
                0.02f, 1, 26, TextAnchor.MiddleLeft, UiKit.Muted);
            if (item.subs == null || item.subs.Length == 0)
            {
                Cell(UiKit.Row(list, 70), Loc.T("No sub-stats yet (they appear at +3, +6, …)."), 0.02f, 1, 26, TextAnchor.MiddleLeft, UiKit.Muted);
                return;
            }
            for (int i = 0; i < item.subs.Length; i++)
            {
                int index = i;
                string uid = item.uid;
                var row = UiKit.Row(list, 80, UiKit.Surface);
                Cell(row, Fmt.Stat(item.subs[i]), 0.02f, 0.7f, 28);
                CellButton(row, Loc.T("REROLL"), 0.72f, 1, () => Menu.Commit(GameActions.Refine(save, uid, index, Menu.Random)),
                    save.wallet.refineStones >= EquipmentRules.RefineCost(item.rarity));
            }
        }

        public override void Refresh()
        {
            UiKit.Clear(list);
            var save = Save;
            var d = save.dragon;
            var loadout = GameActions.ToLoadout(save);
            var sheet = LoadoutCalculator.Compute(loadout);
            var affinity = LoadoutCalculator.Affinity(loadout);

            var head = UiKit.Row(list, 150, UiKit.Surface);
            Cell(head, "<b>" + d.name + "</b>  " + NestScreen.StageName(d.stage) + " " + Loc.F("(Stage {0})", d.stage) + "\n"
                + Loc.F("Lv {0} / {1}", d.level, DragonRules.LevelCap(d.stage)) + "   " + Loc.F("EXP {0} / {1}", d.exp, DragonRules.ExpToNext(d.level))
                + "   " + Loc.F("Power {0}", sheet.PowerScore().ToString("N0")), 0.02f, 1, 32);

            var stats = UiKit.Row(list, 110, UiKit.Surface);
            Cell(stats, "HP " + sheet.Get(StatType.HP).ToString("N0") + "   ATK " + sheet.Get(StatType.ATK).ToString("N0")
                + "   DEF " + sheet.Get(StatType.DEF).ToString("N0") + "   SPD " + sheet.Get(StatType.SPD).ToString("N0")
                + "\nCRIT " + (sheet.Get(StatType.CritRate) * 100).ToString("0.#") + "% / " + (sheet.Get(StatType.CritDmg) * 100).ToString("0")
                + "%   " + Loc.F("Element {0}%   Haste {1}", (sheet.Get(StatType.ElementDmg) * 100).ToString("0.#"), sheet.Get(StatType.SkillHaste).ToString("0")),
                0.02f, 1, 30);

            var selected = FeatureUnlocks.IsUnlocked(save, Feature.Workshop) ? save.FindEquipment(workshopUid) : null;
            if (selected != null) Workshop(save, selected);

            var sets = LoadoutCalculator.SetPieces(loadout);
            if (sets.Count > 0)
            {
                Header(list, Loc.T("Set bonuses"));
                foreach (var pair in sets)
                {
                    var set = SetCatalog.Find(pair.Key);
                    var row = UiKit.Row(list, 90, UiKit.Surface);
                    string two = (pair.Value >= SetCatalog.TwoPiece ? "<color=#6cc27a>" : "<color=#7a8094>") + "2: " + Loc.T(set.twoPieceText) + "</color>";
                    string four = (pair.Value >= SetCatalog.FourPiece ? "<color=#6cc27a>" : "<color=#7a8094>") + "4: " + Loc.T(set.fourPieceText) + "</color>";
                    Cell(row, "<b>" + Loc.T(set.name) + "</b> " + pair.Value + "/4\n" + two + "   " + four, 0.02f, 1, 24);
                }
            }

            Header(list, Loc.T("Growth paths  (tiers at 20 / 60 / 120 / 200)"));
            foreach (GrowthPath path in Enum.GetValues(typeof(GrowthPath)))
            {
                int value = affinity[path];
                var row = UiKit.Row(list, 56);
                Cell(row, Loc.Enum(path), 0, 0.22f, 28);
                var barCell = UiKit.Slice(UiKit.Fill(row, "Bar"), 0.22f, 0.82f, 14);
                UiKit.Bar(barCell, UiKit.SurfaceAlt, PathColor(path), value / 220f);
                Cell(row, value + "  T" + AffinityRules.Tier(value), 0.82f, 1, 28);
            }

            var skills = UiKit.Row(list, 100, UiKit.Surface);
            var names = new List<string>();
            foreach (var id in d.skillIds) if (!string.IsNullOrEmpty(id)) names.Add(Loc.Skill(id));
            Cell(skills, Loc.F("Skills ({0} slots): ", DragonRules.SkillSlotCount(d.stage))
                + (names.Count > 0 ? string.Join(", ", names.ToArray()) : Loc.T("none unlocked — reach Tier I in a path")), 0.02f, 0.72f, 28);
            CellButton(skills, Loc.T("AUTO PICK"), 0.74f, 1, () =>
            {
                GameActions.AutoSkills(save);
                Menu.Commit(ActionResult.Ok(Loc.T("Skills updated")));
            });

            var grow = UiKit.Row(list, 110);
            CellButton(grow, Loc.T("FEED x10") + "\n" + Loc.F("(essence {0})", save.wallet.dragonEssence), 0, 0.5f,
                () => Menu.Commit(GameActions.FeedEssence(save, 10)), true, UiKit.Accent);
            string evolveText = d.stage < DragonRules.MaxStage
                ? Loc.T("EVOLVE") + "\n" + Loc.F("({0} scales)", DragonRules.EvolveCostFrom(d.stage).ancientScales)
                : Loc.T("MAX STAGE");
            CellButton(grow, evolveText, 0.5f, 1, () => Menu.Commit(GameActions.Evolve(save)),
                DragonRules.CanEvolve(d.level, d.stage));

            bool enhance = FeatureUnlocks.IsUnlocked(save, Feature.Enhance);
            bool workshop = FeatureUnlocks.IsUnlocked(save, Feature.Workshop);
            string workshopHint = Loc.F("story {0}", StoryActions.Label(FeatureUnlocks.UnlockStage(Feature.Workshop)));
            string enhanceHint = Loc.F("story {0}", StoryActions.Label(FeatureUnlocks.UnlockStage(Feature.Enhance)));
            string workshopLabel = Loc.T("WORKSHOP");
            if (workshop) Presets(save);

            Header(list, Loc.T("Equipped"));
            foreach (EquipSlot slot in Enum.GetValues(typeof(EquipSlot)))
            {
                var row = UiKit.Row(list, 120, UiKit.Surface);
                if (!DragonRules.IsSlotUnlocked(slot, d.stage))
                {
                    Cell(row, Loc.F("{0}  —  unlocks at stage {1}", Loc.Enum(slot), DragonRules.UnlockStage(slot)), 0.02f, 1, 28, TextAnchor.MiddleLeft, UiKit.Muted);
                    continue;
                }
                var item = save.FindEquipment(d.equipped[(int)slot]);
                if (item == null)
                {
                    Cell(row, Loc.F("{0}  —  empty", Loc.Enum(slot)), 0.02f, 1, 28, TextAnchor.MiddleLeft, UiKit.Muted);
                    continue;
                }
                Cell(row, Fmt.Item(item), 0.02f, 0.56f, 26);
                string uid = item.uid;
                CellButton(row, workshop ? workshopLabel : workshopLabel + "\n" + workshopHint, 0.57f, 0.76f, () => { workshopUid = uid; Refresh(); }, workshop, UiKit.Surface, 22);
                bool maxed = item.enhance >= EquipmentRules.MaxEnhance;
                CellButton(row, !enhance ? Loc.T("ENHANCE") + "\n" + enhanceHint : maxed ? Loc.T("MAX") : Loc.T("ENHANCE") + "\n" + Loc.F("{0} stones", EquipmentRules.StoneCost(item.enhance, item.rarity)),
                    0.76f, 1, () => Menu.Commit(GameActions.Enhance(save, uid, Menu.Random)), enhance && !maxed);
            }

            var bag = new List<EquipmentState>();
            foreach (var item in save.equipment)
                if (Array.IndexOf(d.equipped, item.uid) < 0) bag.Add(item);
            bag.Sort((a, b) => a.rarity != b.rarity ? b.rarity.CompareTo(a.rarity) : b.enhance.CompareTo(a.enhance));

            Header(list, Loc.F("Inventory ({0})", bag.Count));
            if (bag.Count == 0)
                Cell(UiKit.Row(list, 80), Loc.T("Clear spire floors to find gear."), 0.02f, 1, 28, TextAnchor.MiddleLeft, UiKit.Muted);
            foreach (var item in bag)
            {
                var row = UiKit.Row(list, 120, UiKit.Surface);
                Cell(row, Fmt.Item(item), 0.02f, 0.5f, 26);
                string uid = item.uid;
                CellButton(row, workshop ? workshopLabel : workshopLabel + "\n" + workshopHint, 0.51f, 0.67f, () => { workshopUid = uid; Refresh(); }, workshop, UiKit.Surface, 20);
                CellButton(row, Loc.T("EQUIP"), 0.67f, 0.83f, () => Menu.Commit(GameActions.Equip(save, uid), Loc.T("Item equipped")),
                    DragonRules.IsSlotUnlocked(item.slot, d.stage));
                CellButton(row, Loc.T("SALVAGE"), 0.83f, 1, () => Menu.Commit(GameActions.Salvage(save, uid), Loc.T("Salvaged")), true, UiKit.Surface);
            }
        }

        /// <summary>Three loadout presets: SAVE stores the current gear, skills and rider; USE switches to it.</summary>
        void Presets(PlayerSave save)
        {
            Header(list, Loc.T("Loadout presets"));
            for (int i = 0; i < PresetActions.Slots; i++)
            {
                int index = i;
                var preset = i < save.presets.Count ? save.presets[i] : null;
                bool empty = PresetActions.IsEmpty(preset);
                int items = 0;
                if (!empty) foreach (var uid in preset.equipped) if (save.FindEquipment(uid) != null) items++;
                var row = UiKit.Row(list, 100, UiKit.Surface);
                Cell(row, "<b>" + PresetActions.Name(save, i) + "</b>\n<color=#a3a8ba>"
                    + (empty ? Loc.T("empty") : Loc.F("{0} items · rider {1}", items, Loc.Rider(preset.mainRiderId))) + "</color>", 0.02f, 0.6f, 26);
                CellButton(row, Loc.T("SAVE"), 0.61f, 0.8f, () => Menu.Commit(PresetActions.Store(save, index)), true, UiKit.SurfaceAlt, 24);
                CellButton(row, Loc.T("USE"), 0.8f, 1, () => Menu.Commit(PresetActions.Apply(save, index)), !empty, UiKit.Accent, 24);
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
