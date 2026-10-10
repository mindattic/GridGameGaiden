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
            Application.logMessageReceivedThreaded += (msg, stack, type) =>
            {
                if (type == LogType.Log && !msg.StartsWith("[")) return;
                lock (logs) { logs.Add($"{type}: {msg}" + (type == LogType.Exception || type == LogType.Error ? "\n" + stack : "")); if (logs.Count > 400) logs.RemoveAt(0); }
            };
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
                try { ctx = listener.GetContext(); } catch { return; }
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
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/json";
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.OutputStream.Close();
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
                case "screenshot":
                    StartCoroutine(Screenshot(q["name"] ?? DateTime.Now.ToString("HHmmss"), reply));
                    return Pending;
                case "tap":
                    StartCoroutine(Tap(F(q["x"]), F(q["y"]), reply));
                    return Pending;
                case "drag":
                    StartCoroutine(Drag(ParsePoints(q["path"]), F(q["step"], 0.03f), reply));
                    return Pending;
                case "click":
                {
                    var e = app.Stage.Q(q["name"]);
                    if (e == null) return new { error = $"no element named '{q["name"]}'" };
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
                    phase = s.Phase.ToString(), wave = s.Wave, clock = s.Clock, busy = b.Busy, selected = b.SelectedHeroId, targeting = b.TargetingSlot,
                    bank = s.Bank.ToString(),
                    units = s.Units.Where(u => u.Alive).Select(u => new
                    {
                        u.Id, cls = u.Def.Id, team = u.Team.ToString(), x = u.Pos.X, y = u.Pos.Y, u.W, u.H, u.Hp, u.MaxHp, u.Level,
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

        private static List<Coord> ParseTiles(string s)
        {
            var list = new List<Coord>();
            foreach (var p in ParsePoints(s)) list.Add(new Coord((int)p.x, (int)p.y));
            return list;
        }
    }
}
#endif
