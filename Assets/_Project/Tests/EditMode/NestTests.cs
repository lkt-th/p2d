using System;
using NUnit.Framework;

namespace P2D.Domain.Tests
{
    public class NestTests
    {
        static readonly DateTime Now = new DateTime(2026, 10, 30, 3, 0, 0, DateTimeKind.Utc);

        static PlayerSave NewGame() => PlayerSave.CreateNew("p", "Ignis", Now, "tim");

        [TestCase(1, 1500, 1.0)]
        [TestCase(2, 2000, 1.5)]
        [TestCase(10, 6000, 5.5)]
        public void Rates_FollowBalanceTable(int level, long gold, double essence)
        {
            Assert.AreEqual(gold, NestActions.GoldPerHour(level));
            Assert.AreEqual(essence, NestActions.EssencePerHour(level), 1e-9);
        }

        [Test]
        public void Income_AccruesPerHour_AndCapsAtTwelve()
        {
            var save = NewGame();
            var two = NestActions.Pending(save, Now.AddHours(2));
            Assert.AreEqual(3000, two.gold);
            Assert.AreEqual(2, two.essence);
            Assert.IsFalse(two.full);

            var day = NestActions.Pending(save, Now.AddHours(30));
            Assert.AreEqual(1500 * 12, day.gold);
            Assert.AreEqual(12, day.hours, 1e-9);
            Assert.IsTrue(day.full);
        }

        [Test]
        public void Collect_PaysOnce()
        {
            var save = NewGame();
            Assert.IsFalse(NestActions.Collect(save, Now).ok, "nothing stored at the start");
            Assert.IsTrue(NestActions.Collect(save, Now.AddHours(3)).ok);
            Assert.AreEqual(4500, save.wallet.gold);
            Assert.AreEqual(3, save.wallet.dragonEssence);
            Assert.IsFalse(NestActions.Collect(save, Now.AddHours(3)).ok);
        }

        [Test]
        public void Upgrade_CostsGold_CollectsFirst_AndIsCappedByStage()
        {
            var save = NewGame();
            save.wallet.gold = 100000;
            Assert.IsTrue(NestActions.Upgrade(save, Now.AddHours(1)).ok);
            Assert.AreEqual(2, save.nest.level);
            Assert.AreEqual(100000 + 1500 - 20000, save.wallet.gold, "1 h collected at the old rate, then 20,000 paid");

            Assert.IsFalse(NestActions.Upgrade(save, Now.AddHours(1)).ok, "stage 1 caps the nest at Lv 2");
            save.dragon.stage = 2;
            Assert.IsTrue(NestActions.Upgrade(save, Now.AddHours(1)).ok);
            Assert.AreEqual(3, save.nest.level);
            Assert.AreEqual(100000 + 1500 - 20000 - 80000, save.wallet.gold);
        }

        [Test]
        public void OlderSaveWithoutTimestamp_StartsNow()
        {
            var save = NewGame();
            save.nest = new NestSave();
            Assert.AreEqual(0, NestActions.Pending(save, Now.AddHours(5)).gold);
            Assert.AreEqual(1500, NestActions.Pending(save, Now.AddHours(6)).gold);
        }
    }
}
