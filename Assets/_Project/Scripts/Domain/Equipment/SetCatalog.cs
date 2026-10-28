using System.Collections.Generic;

namespace P2D.Domain
{
    public sealed class EquipmentSet
    {
        public string id;
        public string name;
        public Element dropElement;
        public StatModifier[] twoPiece;
        public string twoPieceText;
        public string fourPieceText;
    }

    /// <summary>
    /// Equipment sets from docs/GDD.md §4.6. Each weekly spire theme drops its set from the
    /// guardian and boss floors. 2 pieces give stats; 4 pieces add a battle effect
    /// (applied through PathBattleTraits by <see cref="LoadoutCalculator"/>).
    /// </summary>
    public static class SetCatalog
    {
        public const int TwoPiece = 2;
        public const int FourPiece = 4;

        public static readonly List<EquipmentSet> All = new List<EquipmentSet>
        {
            new EquipmentSet
            {
                id = "magma", name = "Magma Scale", dropElement = Element.Fire,
                twoPiece = new[] { new StatModifier(StatType.ATK, ModType.Percent, 0.12) },
                twoPieceText = "ATK +12%", fourPieceText = "A crit triggers a fire burst for 40% ATK (once per second)",
            },
            new EquipmentSet
            {
                id = "glacial", name = "Glacial Thorn", dropElement = Element.Water,
                twoPiece = new[] { new StatModifier(StatType.DEF, ModType.Percent, 0.15) },
                twoPieceText = "DEF +15%", fourPieceText = "Reflect 40% of damage taken while shielded",
            },
            new EquipmentSet
            {
                id = "storm", name = "Storm Plume", dropElement = Element.Wind,
                twoPiece = new[] { new StatModifier(StatType.SPD, ModType.Flat, 12) },
                twoPieceText = "SPD +12", fourPieceText = "Dodging counterattacks for 100% ATK",
            },
            new EquipmentSet
            {
                id = "star", name = "Ancient Star", dropElement = Element.Earth,
                twoPiece = new[] { new StatModifier(StatType.SkillHaste, ModType.Flat, 8) },
                twoPieceText = "Skill Haste +8", fourPieceText = "After a skill, Element DMG +40% for 5 s",
            },
        };

        public static EquipmentSet Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var s in All) if (s.id == id) return s;
            return null;
        }

        /// <summary>The set a spire week of this enemy element drops.</summary>
        public static EquipmentSet ForElement(Element element)
        {
            foreach (var s in All) if (s.dropElement == element) return s;
            return null;
        }

        /// <summary>Pieces of each set among <paramref name="items"/>.</summary>
        public static Dictionary<string, int> Count(IEnumerable<EquipmentState> items)
        {
            var counts = new Dictionary<string, int>();
            foreach (var item in items)
            {
                if (item == null || Find(item.setId) == null) continue;
                int n;
                counts.TryGetValue(item.setId, out n);
                counts[item.setId] = n + 1;
            }
            return counts;
        }

        public static IEnumerable<StatModifier> StatModifiers(Dictionary<string, int> counts)
        {
            foreach (var pair in counts)
                if (pair.Value >= TwoPiece)
                    foreach (var m in Find(pair.Key).twoPiece) yield return m;
        }

        /// <summary>Adds the 4-piece battle effects to <paramref name="traits"/>.</summary>
        public static void ApplyFourPiece(Dictionary<string, int> counts, ref PathBattleTraits traits)
        {
            foreach (var pair in counts)
            {
                if (pair.Value < FourPiece) continue;
                switch (pair.Key)
                {
                    case "magma": traits.critBurstMultiplier = 0.40; traits.critBurstCooldownSeconds = 1; break;
                    case "glacial": traits.shieldedReflectPercent = 0.40; break;
                    case "storm": traits.dodgeCounterMultiplier = 1.00; break;
                    case "star": traits.skillElementBuff = 0.40; traits.skillElementBuffSeconds = 5; break;
                }
            }
        }
    }
}
