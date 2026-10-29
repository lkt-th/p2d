using System.Collections.Generic;
using P2D.Domain;
using P2D.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace P2D.Presentation.Battle
{
    /// <summary>
    /// Greybox look for a battle: primitive dragon and enemies on a tilted 3D stage, and a
    /// portrait uGUI HUD (HP, gauge, skill buttons, dodge). It only reads the simulation and
    /// sends inputs through BattleRunner. Controls: tap an enemy to target, swipe sideways or
    /// press Dodge to dodge; in the editor 1/2/3 = skills, Q = command, Space = dodge.
    /// </summary>
    public sealed class GreyboxBattleView : MonoBehaviour
    {
        const float SwipeMinPixels = 120f;

        sealed class EnemyView
        {
            public Transform root;
            public Renderer body;
            public Color baseColor;
            public RectTransform barRoot;
            public RectTransform barFill;
            public Text label;
            public float punch;
        }

        sealed class Floater
        {
            public RectTransform rect;
            public Text text;
            public float age;
        }

        GreyboxBattleBootstrap bootstrap;
        BattleRunner runner;
        BattleSetup setup;
        Camera cam;
        Font font;
        Material baseMaterial;

        Transform world;
        Transform enemyRoot;
        Transform dragon;
        DragonFigure dragonFigure;
        Color dragonColor;
        Transform targetRing;
        Renderer warningDisc;
        float dragonFlash;
        float dragonLunge;

        RectTransform canvasRect;
        RectTransform floatLayer;
        Text titleText;
        Text timerText;
        Text autoText;
        Text speedText;
        Text dodgeHint;
        RectTransform hpFill;
        RectTransform shieldFill;
        Text hpText;
        RectTransform gaugeFill;
        readonly List<Button> skillButtons = new List<Button>();
        readonly List<Text> skillTexts = new List<Text>();
        Button commandButton;
        Text commandText;
        GameObject resultPanel;
        Button buildButton;
        Button retryButton;
        Button nextButton;
        Button continueButton;
        Text resultText;

        readonly List<EnemyView> enemies = new List<EnemyView>();
        readonly List<Floater> floaters = new List<Floater>();
        Vector2 pointerDown;
        bool pointerTracking;

        public bool ResultVisible => resultPanel != null && resultPanel.activeSelf;

        public void Initialize(GreyboxBattleBootstrap owner)
        {
            bootstrap = owner;
            font = UiKit.Font;
            BuildWorld();
            BuildHud();
        }

        /// <summary>
        /// Campaign battles (started from a menu): the BUILD button becomes LEAVE and the result
        /// screen offers a single CONTINUE that returns to the menu.
        /// </summary>
        public void SetCampaignMode(UnityEngine.Events.UnityAction onContinue, UnityEngine.Events.UnityAction onLeave)
        {
            buildButton.onClick.RemoveAllListeners();
            buildButton.onClick.AddListener(onLeave);
            buildButton.GetComponentInChildren<Text>().text = Loc.T("LEAVE");
            retryButton.gameObject.SetActive(false);
            nextButton.gameObject.SetActive(false);
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(onContinue);
            continueButton.gameObject.SetActive(true);
        }

        /// <summary>How the dragon looks in the next <see cref="Bind"/> (stage + dominant path); visual only.</summary>
        public DragonLook Look { get; set; }

        public void Bind(BattleRunner battleRunner, BattleSetup battleSetup) =>
            Bind(battleRunner, battleSetup, Loc.F("Floor {0}", bootstrap.Floor) + "  ·  " + bootstrap.CurrentBuild.name);

        public void Bind(BattleRunner battleRunner, BattleSetup battleSetup, string title)
        {
            if (runner != null)
            {
                runner.EventRaised -= OnBattleEvent;
                runner.Finished -= OnFinished;
            }
            runner = battleRunner;
            setup = battleSetup;
            runner.EventRaised += OnBattleEvent;
            runner.Finished += OnFinished;

            if (dragonFigure != null) dragonFigure.Destroy();
            dragonFigure = DragonFigure.Build(dragon, Look ?? DragonLook.Plain(1), baseMaterial, true);
            dragonColor = dragonFigure.Look.body;
            BuildEnemies();
            BuildSkillButtons();
            titleText.text = title;
            resultPanel.SetActive(false);
            foreach (var f in floaters) Destroy(f.rect.gameObject);
            floaters.Clear();
        }

        // ---- world ---------------------------------------------------------------------

        void BuildWorld()
        {
            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseMaterial = probe.GetComponent<Renderer>().sharedMaterial;
            Destroy(probe);

            world = new GameObject("World").transform;

            var camGo = new GameObject("Battle Camera");
            camGo.transform.SetParent(world);
            cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.06f, 0.06f);
            cam.fieldOfView = 55;
            cam.transform.position = new Vector3(0, 9.5f, -9.5f);
            cam.transform.LookAt(new Vector3(0, 0, 1.2f));
            camGo.tag = "MainCamera";

            var lightGo = new GameObject("Sun");
            lightGo.transform.SetParent(world);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.9f, 0.8f);
            lightGo.transform.rotation = Quaternion.Euler(50, -30, 0);

            Primitive(PrimitiveType.Plane, world, "Ground", new Vector3(0, 0, 1), new Vector3(1.4f, 1, 1.8f), new Color(0.18f, 0.12f, 0.1f));

            dragon = new GameObject("Dragon").transform;
            dragon.SetParent(world);
            dragon.position = new Vector3(0, 0, -2.6f);
            // The dragon's parts come from DragonLook (stage + dominant path) and are built in Bind.

            warningDisc = Primitive(PrimitiveType.Cylinder, world, "Telegraph", new Vector3(0, 0.02f, -2.6f), new Vector3(3.2f, 0.01f, 3.2f), new Color(0.9f, 0.1f, 0.1f));
            warningDisc.gameObject.SetActive(false);

            targetRing = Primitive(PrimitiveType.Cylinder, world, "Target Ring", Vector3.zero, new Vector3(1.6f, 0.01f, 1.6f), new Color(1f, 0.6f, 0.2f)).transform;

            enemyRoot = new GameObject("Enemies").transform;
            enemyRoot.SetParent(world);
        }

        Renderer Primitive(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.material = new Material(baseMaterial) { color = color };
            return r;
        }

        void BuildEnemies()
        {
            foreach (var e in enemies)
            {
                Destroy(e.root.gameObject);
                Destroy(e.barRoot.gameObject);
            }
            enemies.Clear();

            var sim = runner.Simulation;
            int count = sim.Enemies.Count;
            for (int i = 0; i < count; i++)
            {
                var spec = sim.Enemies[i].spec;
                bool big = spec.id == "boss" || spec.id == "guardian";
                float x = count == 1 ? 0 : Mathf.Lerp(-2.6f, 2.6f, i / (float)(count - 1));
                var root = new GameObject("Enemy " + i).transform;
                root.SetParent(enemyRoot);
                root.position = new Vector3(x, 0, 3.6f);
                float size = spec.id == "boss" ? 2.4f : big ? 1.8f : 1.1f;
                var color = ElementColor(spec.element);
                var body = Primitive(PrimitiveType.Sphere, root, "Body", new Vector3(0, size * 0.55f, 0), Vector3.one * size, color);
                Primitive(PrimitiveType.Cube, root, "Horn", new Vector3(0, size * 1.1f, 0), new Vector3(0.2f, 0.5f, 0.2f) * size, color * 0.6f);

                var bar = MakeBar(floatLayer, "Enemy HP " + i, new Vector2(170, 18), new Color(0.15f, 0.05f, 0.05f), new Color(0.9f, 0.25f, 0.2f), out var fill);
                var label = MakeLabel(bar, "Name", Loc.T(spec.id), 22, TextAnchor.LowerCenter, Color.white);
                label.rectTransform.anchoredPosition = new Vector2(0, 26);

                enemies.Add(new EnemyView { root = root, body = body, baseColor = color, barRoot = bar, barFill = fill, label = label });
            }
        }

        static Color ElementColor(Element e)
        {
            switch (e)
            {
                case Element.Fire: return new Color(0.85f, 0.3f, 0.2f);
                case Element.Water: return new Color(0.3f, 0.55f, 0.9f);
                case Element.Wind: return new Color(0.35f, 0.8f, 0.6f);
                case Element.Earth: return new Color(0.65f, 0.5f, 0.3f);
                case Element.Light: return new Color(0.95f, 0.9f, 0.6f);
                default: return new Color(0.45f, 0.3f, 0.6f);
            }
        }

        // ---- HUD -----------------------------------------------------------------------

        void BuildHud()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            var canvasGo = new GameObject("HUD");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<PortraitCanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasRect = (RectTransform)canvasGo.transform;

            floatLayer = MakeRect(canvasRect, "World Labels", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var safe = MakeRect(canvasRect, "Safe Area", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            // Top bar
            var top = MakePanel(safe, "Top Bar", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -120), Vector2.zero, new Color(0, 0, 0, 0.45f));
            titleText = MakeLabel(top, "Title", "", 34, TextAnchor.MiddleLeft, Color.white);
            titleText.rectTransform.offsetMin = new Vector2(28, 0);
            timerText = MakeLabel(top, "Timer", "", 30, TextAnchor.MiddleRight, new Color(1, 1, 1, 0.8f));
            timerText.rectTransform.offsetMax = new Vector2(-470, 0);
            speedText = MakeButton(top, "Speed", "x1", new Vector2(1, 0.5f), new Vector2(-400, 0), new Vector2(110, 84), () =>
            {
                runner.SetSpeed(runner.Speed % 3 + 1);
            }).GetComponentInChildren<Text>();
            autoText = MakeButton(top, "Auto", Loc.T("AUTO"), new Vector2(1, 0.5f), new Vector2(-260, 0), new Vector2(150, 84), () =>
            {
                runner.SetAuto(!runner.Simulation.Auto);
            }).GetComponentInChildren<Text>();
            buildButton = MakeButton(top, "Build", Loc.T("BUILD"), new Vector2(1, 0.5f), new Vector2(-90, 0), new Vector2(150, 84), () => bootstrap.CycleBuild());

            // Bottom controls
            var bottom = MakePanel(safe, "Controls", new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 560), new Color(0.07f, 0.05f, 0.05f, 0.92f));

            var hp = MakeBar(bottom, "Dragon HP", new Vector2(980, 34), new Color(0.2f, 0.12f, 0.1f), new Color(0.4f, 0.8f, 0.4f), out hpFill);
            hp.anchorMin = hp.anchorMax = new Vector2(0.5f, 1);
            hp.anchoredPosition = new Vector2(0, -50);
            shieldFill = MakeRect(hp, "Shield", Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero);
            shieldFill.gameObject.AddComponent<Image>().color = new Color(0.45f, 0.7f, 1f, 0.6f);
            hpText = MakeLabel(hp, "HP Text", "", 24, TextAnchor.MiddleCenter, Color.white);

            var gauge = MakeBar(bottom, "Gauge", new Vector2(980, 16), new Color(0.2f, 0.15f, 0.08f), new Color(0.95f, 0.8f, 0.3f), out gaugeFill);
            gauge.anchorMin = gauge.anchorMax = new Vector2(0.5f, 1);
            gauge.anchoredPosition = new Vector2(0, -88);

            commandButton = MakeButton(bottom, "Command", Loc.T("CMD"), new Vector2(1, 1), new Vector2(-130, -230), new Vector2(200, 200), () => runner.UseCommand());
            commandText = commandButton.GetComponentInChildren<Text>();

            MakeButton(bottom, "Dodge", "<<  " + Loc.T("DODGE") + "  >>", new Vector2(0.5f, 0), new Vector2(0, 90), new Vector2(980, 110), () => runner.Dodge());

            dodgeHint = MakeLabel(safe, "Dodge Hint", Loc.T("INCOMING! SWIPE TO DODGE"), 52, TextAnchor.MiddleCenter, new Color(1f, 0.35f, 0.3f));
            dodgeHint.rectTransform.anchorMin = new Vector2(0, 0.42f);
            dodgeHint.rectTransform.anchorMax = new Vector2(1, 0.5f);
            dodgeHint.gameObject.SetActive(false);

            // Result overlay
            var result = MakePanel(canvasRect, "Result", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.75f));
            resultPanel = result.gameObject;
            resultText = MakeLabel(result, "Result Text", "", 64, TextAnchor.MiddleCenter, Color.white);
            resultText.rectTransform.anchorMin = new Vector2(0, 0.55f);
            resultText.rectTransform.anchorMax = new Vector2(1, 0.75f);
            retryButton = MakeButton(result, "Retry", Loc.T("RETRY"), new Vector2(0.5f, 0.45f), new Vector2(-230, 0), new Vector2(400, 130), () => bootstrap.StartBattle());
            nextButton = MakeButton(result, "Next", Loc.T("NEXT FLOOR"), new Vector2(0.5f, 0.45f), new Vector2(230, 0), new Vector2(400, 130), () => bootstrap.NextFloor());
            continueButton = MakeButton(result, "Continue", Loc.T("CONTINUE"), new Vector2(0.5f, 0.45f), Vector2.zero, new Vector2(500, 130), () => { });
            continueButton.gameObject.SetActive(false);
            resultPanel.SetActive(false);
        }

        void BuildSkillButtons()
        {
            foreach (var b in skillButtons) Destroy(b.gameObject);
            skillButtons.Clear();
            skillTexts.Clear();
            var bottom = (RectTransform)commandButton.transform.parent;
            for (int i = 0; i < setup.skills.Count; i++)
            {
                int slot = i;
                var b = MakeButton(bottom, "Skill " + i, Loc.Skill(setup.skills[i].id), new Vector2(0, 1), new Vector2(150 + i * 230, -230),
                    new Vector2(210, 200), () => runner.UseSkill(slot));
                skillButtons.Add(b);
                skillTexts.Add(b.GetComponentInChildren<Text>());
            }
            commandButton.gameObject.SetActive(setup.command != null);
        }

        // ---- per frame -----------------------------------------------------------------

        void Update()
        {
            if (runner == null || runner.Simulation == null) return;
            var sim = runner.Simulation;

            HandlePointer(sim);
            HandleKeys();
            UpdateEnemies(sim);
            UpdateDragon(sim);
            UpdateHud(sim);
            UpdateFloaters();
        }

        void HandleKeys()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) runner.UseSkill(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) runner.UseSkill(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) runner.UseSkill(2);
            if (Input.GetKeyDown(KeyCode.Q)) runner.UseCommand();
            if (Input.GetKeyDown(KeyCode.Space)) runner.Dodge();
        }

        void HandlePointer(BattleSimulation sim)
        {
            if (Input.GetMouseButtonDown(0))
            {
                pointerTracking = EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject();
                pointerDown = Input.mousePosition;
            }
            if (!pointerTracking || !Input.GetMouseButtonUp(0)) return;
            pointerTracking = false;

            Vector2 delta = (Vector2)Input.mousePosition - pointerDown;
            if (Mathf.Abs(delta.x) >= SwipeMinPixels && Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            {
                runner.Dodge();
                return;
            }

            // Tap: target the nearest living enemy on screen.
            int best = -1;
            float bestDist = 220f * canvasRect.lossyScale.x;
            for (int i = 0; i < enemies.Count; i++)
            {
                if (!sim.Enemies[i].alive) continue;
                Vector2 screen = cam.WorldToScreenPoint(enemies[i].body.transform.position);
                float d = Vector2.Distance(screen, pointerDown);
                if (d < bestDist) { bestDist = d; best = i; }
            }
            if (best >= 0) runner.SetTarget(best);
        }

        void UpdateEnemies(BattleSimulation sim)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 18f);
            for (int i = 0; i < enemies.Count; i++)
            {
                var view = enemies[i];
                var state = sim.Enemies[i];
                bool visible = state.alive;
                view.root.gameObject.SetActive(visible);
                view.barRoot.gameObject.SetActive(visible);
                if (!visible) continue;

                view.punch = Mathf.MoveTowards(view.punch, 0, Time.deltaTime * 4f);
                view.root.localScale = Vector3.one * (1f + view.punch * 0.25f);
                var color = view.baseColor;
                if (state.windupLeft > 0) color = Color.Lerp(view.baseColor, Color.white, pulse);
                else if (state.burnTicksLeft > 0) color = Color.Lerp(view.baseColor, new Color(1f, 0.55f, 0f), 0.5f);
                view.body.material.color = color;

                view.barFill.anchorMax = new Vector2((float)state.HpFraction, 1);
                view.barRoot.anchoredPosition = WorldToCanvas(view.root.position + Vector3.up * 3.0f);
                view.label.text = state.escapeTicksLeft > 0
                    ? Loc.F("{0}  flees in {1}s", Loc.T(state.spec.id), Mathf.CeilToInt(state.escapeTicksLeft / (float)BattleSimulation.TicksPerSecond))
                    : Loc.T(state.spec.id);
            }

            int target = sim.Target;
            bool targetAlive = target >= 0 && target < enemies.Count && sim.Enemies[target].alive;
            targetRing.gameObject.SetActive(targetAlive);
            if (targetAlive) targetRing.position = enemies[target].root.position + Vector3.up * 0.02f;
        }

        void UpdateDragon(BattleSimulation sim)
        {
            dragonFlash = Mathf.MoveTowards(dragonFlash, 0, Time.deltaTime * 5f);
            dragonLunge = Mathf.MoveTowards(dragonLunge, 0, Time.deltaTime * 6f);
            dragonFigure.Body.material.color = Color.Lerp(dragonColor, Color.white, dragonFlash);
            dragonFigure.Animate(Time.time);
            dragon.position = new Vector3(Mathf.Sin(Time.time * 40f) * dragonFlash * 0.1f, 0, -2.6f + dragonLunge * 0.6f);

            bool telegraph = sim.IsTelegraphActive;
            warningDisc.gameObject.SetActive(telegraph);
            dodgeHint.gameObject.SetActive(telegraph);
        }

        void UpdateHud(BattleSimulation sim)
        {
            float hp = (float)(sim.DragonHp / sim.DragonMaxHp);
            hpFill.anchorMax = new Vector2(hp, 1);
            shieldFill.anchorMax = new Vector2(Mathf.Clamp01((float)(sim.Shield / sim.DragonMaxHp)), 1);
            hpText.text = Mathf.CeilToInt((float)sim.DragonHp) + " / " + Mathf.CeilToInt((float)sim.DragonMaxHp)
                + (sim.Shield > 0 ? "   " + Loc.F("+{0} shield", Mathf.CeilToInt((float)sim.Shield)) : "");
            gaugeFill.anchorMax = new Vector2((float)(sim.Gauge / BattleSimulation.MaxGauge), 1);

            double left = setup.timeLimitSeconds - sim.ElapsedSeconds;
            timerText.text = Mathf.Max(0, Mathf.CeilToInt((float)left)) + "s";
            autoText.text = sim.Auto ? Loc.T("AUTO ON") : Loc.T("AUTO");
            speedText.text = "x" + runner.Speed;

            for (int i = 0; i < skillButtons.Count; i++)
            {
                int ticks = sim.SkillCooldownTicks(i);
                bool ready = ticks == 0;
                skillButtons[i].interactable = ready && runner.IsRunning;
                skillTexts[i].text = Loc.Skill(setup.skills[i].id).Replace(' ', '\n')
                    + (ready ? "" : "\n" + (ticks / (float)BattleSimulation.TicksPerSecond).ToString("0.0") + "s");
            }
            commandButton.interactable = sim.IsCommandReady && runner.IsRunning;
            commandText.text = sim.IsCommandReady ? Loc.T("COMMAND") + "\n" + Loc.T("READY") : Loc.T("CMD") + "\n" + Mathf.FloorToInt((float)sim.Gauge) + "%";
        }

        // ---- events --------------------------------------------------------------------

        void OnBattleEvent(BattleEvent e)
        {
            // After Skip() every remaining event arrives at once: only show the last half second.
            if (runner.Simulation.Tick - e.tick > BattleSimulation.TicksPerSecond / 2) return;

            switch (e.type)
            {
                case BattleEventType.EnemyHit:
                case BattleEventType.BurnTick:
                case BattleEventType.Reflected:
                    if (e.enemy >= 0 && e.enemy < enemies.Count)
                    {
                        enemies[e.enemy].punch = 1f;
                        var color = e.type == BattleEventType.BurnTick ? new Color(1f, 0.6f, 0.2f)
                            : e.crit ? new Color(1f, 0.9f, 0.3f) : Color.white;
                        Float(enemies[e.enemy].root.position + Vector3.up * 2f, Mathf.RoundToInt((float)e.amount) + (e.crit ? "!" : ""),
                            color, e.crit ? 48 : 36);
                    }
                    if (e.type == BattleEventType.EnemyHit) dragonLunge = 1f;
                    break;
                case BattleEventType.DragonHit:
                    dragonFlash = 1f;
                    Float(dragon.position + Vector3.up * 2f, "-" + Mathf.RoundToInt((float)e.amount), new Color(1f, 0.4f, 0.4f), 40);
                    break;
                case BattleEventType.Healed:
                    Float(dragon.position + Vector3.up * 2.4f, "+" + Mathf.RoundToInt((float)e.amount), new Color(0.5f, 1f, 0.5f), 40);
                    break;
                case BattleEventType.Dodged:
                    Float(dragon.position + Vector3.up * 2.4f, Loc.T("DODGE"), new Color(0.6f, 0.9f, 1f), 44);
                    break;
                case BattleEventType.Enraged:
                    Float(new Vector3(0, 3, 3.6f), Loc.T("ENRAGED"), new Color(1f, 0.2f, 0.2f), 56);
                    break;
            }
        }

        void OnFinished(BattleOutcome outcome, IReadOnlyList<BattleInput> inputs)
        {
            resultPanel.SetActive(true);
            string head;
            switch (outcome)
            {
                case BattleOutcome.Victory: head = Loc.T("VICTORY"); break;
                case BattleOutcome.Escaped: head = Loc.T("IT GOT AWAY"); break;
                case BattleOutcome.Timeout: head = Loc.T("TIME UP"); break;
                default: head = Loc.T("DEFEAT"); break;
            }
            resultText.text = head + "\n<size=36>" + Loc.F("{0} s · {1} inputs · HP {2}%", runner.Simulation.ElapsedSeconds.ToString("0.0"), inputs.Count,
                Mathf.RoundToInt((float)(runner.Simulation.DragonHp / runner.Simulation.DragonMaxHp * 100))) + "</size>";
        }

        // ---- floating text -------------------------------------------------------------

        void Float(Vector3 worldPos, string text, Color color, int size)
        {
            var label = MakeLabel(floatLayer, "Float", text, size, TextAnchor.MiddleCenter, color);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            label.rectTransform.sizeDelta = new Vector2(300, 80);
            label.rectTransform.anchoredPosition = WorldToCanvas(worldPos) + new Vector2(Random.Range(-40f, 40f), 0);
            label.raycastTarget = false;
            floaters.Add(new Floater { rect = label.rectTransform, text = label });
        }

        void UpdateFloaters()
        {
            for (int i = floaters.Count - 1; i >= 0; i--)
            {
                var f = floaters[i];
                f.age += Time.deltaTime;
                f.rect.anchoredPosition += Vector2.up * 120f * Time.deltaTime;
                var c = f.text.color;
                c.a = Mathf.Clamp01(1.2f - f.age);
                f.text.color = c;
                if (f.age > 1.2f)
                {
                    Destroy(f.rect.gameObject);
                    floaters.RemoveAt(i);
                }
            }
        }

        Vector2 WorldToCanvas(Vector3 worldPos)
        {
            Vector2 screen = cam.WorldToScreenPoint(worldPos);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(floatLayer, screen, null, out var local);
            return local;
        }

        // ---- uGUI helpers --------------------------------------------------------------

        static RectTransform MakeRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        static RectTransform MakePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var rt = MakeRect(parent, name, anchorMin, anchorMax, offsetMin, offsetMax);
            rt.gameObject.AddComponent<Image>().color = color;
            return rt;
        }

        Text MakeLabel(Transform parent, string name, string text, int size, TextAnchor anchor, Color color)
        {
            var rt = MakeRect(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.text = text;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            rt.gameObject.AddComponent<ThaiMarkPositioner>();
            return t;
        }

        Button MakeButton(Transform parent, string name, string text, Vector2 anchor, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var rt = MakeRect(parent, name, anchor, anchor, Vector2.zero, Vector2.zero);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            var image = rt.gameObject.AddComponent<Image>();
            image.color = new Color(0.32f, 0.2f, 0.16f);
            var button = rt.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            colors.highlightedColor = new Color(1f, 0.85f, 0.75f);
            button.colors = colors;
            button.onClick.AddListener(onClick);
            MakeLabel(rt, "Label", text, 28, TextAnchor.MiddleCenter, Color.white);
            return button;
        }

        RectTransform MakeBar(Transform parent, string name, Vector2 size, Color back, Color front, out RectTransform fill)
        {
            var rt = MakeRect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rt.sizeDelta = size;
            rt.gameObject.AddComponent<Image>().color = back;
            fill = MakeRect(rt, "Fill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            fill.gameObject.AddComponent<Image>().color = front;
            fill.GetComponent<Image>().raycastTarget = false;
            rt.GetComponent<Image>().raycastTarget = false;
            return rt;
        }
    }
}
