using System.Collections.Generic;

namespace P2D.Domain
{
    public sealed class BalanceScenario
    {
        public string title;
        public int bracket;
        public Element themeElement = Element.Fire;
        public List<BuildPreset> builds = new List<BuildPreset>();
    }

    /// <summary>
    /// Builds that match the pacing targets in docs/Balance.md §8, one per path at the same
    /// investment so path strength can be compared fairly. All use a Water heart against the
    /// Fire week (the advice the spire screen gives).
    /// </summary>
    public static class BalanceScenarios
    {
        static BuildPreset Build(string name, int level, int stage, Rarity rarity, int enhance,
            GrowthPath path, string rider, int star, params string[] skills)
        {
            return new BuildPreset
            {
                name = name, level = level, stage = stage, rarity = rarity, enhance = enhance,
                riderId = rider, riderStar = star, skillIds = skills,
            }.WithAllSlots(path);
        }

        public static List<BalanceScenario> Default()
        {
            var b2 = new BalanceScenario { title = "สัปดาห์ที่ 3 · Stage 2 · มหากาพย์ +9", bracket = 2 };
            b2.builds.Add(Build("B2 Fury", 35, 2, Rarity.Epic, 9, GrowthPath.Fury, "kyren", 4, "flame_claw", "searing_breath"));
            b2.builds.Add(Build("B2 Bastion", 35, 2, Rarity.Epic, 9, GrowthPath.Bastion, "brogan", 4, "scale_guard", "tail_slam"));
            b2.builds.Add(Build("B2 Tempest", 35, 2, Rarity.Epic, 9, GrowthPath.Tempest, "zephyr", 4, "gale_claws", "cyclone_step"));
            b2.builds.Add(Build("B2 Arcane", 35, 2, Rarity.Epic, 9, GrowthPath.Arcane, "selena", 4, "rune_orb", "star_bolt"));
            b2.builds.Add(Build("B2 Tempest (ลีร่า SR)", 35, 2, Rarity.Epic, 9, GrowthPath.Tempest, "lira", 4, "gale_claws", "cyclone_step"));
            b2.builds.Add(Build("B2 Fury ของต่ำ", 35, 2, Rarity.Rare, 6, GrowthPath.Fury, "kyren", 2, "flame_claw"));

            var b4 = new BalanceScenario { title = "สัปดาห์ที่ 7–12 · Stage 4 · ตำนาน +12", bracket = 4 };
            b4.builds.Add(Build("B4 Fury", 75, 4, Rarity.Legendary, 12, GrowthPath.Fury, "kyren", 5, "flame_claw", "searing_breath", "executioner_bite"));
            b4.builds.Add(Build("B4 Bastion", 75, 4, Rarity.Legendary, 12, GrowthPath.Bastion, "brogan", 5, "scale_guard", "tail_slam", "stone_mend"));
            b4.builds.Add(Build("B4 Tempest", 75, 4, Rarity.Legendary, 12, GrowthPath.Tempest, "zephyr", 5, "gale_claws", "cyclone_step", "wind_shear"));
            b4.builds.Add(Build("B4 Arcane", 75, 4, Rarity.Legendary, 12, GrowthPath.Arcane, "selena", 5, "rune_orb", "star_bolt", "time_mirror"));
            var hybrid = Build("B4 Fury+Tempest", 75, 4, Rarity.Legendary, 12, GrowthPath.Fury, "kyren", 5, "flame_claw", "searing_breath", "gale_claws");
            hybrid.slotPaths[(int)EquipSlot.Horn] = GrowthPath.Tempest;
            hybrid.slotPaths[(int)EquipSlot.Wing] = GrowthPath.Tempest;
            hybrid.slotPaths[(int)EquipSlot.Tail] = GrowthPath.Tempest;
            b4.builds.Add(hybrid);

            var b5 = new BalanceScenario { title = "6 เดือน+ · Stage 5 · เทพนิยาย +15 (มีสกิลขั้นสุด)", bracket = 5 };
            b5.builds.Add(Build("B5 Fury", 100, 5, Rarity.Mythic, 15, GrowthPath.Fury, "kyren", 5, "flame_claw", "executioner_bite", "inferno_cataclysm"));
            b5.builds.Add(Build("B5 Bastion", 100, 5, Rarity.Mythic, 15, GrowthPath.Bastion, "brogan", 5, "tail_slam", "stone_mend", "eternal_bastion"));
            b5.builds.Add(Build("B5 Tempest", 100, 5, Rarity.Mythic, 15, GrowthPath.Tempest, "zephyr", 5, "gale_claws", "wind_shear", "thousand_wings"));
            b5.builds.Add(Build("B5 Arcane", 100, 5, Rarity.Mythic, 15, GrowthPath.Arcane, "selena", 5, "rune_orb", "time_mirror", "galaxy_rain"));

            // Same investment as B4, with a 4-piece set on Claw / Chest / Horn / Wing. Each path with
            // "its" set, plus two cross combinations worth watching (multi-hit crits, dodge counters).
            var b4Sets = new BalanceScenario { title = "สัปดาห์ที่ 7–12 · Stage 4 · ตำนาน +12 + ชุด 4 ชิ้น", bracket = 4 };
            var four = new[] { EquipSlot.Claw, EquipSlot.Chest, EquipSlot.Horn, EquipSlot.Wing };
            b4Sets.builds.Add(Build("B4 Fury+Magma", 75, 4, Rarity.Legendary, 12, GrowthPath.Fury, "kyren", 5, "flame_claw", "searing_breath", "executioner_bite").WithSet("magma", four));
            b4Sets.builds.Add(Build("B4 Bastion+Glacial", 75, 4, Rarity.Legendary, 12, GrowthPath.Bastion, "brogan", 5, "scale_guard", "tail_slam", "stone_mend").WithSet("glacial", four));
            b4Sets.builds.Add(Build("B4 Tempest+Storm", 75, 4, Rarity.Legendary, 12, GrowthPath.Tempest, "zephyr", 5, "gale_claws", "cyclone_step", "wind_shear").WithSet("storm", four));
            b4Sets.builds.Add(Build("B4 Arcane+Star", 75, 4, Rarity.Legendary, 12, GrowthPath.Arcane, "selena", 5, "rune_orb", "star_bolt", "time_mirror").WithSet("star", four));
            b4Sets.builds.Add(Build("B4 Tempest+Magma", 75, 4, Rarity.Legendary, 12, GrowthPath.Tempest, "zephyr", 5, "gale_claws", "cyclone_step", "wind_shear").WithSet("magma", four));
            b4Sets.builds.Add(Build("B4 Fury+Storm", 75, 4, Rarity.Legendary, 12, GrowthPath.Fury, "kyren", 5, "flame_claw", "searing_breath", "executioner_bite").WithSet("storm", four));

            return new List<BalanceScenario> { b2, b4, b4Sets, b5 };
        }
    }
}
