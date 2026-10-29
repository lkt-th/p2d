using System;
using NUnit.Framework;

namespace P2D.Domain.Tests
{
    public class LocTests
    {
        [TearDown]
        public void TearDown() => Loc.Current = Language.English;

        [Test]
        public void ThaiLines_KeepTheEnglishPlaceholders()
        {
            foreach (var pair in ThaiText.Table)
            {
                CollectionAssert.AreEqual(Loc.Placeholders(pair.Key), Loc.Placeholders(pair.Value), "placeholders differ for \"" + pair.Key + "\"");
                Assert.AreEqual(pair.Key.Contains(StoryScript.DragonToken), pair.Value.Contains(StoryScript.DragonToken),
                    "{dragon} differs for \"" + pair.Key + "\"");
                Assert.IsNotEmpty(pair.Value.Trim(), pair.Key);
            }
        }

        [Test]
        public void English_ReturnsTheKey_Thai_LooksItUp()
        {
            Assert.AreEqual("Need 5 gold", Loc.F("Need {0} gold", 5));
            Assert.AreEqual("Epic", Loc.Enum(Rarity.Epic));
            Assert.AreEqual("Tim", Loc.Rider("tim"));
            Assert.AreEqual("Flame Claw", Loc.Skill("flame_claw"));

            Loc.Current = Language.Thai;
            Assert.AreEqual("ต้องใช้ทอง 5", Loc.F("Need {0} gold", 5));
            Assert.AreEqual("มหากาพย์", Loc.Enum(Rarity.Epic));
            Assert.AreEqual("หายากมาก", Loc.Enum(EncounterKind.Epic), "enum keys are per type");
            Assert.AreEqual("ทิม", Loc.Rider("tim"));
            Assert.AreEqual("No such line yet", Loc.T("No such line yet"), "missing lines fall back to English");
        }

        [Test]
        public void EveryDataName_HasThai()
        {
            foreach (var c in StoryActions.Chapters)
                foreach (var name in new[] { c.name, c.guardian, c.boss })
                    Assert.IsTrue(ThaiText.Table.ContainsKey(name), name);
            foreach (var z in HuntActions.Zones) Assert.IsTrue(ThaiText.Table.ContainsKey(z.name), z.name);
            foreach (var t in SpireThemes.All)
            {
                Assert.IsTrue(ThaiText.Table.ContainsKey(t.name), t.name);
                Assert.IsTrue(ThaiText.Table.ContainsKey(t.boss), t.boss);
            }
            foreach (var s in SetCatalog.All)
                foreach (var text in new[] { s.name, s.twoPieceText, s.fourPieceText })
                    Assert.IsTrue(ThaiText.Table.ContainsKey(text), text);
            foreach (var r in RiderCatalog.All) Assert.IsTrue(ThaiText.Table.ContainsKey("Rider." + r.id), r.id);
            foreach (var s in SkillCatalog.All) Assert.IsTrue(ThaiText.Table.ContainsKey("Skill." + s.id), s.id);
            foreach (var item in ShopActions.ShardShop(0))
                if (!item.id.StartsWith("rider_")) Assert.IsTrue(ThaiText.Table.ContainsKey(item.name), item.name);
            foreach (Rarity r in Enum.GetValues(typeof(Rarity))) Assert.IsTrue(ThaiText.Table.ContainsKey("Rarity." + r), r.ToString());
            foreach (EquipSlot s in Enum.GetValues(typeof(EquipSlot))) Assert.IsTrue(ThaiText.Table.ContainsKey("EquipSlot." + s), s.ToString());
            foreach (GrowthPath p in Enum.GetValues(typeof(GrowthPath))) Assert.IsTrue(ThaiText.Table.ContainsKey("GrowthPath." + p), p.ToString());
            foreach (Element e in Enum.GetValues(typeof(Element))) Assert.IsTrue(ThaiText.Table.ContainsKey("Element." + e), e.ToString());
        }

        [Test]
        public void Dialogue_IsThai_WithTheDragonName()
        {
            Loc.Current = Language.Thai;
            for (int index = 1; index <= StoryActions.TotalStages; index++)
                foreach (var line in StoryScript.Before(index, "Pyra"))
                {
                    Assert.IsTrue(ThaiText.Table.ContainsValue(line.speaker), line.speaker);
                    Assert.IsFalse(line.text.Contains(StoryScript.DragonToken));
                    StringAssert.DoesNotMatch("[A-Za-z]{4,}", line.text.Replace("Pyra", "").Replace("Path", ""), "untranslated line at " + index);
                }
        }
    }
}
