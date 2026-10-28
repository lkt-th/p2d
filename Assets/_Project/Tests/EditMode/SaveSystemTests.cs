using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace P2D.Domain.Tests
{
    /// <summary>In Unity this uses JsonUtility (the shipping serializer); outside Unity, DataContractJson.</summary>
    sealed class TestSerializer : ISaveSerializer
    {
        public string Serialize<T>(T value)
        {
#if UNITY_5_3_OR_NEWER
            return UnityEngine.JsonUtility.ToJson(value);
#else
            var serializer = new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(T));
            using (var stream = new System.IO.MemoryStream())
            {
                serializer.WriteObject(stream, value);
                return System.Text.Encoding.UTF8.GetString(stream.ToArray());
            }
#endif
        }

        public T Deserialize<T>(string text)
        {
#if UNITY_5_3_OR_NEWER
            return UnityEngine.JsonUtility.FromJson<T>(text);
#else
            var serializer = new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(T));
            using (var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(text)))
                return (T)serializer.ReadObject(stream);
#endif
        }
    }

    sealed class MemoryStorage : ISaveStorage
    {
        public readonly Dictionary<string, string> Data = new Dictionary<string, string>();
        public bool Exists(string key) => Data.ContainsKey(key);
        public string Read(string key) => Data[key];
        public void Write(string key, string text) => Data[key] = text;
        public void Delete(string key) => Data.Remove(key);
    }

    public class SaveSystemTests
    {
        static readonly DateTime Now = new DateTime(2026, 10, 28, 3, 0, 0, DateTimeKind.Utc); // Wed 10:00 ICT

        static PlayerSave Sample()
        {
            var save = PlayerSave.CreateNew("player-1", "Ignis", Now, "tim");
            save.dragon.level = 34;
            save.dragon.stage = 2;
            save.wallet.gold = 1200000;
            save.wallet.elementCrystals[(int)Element.Fire] = 42;
            save.equipment.Add(new EquipmentState
            {
                uid = "eq-1", definitionId = "magma_claw", slot = EquipSlot.Claw, rarity = Rarity.Legendary,
                path = GrowthPath.Fury, setId = "magma", enhance = 9,
                main = new StatModifier(StatType.ATK, ModType.Flat, 170),
                subs = new[] { new StatModifier(StatType.CritDmg, ModType.Percent, 0.092) },
            });
            save.dragon.equipped[(int)EquipSlot.Claw] = "eq-1";
            save.riders.Add(new RiderState { riderId = "kyren", star = 4, copies = 2 });
            save.summonPity.sinceUr = 37;
            return save;
        }

        [Test]
        public void NothingStored_IsNewGame()
        {
            var service = new SaveService(new MemoryStorage(), new TestSerializer());
            Assert.AreEqual(LoadStatus.NewGame, service.Load(Now).status);
        }

        [Test]
        public void SaveThenLoad_RoundTrips()
        {
            var service = new SaveService(new MemoryStorage(), new TestSerializer());
            service.Save(Sample(), Now);

            var result = service.Load(Now);
            Assert.AreEqual(LoadStatus.Loaded, result.status);
            var save = result.save;
            Assert.AreEqual("Ignis", save.dragon.name);
            Assert.AreEqual(34, save.dragon.level);
            Assert.AreEqual(1200000, save.wallet.gold);
            Assert.AreEqual(42, save.wallet.elementCrystals[(int)Element.Fire]);
            Assert.AreEqual(9, save.FindEquipment("eq-1").enhance);
            Assert.AreEqual(0.092, save.FindEquipment("eq-1").subs[0].value, 1e-12);
            Assert.AreEqual("eq-1", save.dragon.equipped[(int)EquipSlot.Claw]);
            Assert.AreEqual(4, save.FindRider("kyren").star);
            Assert.AreEqual(37, save.summonPity.sinceUr);
            Assert.AreEqual(3, save.presets.Count);
        }

        [Test]
        public void DamagedMainSave_RecoversFromBackup()
        {
            var storage = new MemoryStorage();
            var service = new SaveService(storage, new TestSerializer());
            var save = Sample();
            service.Save(save, Now);
            save.wallet.gold = 999;
            service.Save(save, Now.AddMinutes(1));

            storage.Data[SaveService.MainKey] = storage.Data[SaveService.MainKey].Substring(0, 40);

            var result = service.Load(Now.AddMinutes(2));
            Assert.AreEqual(LoadStatus.RecoveredFromBackup, result.status);
            Assert.AreEqual(1200000, result.save.wallet.gold);
        }

        [Test]
        public void TamperedPayload_FailsChecksum()
        {
            var storage = new MemoryStorage();
            var service = new SaveService(storage, new TestSerializer());
            service.Save(Sample(), Now);
            storage.Data[SaveService.MainKey] = storage.Data[SaveService.MainKey].Replace("1200000", "9999999");
            storage.Data.Remove(SaveService.BackupKey);

            Assert.AreEqual(LoadStatus.Corrupt, service.Load(Now).status);
        }

        [Test]
        public void OlderSave_IsMigrated()
        {
            var storage = new MemoryStorage();
            var old = Sample();
            old.version = 1;
            new SaveService(storage, new TestSerializer(), new SaveMigrator(1)).Save(old, Now);

            var migrator = new SaveMigrator(2).Add(1, s => s.wallet.crystals += 500);
            var result = new SaveService(storage, new TestSerializer(), migrator).Load(Now);
            Assert.AreEqual(2, result.save.version);
            Assert.AreEqual(500, result.save.wallet.crystals);
        }

        [Test]
        public void NewerSave_IsRejected()
        {
            var storage = new MemoryStorage();
            var future = Sample();
            future.version = 99;
            new SaveService(storage, new TestSerializer(), new SaveMigrator(99)).Save(future, Now);

            var result = new SaveService(storage, new TestSerializer()).Load(Now);
            Assert.AreEqual(LoadStatus.NewerVersion, result.status);
            Assert.IsNull(result.save);
        }

        [Test]
        public void Offline_StaminaRegeneratesAndKeepsPartialMinutes()
        {
            var save = Sample();
            Assert.IsTrue(OfflineProgress.SpendHuntStamina(save.hunt, Now));
            Assert.IsTrue(OfflineProgress.SpendHuntStamina(save.hunt, Now));
            Assert.AreEqual(108, save.hunt.stamina);

            var report = OfflineProgress.Apply(save, Now.AddMinutes(37));
            Assert.AreEqual(7, report.staminaGained);
            Assert.AreEqual(115, save.hunt.stamina);

            OfflineProgress.Apply(save, Now.AddMinutes(40));
            Assert.AreEqual(116, save.hunt.stamina);

            OfflineProgress.Apply(save, Now.AddDays(1));
            Assert.AreEqual(HuntRules.MaxStamina, save.hunt.stamina);
        }

        [Test]
        public void Offline_TorchesRefillDailyUpToNine()
        {
            var save = Sample();
            save.spire.torches = 2;
            OfflineProgress.Apply(save, Now.AddHours(20)); // 06:00 ICT the next day
            Assert.AreEqual(5, save.spire.torches);
            OfflineProgress.Apply(save, Now.AddDays(5));
            Assert.AreEqual(SpireRules.MaxTorches, save.spire.torches);
        }

        [Test]
        public void Offline_WeeklyResetUsesCatchUpAndLocksBracket()
        {
            var save = Sample();
            save.spire.highestFloor = 25;
            save.spire.bossClaims[0] = 3;

            var report = OfflineProgress.Apply(save, WeeklyCalendar.NextResetUtc(Now).AddMinutes(1));
            Assert.IsTrue(report.weekRolledOver);
            Assert.AreEqual(25, report.weeklyChestFloor);
            Assert.AreEqual(25, save.spire.lastWeekBest);
            Assert.AreEqual(12, save.spire.highestFloor);
            Assert.AreEqual(2, save.spire.bracket);
            Assert.AreEqual(0, save.spire.bossClaims[0]);
        }

        [Test]
        public void Offline_SkippedWeekGivesNoChest()
        {
            var save = Sample();
            save.spire.highestFloor = 30;
            var report = OfflineProgress.Apply(save, Now.AddDays(15));
            Assert.AreEqual(0, report.weeklyChestFloor);
            Assert.AreEqual(0, save.spire.highestFloor);
        }
    }
}
