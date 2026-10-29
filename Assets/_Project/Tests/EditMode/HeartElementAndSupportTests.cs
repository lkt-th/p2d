using System;
using NUnit.Framework;

namespace P2D.Domain.Tests
{
    public class HeartElementAndSupportTests
    {
        static readonly DateTime Now = new DateTime(2026, 10, 30, 3, 0, 0, DateTimeKind.Utc);

        [TearDown]
        public void TearDown() => SpireAffixes.Live = true;

        static PlayerSave NewGame()
        {
            var save = PlayerSave.CreateNew("p", "Ignis", Now, "tim");
            save.story.cleared = StoryActions.TotalStages;
            GameActions.EnsureStarterKit(save, new SeededRandom(7));
            return save;
        }

        [Test]
        public void StarterHeart_BreathesWater()
        {
            var save = NewGame();
            var heart = save.FindEquipment(save.dragon.equipped[(int)EquipSlot.HeartCore]);
            Assert.AreEqual(Element.Water, heart.element);
            Assert.AreEqual(Element.Water, GameActions.HeartElement(heart));
        }

        [Test]
        public void NewHeartCores_GetAnElement_WithoutChangingTheRandomStream()
        {
            var seen = new System.Collections.Generic.HashSet<Element>();
            for (int i = 0; i < 40; i++)
            {
                var a = new SeededRandom(5);
                var b = new SeededRandom(5);
                var heart = EquipmentGenerator.Create(a, Rarity.Rare, EquipSlot.HeartCore, GrowthPath.Fury, "eq" + i.ToString("D5"));
                var claw = EquipmentGenerator.Create(b, Rarity.Rare, EquipSlot.Claw, GrowthPath.Fury, "eq" + i.ToString("D5"));
                Assert.AreEqual(a.Next(1000), b.Next(1000), "the element is not a random draw");
                Assert.AreEqual(heart.element, EquipmentGenerator.HeartElementFor(heart.uid));
                Assert.That((int)heart.element, Is.InRange(0, 3), "Fire / Water / Wind / Earth only");
                seen.Add(heart.element);
            }
            Assert.AreEqual(4, seen.Count, "all four elements appear");
        }

        [Test]
        public void EquippingAnotherHeart_ChangesTheBattleElement()
        {
            var save = NewGame();
            SpireAffixes.Live = false;
            Element target = Element.Fire;
            EquipmentState fire = null;
            for (int i = 0; fire == null; i++)
            {
                var item = EquipmentGenerator.Create(new SeededRandom(1), Rarity.Rare, EquipSlot.HeartCore, GrowthPath.Fury, "eq8" + i.ToString("D4"));
                if (item.element == target) fire = item;
            }
            save.equipment.Add(fire);
            Assert.IsTrue(GameActions.Equip(save, fire.uid).ok);
            Assert.AreEqual(Element.Fire, GameActions.BuildSpireBattle(save, 1, Element.Wind, 1, new SpireTuning()).dragonElement);
            Assert.AreEqual(Element.Fire, StoryActions.BuildBattle(save, 1, 1, new SpireTuning()).dragonElement);
        }

        [Test]
        public void SavesFromVersion2_KeepWaterHearts()
        {
            var save = NewGame();
            var heart = save.FindEquipment(save.dragon.equipped[(int)EquipSlot.HeartCore]);
            heart.element = Element.Fire; // what JsonUtility gives an old save: the enum default
            save.version = 2;
            Assert.IsTrue(SaveMigrator.Default().Migrate(save));
            Assert.AreEqual(3, save.version);
            Assert.AreEqual(Element.Water, heart.element);
        }

        [Test]
        public void SupportRiders_OpenAtStage4_TwoSlots_NotTheMainRider()
        {
            var save = NewGame();
            foreach (var id in new[] { "lira", "gorth", "finn" }) save.riders.Add(new RiderState { riderId = id, star = 1 });
            Assert.IsFalse(GameActions.ToggleSupportRider(save, "lira").ok, "stage 1 has no support slots");

            save.dragon.stage = 4;
            Assert.IsFalse(GameActions.ToggleSupportRider(save, "tim").ok, "tim is the main rider");
            Assert.IsTrue(GameActions.ToggleSupportRider(save, "lira").ok);
            Assert.IsTrue(GameActions.ToggleSupportRider(save, "gorth").ok);
            Assert.IsFalse(GameActions.ToggleSupportRider(save, "finn").ok, "both slots full");
            Assert.AreEqual(2, GameActions.ToLoadout(save).supportRiders.Count);

            Assert.IsTrue(GameActions.ToggleSupportRider(save, "lira").ok, "a second tap removes");
            Assert.AreEqual("", save.dragon.supportRiderIds[0]);
            Assert.IsTrue(GameActions.ToggleSupportRider(save, "finn").ok, "the free slot is reused");
            Assert.AreEqual("finn", save.dragon.supportRiderIds[0]);

            Assert.IsTrue(GameActions.SetMainRider(save, "gorth").ok);
            Assert.AreEqual("", save.dragon.supportRiderIds[1], "riding a supporter frees its slot");
        }
    }
}
