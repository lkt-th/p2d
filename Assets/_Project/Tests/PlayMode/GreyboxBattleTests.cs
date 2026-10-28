using System.Collections;
using NUnit.Framework;
using P2D.Domain;
using P2D.Presentation.Battle;
using UnityEngine;
using UnityEngine.TestTools;

namespace P2D.PlayMode.Tests
{
    public class GreyboxBattleTests
    {
        GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.Destroy(root);
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (go.transform.parent == null && go.name != "Code-based tests runner") Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator Battle_StartsRunsAndShowsResult()
        {
            root = new GameObject("Greybox Battle");
            var bootstrap = root.AddComponent<GreyboxBattleBootstrap>();
            yield return null;
            yield return null;

            Assert.IsNotNull(bootstrap.Runner.Simulation);
            Assert.IsTrue(bootstrap.Runner.IsRunning);
            Assert.IsNotNull(Camera.main);

            // Let it play for a moment in real time, then finish instantly.
            bootstrap.Runner.SetSpeed(3);
            for (int i = 0; i < 30; i++) yield return null;
            bootstrap.Runner.Skip();
            yield return null;

            Assert.AreNotEqual(BattleOutcome.Running, bootstrap.Runner.Simulation.Outcome);
            Assert.IsTrue(bootstrap.View.ResultVisible);
        }

        [UnityTest]
        public IEnumerator RetryAndCycleBuild_RebuildTheBattle()
        {
            root = new GameObject("Greybox Battle");
            var bootstrap = root.AddComponent<GreyboxBattleBootstrap>();
            yield return null;

            string firstBuild = bootstrap.CurrentBuild.name;
            bootstrap.CycleBuild();
            yield return null;
            Assert.AreNotEqual(firstBuild, bootstrap.CurrentBuild.name);
            Assert.IsTrue(bootstrap.Runner.IsRunning);

            bootstrap.Runner.Skip();
            yield return null;
            bootstrap.NextFloor();
            yield return null;
            Assert.AreEqual(18, bootstrap.Floor);
            Assert.IsTrue(bootstrap.Runner.IsRunning);
            Assert.IsFalse(bootstrap.View.ResultVisible);
        }
    }
}
