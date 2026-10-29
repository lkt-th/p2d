using System;
using NUnit.Framework;

namespace P2D.Domain.Tests
{
    public class FreeSummonTests
    {
        static readonly DateTime Now = new DateTime(2026, 10, 30, 3, 0, 0, DateTimeKind.Utc);

        [Test]
        public void OneFreePullPerDay_WithoutSpendingSigils()
        {
            var save = PlayerSave.CreateNew("p", "Ignis", Now, "tim");
            save.story.cleared = StoryActions.TotalStages;
            int sigils = save.wallet.pactSigils;
            var random = new SeededRandom(4);

            Assert.IsTrue(FreeSummon.IsAvailable(save, Now));
            Assert.AreEqual(1, FreeSummon.Pull(save, Now, random).Count);
            Assert.AreEqual(sigils, save.wallet.pactSigils, "free pulls cost nothing");
            Assert.AreEqual(1, save.summonPity.sinceUr, "it counts toward pity");
            Assert.AreEqual(0, FreeSummon.Pull(save, Now.AddHours(5), random).Count, "once per day");

            var tomorrow = FreeSummon.NextFreeUtc(Now);
            Assert.Greater(tomorrow, Now);
            Assert.LessOrEqual(tomorrow - Now, TimeSpan.FromDays(1));
            Assert.AreEqual(1, FreeSummon.Pull(save, tomorrow, random).Count, "a new day, a new pull");
        }

        [Test]
        public void LockedUntilRidersOpen()
        {
            var save = PlayerSave.CreateNew("p", "Ignis", Now, "tim");
            Assert.IsFalse(FreeSummon.IsAvailable(save, Now));
            Assert.AreEqual(0, FreeSummon.Pull(save, Now, new SeededRandom(1)).Count);
        }
    }
}
