using System;
using System.Collections.Generic;

namespace P2D.Domain
{
    // Save data uses only fields, arrays and List<T> so it round-trips through Unity's JsonUtility
    // (no Dictionary, no properties). Times are UTC ticks (long).

    [Serializable]
    public sealed class DragonSave
    {
        public string name = "Ignis";

        /// <summary>False until the player taps the egg open and names the dragon (GDD §11 step 1).</summary>
        public bool hatched;
        public int level = 1;
        public int stage = 1;
        public long exp;

        /// <summary>Equipment uid per slot, index = (int)EquipSlot; empty string = nothing.</summary>
        public string[] equipped = { "", "", "", "", "", "" };
        public string[] skillIds = { "", "", "" };
        public string mainRiderId = "";
        public string[] supportRiderIds = { "", "" };
    }

    [Serializable]
    public sealed class WalletSave
    {
        public long gold;
        public long crystals;
        public long forgeStones;
        public long dragonEssence;
        public int ancientScales;
        public int pactSigils;
        public int refineStones;
        public int soulShards;

        /// <summary>Universal soul seals per rider rank, index = (int)RiderRank.</summary>
        public int[] soulSeals = new int[4];

        /// <summary>Element crystals, index = (int)Element.</summary>
        public int[] elementCrystals = new int[6];

        /// <summary>Path essence, index = (int)GrowthPath.</summary>
        public int[] pathEssence = new int[4];
    }

    [Serializable]
    public sealed class SpireSave
    {
        public int weekIndex = int.MinValue;
        public int bracket = 1;
        public int highestFloor;
        public int lastWeekBest;
        public int torches = SpireRules.MaxTorches;
        public int lastTorchDay = int.MinValue;

        /// <summary>Boss reward claims this week for floors 10, 20, 30.</summary>
        public int[] bossClaims = new int[3];

        /// <summary>Weekly chest earned at the last reset and not yet opened (0 = none).</summary>
        public int pendingChestFloor;
    }

    [Serializable]
    public sealed class HuntSave
    {
        public int stamina = HuntRules.MaxStamina;
        public long staminaUpdatedUtcTicks;
        public int unlockedZone = 1;
    }

    [Serializable]
    public sealed class LoadoutPresetSave
    {
        public string name = "";
        public string[] equipped = { "", "", "", "", "", "" };
        public string[] skillIds = { "", "", "" };
        public string mainRiderId = "";
    }

    [Serializable]
    public sealed class PlayerSave
    {
        public int version = SaveMigrator.CurrentVersion;
        public string playerId = "";
        public long createdUtcTicks;
        public long savedUtcTicks;

        /// <summary>Counter for new equipment uids.</summary>
        public int nextItemId;

        public DragonSave dragon = new DragonSave();
        public WalletSave wallet = new WalletSave();
        public List<EquipmentState> equipment = new List<EquipmentState>();
        public List<RiderState> riders = new List<RiderState>();
        public SpireSave spire = new SpireSave();
        public HuntSave hunt = new HuntSave();
        public SummonPityState summonPity = new SummonPityState();
        public ShopSave shop = new ShopSave();
        public NestSave nest = new NestSave();
        public StorySave story = new StorySave();
        public MissionSave missions = new MissionSave();
        public List<LoadoutPresetSave> presets = new List<LoadoutPresetSave>();

        public static PlayerSave CreateNew(string playerId, string dragonName, DateTime utcNow, string starterRiderId)
        {
            var save = new PlayerSave
            {
                playerId = playerId,
                createdUtcTicks = utcNow.Ticks,
                savedUtcTicks = utcNow.Ticks,
            };
            save.dragon.name = dragonName;
            save.dragon.mainRiderId = starterRiderId;
            save.riders.Add(new RiderState { riderId = starterRiderId, star = 1 });
            save.hunt.staminaUpdatedUtcTicks = utcNow.Ticks;
            save.nest.lastCollectUtcTicks = utcNow.Ticks;
            save.spire.weekIndex = WeeklyCalendar.WeekIndex(utcNow);
            save.spire.lastTorchDay = WeeklyCalendar.DayIndex(utcNow);
            for (int i = 0; i < 3; i++)
                save.presets.Add(new LoadoutPresetSave { name = "ชุด " + (i + 1) });
            return save;
        }

        public RiderState FindRider(string riderId)
        {
            foreach (var r in riders) if (r.riderId == riderId) return r;
            return null;
        }

        public EquipmentState FindEquipment(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return null;
            foreach (var e in equipment) if (e.uid == uid) return e;
            return null;
        }
    }
}
