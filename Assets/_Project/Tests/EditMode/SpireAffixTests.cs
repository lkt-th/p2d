using System;
using NUnit.Framework;

namespace P2D.Domain.Tests
{
    public class SpireAffixTests
    {
        static readonly DateTime Now = new DateTime(2026, 10, 30, 3, 0, 0, DateTimeKind.Utc);

        [TearDown]
        public void TearDown() => SpireAffixes.Live = false;

        static PlayerSave NewGame()
        {
            var save = PlayerSave.CreateNew("p", "Ignis", Now, "tim");
            save.story.cleared = StoryActions.TotalStages;
            GameActions.EnsureStarterKit(save, new SeededRandom(7));
            return save;
        }

        [Test]
        public void EachWeek_HasTheTheme_OnePositive_OneNegative_AndAllFourMixesAppear()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int week = -4; week < 8; week++)
            {
                var a = SpireAffixes.ForWeek(week);
                Assert.AreEqual(SpireAffix.ThemeSurge, a[0]);
                Assert.IsTrue(SpireAffixes.IsPositive(a[1]));
                Assert.IsFalse(SpireAffixes.IsPositive(a[2]));
                seen.Add(a[1] + "+" + a[2]);
            }
            Assert.AreEqual(4, seen.Count);
        }

        [Test]
        public void Apply_ChangesEnemiesDragonAndTimeLimit()
        {
            var save = NewGame();
            var plain = GameActions.BuildSpireBattle(save, 5, Element.Fire, 1, new SpireTuning());
            var setup = GameActions.BuildSpireBattle(save, 5, Element.Fire, 1, new SpireTuning());
            var loadout = GameActions.ToLoadout(save);
            SpireAffixes.Apply(setup, loadout, new[] { SpireAffix.ThemeSurge, SpireAffix.Rush, SpireAffix.ThickHide }, 5);
            Assert.AreEqual(plain.enemies[0].attack * 1.2, setup.enemies[0].attack, 1e-6);
            Assert.AreEqual(plain.enemies[0].defense * 1.3, setup.enemies[0].defense, 1e-6);
            Assert.Greater(setup.enemies[0].attackIntervalSeconds, plain.enemies[0].attackIntervalSeconds);
            Assert.AreEqual(60, setup.timeLimitSeconds);

            var boss = GameActions.BuildSpireBattle(save, 30, Element.Fire, 1, new SpireTuning());
            double bossLimit = boss.timeLimitSeconds;
            SpireAffixes.Apply(boss, loadout, new[] { SpireAffix.Rush }, 30);
            Assert.AreEqual(bossLimit, boss.timeLimitSeconds, "rush leaves the boss its time");

            var mist = GameActions.BuildSpireBattle(save, 5, Element.Fire, 1, new SpireTuning());
            SpireAffixes.Apply(mist, loadout, new[] { SpireAffix.Mist }, 5);
            Assert.AreEqual(Math.Max(0, plain.dragon.critRate - 0.1), mist.dragon.critRate, 1e-9);

            var blessed = GameActions.BuildSpireBattle(save, 5, Element.Fire, 1, new SpireTuning());
            SpireAffixes.Apply(blessed, loadout, new[] { SpireAffix.RiderBlessing }, 5);
            Assert.Greater(blessed.dragon.hp, plain.dragon.hp, "tim's HP pact buff is 25% stronger");
        }

        [Test]
        public void OffByDefault_LiveSwitchAppliesThem()
        {
            var save = NewGame();
            Assert.IsNull(SpireAffixes.Current(save));
            var off = GameActions.BuildSpireBattle(save, 5, Element.Fire, 1, new SpireTuning());
            SpireAffixes.Live = true;
            var on = GameActions.BuildSpireBattle(save, 5, Element.Fire, 1, new SpireTuning());
            Assert.Greater(on.enemies[0].attack, off.enemies[0].attack, "the theme affix is always on when live");
        }

        [Test]
        public void Rush_BoostsFirstClearMaterials_NotTheBoss()
        {
            var plain = SpireRules.FirstClearRewards(5, 1);
            var rush = new[] { SpireAffix.ThemeSurge, SpireAffix.Rush, SpireAffix.Mist };
            Assert.AreEqual((long)Math.Floor(plain.gold * 1.2), SpireAffixes.Boost(rush, 5, plain).gold);
            Assert.AreEqual(plain.pactSigils, SpireAffixes.Boost(rush, 5, plain).pactSigils);
            var boss = SpireRules.FirstClearRewards(30, 1);
            Assert.AreEqual(boss.gold, SpireAffixes.Boost(rush, 30, boss).gold);
        }
    }
}
