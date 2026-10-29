using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using P2D.Domain;
using P2D.Presentation.Battle;
using P2D.Presentation.Screens;
using P2D.Services;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace P2D.PlayMode.Tests
{
    /// <summary>Path visuals (DragonLook), the dragon in battle and on the Nest, and the evolution cutscene.</summary>
    public class DragonLookTests
    {
        [SetUp]
        public void SetUp()
        {
            SaveManager.StorageOverride = new MemorySaveStorage();
            LanguageSetting.Override = Language.English;
            BattleHandoff.Pending = null;
            BattleHandoff.LastMessage = null;
        }

        [TearDown]
        public void TearDown()
        {
            if (SaveManager.Instance != null) Object.DestroyImmediate(SaveManager.Instance.gameObject);
            SaveManager.StorageOverride = null;
            LanguageSetting.Override = null;
            Loc.Current = Language.English;
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (go.transform.parent == null && go.name != "Code-based tests runner") Object.Destroy(go);
        }

        static Dictionary<GrowthPath, int> Affinity(int fury, int bastion, int tempest, int arcane) => new Dictionary<GrowthPath, int>
        {
            [GrowthPath.Fury] = fury, [GrowthPath.Bastion] = bastion, [GrowthPath.Tempest] = tempest, [GrowthPath.Arcane] = arcane,
        };

        [Test]
        public void Look_FollowsTheDominantPath()
        {
            var look = DragonLook.For(3, Affinity(10, 0, 70, 30));
            Assert.AreEqual(GrowthPath.Tempest, look.path);
            Assert.AreEqual(AffinityRules.Tier(70), look.tier);
            Assert.Greater(look.wingLength, 1f, "Tempest has longer wings");

            var bastion = DragonLook.For(3, Affinity(0, 90, 0, 0));
            Assert.IsTrue(bastion.backPlate);
            Assert.Greater(bastion.bulk, 1f);
            Assert.IsTrue(DragonLook.For(3, Affinity(0, 0, 0, 25)).orb, "Arcane carries an orb");
            Assert.Greater(DragonLook.For(3, Affinity(25, 0, 0, 0)).clawSize, 1f, "Fury has bigger claws");
            Assert.IsTrue(DragonLook.For(3, Affinity(0, 0, 0, 0)).plain, "no gear: plain skin");
        }

        [Test]
        public void Look_PartsAppearWithStage_AndSizeGrows()
        {
            var aff = Affinity(80, 0, 0, 0);
            var looks = new DragonLook[DragonRules.MaxStage + 1];
            for (int stage = 1; stage <= DragonRules.MaxStage; stage++)
            {
                looks[stage] = DragonLook.For(stage, aff);
                Assert.AreEqual(DragonRules.IsSlotUnlocked(EquipSlot.Horn, stage), looks[stage].horns, "horns at stage " + stage);
                Assert.AreEqual(DragonRules.IsSlotUnlocked(EquipSlot.Wing, stage), looks[stage].wings, "wings at stage " + stage);
                Assert.AreEqual(DragonRules.IsSlotUnlocked(EquipSlot.Tail, stage), looks[stage].tail, "tail at stage " + stage);
                if (stage > 1) Assert.Greater(looks[stage].scale, looks[stage - 1].scale);
            }
            Assert.IsFalse(looks[1].horns || looks[1].wings || looks[1].tail, "a hatchling has none of them");
            Assert.IsTrue(looks[4].horns && looks[4].wings && looks[4].tail);
        }

        [Test]
        public void Look_HigherTierAddsAccents()
        {
            var t1 = DragonLook.For(2, GrowthPath.Fury, 1);
            var t2 = DragonLook.For(2, GrowthPath.Fury, 2);
            var t3 = DragonLook.For(2, GrowthPath.Fury, 3);
            Assert.IsFalse(t1.crest || t1.glow);
            Assert.IsTrue(t2.crest && !t2.glow);
            Assert.IsTrue(t3.crest && t3.glow);
            Assert.AreEqual(GrowthPath.Fury, t3.AtStage(4).path);
            Assert.AreEqual(4, t3.AtStage(4).stage);
        }

        [UnityTest]
        public IEnumerator Battle_DragonShowsTheBuildsPath()
        {
            var root = new GameObject("Greybox Battle");
            var bootstrap = root.AddComponent<GreyboxBattleBootstrap>();
            yield return null;
            while (bootstrap.CurrentBuild.name != "B4 Bastion") bootstrap.CycleBuild();
            yield return null;
            yield return null;
            Assert.IsNotNull(GameObject.Find("Back Plate"), "Bastion plate");
            Assert.IsNotNull(GameObject.Find("Tail"), "stage 4 has a tail");
            Assert.IsNull(GameObject.Find("Orb"));

            while (bootstrap.CurrentBuild.name != "B2 Arcane") bootstrap.CycleBuild();
            yield return null;
            yield return null;
            Assert.IsNotNull(GameObject.Find("Orb"), "Arcane orb");
            Assert.IsNull(GameObject.Find("Back Plate"));
            Assert.IsNull(GameObject.Find("Wing L"), "stage 2 has no wings yet");
        }

        [UnityTest]
        public IEnumerator Nest_ShowsTheDragonPortrait()
        {
            var menu = new GameObject("Main Menu").AddComponent<MainMenu>();
            yield return null;
            menu.Save.dragon.hatched = true;
            menu.Show(ScreenId.Nest);
            yield return null;
            Assert.IsNotNull(GameObject.Find("Dragon Portrait"));
        }

        static Button FindButton(string labelStart)
        {
            foreach (var b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
                if (b.gameObject.activeInHierarchy && b.GetComponentInChildren<Text>().text.StartsWith(labelStart)) return b;
            return null;
        }

        static MainMenu ReadyToEvolve(MainMenu menu)
        {
            var save = menu.Save;
            save.dragon.hatched = true;
            save.story.cleared = StoryActions.TotalStages;
            save.dragon.level = DragonRules.LevelCap(1);
            save.dragon.stage = 1;
            save.wallet.ancientScales = DragonRules.EvolveCostFrom(1).ancientScales;
            save.wallet.gold = DragonRules.EvolveCostFrom(1).gold;
            return menu;
        }

        [UnityTest]
        public IEnumerator EvolveButton_PlaysCutscene_AndSkipClosesIt()
        {
            var menu = ReadyToEvolve(new GameObject("Main Menu").AddComponent<MainMenu>());
            yield return null;
            menu.Show(ScreenId.Dragon);
            yield return null;

            var evolve = FindButton("EVOLVE");
            Assert.IsNotNull(evolve);
            Assert.IsTrue(evolve.interactable);
            evolve.onClick.Invoke();
            yield return null;

            Assert.AreEqual(2, menu.Save.dragon.stage);
            var cutscene = EvolutionCutscene.Current;
            Assert.IsNotNull(cutscene, "the cutscene opens");
            bool stagesShown = false;
            foreach (var t in cutscene.GetComponentsInChildren<Text>())
                if (t.text.Contains("Hatchling") && t.text.Contains("Drake")) stagesShown = true;
            Assert.IsTrue(stagesShown, "Hatchling → Drake");

            cutscene.SkipButton.onClick.Invoke();
            yield return null;
            Assert.IsNull(EvolutionCutscene.Current);
            Assert.IsNull(Object.FindFirstObjectByType<EvolutionCutscene>(), "SKIP closes it");
        }

        [UnityTest]
        public IEnumerator Cutscene_RevealsTheNewForm_ThenClosesByItself()
        {
            var menu = ReadyToEvolve(new GameObject("Main Menu").AddComponent<MainMenu>());
            yield return null;
            menu.Commit(GameActions.Evolve(menu.Save));
            var cutscene = EvolutionCutscene.Play(menu, 1, 2);
            float start = Time.unscaledTime;
            while (!cutscene.Swapped && Time.unscaledTime - start < 5f) yield return null;
            Assert.IsTrue(cutscene.Swapped, "the new form appears");
            Assert.IsNotNull(GameObject.Find("Horn"), "a drake grows horns");
            while (EvolutionCutscene.Current != null && Time.unscaledTime - start < EvolutionCutscene.Duration + 2f) yield return null;
            Assert.IsNull(EvolutionCutscene.Current, "closes after " + EvolutionCutscene.Duration + " s");
        }

        [UnityTest]
        public IEnumerator Cutscene_IsThai()
        {
            LanguageSetting.Override = Language.Thai;
            var menu = ReadyToEvolve(new GameObject("Main Menu").AddComponent<MainMenu>());
            yield return null;
            menu.Save.dragon.name = "ไฟ";
            menu.Commit(GameActions.Evolve(menu.Save));
            var cutscene = EvolutionCutscene.Play(menu, 1, 2);
            float start = Time.unscaledTime;
            while (!cutscene.Swapped && Time.unscaledTime - start < 5f) yield return null;

            var words = new Regex("[A-Za-z]{2,}");
            foreach (var t in cutscene.GetComponentsInChildren<Text>())
                Assert.IsFalse(words.IsMatch(t.text), "English left in the cutscene: " + t.text);
        }
    }
}
