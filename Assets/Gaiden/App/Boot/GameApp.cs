using System;
using System.Collections;
using System.Collections.Generic;
using Gaiden.Core.Battle;
using Gaiden.Core.Content;
using Gaiden.Meta;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Gaiden.App
{
    /// <summary>
    /// The whole app is constructed here, in code, at startup — the scene file is an empty shell.
    /// Owns the services (content, saves, audio), the UI Toolkit panel, the letterboxed 1170×2532
    /// stage (GG-LAW-8) and the screen router.
    /// </summary>
    public sealed class GameApp : MonoBehaviour
    {
        public const int DesignWidth = 1170;
        public const int DesignHeight = 2532;

        public static GameApp I { get; private set; }

        public ContentDb Content { get; private set; }
        public SaveStore Saves { get; private set; }
        public Audio Audio { get; private set; }
        public ScreenRouter Router { get; private set; }
        public VisualElement Stage { get; private set; }
        public VisualElement Overlay { get; private set; }
        public UIDocument Document { get; private set; }

        /// <summary>The save being played (null on the title screen).</summary>
        public SaveData Save { get; set; }
        public int Slot { get; set; } = -1;

        public List<string> ContentProblems { get; private set; } = new List<string>();
        public event Action<float> Ticked;

        private VisualElement letterbox;
        private Vector2 lastScreen;
        private float playClock;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (I != null) return;
            var go = new GameObject("GameApp");
            DontDestroyOnLoad(go);
            go.AddComponent<GameApp>();
        }

        private void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.transform.SetParent(transform, false);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = 0;

            Content = LoadContent();
            Art.IndexSpells(Content.Abilities.Keys);
            Saves = new SaveStore(CommandLine.Value("-saveRoot"));
            Audio = Audio.Create(transform);

            BuildPanel();
            Router = new ScreenRouter(Stage.Q("screens"), Stage.Q("fade"));

            if (CommandLine.Has("-noDiagonalSlip")) BattleConfig.DiagonalSlipEnabled = false;
            var skill = CommandLine.Value("-skill");
            if (skill != null && float.TryParse(skill, out var skillLevel)) BotSkillProfile.Level = skillLevel;
            BotSkillProfile.TelemetryEnabled = CommandLine.Has("-telemetry");

            var start = CommandLine.Value("-start");
            if (TestMode.Active) TestMode.Start();
            else if (DemoMode.Active) DemoMode.Start();
            else if (!string.IsNullOrEmpty(start) && Automation.StartScreens.TryGet(start, out var startScreen)) Router.ResetTo(startScreen);
            else Router.ResetTo(new TitleScreen());

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Automation.AutomationServer.TryStart(this);
#endif
        }

        private ContentDb LoadContent()
        {
            var db = ContentLoader.Load(name => Resources.Load<TextAsset>("Content/" + name)?.text);
            ContentProblems = ContentLoader.Validate(db, id => Art.Exists("Portraits/" + id));
            foreach (var p in ContentProblems) Debug.LogWarning("[Content] " + p);
            Debug.Log($"[GameApp] content: {db.Classes.Count} classes, {db.Abilities.Count} abilities, {db.Items.Count} items, {db.Stages.Count} stages");
            return db;
        }

        private void BuildPanel()
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "GaidenPanel";
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/GaidenTheme");
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(DesignWidth, DesignHeight);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.clearColor = true;
            settings.colorClearValue = Color.black;

            var docGo = new GameObject("UI");
            docGo.transform.SetParent(transform, false);
            docGo.SetActive(false);
            Document = docGo.AddComponent<UIDocument>();
            Document.panelSettings = settings;
            docGo.SetActive(true);

            var root = Document.rootVisualElement;
            root.styleSheets.Add(Resources.Load<StyleSheet>("UI/Theme"));
            root.style.backgroundColor = Color.black;
            Fonts.UseBody(root);

            letterbox = new VisualElement();
            letterbox.AddToClassList("fill");
            letterbox.AddToClassList("letterbox");
            root.Add(letterbox);

            Stage = new VisualElement { name = "stage" };
            Stage.AddToClassList("stage");
            letterbox.Add(Stage);

            var screens = new VisualElement { name = "screens" };
            screens.AddToClassList("fill");
            Stage.Add(screens);

            Overlay = new VisualElement { name = "overlay", pickingMode = PickingMode.Ignore };
            Overlay.AddToClassList("fill");
            Stage.Add(Overlay);

            var fade = new VisualElement { name = "fade", pickingMode = PickingMode.Ignore };
            fade.AddToClassList("fill");
            fade.AddToClassList("fade");
            Stage.Add(fade);

            FitStage();
#if DEVELOPMENT_BUILD
            if (CommandLine.Has("-traceInput"))
            {
                root.RegisterCallback<PointerDownEvent>(e => Debug.Log($"[Input] down at {e.position} target={(e.target as VisualElement)?.name}/{(e.target as VisualElement)?.GetType().Name} id={e.pointerId}"), TrickleDown.TrickleDown);
                root.RegisterCallback<PointerUpEvent>(e => Debug.Log($"[Input] up at {e.position} target={(e.target as VisualElement)?.name}/{(e.target as VisualElement)?.GetType().Name} id={e.pointerId}"), TrickleDown.TrickleDown);
                root.RegisterCallback<ClickEvent>(e =>
                {
                    var sb = new System.Text.StringBuilder();
                    for (var v = e.target as VisualElement; v != null; v = v.parent)
                        sb.Append($" > {v.GetType().Name}#{v.name}[{string.Join(".", v.GetClasses())}]{v.worldBound}");
                    Debug.Log("[Input] click path" + sb);
                }, TrickleDown.TrickleDown);
            }
#endif
        }

        /// <summary>Fit the fixed design canvas inside the screen: scale to the limiting axis, center, bars elsewhere — never stretch.</summary>
        private void FitStage()
        {
            var screen = new Vector2(Screen.width, Screen.height);
            if (screen == lastScreen && Stage.Q("screens") != null) return;
            lastScreen = screen;
            float designAspect = DesignWidth / (float)DesignHeight;
            float screenAspect = screen.x / Mathf.Max(1f, screen.y);
            // Match the axis that limits the fit so the whole 1170×2532 stage is visible.
            Document.panelSettings.match = screenAspect > designAspect ? 1f : 0f;
            float logicalW = screenAspect > designAspect ? DesignHeight * screenAspect : DesignWidth;
            float logicalH = screenAspect > designAspect ? DesignHeight : DesignWidth / screenAspect;
            Stage.style.left = (logicalW - DesignWidth) / 2f;
            Stage.style.top = (logicalH - DesignHeight) / 2f;
            ApplySafeArea(logicalW, logicalH);
        }

        /// <summary>Insets the screens where the device's safe area (notch / home bar) overlaps the stage (bible §26.5).</summary>
        private void ApplySafeArea(float logicalW, float logicalH)
        {
            var safe = Screen.safeArea;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            // Test override: -safeArea <topPx>,<bottomPx> simulates a notch / home bar on desktop.
            var ov = CommandLine.Value("-safeArea");
            if (!string.IsNullOrEmpty(ov))
            {
                var parts = ov.Split(',');
                if (parts.Length == 2 && float.TryParse(parts[0], out var top) && float.TryParse(parts[1], out var bottom))
                    safe = new Rect(0, bottom, Screen.width, Screen.height - top - bottom);
            }
            float k = logicalH / Screen.height;
            float stageTop = (logicalH - DesignHeight) / 2f, stageBottom = stageTop + DesignHeight;
            float safeTop = (Screen.height - safe.yMax) * k, safeBottom = logicalH - safe.yMin * k;
            var screens = Stage.Q("screens");
            screens.style.top = Mathf.Max(0f, safeTop - stageTop);
            screens.style.bottom = Mathf.Max(0f, stageBottom - safeBottom);
        }

        private void Update()
        {
            FitStage();
            float dt = Tween.Dt;
            if (Save != null) { playClock += dt; if (playClock >= 1f) { Save.PlaySeconds += playClock; playClock = 0f; } }
            Router.Current?.Tick(dt);
            Ticked?.Invoke(dt);

            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Router.Back();
        }

        // ── Session helpers ─────────────────────────────────────────────────────────

        public void Persist()
        {
            if (Save == null || Slot < 0) return;
            try { Saves.Write(Slot, Save); }
            catch (Exception e) { Debug.LogError("[Save] write failed: " + e); Toast("Couldn't save!"); }
        }

        public void Toast(string text, float seconds = 1.8f)
        {
            StartCoroutine(ToastRoutine(text, seconds));
        }

        private IEnumerator ToastRoutine(string text, float seconds)
        {
            var t = Ui.Text(text, "toast shadow", Overlay);
            t.name = "toast";
            t.style.opacity = 0;
            yield return null;
            t.style.opacity = 1;
            yield return new WaitForSecondsRealtime(seconds);
            t.style.opacity = 0;
            yield return new WaitForSecondsRealtime(0.25f);
            t.RemoveFromHierarchy();
        }

        public void ApplySettings()
        {
            if (Save == null) return;
            Audio.Apply(Save.Settings);
            Tween.Speed = Mathf.Clamp(Save.Settings.BattleSpeed, 0.5f, 3f);
            Art.ApplyTheme(Save.Settings.MenuTheme);
            Fonts.ApplyFont(Save.Settings.FontName);
        }
    }

    public static class CommandLine
    {
        public static string Value(string flag)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        public static bool Has(string flag)
        {
            foreach (var a in Environment.GetCommandLineArgs())
                if (string.Equals(a, flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }

    /// <summary>
    /// `-demo`: boots straight into a scripted self-play loop (new game → battle, autoplayed → next
    /// stage → …, looping with a fresh save when the campaign ends or nothing is launchable) — this
    /// is the tour, the same flag name used for it in every MindAttic Unity project. The same name
    /// tools/harness.py campaign drives over HTTP. (The narrower, pre-existing meaning of "auto-resolve
    /// the current battle" was renamed to `-autobattle` — see BattleScreen.AutoPlay.)
    /// </summary>
    public static class DemoMode
    {
        public static bool Active => CommandLine.Has("-demo");

        public static void Start()
        {
            GameApp.I.Router.ResetTo(new TitleScreen());
            Restart();
        }

        /// <summary>Called from PostBattleScreen once the results are up: does whatever a human would click next.</summary>
        public static void AfterBattle(BattleReport report, string stageId)
        {
            var app = GameApp.I;
            var c = app.Content;
            if (!report.Victory)
            {
                var theme = c.ThemeOf(stageId);
                string retryId = report.ExpeditionFailed && theme != null ? theme.Stages[0] : stageId;
                StageLauncher.Launch(retryId, true);
                return;
            }
            if (report.NextStageId != null) { StageLauncher.Launch(report.NextStageId, true); return; }
            var stages = c.CampaignStages();
            int idx = stages.FindIndex(s => s.Id == stageId);
            if (idx >= 0 && idx + 1 < stages.Count && Progression.IsStageUnlocked(app.Save, idx + 1) && Expeditions.CanLaunch(c, app.Save, stages[idx + 1].Id, out _))
            {
                StageLauncher.Launch(stages[idx + 1].Id, true);
                return;
            }
            Restart(); // campaign complete, or nothing left to launch — loop with a fresh save
        }

        /// <summary>Unlocks every class and shows the fighting-game-style select screen, which drives itself
        /// in demo mode: random tiles, so each run shows a different cross-section of the roster.</summary>
        private static void Restart()
        {
            GameApp.I.Router.Push(new CharacterSelectScreen(BeginRun, auto: true));
        }

        private static void BeginRun(List<string> party)
        {
            var app = GameApp.I;
            app.Save = Progression.NewGame(app.Content, "Demo", party);
            app.Slot = -1;
            // ResetTo fades out the still-visible CharacterSelectScreen before its mutate actually applies
            // (ScreenRouter.Go only mutates the stack synchronously when nothing is on screen yet), so pushing
            // the first stage immediately after would race it and land on top of the stale stack, then get wiped
            // out when the delayed Hub mutate finally runs. Wait for Hub to actually land before launching.
            void OnShown(GameScreen shown)
            {
                if (shown is not HubScreen) return;
                app.Router.Changed -= OnShown;
                var stages = app.Content.CampaignStages();
                if (stages.Count > 0) StageLauncher.Launch(stages[0].Id, false);
            }
            app.Router.Changed += OnShown;
            app.Router.ResetTo(new HubScreen());
        }
    }

    /// <summary>
    /// `-testmode` (GG-T93, bot-only): self-plays the whole mechanical loop with no story, crawl, overworld
    /// or tutorial, to verify the game end to end — throwaway save on slot -1 (Persist never writes it) with
    /// the starting party → launch a stage → autoplayed battle → results → back to the Hub → CampRoutine
    /// (repair, Inn, potions, forge/equip, learn/assign) → launch again, forever. Unlike `-demo`, it visits
    /// camp and spends between every fight. Enemy level follows the party (Progression.StageRecommendedLevel)
    /// and stage choice lives in Meta (TestLoop.PickStage); this class only drives screens and logs one
    /// "[TestMode] cycle N | …" line per cycle (stage, party/enemy level, result, XP, gold, mats, camp actions).
    /// </summary>
    public static class TestMode
    {
        public static bool Active => CommandLine.Has("-testmode");
        /// <summary>Cycles finished (camp done and logged) — reported by the automation state dump.</summary>
        public static int Completed { get; private set; }
        public static string LastLine { get; private set; }
        private static TestCycle current;
        private static int losses;
        private static bool learned;

        public static void Start()
        {
            var app = GameApp.I;
            app.Save = TestLoop.NewSave(app.Content);
            app.Slot = -1;
            app.Save.Settings.BattleSpeed = 3f;
            app.ApplySettings();
            Debug.Log($"[TestMode] started: throwaway save (slot -1), party {string.Join(", ", app.Save.Party)}, {app.Save.Gold}g");
            GoToCamp(runRoutine: false);
        }

        /// <summary>Called from PostBattleScreen once the results are up.</summary>
        public static void AfterBattle(BattleReport report, string stageId)
        {
            var app = GameApp.I;
            current?.Battle(app.Content, app.Save, report);
            losses = report.Victory ? 0 : losses + 1;
            GoToCamp(runRoutine: true);
        }

        private static void GoToCamp(bool runRoutine)
        {
            var app = GameApp.I;
            void OnShown(GameScreen shown)
            {
                if (shown is not HubScreen hub) return;
                app.Router.Changed -= OnShown;
                app.StartCoroutine(Camp(hub, runRoutine));
            }
            app.Router.Changed += OnShown;
            app.Router.ResetTo(new HubScreen());
        }

        private static IEnumerator Camp(HubScreen hub, bool runRoutine)
        {
            var app = GameApp.I;
            yield return new WaitForSecondsRealtime(1.0f);
            if (runRoutine && current != null)
            {
                current.RunCamp(app.Content, app.Save);
                LastLine = current.ToString();
                Completed++;
                Debug.Log(LastLine);
                learned |= TestLoop.LearnedAnAbility(current.Camp);
                if (!learned && Completed == TestLoop.CyclesThatMustLearnAnAbility)
                    Debug.LogWarning($"[TestMode] check failed: {Completed} cycles and camp never learned an ability (GG-T100)");
                if (app.Router.Current == hub) hub.Refresh();
                yield return new WaitForSecondsRealtime(1.5f);
            }
            if (app.Router.Current != hub) yield break; // something else took over the screen; don't fight it
            Launch();
        }

        private static void Launch()
        {
            var app = GameApp.I;
            var c = app.Content; var s = app.Save;
            string stageId = TestLoop.PickStage(c, s, losses);
            if (stageId != null && !Expeditions.CanLaunch(c, s, stageId, out var why))
            {
                Debug.Log($"[TestMode] can't launch {stageId} ({why}) — abandoning the expedition");
                Expeditions.Abandon(s);
                stageId = TestLoop.PickStage(c, s, losses);
            }
            if (stageId == null) { Debug.LogError("[TestMode] no launchable stage"); return; }
            current = TestCycle.Begin(c, s, stageId, Completed + 1);
            Debug.Log($"[TestMode] cycle {current.Number} launch {stageId} | party Lv {current.PartyLevel} | enemy Lv {current.EnemyMin}-{current.EnemyMax}");
            StageLauncher.Launch(stageId, false);
        }
    }
}
