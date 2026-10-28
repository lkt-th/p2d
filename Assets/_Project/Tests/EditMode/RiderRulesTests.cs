using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace P2D.Domain.Tests
{
    public class RiderRulesTests
    {
        [Test]
        public void Kyren_FiveStarBuffs()
        {
            var buffs = RiderRules.Buffs(TestRiders.Kyren(), new RiderState { star = 5 }).ToList();
            Assert.AreEqual(0.22, buffs[0].value, 1e-9);
            Assert.AreEqual(0.33, buffs[1].value, 1e-9);
            Assert.AreEqual(26, RiderRules.Affinity(TestRiders.Kyren(), new RiderState { star = 5 }));
            Assert.AreEqual(21, RiderRules.Affinity(TestRiders.Kyren(), new RiderState { star = 4 }));
        }

        [Test]
        public void SupportRider_GivesThirtyPercent()
        {
            var buffs = RiderRules.Buffs(TestRiders.Kyren(), new RiderState { star = 1 }, RiderRules.SupportRatio).ToList();
            Assert.AreEqual(0.03, buffs[0].value, 1e-9);
        }

        [Test]
        public void StarUp_NeedsTenCopiesFromOneToFive()
        {
            int total = 0;
            for (int star = 2; star <= RiderRules.MaxStar; star++) total += RiderRules.CopiesNeeded(star);
            Assert.AreEqual(10, total);
        }

        [Test]
        public void StarUp_SealsCoverAtMostHalf()
        {
            // 4★ → 5★ needs 4; 2 copies + 1 seal is not enough (as in the mockup)
            var state = new RiderState { star = 4, copies = 2 };
            Assert.IsFalse(RiderRules.CanStarUp(state, 1));
            Assert.IsTrue(RiderRules.CanStarUp(state, 2));
            Assert.IsFalse(RiderRules.CanStarUp(new RiderState { star = 4, copies = 1 }, 5));
        }

        [Test]
        public void StarUp_SpendsCopiesBeforeSeals()
        {
            var state = new RiderState { star = 4, copies = 3 };
            int sealsUsed = RiderRules.StarUp(state, 2);
            Assert.AreEqual(5, state.star);
            Assert.AreEqual(0, state.copies);
            Assert.AreEqual(1, sealsUsed);
            Assert.IsFalse(RiderRules.CanStarUp(state, 99));
        }

        [Test]
        public void HuntBonus_ScalesByStarAndCaps()
        {
            Assert.AreEqual(0.22, RiderRules.HuntBonus(TestRiders.Finn(), new RiderState { star = 5 }), 1e-9);
            Assert.AreEqual(0.44, RiderRules.HuntBonus(TestRiders.Lycan(), new RiderState { star = 5 }), 1e-9);

            double total = RiderRules.TotalHuntBonus(
                TestRiders.Lycan(), new RiderState { star = 5 },
                new List<RiderProfile> { TestRiders.Finn() },
                new List<RiderState> { new RiderState { star = 5 } });
            Assert.AreEqual(RiderRules.MaxHuntBonus, total, 1e-9);
        }
    }
}
