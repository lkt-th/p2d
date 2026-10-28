using System;
using NUnit.Framework;

namespace P2D.Domain.Tests
{
    public class HuntAndShopTests
    {
        static readonly DateTime Now = new DateTime(2026, 10, 29, 3, 0, 0, DateTimeKind.Utc);

        static PlayerSave NewGame()
        {
            var save = PlayerSave.CreateNew("p", "Ignis", Now, "tim");
            GameActions.EnsureStarterKit(save, new SeededRandom(1));
            return save;
        }

        // ---- hunting grounds ----

        [Test]
        public void Zones_OpenWithStage()
        {
            var save = NewGame();
            Assert.IsTrue(HuntActions.IsZoneOpen(save, 1));
            Assert.IsFalse(HuntActions.IsZoneOpen(save, 2));
            save.dragon.stage = 2;
            Assert.IsTrue(HuntActions.IsZoneOpen(save, 2));
        }

        [Test]
        public void BeginRun_SpendsStaminaUntilEmpty()
        {
            var save = NewGame();
            save.hunt.stamina = 13;
            EncounterKind kind;
            Assert.IsTrue(HuntActions.BeginRun(save, Now, new SeededRandom(2), out kind));
            Assert.IsTrue(HuntActions.BeginRun(save, Now, new SeededRandom(3), out kind));
            Assert.IsFalse(HuntActions.BeginRun(save, Now, new SeededRandom(4), out kind));
            Assert.AreEqual(1, save.hunt.stamina);
        }

        [Test]
        public void NormalWin_PaysBasicResourcesOnly()
        {
            var save = NewGame();
            int items = save.equipment.Count;
            long gold = save.wallet.gold;
            var run = HuntActions.ApplyResult(save, 1, EncounterKind.Normal, BattleOutcome.Victory, new SeededRandom(5));
            Assert.AreEqual(gold + 1000, save.wallet.gold);
            Assert.IsNull(run.gear);
            Assert.AreEqual(items, save.equipment.Count);
        }

        [Test]
        public void RareWin_DropsGear_EscapeGivesConsolation()
        {
            var save = NewGame();
            var win = HuntActions.ApplyResult(save, 1, EncounterKind.Rare, BattleOutcome.Victory, new SeededRandom(6));
            Assert.IsNotNull(win.gear);
            Assert.AreEqual(3000, win.rewards.gold);

            var escaped = HuntActions.ApplyResult(save, 1, EncounterKind.Rare, BattleOutcome.Escaped, new SeededRandom(7));
            Assert.IsNull(escaped.gear);
            Assert.AreEqual(500, escaped.rewards.gold);

            long gold = save.wallet.gold;
            HuntActions.ApplyResult(save, 1, EncounterKind.Normal, BattleOutcome.Defeat, new SeededRandom(8));
            Assert.AreEqual(gold, save.wallet.gold, "losing to normals pays nothing");
        }

        [Test]
        public void RareMonsters_FleeAndAreTougher()
        {
            var normal = HuntActions.Enemies(2, EncounterKind.Normal);
            var rare = HuntActions.Enemies(2, EncounterKind.Rare);
            var epic = HuntActions.Enemies(2, EncounterKind.Epic);
            Assert.AreEqual(3, normal.Count);
            Assert.AreEqual(1, rare.Count);
            Assert.AreEqual(HuntRules.RareEscapeSeconds, rare[0].escapeSeconds);
            Assert.AreEqual(normal[0].hp * 9, rare[0].hp, 1e-6);
            Assert.AreEqual(normal[0].hp * 15, epic[0].hp, 1e-6);
        }

        [Test]
        public void AutoHunt_NewPlayerWinsZoneOne_AndStopsOnRare()
        {
            var save = NewGame();
            var runs = HuntActions.AutoHunt(save, 1, 10, Now, new SeededRandom(9));
            Assert.Greater(runs.Count, 0);
            for (int i = 0; i < runs.Count - 1; i++) Assert.AreEqual(EncounterKind.Normal, runs[i].kind);
            Assert.AreEqual(BattleOutcome.Victory, runs[0].outcome, "zone 1 normals are beatable by a new player");
            Assert.AreEqual(HuntRules.MaxStamina - HuntRules.StaminaCost * runs.Count, save.hunt.stamina);
        }

        [Test]
        public void ZoneOne_NewPlayerWinsNormalsReliably()
        {
            int wins = 0;
            const int runs = 200;
            for (int seed = 0; seed < runs; seed++)
            {
                var save = NewGame();
                var setup = HuntActions.BuildBattle(save, 1, EncounterKind.Normal, seed);
                setup.auto = true;
                if (new BattleSimulation(setup).RunToEnd() == BattleOutcome.Victory) wins++;
            }
            Assert.GreaterOrEqual(wins / (double)runs, 0.95, "zone 1 is the safe farm for a brand-new dragon");
        }

        [Test]
        public void SpireFloorOne_NewPlayerWinsReliably()
        {
            int wins = 0;
            const int runs = 200;
            for (int seed = 0; seed < runs; seed++)
            {
                var save = NewGame();
                var setup = GameActions.BuildSpireBattle(save, 1, Element.Fire, seed, new SpireTuning());
                setup.auto = true;
                if (new BattleSimulation(setup).RunToEnd() == BattleOutcome.Victory) wins++;
            }
            Assert.GreaterOrEqual(wins / (double)runs, 0.95);
        }

        [Test]
        public void HunterRider_RaisesBonus()
        {
            var save = NewGame();
            Assert.AreEqual(0, HuntActions.HuntBonus(save));
            save.riders.Add(new RiderState { riderId = "lycan", star = 5 });
            GameActions.SetMainRider(save, "lycan");
            Assert.AreEqual(0.44, HuntActions.HuntBonus(save), 1e-9);
        }

        // ---- shop ----

        [Test]
        public void ShardShop_RespectsPriceAndWeeklyLimit()
        {
            var save = NewGame();
            save.wallet.soulShards = 300;
            Assert.IsTrue(ShopActions.Buy(save, Now, "seal_ssr").ok);
            Assert.IsTrue(ShopActions.Buy(save, Now, "seal_ssr").ok);
            Assert.IsFalse(ShopActions.Buy(save, Now, "seal_ssr").ok, "limit 2 per week");
            Assert.AreEqual(2, save.wallet.soulSeals[(int)RiderRank.SSR]);
            Assert.AreEqual(100, save.wallet.soulShards);
            Assert.IsFalse(ShopActions.Buy(save, Now, "not_an_item").ok);

            var nextWeek = WeeklyCalendar.NextResetUtc(Now).AddMinutes(1);
            Assert.AreEqual(0, ShopActions.Bought(save, nextWeek, "seal_ssr"), "limits reset weekly");
        }

        [Test]
        public void FeaturedRiderCopy_AddsRiderOrCopy()
        {
            var save = NewGame();
            save.wallet.soulShards = 1000;
            int week = WeeklyCalendar.WeekIndex(Now);
            var featured = ShopActions.ShardShop(week).Find(i => i.id.StartsWith("rider_"));
            string riderId = featured.id.Substring(6);
            Assert.IsTrue(ShopActions.Buy(save, Now, featured.id).ok);
            Assert.IsNotNull(save.FindRider(riderId));
            Assert.IsFalse(ShopActions.Buy(save, Now, featured.id).ok, "limit 1");
        }

        [Test]
        public void StaminaRefill_TwicePerDayForCrystals()
        {
            var save = NewGame();
            Assert.AreEqual(300, save.wallet.crystals);
            Assert.IsTrue(ShopActions.RefillStamina(save, Now).ok);
            Assert.AreEqual(HuntRules.MaxStamina + ShopActions.StaminaRefillAmount, save.hunt.stamina);
            Assert.IsTrue(ShopActions.RefillStamina(save, Now).ok);
            Assert.IsFalse(ShopActions.RefillStamina(save, Now).ok);
            Assert.AreEqual(200, save.wallet.crystals);
            Assert.AreEqual(2, ShopActions.StaminaRefillsLeft(save, Now.AddDays(1)));
        }
    }
}
