using System.Collections;
using NUnit.Framework;
using P2D.Domain;
using P2D.Presentation.Battle;
using P2D.Presentation.Screens;
using P2D.Services;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace P2D.PlayMode.Tests
{
    public class MainMenuTests
    {
        [SetUp]
        public void SetUp()
        {
            SaveManager.StorageOverride = new MemorySaveStorage(); // never touch the real save
            P2D.Services.LanguageSetting.Override = P2D.Domain.Language.English; // tests find buttons by their English labels
            BattleHandoff.Pending = null;
            BattleHandoff.LastMessage = null;
        }

        [TearDown]
        public void TearDown()
        {
            if (SaveManager.Instance != null) Object.DestroyImmediate(SaveManager.Instance.gameObject);
            SaveManager.StorageOverride = null;
            P2D.Services.LanguageSetting.Override = null;
            P2D.Domain.Loc.Current = P2D.Domain.Language.English;
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (go.transform.parent == null && go.name != "Code-based tests runner") Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator Menu_StartsNewGame_AndEveryScreenRenders()
        {
            var menu = new GameObject("Main Menu").AddComponent<MainMenu>();
            yield return null;

            Assert.IsNotNull(menu.Save);
            Assert.AreEqual(3, menu.Save.equipment.Count, "starter kit");
            foreach (ScreenId id in System.Enum.GetValues(typeof(ScreenId)))
            {
                menu.Show(id);
                yield return null;
                Assert.AreEqual(id, menu.Current);
            }
        }

        [UnityTest]
        public IEnumerator DragonScreen_FeedButton_LevelsUp()
        {
            var menu = new GameObject("Main Menu").AddComponent<MainMenu>();
            yield return null;
            menu.Show(ScreenId.Dragon);
            yield return null;

            long essence = menu.Save.wallet.dragonEssence;
            Button feed = null;
            foreach (var b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
                if (b.GetComponentInChildren<Text>().text.StartsWith("FEED")) feed = b;
            Assert.IsNotNull(feed);
            feed.onClick.Invoke();
            yield return null;
            // 10 essence = 1,000 EXP; Lv 10 → 11 needs 3,981, so EXP rises without a level-up
            Assert.AreEqual(essence - 10, menu.Save.wallet.dragonEssence);
            Assert.AreEqual(10 * GameActions.ExpPerEssence, menu.Save.dragon.exp);
        }

        static Button FindButton(string labelStart)
        {
            foreach (var b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
                if (b.gameObject.activeInHierarchy && b.GetComponentInChildren<Text>().text.StartsWith(labelStart)) return b;
            return null;
        }

        /// <summary>The button labelled <paramref name="label"/> in the row whose text mentions <paramref name="rowText"/>.</summary>
        static Button RowButton(string rowText, string label)
        {
            foreach (var b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                if (!b.gameObject.activeInHierarchy || !b.GetComponentInChildren<Text>().text.StartsWith(label)) continue;
                var row = b.transform.parent.parent;
                foreach (var t in row.GetComponentsInChildren<Text>())
                    if (t.text.Contains(rowText)) return b;
            }
            return null;
        }

        [UnityTest]
        public IEnumerator HuntScreen_AutoHunt_SpendsStaminaAndPays()
        {
            var menu = new GameObject("Main Menu").AddComponent<MainMenu>();
            yield return null;
            menu.Save.story.cleared = StoryActions.TotalStages;
            menu.Show(ScreenId.Hunt);
            yield return null;

            menu.Save.dragon.level = 20; // keep the outcome out of the test: a Lv 20 dragon never loses zone 1
            int stamina = menu.Save.hunt.stamina;
            long gold = menu.Save.wallet.gold;
            var hunt = FindButton("AUTO HUNT x1");
            Assert.IsNotNull(hunt);
            hunt.onClick.Invoke();
            yield return null;
            Assert.AreEqual(stamina - HuntRules.StaminaCost, menu.Save.hunt.stamina);
            Assert.Greater(menu.Save.wallet.gold, gold);
        }

        [UnityTest]
        public IEnumerator DragonWorkshop_ReforgeAndRefine()
        {
            var menu = new GameObject("Main Menu").AddComponent<MainMenu>();
            yield return null;
            var save = menu.Save;
            save.story.cleared = StoryActions.TotalStages;
            save.wallet.elementCrystals[0] = 50;
            save.wallet.pathEssence[(int)GrowthPath.Arcane] = 50;
            save.wallet.refineStones = 10;
            var heart = save.FindEquipment(save.dragon.equipped[(int)EquipSlot.HeartCore]);
            heart.subs = new[] { new StatModifier(StatType.SPD, ModType.Flat, 2) };

            menu.Show(ScreenId.Dragon);
            yield return null;
            RowButton("HeartCore", "WORKSHOP").onClick.Invoke();
            yield return null;

            FindButton("Arcane").onClick.Invoke();
            yield return null;
            Assert.AreEqual(GrowthPath.Arcane, heart.path);

            int stones = save.wallet.refineStones;
            FindButton("REROLL").onClick.Invoke();
            yield return null;
            Assert.Less(save.wallet.refineStones, stones);
        }

        [UnityTest]
        public IEnumerator ShopScreen_StaminaRefill_UsesCrystals()
        {
            var menu = new GameObject("Main Menu").AddComponent<MainMenu>();
            yield return null;
            menu.Show(ScreenId.Shop);
            yield return null;

            long crystals = menu.Save.wallet.crystals;
            var refill = FindButton(ShopActions.StaminaRefillCrystals + " crystals");
            Assert.IsNotNull(refill);
            refill.onClick.Invoke();
            yield return null;
            Assert.AreEqual(crystals - ShopActions.StaminaRefillCrystals, menu.Save.wallet.crystals);
        }

        [UnityTest]
        public IEnumerator NestScreen_CollectAndUpgrade()
        {
            var menu = new GameObject("Main Menu").AddComponent<MainMenu>();
            yield return null;
            var save = menu.Save;
            save.nest.lastCollectUtcTicks = ServerClock.UtcNow.AddHours(-3).Ticks;
            save.wallet.gold = 0;
            menu.Show(ScreenId.Nest);
            yield return null;

            FindButton("COLLECT").onClick.Invoke();
            yield return null;
            Assert.GreaterOrEqual(save.wallet.gold, 3 * NestActions.GoldPerHour(1));
            Assert.Less(save.wallet.gold, 4 * NestActions.GoldPerHour(1));

            save.wallet.gold = NestActions.UpgradeCost(1);
            menu.Redraw();
            yield return null;
            FindButton("UPGRADE").onClick.Invoke();
            yield return null;
            Assert.AreEqual(2, save.nest.level);
            Assert.IsNull(FindButton("UPGRADE"), "stage 1 caps the nest at Lv 2");
        }

        /// <summary>The egg button: the one inside the "Egg" rect of the hatch screen.</summary>
        static Button EggButton()
        {
            foreach (var b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
                if (b.gameObject.activeInHierarchy && b.transform.parent != null && b.transform.parent.name == "Egg") return b;
            return null;
        }

        [UnityTest]
        public IEnumerator NewGame_HatchesTheEgg_ThenOpensTheStory()
        {
            var menu = new GameObject("Main Menu").AddComponent<MainMenu>();
            yield return null;
            Assert.AreEqual(ScreenId.Hatch, menu.Current, "a new game starts at the egg");
            Assert.IsNull(FindButton("ADVENTURE"), "no tabs before hatching");

            for (int i = 0; i < GameActions.HatchTaps; i++)
            {
                EggButton().onClick.Invoke();
                yield return null;
            }
            var input = Object.FindAnyObjectByType<InputField>();
            Assert.IsNotNull(input, "the name field appears once the egg cracks");
            input.text = "Pyra";
            FindButton("HATCH").onClick.Invoke();
            yield return null;

            Assert.IsTrue(menu.Save.dragon.hatched);
            Assert.AreEqual("Pyra", menu.Save.dragon.name);
            Assert.AreEqual(ScreenId.Story, menu.Current);
            Assert.IsNotNull(FindButton("ADVENTURE"));
        }

        [UnityTest]
        public IEnumerator StoryBattle_RoundTrip_ClearsStageOne()
        {
            var menu = new GameObject("Main Menu").AddComponent<MainMenu>();
            yield return null;
            menu.Save.dragon.hatched = true;
            menu.Show(ScreenId.Story);
            yield return null;
            FindButton("FIGHT STAGE 1-1").onClick.Invoke();
            yield return null;
            Assert.IsNotNull(FindButton("NEXT"), "1-1 opens with a scene");
            FindButton("NEXT").onClick.Invoke();
            yield return null;
            FindButton("SKIP").onClick.Invoke();

            GreyboxBattleBootstrap battle = null;
            for (int i = 0; i < 120 && battle == null; i++)
            {
                yield return null;
                battle = Object.FindAnyObjectByType<GreyboxBattleBootstrap>();
            }
            Assert.IsNotNull(battle);
            yield return null;
            battle.Runner.SetAuto(true);
            yield return null;
            battle.Runner.Skip();
            yield return null;
            Assert.AreEqual(BattleOutcome.Victory, battle.Runner.Simulation.Outcome);
            foreach (var b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
                if (b.gameObject.activeInHierarchy && b.name == "Continue") b.onClick.Invoke();

            MainMenu back = null;
            for (int i = 0; i < 120 && back == null; i++)
            {
                yield return null;
                back = Object.FindAnyObjectByType<MainMenu>();
            }
            Assert.IsNotNull(back);
            yield return null;
            Assert.AreEqual(ScreenId.Story, back.Current);
            Assert.AreEqual(1, SaveManager.Instance.Current.story.cleared);
            Assert.IsNotNull(FindButton("FIGHT STAGE 1-2"));
            Assert.AreEqual(1, SaveManager.Instance.Current.story.dialogueSeen, "the scene is not shown again");
        }

        [UnityTest]
        public IEnumerator SpireScreen_SweepAndChest()
        {
            var menu = new GameObject("Main Menu").AddComponent<MainMenu>();
            yield return null;
            var save = menu.Save;
            save.dragon.hatched = true;
            save.story.cleared = StoryActions.TotalStages;
            save.spire.highestFloor = 12;
            save.spire.pendingChestFloor = 12;
            menu.Show(ScreenId.Spire);
            yield return null;

            int torches = save.spire.torches;
            long gold = save.wallet.gold;
            RowButton("Floor 12", "SWEEP").onClick.Invoke();
            yield return null;
            Assert.AreEqual(torches - 1, save.spire.torches);
            Assert.Greater(save.wallet.gold, gold);

            int sigils = save.wallet.pactSigils;
            FindButton("OPEN").onClick.Invoke();
            yield return null;
            Assert.AreEqual(0, save.spire.pendingChestFloor);
            Assert.AreEqual(sigils + 2, save.wallet.pactSigils, "silver chest");
            Assert.IsNull(FindButton("OPEN"));
        }

        [UnityTest]
        public IEnumerator DragonScreen_PresetSaveAndUse()
        {
            var menu = new GameObject("Main Menu").AddComponent<MainMenu>();
            yield return null;
            var save = menu.Save;
            save.dragon.hatched = true;
            save.story.cleared = StoryActions.TotalStages;
            string claw = save.dragon.equipped[(int)EquipSlot.Claw];
            menu.Show(ScreenId.Dragon);
            yield return null;

            RowButton("Set 1", "SAVE").onClick.Invoke();
            yield return null;
            var other = EquipmentGenerator.Create(new SeededRandom(1), Rarity.Rare, EquipSlot.Claw, GrowthPath.Arcane, "eq90010");
            save.equipment.Add(other);
            GameActions.Equip(save, other.uid);
            menu.Redraw();
            yield return null;

            RowButton("Set 1", "USE").onClick.Invoke();
            yield return null;
            Assert.AreEqual(claw, save.dragon.equipped[(int)EquipSlot.Claw]);
        }

        [UnityTest]
        public IEnumerator HuntBattle_RoundTrip_ReturnsToHunt()
        {
            var menu = new GameObject("Main Menu").AddComponent<MainMenu>();
            yield return null;
            menu.Save.dragon.hatched = true;
            menu.Save.story.cleared = StoryActions.TotalStages;
            menu.Show(ScreenId.Hunt);
            yield return null;
            FindButton("FIGHT").onClick.Invoke();

            GreyboxBattleBootstrap battle = null;
            for (int i = 0; i < 120 && battle == null; i++)
            {
                yield return null;
                battle = Object.FindAnyObjectByType<GreyboxBattleBootstrap>();
            }
            Assert.IsNotNull(battle);
            yield return null;
            battle.Runner.SetAuto(true);
            yield return null;
            battle.Runner.Skip();
            yield return null;
            foreach (var b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
                if (b.gameObject.activeInHierarchy && b.name == "Continue") b.onClick.Invoke();

            MainMenu back = null;
            for (int i = 0; i < 120 && back == null; i++)
            {
                yield return null;
                back = Object.FindAnyObjectByType<MainMenu>();
            }
            Assert.IsNotNull(back);
            yield return null;
            Assert.AreEqual(ScreenId.Hunt, back.Current);
            Assert.AreEqual(HuntRules.MaxStamina - HuntRules.StaminaCost, SaveManager.Instance.Current.hunt.stamina);
        }

        [UnityTest]
        public IEnumerator SpireBattle_RoundTrip_SavesTheClear()
        {
            var menu = new GameObject("Main Menu").AddComponent<MainMenu>();
            yield return null;
            menu.Save.dragon.hatched = true;
            menu.Save.story.cleared = StoryActions.TotalStages;
            menu.StartSpireBattle(1);

            GreyboxBattleBootstrap battle = null;
            for (int i = 0; i < 120 && battle == null; i++)
            {
                yield return null;
                battle = Object.FindAnyObjectByType<GreyboxBattleBootstrap>();
            }
            Assert.IsNotNull(battle, "battle scene loaded");
            yield return null;
            Assert.IsTrue(battle.Runner.IsRunning);

            battle.Runner.SetAuto(true);
            yield return null;
            battle.Runner.Skip();
            yield return null;
            Assert.AreEqual(BattleOutcome.Victory, battle.Runner.Simulation.Outcome);

            Button resume = null;
            foreach (var b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
                if (b.gameObject.activeInHierarchy && b.name == "Continue") resume = b;
            Assert.IsNotNull(resume);
            resume.onClick.Invoke();

            MainMenu back = null;
            for (int i = 0; i < 120 && back == null; i++)
            {
                yield return null;
                back = Object.FindAnyObjectByType<MainMenu>();
            }
            Assert.IsNotNull(back, "menu scene loaded again");
            yield return null;
            Assert.AreEqual(1, SaveManager.Instance.Current.spire.highestFloor);
            Assert.AreEqual(4, SaveManager.Instance.Current.equipment.Count, "first-clear item");
            Assert.AreEqual(ScreenId.Spire, back.Current);
            Assert.AreEqual(SceneManager.GetActiveScene().name, BattleHandoff.MenuScene);
        }

        [UnityTest]
        public IEnumerator MissionsScreen_FeedThenClaim()
        {
            var menu = new GameObject("Main Menu").AddComponent<MainMenu>();
            yield return null;
            var save = menu.Save;
            save.story.cleared = StoryActions.TotalStages;
            menu.Show(ScreenId.Dragon);
            yield return null;
            FindButton("FEED").onClick.Invoke(); // records the daily "feed the dragon" mission
            yield return null;

            menu.Show(ScreenId.Nest);
            yield return null;
            FindButton("MISSIONS").onClick.Invoke();
            yield return null;
            Assert.AreEqual(ScreenId.Missions, menu.Current);

            long crystals = save.wallet.crystals;
            var claim = RowButton("Feed the dragon", "CLAIM");
            Assert.IsNotNull(claim);
            Assert.IsTrue(claim.interactable);
            claim.onClick.Invoke();
            yield return null;
            Assert.AreEqual(crystals + 10, save.wallet.crystals);
            Assert.IsNotNull(RowButton("Feed the dragon", "CLAIMED"), "the row shows it was claimed");
            Assert.IsFalse(RowButton("Feed the dragon", "CLAIMED").interactable);
        }
    }
}
