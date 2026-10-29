using System.Collections.Generic;
using P2D.Domain;
using P2D.Services;
using UnityEngine;

namespace P2D.Presentation.Battle
{
    /// <summary>
    /// Phase 0 greybox: builds a spire battle from the balance-simulator builds, runs it with
    /// BattleRunner and shows it with GreyboxBattleView. Drop it on an empty GameObject in an
    /// empty scene; it creates its own camera, light, world and UI.
    /// </summary>
    public sealed class GreyboxBattleBootstrap : MonoBehaviour
    {
        [SerializeField] string buildName = "B2 Fury";
        [SerializeField, Range(1, 40)] int floor = 17;
        [SerializeField] Element themeElement = Element.Fire;
        [Tooltip("0 = a new seed every battle")]
        [SerializeField] int fixedSeed;

        readonly List<BuildPreset> builds = new List<BuildPreset>();
        readonly Dictionary<BuildPreset, int> brackets = new Dictionary<BuildPreset, int>();
        readonly SpireTuning tuning = new SpireTuning();
        BattleRunner runner;
        GreyboxBattleView view;
        int buildIndex;
        int battleCount;

        public BattleRunner Runner => runner;
        public GreyboxBattleView View => view;
        public BuildPreset CurrentBuild => builds[buildIndex];
        public int Floor => floor;
        public int Bracket => brackets[CurrentBuild];

        void Awake()
        {
            if (BattleHandoff.Pending == null) LanguageSetting.Apply(); // opened directly as a sandbox
            foreach (var scenario in BalanceScenarios.Default())
                foreach (var build in scenario.builds)
                {
                    builds.Add(build);
                    brackets[build] = scenario.bracket;
                }
            buildIndex = Mathf.Max(0, builds.FindIndex(b => b.name == buildName));

            runner = gameObject.AddComponent<BattleRunner>();
            view = gameObject.AddComponent<GreyboxBattleView>();
            view.Initialize(this);
        }

        void Start()
        {
            var request = BattleHandoff.Pending;
            if (request == null)
            {
                StartBattle();
                return;
            }

            // Campaign battle from the menu: play the given setup once, then report and go back.
            request.setup.auto = false;
            runner.Begin(request.setup);
            view.Bind(runner, request.setup, request.title);
            view.SetCampaignMode(() => FinishCampaign(request, runner.Simulation.Outcome), () => FinishCampaign(request, BattleOutcome.Defeat));
        }

        void FinishCampaign(BattleHandoff.Request request, BattleOutcome outcome)
        {
            BattleHandoff.Pending = null;
            if (request.onFinished != null) request.onFinished(outcome, runner.Simulation.AppliedInputs);
            UnityEngine.SceneManagement.SceneManager.LoadScene(request.returnScene);
        }

        public void StartBattle()
        {
            int seed = fixedSeed != 0 ? fixedSeed + battleCount : System.Environment.TickCount + battleCount;
            battleCount++;
            var enemies = SpireEnemyFactory.Floor(Bracket, floor, themeElement, tuning);
            var setup = BuildFactory.ToBattle(CurrentBuild, enemies, seed, SpireEnemyFactory.TimeLimit(floor, tuning));
            setup.auto = false;
            runner.Begin(setup);
            view.Bind(runner, setup);
        }

        public void NextFloor()
        {
            floor = Mathf.Min(40, floor + 1);
            StartBattle();
        }

        public void CycleBuild()
        {
            buildIndex = (buildIndex + 1) % builds.Count;
            StartBattle();
        }
    }
}
