using System;
using NUnit.Framework;

namespace P2D.Domain.Tests
{
    public class HatchTests
    {
        static readonly DateTime Now = new DateTime(2026, 10, 30, 3, 0, 0, DateTimeKind.Utc);

        static PlayerSave NewGame() => PlayerSave.CreateNew("p", "Ignis", Now, "tim");

        [Test]
        public void NewGame_StartsAsAnEgg()
        {
            Assert.IsFalse(NewGame().dragon.hatched);
        }

        [Test]
        public void Hatch_NamesTheDragonOnce()
        {
            var save = NewGame();
            Assert.IsTrue(GameActions.Hatch(save, "  Pyra  ").ok);
            Assert.IsTrue(save.dragon.hatched);
            Assert.AreEqual("Pyra", save.dragon.name, "trimmed");
            Assert.IsFalse(GameActions.Hatch(save, "Other").ok, "hatching happens once");
            Assert.AreEqual("Pyra", save.dragon.name);
        }

        [Test]
        public void Hatch_EmptyName_KeepsTheDefault_LongNameIsRefused()
        {
            var save = NewGame();
            Assert.IsFalse(GameActions.Hatch(save, new string('a', GameActions.MaxNameLength + 1)).ok);
            Assert.IsFalse(save.dragon.hatched);
            Assert.IsTrue(GameActions.Hatch(save, "").ok);
            Assert.AreEqual("Ignis", save.dragon.name);
        }

        [Test]
        public void SavesFromBeforeTheEgg_AreMigratedAsHatched()
        {
            var save = NewGame();
            save.version = 1;
            Assert.IsTrue(SaveMigrator.Default().Migrate(save));
            Assert.AreEqual(SaveMigrator.CurrentVersion, save.version);
            Assert.IsTrue(save.dragon.hatched);
        }
    }
}
