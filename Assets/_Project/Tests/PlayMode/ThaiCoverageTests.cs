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
    /// <summary>
    /// Renders every screen (locked and unlocked, the workshop, a story scene, summon results and a
    /// battle) in Thai and fails on any English word left on screen, listing each one.
    /// </summary>
    public class ThaiCoverageTests
    {
        // Words that stay in English in the Thai UI: stat abbreviations, ranks, the store name and the
        // language button. Everything else on screen must be Thai.
        static readonly HashSet<string> Allowed = new HashSet<string>
        {
            "HP", "ATK", "DEF", "SPD", "CRIT", "Lv", "EXP", "Stage", "Path", "Tier", "SR", "SSR", "UR",
            "Dragon", "Pass", "Unity", "IAP", "greybox", "English", "Ignis",
        };

        static readonly Regex Tags = new Regex("<[^>]+>");
        static readonly Regex Words = new Regex("[A-Za-z]{2,}");

        readonly SortedSet<string> offenders = new SortedSet<string>();

        [SetUp]
        public void SetUp()
        {
            SaveManager.StorageOverride = new MemorySaveStorage();
            LanguageSetting.Override = Language.Thai;
            BattleHandoff.Pending = null;
            BattleHandoff.LastMessage = null;
            offenders.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            if (SaveManager.Instance != null) Object.DestroyImmediate(SaveManager.Instance.gameObject);
            SaveManager.StorageOverride = null;
            LanguageSetting.Override = null;
            SpireAffixes.Live = false;
            Loc.Current = Language.English;
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (go.transform.parent == null && go.name != "Code-based tests runner") Object.Destroy(go);
        }

        void Collect(string where)
        {
            foreach (var text in Object.FindObjectsByType<Text>(FindObjectsSortMode.None))
            {
                if (!text.gameObject.activeInHierarchy || string.IsNullOrEmpty(text.text)) continue;
                string plain = Tags.Replace(text.text, "");
                foreach (Match m in Words.Matches(plain))
                    if (!Allowed.Contains(m.Value)) offenders.Add(where + ": \"" + plain.Replace("\n", " / ") + "\"");
            }
        }

        static Button FindButton(string labelStart)
        {
            foreach (var b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
                if (b.gameObject.activeInHierarchy && b.GetComponentInChildren<Text>().text.StartsWith(labelStart)) return b;
            return null;
        }

        [UnityTest]
        public IEnumerator EveryScreen_IsThai()
        {
            var menu = new GameObject("Main Menu").AddComponent<MainMenu>();
            yield return null;
            Assert.AreEqual(Language.Thai, Loc.Current);
            Collect("hatch");
            for (int i = 0; i < GameActions.HatchTaps; i++)
            {
                foreach (var b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
                    if (b.transform.parent != null && b.transform.parent.name == "Egg") { b.onClick.Invoke(); break; }
                yield return null;
            }
            Collect("hatch name");
            FindButton(Loc.T("HATCH")).onClick.Invoke();
            yield return null;

            // A fresh game: locked cards, the 1-1 scene.
            foreach (ScreenId id in System.Enum.GetValues(typeof(ScreenId)))
            {
                menu.Show(id);
                yield return null;
                Collect("locked " + id);
            }
            menu.Show(ScreenId.Story);
            yield return null;
            FindButton(Loc.F("FIGHT STAGE {0}", "1-1")).onClick.Invoke();
            yield return null;
            Collect("scene 1-1");

            // Everything open, with things to show: a sub-stat, summon results, a hunt.
            var save = menu.Save;
            save.story.cleared = 20;
            save.spire.highestFloor = 20;       // sweep rows, boss claims
            save.spire.pendingChestFloor = 25;  // the weekly chest row
            save.spire.catchUpFloors = 10;      // the catch-up row
            SpireAffixes.Live = true;           // the affix row (off in the live game for now)
            PresetActions.Store(save, 0);       // a filled preset next to empty ones
            save.wallet.pactSigils = 30;
            save.wallet.refineStones = 10;
            var heart = save.FindEquipment(save.dragon.equipped[(int)EquipSlot.HeartCore]);
            heart.subs = new[] { new StatModifier(StatType.CritRate, ModType.Percent, 0.03) };
            foreach (ScreenId id in System.Enum.GetValues(typeof(ScreenId)))
            {
                menu.Show(id);
                yield return null;
                Collect("open " + id);
            }
            menu.Show(ScreenId.Dragon);
            yield return null;
            FindButton(Loc.T("WORKSHOP")).onClick.Invoke();
            yield return null;
            Collect("workshop");
            menu.Show(ScreenId.Summon);
            yield return null;
            FindButton(Loc.T("SUMMON x10")).onClick.Invoke();
            yield return null;
            Collect("summon results");
            menu.Show(ScreenId.Hunt);
            yield return null;
            save.dragon.level = 20;
            FindButton(Loc.T("AUTO HUNT x1")).onClick.Invoke();
            yield return null;
            Collect("hunt results");

            // The battle HUD and its result screen.
            save.story.cleared = 0;
            menu.StartStoryBattle(1);
            GreyboxBattleBootstrap battle = null;
            for (int i = 0; i < 120 && battle == null; i++)
            {
                yield return null;
                battle = Object.FindAnyObjectByType<GreyboxBattleBootstrap>();
            }
            Assert.IsNotNull(battle);
            for (int i = 0; i < 30; i++) yield return null;
            Collect("battle");
            battle.Runner.SetAuto(true);
            yield return null;
            Collect("battle auto");
            battle.Runner.Skip();
            yield return null;
            Collect("battle result");

            Assert.IsEmpty(offenders, "English left on screen:\n" + string.Join("\n", offenders));
        }
    }
}
