using System;
using System.Collections.Generic;

namespace P2D.Domain
{
    [Serializable]
    public sealed class StorySave
    {
        /// <summary>Highest stage cleared, counted across chapters (chapter 2 stage 3 = 13); 0 = none.</summary>
        public int cleared;
    }

    public enum StoryStageKind { Normal, Guardian, Boss }

    public struct StoryChapter
    {
        public int chapter;
        public string name;
        public Element element;
        public string guardian;
        public string boss;
        /// <summary>Rider given on the chapter boss's first clear (GDD §5: R/SR by chapter, one SSR in chapter 3).</summary>
        public string riderReward;
    }

    public sealed class StoryReward
    {
        public bool firstClear;
        public long gold;
        public int essence;
        public int forgeStones;
        public int pactSigils;
        public int crystals;
        public EquipmentState gear;
        public string riderId = "";
        public bool riderIsNew;
    }

    /// <summary>
    /// Story mode, "Path of the Dragon" (docs/GDD.md §8, numbers in Balance §14): chapters of 10
    /// stages played in order. Stage 5 is a guardian, stage 10 the chapter boss. Only the next stage
    /// can be fought and its first clear pays once; there is no stamina cost.
    /// </summary>
    public static class StoryActions
    {
        public const int StagesPerChapter = 10;
        public const int GuardianStage = 5;
        public const int FirstEnemyLevel = 5;
        public const double TimeLimitSeconds = 90;
        public const double BossTimeLimitSeconds = 120;

        public static readonly StoryChapter[] Chapters =
        {
            new StoryChapter { chapter = 1, name = "Ember Cradle", element = Element.Fire, guardian = "cinder hound", boss = "Ashmaw", riderReward = "mina" },
            new StoryChapter { chapter = 2, name = "Galewind Steppe", element = Element.Wind, guardian = "gale harpy", boss = "Stormcrown", riderReward = "lira" },
            new StoryChapter { chapter = 3, name = "Tidecall Coast", element = Element.Water, guardian = "reef crab", boss = "Abyssal Maw", riderReward = "brogan" },
        };

        public static int TotalStages => Chapters.Length * StagesPerChapter;

        /// <summary>Stage number across chapters, 1-based.</summary>
        public static int Index(int chapter, int stage) => (chapter - 1) * StagesPerChapter + stage;

        public static int ChapterOf(int index) => (index - 1) / StagesPerChapter + 1;

        public static int StageOf(int index) => (index - 1) % StagesPerChapter + 1;

        public static StoryChapter Chapter(int chapter) => Chapters[Math.Max(1, Math.Min(Chapters.Length, chapter)) - 1];

        public static string Label(int index) => ChapterOf(index) + "-" + StageOf(index);

        public static StoryStageKind KindOf(int index)
        {
            int stage = StageOf(index);
            return stage == StagesPerChapter ? StoryStageKind.Boss : stage == GuardianStage ? StoryStageKind.Guardian : StoryStageKind.Normal;
        }

        /// <summary>Next stage to fight, or 0 when every stage is cleared.</summary>
        public static int NextStage(PlayerSave save)
        {
            int next = Story(save).cleared + 1;
            return next > TotalStages ? 0 : next;
        }

        /// <summary>Enemies climb two levels every three stages: Lv 5 at 1-1, 11 at 1-10, 18 at 2-10, 24 at 3-10.</summary>
        public static int EnemyLevel(int index) => FirstEnemyLevel + (2 * (index - 1) + 1) / 3;

        public static List<EnemySpec> Enemies(int index, SpireTuning tuning)
        {
            var chapter = Chapter(ChapterOf(index));
            int level = EnemyLevel(index);
            int stage = Math.Min(DragonRules.MaxStage, (level - 1) / 20 + 1);
            double hp = DragonRules.BaseStat(StatType.HP, level, stage) * tuning.normalHp;
            double atk = DragonRules.BaseStat(StatType.ATK, level, stage) * tuning.normalAtk;
            double def = DragonRules.BaseStat(StatType.DEF, level, stage) * tuning.defense;

            var list = new List<EnemySpec>();
            switch (KindOf(index))
            {
                case StoryStageKind.Guardian:
                    list.Add(new EnemySpec
                    {
                        id = chapter.guardian, level = level, element = chapter.element, defense = def,
                        hp = hp * tuning.guardianHp, attack = atk * tuning.guardianAtk,
                        attackIntervalSeconds = 2, heavyIntervalSeconds = tuning.guardianHeavyEvery,
                        heavyMultiplier = tuning.heavyMultiplier,
                    });
                    break;
                case StoryStageKind.Boss:
                    list.Add(new EnemySpec
                    {
                        id = chapter.boss, level = level, element = chapter.element, defense = def,
                        hp = hp * tuning.bossHp, attack = atk * tuning.bossAtk,
                        attackIntervalSeconds = 2, heavyIntervalSeconds = tuning.bossHeavyEvery,
                        heavyMultiplier = tuning.heavyMultiplier + 1, enrageSeconds = tuning.bossEnrageSeconds,
                    });
                    break;
                default:
                    for (int i = 0; i < tuning.packSize; i++)
                        list.Add(new EnemySpec
                        {
                            id = "wild drake", level = level, element = chapter.element, defense = def, hp = hp, attack = atk,
                            attackIntervalSeconds = 2,
                        });
                    break;
            }
            return list;
        }

        /// <summary>First-clear payout for a stage (Balance §14); gear and riders are added by <see cref="ApplyResult"/>.</summary>
        public static StoryReward Rewards(int index)
        {
            var kind = KindOf(index);
            var reward = new StoryReward
            {
                firstClear = true,
                gold = 500 + 150L * index,
                essence = 10 + 2 * index,
                forgeStones = 5 + index,
            };
            if (kind == StoryStageKind.Boss)
            {
                reward.gold *= 3;
                reward.pactSigils = 10;
                reward.crystals = 100;
                reward.riderId = Chapter(ChapterOf(index)).riderReward;
            }
            return reward;
        }

        /// <summary>Guardians drop one item: Uncommon in chapter 1, Rare after; bosses drop one Rare.</summary>
        public static Rarity GearRarity(int index) =>
            KindOf(index) == StoryStageKind.Guardian && ChapterOf(index) == 1 ? Rarity.Uncommon : Rarity.Rare;

        public static BattleSetup BuildBattle(PlayerSave save, int index, int seed, SpireTuning tuning)
        {
            var loadout = GameActions.ToLoadout(save);
            var heart = save.FindEquipment(save.dragon.equipped[(int)EquipSlot.HeartCore]);
            var setup = new BattleSetup
            {
                seed = seed,
                dragonLevel = save.dragon.level,
                dragonElement = GameActions.HeartElement(heart),
                dragon = DragonCombatStats.FromSheet(LoadoutCalculator.Compute(loadout)),
                traits = LoadoutCalculator.BattleTraits(loadout),
                command = loadout.mainRider != null ? RiderCatalog.Command(loadout.mainRider.commandSkillId) : null,
                timeLimitSeconds = KindOf(index) == StoryStageKind.Boss ? BossTimeLimitSeconds : TimeLimitSeconds,
            };
            setup.skills.AddRange(SkillLoadoutRules.Resolve(save.dragon.skillIds));
            setup.enemies.AddRange(Enemies(index, tuning));
            return setup;
        }

        /// <summary>Pays the first clear of the next stage. Losses and any other stage give nothing.</summary>
        public static StoryReward ApplyResult(PlayerSave save, int index, BattleOutcome outcome, IRandom random)
        {
            if (outcome != BattleOutcome.Victory || index != NextStage(save)) return new StoryReward();

            var reward = Rewards(index);
            var w = save.wallet;
            w.gold += reward.gold;
            w.dragonEssence += reward.essence;
            w.forgeStones += reward.forgeStones;
            w.pactSigils += reward.pactSigils;
            w.crystals += reward.crystals;

            if (KindOf(index) != StoryStageKind.Normal)
            {
                reward.gear = EquipmentGenerator.CreateRandom(random, GearRarity(index), save.dragon.stage, "eq" + (++save.nextItemId).ToString("D5"));
                save.equipment.Add(reward.gear);
            }
            if (!string.IsNullOrEmpty(reward.riderId)) reward.riderIsNew = GrantRider(save, reward.riderId);

            Story(save).cleared = index;
            return reward;
        }

        /// <summary>Adds a rider like a summon would: new, a copy for stars, or shards past max stars. True when new.</summary>
        static bool GrantRider(PlayerSave save, string riderId)
        {
            var state = save.FindRider(riderId);
            if (state == null)
            {
                save.riders.Add(new RiderState { riderId = riderId, star = 1 });
                return true;
            }
            if (state.star >= RiderRules.MaxStar) save.wallet.soulShards += RiderRules.ShardsForOverflow(RiderCatalog.Find(riderId).rank);
            else state.copies++;
            return false;
        }

        static StorySave Story(PlayerSave save)
        {
            if (save.story == null) save.story = new StorySave();
            return save.story;
        }
    }
}
