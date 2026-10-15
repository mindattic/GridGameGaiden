#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Gaiden.Core;
using Gaiden.Core.Battle;
using Gaiden.Core.Content;
using Gaiden.Meta;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

namespace Gaiden.App.Automation
{
    /// <summary>
    /// Development-build-only remote control so the game can be played and inspected by a script:
    /// a localhost HTTP endpoint (start the player with <c>-automation 7777</c>). Input is injected
    /// through the Input System's real mouse device, so taps and drags travel the same path a
    /// player's do. Coordinates are design pixels (1170×2532, origin top-left).
    /// </summary>
    public sealed class AutomationServer : MonoBehaviour
    {
        private HttpListener listener;
        private Thread thread;
        private readonly ConcurrentQueue<Action> mainThread = new ConcurrentQueue<Action>();
        private readonly List<string> logs = new List<string>();
        private string artifacts;
        private Mouse mouse;
        private bool holdDrag;
        private Application.LogCallback logHandler;

        public static void TryStart(GameApp app)
        {
            var port = CommandLine.Value("-automation");
            if (string.IsNullOrEmpty(port)) return;
            var s = app.gameObject.AddComponent<AutomationServer>();
            s.Begin(int.Parse(port));
        }

        private void Begin(int port)
        {
            artifacts = CommandLine.Value("-artifacts") ?? Path.Combine(Application.persistentDataPath, "artifacts");
            Directory.CreateDirectory(artifacts);
            logHandler = (msg, stack, type) =>
            {
                if (type == LogType.Log && !msg.StartsWith("[")) return;
                lock (logs) { logs.Add($"{type}: {msg}" + (type == LogType.Exception || type == LogType.Error ? "\n" + stack : "")); if (logs.Count > 400) logs.RemoveAt(0); }
            };
            Application.logMessageReceivedThreaded += logHandler;
            Debug.developerConsoleEnabled = false;
            mouse = InputSystem.AddDevice<Mouse>("AutomationMouse");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(1, 1) });
            listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Start();
            thread = new Thread(Serve) { IsBackground = true, Name = "Automation" };
            thread.Start();
            Debug.Log($"[Automation] listening on {port}, artifacts → {artifacts}");
        }

        private void OnDestroy()
        {
            if (logHandler != null) Application.logMessageReceivedThreaded -= logHandler;
            try { listener?.Stop(); } catch { }
        }

        private void Update()
        {
            while (mainThread.TryDequeue(out var a))
            {
                try { a(); } catch (Exception e) { Debug.LogException(e); }
            }
        }

        private void Serve()
        {
            while (listener.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = listener.GetContext(); }
                catch (Exception)
                {
                    // Expected when OnDestroy() calls listener.Stop() while GetContext() is blocked;
                    // anything else should not silently end the listener thread.
                    if (!listener.IsListening) return;
                    continue;
                }
                ThreadPool.QueueUserWorkItem(_ => Handle(ctx));
            }
        }

        private void Handle(HttpListenerContext ctx)
        {
            string body;
            int status = 200;
            try
            {
                var q = ctx.Request.QueryString;
                var path = ctx.Request.Url.AbsolutePath.Trim('/');
                var done = new ManualResetEventSlim();
                object result = null;
                Exception error = null;
                mainThread.Enqueue(() =>
                {
                    try
                    {
                        var r = Dispatch(path, q, v => { result = v; done.Set(); });
                        if (r != Pending) { result = r; done.Set(); }
                    }
                    catch (Exception e) { error = e; done.Set(); }
                });
                if (!done.Wait(TimeSpan.FromSeconds(120))) throw new TimeoutException("main thread did not answer");
                if (error != null) throw error;
                body = result as string ?? JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception e)
            {
                status = 500;
                body = JsonConvert.SerializeObject(new { error = e.Message });
            }
            var bytes = Encoding.UTF8.GetBytes(body);
            try
            {
                ctx.Response.StatusCode = status;
                ctx.Response.ContentType = "application/json";
                ctx.Response.ContentLength64 = bytes.Length;
                ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            }
            catch (Exception) { /* client disconnected mid-response; nothing more to do */ }
            finally { try { ctx.Response.OutputStream.Close(); } catch (Exception) { } }
        }

        private static readonly object Pending = new object();

        private object Dispatch(string path, System.Collections.Specialized.NameValueCollection q, Action<object> reply)
        {
            var app = GameApp.I;
            switch (path)
            {
                case "ping": return new { ok = true, screen = app.Router.Current?.Id };
                case "state": return StateDump();
                case "logs":
                    lock (logs) { var copy = logs.ToList(); if (q["clear"] == "1") logs.Clear(); return copy; }
                case "record":
                    StartCoroutine(Record(SafeName(q["name"], "rec"), (int)F(q["frames"], 60), Math.Max(1, (int)F(q["every"], 1)), F(q["step"], 1f / 30f), reply));
                    return Pending;
                case "screenshot":
                    StartCoroutine(Screenshot(SafeName(q["name"], DateTime.Now.ToString("HHmmss")), reply));
                    return Pending;
                case "tap":
                    StartCoroutine(Tap(F(q["x"]), F(q["y"]), reply));
                    return Pending;
                case "drag":
                    holdDrag = q["hold"] == "1";
                    StartCoroutine(Drag(ParsePoints(q["path"]), F(q["step"], 0.03f), reply));
                    return Pending;
                case "click":
                {
                    var e = app.Stage.Q(q["name"]);
                    // Q(name) isn't filtered by visibility like clicktext's Query<>() is — without this,
                    // a hidden/zero-size/off-stage match (e.g. mid-transition, or a same-named element
                    // elsewhere not currently shown) taps nothing and still replies ok:true (GG-T16).
                    if (e == null || e.resolvedStyle.display == DisplayStyle.None || e.worldBound.width <= 0 || !Opaque(e))
                        return new { error = $"no visible element named '{q["name"]}'" };
                    var c = DesignPoint(e.worldBound.center);
                    StartCoroutine(Tap(c.x, c.y, reply));
                    return Pending;
                }
                case "clicktext":
                {
                    var target = q["text"];
                    var label = app.Stage.Query<Label>().ToList().FirstOrDefault(l => l.text != null && l.text.Contains(target) && l.resolvedStyle.display != DisplayStyle.None && l.worldBound.width > 0);
                    var btn = app.Stage.Query<Button>().ToList().FirstOrDefault(b => b.text != null && b.text.Contains(target) && b.worldBound.width > 0);
                    VisualElement hit = (VisualElement)btn ?? label;
                    if (hit == null) return new { error = $"no visible text '{target}'" };
                    var c = DesignPoint(hit.worldBound.center);
                    StartCoroutine(Tap(c.x, c.y, reply));
                    return Pending;
                }
                case "back": app.Router.Back(); return new { ok = true };
                case "resetinput":
                    // GG-T16 recovery: click()/clicktext() have been seen going silently unresponsive
                    // (ok:true, plausible coordinates, but the tap never registers) after a long-running
                    // session's worth of automation traffic, with no error anywhere to react to. Not
                    // root-caused — this gives a harness that detects the stuck pattern (screen doesn't
                    // advance after a click) a cheap recovery to try before resorting to a full process
                    // restart: tear down and recreate the virtual mouse device, in case some InputSystem-
                    // side state (not UI Toolkit's, since a fresh device still shares the same panel) has
                    // gone stale.
                    if (mouse != null) { InputSystem.RemoveDevice(mouse); }
                    mouse = InputSystem.AddDevice<Mouse>("AutomationMouse");
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(1, 1) });
                    return new { ok = true };
                case "wait":
                    StartCoroutine(WaitIdle(F(q["ms"], 0) / 1000f, reply));
                    return Pending;
                case "newgame":
                {
                    int slot = (int)F(q["slot"], 0);
                    app.Save = Progression.NewGame(app.Content, q["name"] ?? "Bot");
                    app.Slot = slot;
                    if (q["gold"] != null) app.Save.Gold = (int)F(q["gold"]);
                    app.ApplySettings();
                    app.Persist();
                    app.Router.ResetTo(new HubScreen());
                    return new { ok = true };
                }
                case "devequipall":
                {
                    // Test-only (GG-T39): a fresh save starts every hero bare-handed (weapons are bought
                    // at the Blacksmith), so harness checks of weapon-durability UI need something equipped
                    // first. Mirrors Meta.Equipment.Equip (grants + equips) across the whole roster.
                    if (app.Save == null) return new { error = "no save" };
                    var itemId = q["item"] ?? "eq_sword_iron";
                    int equipped = 0;
                    foreach (var h in app.Save.Roster)
                    {
                        app.Save.AddItem(itemId, 1);
                        if (Equipment.Equip(app.Content, app.Save, h, EquipmentSlot.Weapon, itemId).Ok) equipped++;
                    }
                    return new { ok = equipped > 0, equipped };
                }
                case "devgive":
                {
                    // Test-only: grants bag items for harness checks that need a specific reagent/material
                    // without grinding a rare drop (e.g. GG-T45 potency-brew UI).
                    if (app.Save == null) return new { error = "no save" };
                    var itemId = q["item"];
                    if (itemId == null) return new { error = "missing item" };
                    app.Save.AddItem(itemId, (int)F(q["count"], 1));
                    return new { ok = true, count = app.Save.Count(itemId) };
                }
                case "goto":
                    if (!StartScreens.TryGet(q["screen"], out var screen)) return new { error = "unknown screen" };
                    app.Router.Push(screen);
                    return new { ok = true };
                case "battle":
                {
                    if (app.Save == null) { app.Save = Progression.NewGame(app.Content, "Bot"); app.Slot = 2; }
                    var stage = q["stage"] ?? app.Content.CampaignStages()[0].Id;
                    app.Router.Push(new BattleScreen(stage));
                    return new { ok = true, stage };
                }
                case "randombattle":
                {
                    // Battle-clip demo: picks a random real stage (any theme, main or postgame) and a
                    // random wave from it — one enemy family grouping, per the house "never mix families
                    // in a wave" convention — as a single-wave skirmish. Registers a throwaway synthetic
                    // StageDef in the in-memory ContentDb (never touches the JSON on disk) so the normal
                    // BattleFactory.Build path — party, LevelOverride-aware recommended level, everything —
                    // works exactly like a real stage. Launch with -capture-config pointing at
                    // capture.json (its "enabled" still true) to turn each one into its own short
                    // clip via its battle-start/battle-end rules — MindAttic.Capture only reads that
                    // file when told to with -capture-config, never just because it sits in
                    // StreamingAssets (see tools/harness.py's battle_clips()).
                    // Earlier calls' throwaway stages must not re-enter the pool — a prior run of this
                    // same endpoint, left running, fed itself its own synthetic stages as "random real"
                    // sources within a few dozen calls, compounding (skirmish of a skirmish of a...) and
                    // collapsing variety toward whatever got picked early. Prune them before picking.
                    const string SynthPrefix = "skirmish-";
                    foreach (var oldId in app.Content.Stages.Keys.Where(k => k.StartsWith(SynthPrefix)).ToList())
                        app.Content.Stages.Remove(oldId);
                    var realStages = app.Content.Stages.Values.Where(s => !s.IsTest && s.Waves.Count > 0).ToList();
                    if (realStages.Count == 0) return new { error = "no stages available" };
                    var src = realStages[UnityEngine.Random.Range(0, realStages.Count)];
                    var wave = src.Waves[UnityEngine.Random.Range(0, src.Waves.Count)];
                    int srcIndex = app.Content.CampaignStages().FindIndex(x => x.Id == src.Id);
                    int level = src.LevelOverride ?? (srcIndex >= 0 ? Progression.RecommendedLevel(srcIndex) : 5);
                    var synthId = SynthPrefix + Guid.NewGuid().ToString("N").Substring(0, 8);
                    app.Content.Stages[synthId] = new StageDef
                    {
                        Id = synthId, Name = $"{src.Name} Skirmish", Theme = src.Theme, Background = src.Background,
                        Music = src.Music, IsTest = false, LevelOverride = level, Waves = new List<WaveDef> { wave },
                    };
                    if (app.Save == null) { app.Save = Progression.NewGame(app.Content, "Bot"); app.Slot = 2; }
                    // ResetTo, not Push: this is meant to be called over and over for "tons of clips" —
                    // Push would grow the screen stack by two (battle, then its postbattle) every time.
                    app.Router.ResetTo(new BattleScreen(synthId));
                    return new { ok = true, stage = synthId, source = src.Id, level, enemies = wave.Enemies.Select(e => e.Class).ToList() };
                }
                case "autoplay":
                    if (app.Router.Current is BattleScreen bs) { bs.AutoPlay = q["on"] != "0"; return new { ok = true }; }
                    return new { error = "not in battle" };
                case "speed":
                    Tween.Speed = F(q["x"], 1);
                    if (app.Save != null) app.Save.Settings.BattleSpeed = Tween.Speed;
                    return new { ok = true, Tween.Speed };
                case "cmd":
                {
                    if (!(app.Router.Current is BattleScreen b)) return new { error = "not in battle" };
                    if (!b.CanCommand()) return new { error = "busy" };
                    BattleCommand cmd = null;
                    switch (q["type"])
                    {
                        case "shield": cmd = new ShieldCommand(); break;
                        case "move": cmd = new MoveCommand { HeroId = (int)F(q["hero"]), Path = ParseTiles(q["path"]) }; break;
                        case "use": cmd = new UseSlotCommand { HeroId = (int)F(q["hero"]), Slot = (int)F(q["slot"]), Target = new Coord((int)F(q["x"]), (int)F(q["y"])) }; break;
                    }
                    if (cmd == null) return new { error = "bad type" };
                    b.Commit(cmd);
                    StartCoroutine(WaitIdle(0, reply));
                    return Pending;
                }
                case "element":
                {
                    var e = app.Stage.Q(q["name"]);
                    if (e == null) return new { error = "not found" };
                    var tl = DesignPoint(e.worldBound.min); var br = DesignPoint(e.worldBound.max);
                    return new { x = tl.x, y = tl.y, w = br.x - tl.x, h = br.y - tl.y, visible = e.resolvedStyle.display != DisplayStyle.None };
                }
                case "layout": return LayoutAudit();
                case "texts":
                    return app.Stage.Query<TextElement>().ToList()
                        .Where(t => !string.IsNullOrEmpty(t.text) && t.resolvedStyle.display != DisplayStyle.None && t.resolvedStyle.visibility == Visibility.Visible && t.worldBound.width > 0 && Opaque(t))
                        .Select(t => t.text).ToList();
                case "names":
                {
                    var prefix = q["prefix"] ?? "";
                    return app.Stage.Query<VisualElement>().ToList().Where(e => e.name != null && e.name.StartsWith(prefix) && e.worldBound.width > 0).Select(e => e.name).ToList();
                }
                case "music": return new { track = app.Audio.CurrentTrack };
                case "cues":
                {
                    var list = app.Audio.CueLog.ToList();
                    if (q["clear"] == "1") app.Audio.CueLog.Clear();
                    return list;
                }
                case "setting":
                {
                    if (app.Save == null) return new { error = "no save" };
                    var st = app.Save.Settings;
                    bool on = q["value"] == "1" || q["value"] == "true";
                    switch (q["key"])
                    {
                        case "colorblind": st.ColorblindMode = on; break;
                        case "reducemotion": st.ReduceMotion = on; break;
                        case "dragtimer": st.DragTimer = on; break;
                        case "mutemusic": st.MuteMusic = on; break;
                        case "mutesfx": st.MuteSfx = on; break;
                        case "cutins": st.PincerCutIns = on; break;
                        case "confirm": st.ConfirmActions = on; break;
                        default: return new { error = "unknown key" };
                    }
                    app.ApplySettings();
                    return new { ok = true };
                }
                case "givemana":
                    if (app.Router.Current is BattleScreen gm) { gm.DevGiveMana(int.TryParse(q["n"], out var gn) ? gn : 6); return new { ok = true }; }
                    return new { error = "not in battle" };
                case "suggest":
                {
                    if (!(app.Router.Current is BattleScreen sb2)) return new { error = "not in battle" };
                    var cmd = new Gaiden.Core.Bot.HeroBot(5).BestMove(sb2.State);
                    if (cmd == null) return new { error = "no move" };
                    var pv = BattleEngine.PreviewMove(sb2.State, cmd.HeroId, cmd.Path);
                    var hero = sb2.State.UnitById(cmd.HeroId);
                    return new { hero = cmd.HeroId, from = new[] { hero.Pos.X, hero.Pos.Y }, path = cmd.Path.Select(c => new[] { c.X, c.Y }).ToList(), pincers = pv.Pincers.Count,
                                 moved = pv.Positions.Where(kv => kv.Key != cmd.HeroId && sb2.State.UnitById(kv.Key).Pos != kv.Value).Select(kv => new { id = kv.Key, x = kv.Value.X, y = kv.Value.Y }).ToList() };
                }
                case "style":
                {
                    var e = app.Stage.Q(q["name"]);
                    if (e == null) return new { error = "not found" };
                    var rs = e.resolvedStyle;
                    string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGBA(c);
                    return new { border = Hex(rs.borderTopColor), borderWidth = rs.borderTopWidth, background = Hex(rs.backgroundColor), tint = Hex(rs.unityBackgroundImageTintColor), rs.opacity, rs.width, rs.height, children = e.childCount, text = (e as TextElement)?.text, key = e.viewDataKey };
                }
                case "child_style":
                {
                    // style of the Nth child of a named element (unit frames are the 2nd child of unit-<id>)
                    var e = app.Stage.Q(q["name"]);
                    int idx = (int)F(q["index"], 0);
                    if (e == null || idx >= e.childCount) return new { error = "not found" };
                    var c = e[idx];
                    if (q["child"] != null) { int ci = (int)F(q["child"], 0); if (ci >= c.childCount) return new { error = "not found" }; c = c[ci]; }
                    var rs = c.resolvedStyle;
                    string Hex(Color col) => "#" + ColorUtility.ToHtmlStringRGBA(col);
                    return new { border = Hex(rs.borderTopColor), background = Hex(rs.backgroundColor), children = c.childCount };
                }
                case "devcast":
                    if (app.Router.Current is BattleScreen dcs) { int caster = dcs.DevQueueCast((int)F(q["target"]), q["ability"], (int)F(q["ticks"], 30)); return new { ok = caster >= 0, caster }; }
                    return new { error = "not in battle" };
                case "devamp":
                    if (app.Router.Current is BattleScreen dab) { dab.State.Amplifier = new Coord((int)F(q["x"]), (int)F(q["y"])); dab.SyncAll(); return new { ok = true }; }
                    return new { error = "not in battle" };
                case "democaster":
                    if (app.Router.Current is BattleScreen democ) { democ.DemoCaster(); return new { ok = true }; }
                    return new { error = "not in battle" };
                case "democutin":
                    if (app.Router.Current is BattleScreen dc) { dc.DemoCutIn(); return new { ok = true }; }
                    return new { error = "not in battle" };
                case "demosurprise":
                    if (app.Router.Current is BattleScreen dsp) { dsp.DemoSurprise(q["speaker"] ?? "captain", q["line"] ?? "Reinforcements!"); return new { ok = true }; }
                    return new { error = "not in battle" };
                case "devhp":
                    if (app.Router.Current is BattleScreen dh) return new { ok = dh.DevHp((int)F(q["unit"]), (int)F(q["hp"], 1)) };
                    return new { error = "not in battle" };
                case "devweapon":
                    if (app.Router.Current is BattleScreen dw) return new { ok = dw.DevWeaponDurability((int)F(q["unit"]), (int)F(q["durability"], 1)) };
                    return new { error = "not in battle" };
                case "setbar":
                    if (app.Router.Current is BattleScreen setb) return new { ok = setb.DevSetBar((int)F(q["unit"]), (int)F(q["slot"]), q["entry"]) };
                    return new { error = "not in battle" };
                case "buff":
                    if (app.Router.Current is BattleScreen bb) return new { ok = bb.DevBuff((int)F(q["unit"]), q["id"]) };
                    return new { error = "not in battle" };
                case "gfx":
                    return new
                    {
                        api = SystemInfo.graphicsDeviceType.ToString(), SystemInfo.graphicsDeviceName, SystemInfo.graphicsShaderLevel,
                        colorSpace = QualitySettings.activeColorSpace.ToString(),
                        clear = app.Document.panelSettings.clearColor, camera = Camera.allCamerasCount,
                        uiShader = Shader.Find("Hidden/UIElements/Runtime") != null || Shader.Find("Hidden/UIE-Runtime") != null,
                        panelTarget = app.Document.panelSettings.targetTexture != null,
                    };
                case "shaders":
                    return Resources.FindObjectsOfTypeAll<Shader>().Where(sh => sh.name.Contains("UI") || sh.name.Contains("UIE") || sh.name.Contains("Text"))
                        .Select(sh => sh.name + (sh.isSupported ? "" : " [UNSUPPORTED]")).Distinct().ToList();
                case "panelclear":
                    app.Document.panelSettings.clearColor = q["on"] == "1";
                    return new { ok = true };
                case "camera":
                    foreach (var cam in Camera.allCameras) cam.enabled = q["on"] == "1";
                    return new { ok = true };
                case "banner":
                    return new { text = (app.Router.Current as BattleScreen)?.BannerText };
                case "press":
                    StartCoroutine(Press(F(q["x"]), F(q["y"]), F(q["ms"], 600) / 1000f, reply));
                    return Pending;
                case "release":
                    MouseTo(lastHeld, false);
                    StartCoroutine(Frames(3, () => reply(new { ok = true })));
                    return Pending;
                case "stagebounds":
                {
                    var sb = app.Stage.worldBound;
                    var root = app.Document.rootVisualElement.worldBound;
                    float k = Screen.width / Mathf.Max(1f, root.width);
                    return new { x = sb.x * k, y = sb.y * k, w = sb.width * k, h = sb.height * k, screen = new[] { Screen.width, Screen.height } };
                }
                case "resize":
                    Screen.SetResolution((int)F(q["w"], 585), (int)F(q["h"], 1266), FullScreenMode.Windowed);
                    return new { ok = true };
                case "quit": Application.Quit(); return new { ok = true };
            }
            return new { error = "unknown endpoint " + path };
        }

        // ── Input injection ──────────────────────────────────────────────────────

        /// <summary>Design pixels → screen pixels (Input System: origin bottom-left).</summary>
        private Vector2 ToScreen(float dx, float dy)
        {
            var root = GameApp.I.Document.rootVisualElement;
            var stage = GameApp.I.Stage;
            var panelPoint = stage.worldBound.position + new Vector2(dx, dy) * (stage.worldBound.width / GameApp.DesignWidth);
            float scale = Screen.width / Mathf.Max(1f, root.worldBound.width);
            return new Vector2(panelPoint.x * scale, Screen.height - panelPoint.y * scale);
        }

        private Vector2 DesignPoint(Vector2 panelPoint)
        {
            var stage = GameApp.I.Stage;
            float k = GameApp.DesignWidth / stage.worldBound.width;
            return (panelPoint - stage.worldBound.position) * k;
        }

        private void MouseTo(Vector2 screen, bool down)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen, buttons = (ushort)(down ? 1 : 0) });
        }

        private IEnumerator Tap(float x, float y, Action<object> reply)
        {
            var p = ToScreen(x, y);
            MouseTo(p, false); yield return null;
            MouseTo(p, true); yield return null; yield return null;
            MouseTo(p, false); yield return null; yield return null;
            reply(new { ok = true, x, y });
        }

        private Vector2 lastHeld;

        private static bool Opaque(VisualElement e)
        {
            for (var x = e; x != null; x = x.parent) if (x.resolvedStyle.opacity <= 0.01f) return false;
            return true;
        }

        private IEnumerator Frames(int n, Action done) { for (int i = 0; i < n; i++) yield return null; done(); }

        /// <summary>Press and hold at a design point for a while, then reply (button stays down until `release`).</summary>
        private IEnumerator Press(float x, float y, float seconds, Action<object> reply)
        {
            var p = ToScreen(x, y);
            lastHeld = p;
            MouseTo(p, false); yield return null;
            MouseTo(p, true);
            yield return new WaitForSecondsRealtime(seconds);
            reply(new { ok = true });
        }

        private IEnumerator Drag(List<Vector2> pts, float stepSeconds, Action<object> reply)
        {
            if (pts.Count == 0) { reply(new { error = "empty path" }); yield break; }
            MouseTo(ToScreen(pts[0].x, pts[0].y), false); yield return null;
            MouseTo(ToScreen(pts[0].x, pts[0].y), true); yield return null;
            for (int i = 1; i < pts.Count; i++)
            {
                var a = pts[i - 1]; var b = pts[i];
                int n = Mathf.Max(2, Mathf.CeilToInt((b - a).magnitude / 30f));
                for (int k = 1; k <= n; k++)
                {
                    var p = Vector2.Lerp(a, b, k / (float)n);
                    MouseTo(ToScreen(p.x, p.y), true);
                    yield return new WaitForSecondsRealtime(stepSeconds / n);
                }
            }
            var last = pts[pts.Count - 1];
            yield return null;
            lastHeld = ToScreen(last.x, last.y);
            if (holdDrag) { holdDrag = false; reply(new { ok = true, held = true }); yield break; }
            MouseTo(ToScreen(last.x, last.y), false);
            yield return null; yield return null;
            reply(new { ok = true, points = pts.Count });
        }

        private IEnumerator WaitIdle(float minSeconds, Action<object> reply)
        {
            float start = Time.unscaledTime;
            yield return null;
            while (Time.unscaledTime - start < 30f)
            {
                bool busy = GameApp.I.Router.Current is BattleScreen b && b.Busy;
                if (!busy && Time.unscaledTime - start >= minSeconds) break;
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.25f);
            reply(StateDump());
        }

        /// <summary>
        /// Frame recorder for art review: animations step a fixed amount per rendered frame (Tween.FixedStep) while every
        /// Nth frame is written to artifacts/&lt;name&gt;/f###.png, so slow PNG encoding never skips a beat.
        /// </summary>
        private IEnumerator Record(string name, int frames, int every, float step, Action<object> reply)
        {
            var dir = Path.Combine(artifacts, name);
            Directory.CreateDirectory(dir);
            foreach (var old in Directory.GetFiles(dir, "f*.png")) File.Delete(old);
            Tween.FixedStep = step;
            int written = 0;
            try
            {
                for (int i = 0; i < frames; i++)
                {
                    yield return new WaitForEndOfFrame();
                    if (i % every != 0) continue;
                    var tex = ScreenCapture.CaptureScreenshotAsTexture();
                    File.WriteAllBytes(Path.Combine(dir, $"f{written++:000}.png"), tex.EncodeToPNG());
                    Destroy(tex);
                }
            }
            finally { Tween.FixedStep = 0f; }
            reply(new { dir, written });
        }

        private IEnumerator Screenshot(string name, Action<object> reply)
        {
            yield return new WaitForEndOfFrame();
            var file = Path.Combine(artifacts, name.EndsWith(".png") ? name : name + ".png");
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Destroy(tex);
            reply(new { file });
        }

        // ── Introspection ────────────────────────────────────────────────────────

        private object StateDump()
        {
            var app = GameApp.I;
            var dump = new Dictionary<string, object>
            {
                ["screen"] = app.Router.Current?.Id,
                ["stack"] = app.Router.Stack.Select(s => s.Id).ToList(),
                ["screen_size"] = new[] { Screen.width, Screen.height },
                ["fps"] = Mathf.Round(1f / Mathf.Max(0.0001f, Time.smoothDeltaTime)),
            };
            if (app.Save != null)
                dump["save"] = new { app.Save.Gold, app.Save.HighestClearedStage, party = app.Save.Party, inventory = app.Save.Inventory, levels = app.Save.Roster.ToDictionary(h => h.ClassId, Progression.Level) };
            if (app.Router.Current is BattleScreen b && b.State != null)
            {
                var s = b.State;
                dump["battle"] = new
                {
                    phase = s.Phase.ToString(), wave = s.Wave, clock = s.Clock, busy = b.Busy, selected = b.SelectedHeroId, targeting = b.TargetingSlot, confirming = b.Confirming, record = b.LastRecordPath,
                    bank = s.Bank.ToString(), cutins = b.CutInsShown, coinsShown = b.CoinsShown, tallies = b.TalliesBurst, billboard = b.BillboardLines, amplifier = s.Amplifier.HasValue ? new[] { s.Amplifier.Value.X, s.Amplifier.Value.Y } : null,
                    units = s.Units.Where(u => u.Alive).Select(u => new
                    {
                        u.Id, cls = u.Def.Id, team = u.Team.ToString(), x = u.Pos.X, y = u.Pos.Y, u.W, u.H, u.Hp, u.MaxHp, u.Level,
                        weapon = u.WeaponId, weaponDurability = u.WeaponDurability, armor = u.ArmorId, armorDurability = u.ArmorDurability,
                        buffs = u.Buffs.Select(x => x.Def.Id + ":" + x.Remaining).ToList(),
                        acts_in = u.IsHero ? -1 : BattleEngine.ActionsUntilTurn(s, u.Id),
                        bar = u.IsHero ? Enumerable.Range(0, u.Bar.Count).Select(i => (u.Bar[i].AbilityId ?? u.Bar[i].ItemId ?? "-") + "=" + BattleEngine.GetSlotStatus(s, u.Id, i)).ToList() : null,
                    }).ToList(),
                    timeline = s.Timeline.Select(e => new { e.Id, kind = e.Kind.ToString(), owner = e.OwnerId, u = Math.Round(e.U, 3), e.AbilityId }).ToList(),
                    results = new { s.Results.Coins, s.Results.Xp, s.Results.ActionsTaken },
                };
            }
            return dump;
        }

        /// <summary>Layout audit of the current screen: elements with text that overflow, zero-size visible text, out-of-stage bounds.</summary>
        private object LayoutAudit()
        {
            var stage = GameApp.I.Stage;
            var sb = stage.worldBound;
            var problems = new List<string>();
            int labels = 0;
            foreach (var l in stage.Query<Label>().ToList())
            {
                if (l.resolvedStyle.display == DisplayStyle.None || string.IsNullOrEmpty(l.text) || l.resolvedStyle.opacity <= 0.01f) continue;
                labels++;
                var wb = l.worldBound;
                if (wb.width < 1 || wb.height < 1) { problems.Add($"zero-size label '{Trim(l.text)}'"); continue; }
                if (wb.xMin < sb.xMin - 2 || wb.xMax > sb.xMax + 2) problems.Add($"label off-stage horizontally '{Trim(l.text)}' x={DesignPoint(wb.position).x:0}..{DesignPoint(wb.max).x:0}");
                var size = l.MeasureTextSize(l.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined);
                float k = GameApp.DesignWidth / sb.width;
                if (l.resolvedStyle.whiteSpace == WhiteSpace.NoWrap && size.x > l.contentRect.width + 4 && l.contentRect.width > 0)
                    problems.Add($"text overflows ({size.x:0}>{l.contentRect.width:0}) '{Trim(l.text)}'");
            }
            return new { screen = GameApp.I.Router.Current?.Id, labels, problems };
        }

        private static string Trim(string s) => s.Length > 40 ? s.Substring(0, 40) + "…" : s;

        private static float F(string s, float def = 0f) => float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : def;

        private static List<Vector2> ParsePoints(string s)
        {
            var list = new List<Vector2>();
            if (string.IsNullOrEmpty(s)) return list;
            foreach (var part in s.Split(';'))
            {
                var xy = part.Split(',');
                if (xy.Length == 2) list.Add(new Vector2(F(xy[0]), F(xy[1])));
            }
            return list;
        }

        /// <summary>Strips any directory component/traversal from a request-supplied file name so it can't write or delete outside `artifacts`.</summary>
        private static string SafeName(string name, string fallback)
        {
            var n = Path.GetFileName(name);
            return string.IsNullOrEmpty(n) ? fallback : n;
        }

        private static List<Coord> ParseTiles(string s)
        {
            var list = new List<Coord>();
            foreach (var p in ParsePoints(s)) list.Add(new Coord((int)p.x, (int)p.y));
            return list;
        }
    }
}
#endif
