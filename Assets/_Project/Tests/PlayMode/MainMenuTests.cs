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
            BattleHandoff.Pending = null;
            BattleHandoff.LastMessage = null;
        }

        [TearDown]
        public void TearDown()
        {
            if (SaveManager.Instance != null) Object.DestroyImmediate(SaveManager.Instance.gameObject);
            SaveManager.StorageOverride = null;
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

        [UnityTest]
        public IEnumerator HuntScreen_AutoHunt_SpendsStaminaAndPays()
        {
            var menu = new GameObject("Main Menu").AddComponent<MainMenu>();
            yield return null;
            menu.Show(ScreenId.Hunt);
            yield return null;

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
            save.wallet.elementCrystals[0] = 50;
            save.wallet.pathEssence[(int)GrowthPath.Arcane] = 50;
            save.wallet.refineStones = 10;
            var heart = save.FindEquipment(save.dragon.equipped[(int)EquipSlot.HeartCore]);
            heart.subs = new[] { new StatModifier(StatType.SPD, ModType.Flat, 2) };

            menu.Show(ScreenId.Dragon);
            yield return null;
            FindButton("WORKSHOP").onClick.Invoke(); // first equipped row = Heart Core
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
        public IEnumerator HuntBattle_RoundTrip_ReturnsToHunt()
        {
            var menu = new GameObject("Main Menu").AddComponent<MainMenu>();
            yield return null;
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
    }
}
