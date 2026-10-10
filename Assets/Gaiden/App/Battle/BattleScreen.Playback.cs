using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gaiden.Core;
using Gaiden.Core.Battle;
using Gaiden.Core.Content;
using Gaiden.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gaiden.App
{
    public sealed partial class BattleScreen
    {
        private int chainCount;
        private float playStarted;

        /// <summary>
        /// Plays an event list back, one awaited beat each (bible: no instant anythings). The simulation
        /// already resolved everything; if any animation is skipped or interrupted, the final SyncAll
        /// snaps every view to the true state.
        /// </summary>
        private IEnumerator Play(List<BattleEvent> events, bool skipMovePrefix)
        {
            playing = true;
            playStarted = Time.unscaledTime;
            chainCount = 0;
            int i = 0;
            if (skipMovePrefix)
            {
                // The drag already showed these steps live; only replay their side effects.
                while (i < events.Count && (events[i] is ActionBeganEvent || events[i] is SteppedEvent || events[i] is DisplacedEvent || events[i] is StepBlockedEvent)) i++;
                foreach (var u in State.Units)
                    if (u.Alive && board.Units.TryGetValue(u.Id, out var v)) { board.Place(v, u.Pos); }
            }
            for (; i < events.Count; i++)
            {
                IEnumerator anim = null;
                try { anim = Animate(events[i]); }
                catch (System.Exception e) { Debug.LogException(e); }
                if (anim != null) yield return Safe(anim);
            }
            board.Lines.ClearLines();
            SyncAll();
            playing = false;
            if (State.IsOver && !ended) yield return EndBattle();
        }

        private float Fx => ReduceMotion ? 0f : 1f;

        /// <summary>Runs a (possibly nested) animation; an exception ends that beat but never the playback.</summary>
        private static IEnumerator Safe(IEnumerator e)
        {
            while (true)
            {
                object cur;
                try
                {
                    if (!e.MoveNext()) yield break;
                    cur = e.Current;
                }
                catch (System.Exception ex) { Debug.LogException(ex); yield break; }
                if (cur is IEnumerator nested) yield return Safe(nested);
                else yield return cur;
            }
        }

        private IEnumerator Animate(BattleEvent e)
        {
            switch (e)
            {
                case ActionBeganEvent a when a.ActorId >= 0 && a.Label != "Move":
                    ShowBanner($"{State.UnitById(a.ActorId)?.Name}: {a.Label}");
                    return Tween.Wait(0.15f);
                case ActionBeganEvent a when a.Label == "Shield":
                    ShowBanner("Shields up!");
                    App.Audio.Sfx("cast");
                    return Tween.Wait(0.2f);

                case SteppedEvent s: return StepAnim(s.UnitId, s.To, 0.16f, "move");
                case DisplacedEvent d: return StepAnim(d.UnitId, d.To, 0.14f, "slide");
                case TeleportedEvent t: return TeleportAnim(t);

                case PincerFormedEvent p: return PincerAnim(p);
                case RespiteEvent rs: return RespiteAnim(rs);
                case AttackEvent at: return AttackAnim(at);
                case DamagedEvent dm: return DamageAnim(dm);
                case HealedEvent h: return HealAnim(h);
                case DiedEvent d: return DeathAnim(d);
                case LootEvent l: return LootAnim(l);
                case AmplifierSpawnedEvent asp: return AmpSpawnAnim(asp);
                case AmplifierMovedEvent am: return AmpMoveAnim(am);
                case AmplifierUsedEvent au: return AmpUsedAnim(au);
                case OrbMintedEvent o: return OrbAnim(o);
                case OrbsSpentEvent _: SyncOrbs(); App.Audio.Sfx("cast", 0.6f); return Tween.Wait(0.12f);

                case TimelineAdvancedEvent ta: return TimelineAnim(ta);
                case PushedBackEvent pb: return PushAnim(pb);
                case HastenedEvent hs: return HastenAnim(hs);
                case TurnStartedEvent ts: return TurnAnim(ts);
                case TurnSkippedEvent sk: return FloatText(sk.UnitId, sk.Reason == "asleep" ? "Zzz…" : "…", new Color(0.7f, 0.8f, 1f), 0.35f);

                case CastQueuedEvent cq: return CastQueuedAnim(cq);
                case CastStaggeredEvent cs: return FloatText(cs.CasterId, cs.Resisted ? "Steady!" : "Stagger!", new Color(1f, 0.85f, 0.4f), 0.25f);
                case CastCancelledEvent cc: SyncTimeline(null); board.Lines.ClearLines(); board.ClearMarks(); return FloatText(cc.CasterId, cc.Reason == "interrupted" ? "Interrupted!" : "Fizzled", new Color(1f, 0.6f, 0.3f), 0.35f);
                case ClutchEvent cl: return ClutchAnim(cl);
                case AbilityResolvedEvent ar: return AbilityAnim(ar);
                case AbilityRefusedEvent rf: App.Audio.Sfx("ui.error"); ShowBanner(rf.Reason); return null;
                case ItemUsedEvent iu: ShowBanner($"{App.Content.TryItem(iu.ItemId)?.Name} ({iu.ChargesLeft} left)"); return Tween.Wait(0.1f);

                case BuffAppliedEvent ba: return BuffAnim(ba);
                case BuffRemovedEvent br: RefreshUnit(br.UnitId); return br.Reason == "steam" ? FloatText(br.UnitId, "Steam!", Color.white, 0.25f) : null;
                case BuffTickedEvent bt: RefreshUnit(bt.UnitId); return null;
                case AnnounceEvent an: ShowBanner(an.Text); return Tween.Wait(0.25f);
                case ScannedEvent sc: return ScanAnim(sc);
                case TrapPlacedEvent tp: board.SyncTraps(State); App.Audio.Sfx("trap"); return FloatText(tp.ById, "Snare!", new Color(0.8f, 0.4f, 1f), 0.3f);
                case TrapTriggeredEvent tt: board.SyncTraps(State); App.Audio.Sfx("trap"); return FloatText(tt.UnitId, "Trap!", new Color(0.8f, 0.4f, 1f), 0.25f);
                case WeaponWornEvent _: return null;
                case WeaponShatteredEvent ws: App.Audio.Sfx("shatter"); return FloatText(ws.UnitId, "SHATTER!", new Color(1f, 0.7f, 0.3f), 0.5f, 80);
                case ArmorBrokenEvent ab: App.Audio.Sfx("shatter", 0.7f); return FloatText(ab.UnitId, "Armor broke!", new Color(1f, 0.6f, 0.4f), 0.4f, 64);
                case WeaponSwappedEvent sw: App.Audio.Sfx("dash"); SyncBar(); return FloatText(sw.UnitId, "⭾ " + (App.Content.TryItem(sw.ToWeapon)?.Name ?? "Swap"), new Color(0.85f, 0.9f, 1f), 0.3f, 54);
                case BossPhaseEvent bp: return BossAnim(bp);
                case WaveStartedEvent wv: return WaveAnim(wv);
                case WaveClearedEvent wc: ShowBanner("Wave cleared!"); return Tween.Wait(0.4f);
                case BattleEndedEvent be: ShowBanner(be.Victory ? "VICTORY!" : be.Escaped ? "ESCAPED" : "DEFEAT"); App.Audio.Sfx(be.Victory ? "victory" : be.Escaped ? "dash" : "defeat"); return Tween.Wait(1.0f);
                case RevivedEvent rv: return ReviveAnim(rv);
                case PlayerControlEvent _: ShowBanner(""); SyncAll(); return null;
            }
            return null;
        }

        // ── Individual beats ──────────────────────────────────────────────────────

        private IEnumerator StepAnim(int unitId, Coord to, float seconds, string sfx)
        {
            if (!board.Units.TryGetValue(unitId, out var v)) yield break;
            var from = BoardView.PosOf(v.Tile);
            v.Tile = to;
            App.Audio.Sfx(sfx, 0.6f);
            yield return Tween.Move(v.Root, from, BoardView.PosOf(to), seconds);
        }

        private IEnumerator TeleportAnim(TeleportedEvent t)
        {
            if (!board.Units.TryGetValue(t.UnitId, out var v)) yield break;
            App.Audio.Sfx("magic");
            yield return Tween.Fade(v.Root, 1, 0, 0.15f);
            board.Place(v, t.To);
            yield return Tween.Fade(v.Root, 0, 1, 0.15f);
        }

        private IEnumerator PincerAnim(PincerFormedEvent p)
        {
            chainCount++;
            var a = State.UnitById(p.Attacker1); var b = State.UnitById(p.Attacker2);
            if (a == null || b == null) yield break;
            board.Lines.ClearLines();
            var color = p.Team == Team.Hero ? new Color(1f, 0.85f, 0.3f, 0.95f) : new Color(1f, 0.3f, 0.3f, 0.95f);
            // The group (Hero, foe…, Hero) gets an outline that draws itself around it; supporters link edge to edge.
            var group = new List<(Coord, int, int)> { (a.Pos, a.W, a.H), (b.Pos, b.W, b.H) };
            foreach (var oid in p.Opponents) if (State.UnitById(oid) is Unit o) group.Add((o.Pos, o.W, o.H));
            var rect = BoardView.GroupRect(group);
            var links = new List<(Vector2, Vector2)>();
            foreach (var sid in p.Supporters)
            {
                var s = State.UnitById(sid);
                if (s == null) continue;
                var partner = s.IsAdjacentTo(a) ? a : b;
                links.Add(BoardView.EdgeLink(s.Pos, partner.Pos));
            }
            App.StartCoroutine(Tween.Run(ReduceMotion ? 0.05f : 0.24f, k =>
            {
                board.Lines.ClearLines();
                board.Lines.Outline(rect, color, 9f, k);
                if (k >= 0.6f) foreach (var (la, lb) in links) board.Lines.Add(la, Vector2.Lerp(la, lb, (k - 0.6f) / 0.4f), new Color(0.4f, 0.8f, 1f, 0.95f), 12f);
            }));
            App.Audio.Sfx("pincer", 1f, 1f + 0.08f * (chainCount - 1));
            string label = p.Team == Team.Hero ? (chainCount > 1 ? $"CHAIN ×{chainCount}!" : "PINCER!") : "Enemy pincer!";
            ShowBanner(label);
            var mid = (BoardView.CenterOf(a.Pos) + BoardView.CenterOf(b.Pos)) / 2f;
            var f = board.Float(label, mid, color, 96);
            App.StartCoroutine(FloatAway(f, 0.7f));
            if (board.Units.TryGetValue(a.Id, out var av)) App.StartCoroutine(Tween.Pop(av.Root, 1.15f, 0.2f));
            if (board.Units.TryGetValue(b.Id, out var bv)) App.StartCoroutine(Tween.Pop(bv.Root, 1.15f, 0.2f));
            // The close-up plays on the first link of an action; chained links keep the quick beat.
            if (chainCount == 1 && CutInsEnabled && !ReduceMotion) yield return PincerCutIn(a, b, p.Team);
            else yield return Tween.Wait(0.32f);
        }

        // ── Pincer cut-in (ported from GridGame2026 PortraitManager / Portrait2DInstance, US-145) ──
        // Both attackers' portraits sweep past in close-up lanes flanking the attacked line on the old
        // slide curve (fast in, slight overshoot, drift back, fast out) with hand-held sway, while the
        // board "camera" pushes in on the pincer and settles back afterwards.

        public int CutInsShown { get; private set; }
        private bool CutInsEnabled => App.Save?.Settings.PincerCutIns ?? true;

        private static readonly AnimationCurve CutInCurve = new AnimationCurve(
            new Keyframe(0.0f, -1.0f, 0f, 0f, 0f, 0f),
            new Keyframe(0.8f, 0.05202637f, 0f, 0f, 0.33333334f, 0.70263505f),
            new Keyframe(1.2f, -0.05f, 0f, 0f, 0.33333334f, 0.33322528f),
            new Keyframe(1.993103f, 1.0f, 0f, 0f, 0f, 0f));
        private const float CutInSpeed = 1.75f;
        private const float CutInStagger = 0.14f;
        private static float CutInSeconds => 1.993103f / CutInSpeed;

        /// <summary>Debug/automation: play the cut-in on the first two living heroes.</summary>
        public void DemoCutIn()
        {
            var heroes = State.Units.Where(u => u.IsHero && u.Alive).Take(2).ToList();
            if (heroes.Count == 2) App.StartCoroutine(PincerCutIn(heroes[0], heroes[1], Team.Hero));
        }

        private sealed class CutInPortrait
        {
            public VisualElement El; public bool Horizontal; public float Lane; public float Dir; public float Delay;
            public float Distance; public float W, H; public float Seed;
        }

        private IEnumerator PincerCutIn(Unit a, Unit b, Team team)
        {
            CutInsShown++;
            var layer = new VisualElement { name = "cutin", pickingMode = PickingMode.Ignore };
            layer.style.position = Position.Absolute;
            layer.style.left = 0; layer.style.top = 0; layer.style.right = 0; layer.style.bottom = 0;
            layer.style.overflow = Overflow.Hidden;
            var dim = new VisualElement { pickingMode = PickingMode.Ignore };
            dim.style.position = Position.Absolute;
            dim.style.left = 0; dim.style.top = 0; dim.style.right = 0; dim.style.bottom = 0;
            dim.style.backgroundColor = team == Team.Hero ? new Color(0.02f, 0.03f, 0.08f) : new Color(0.12f, 0.0f, 0.02f);
            dim.style.opacity = 0f;
            layer.Add(dim);
            App.Overlay.Add(layer);

            var origin = new Vector2(BoardLeft, BoardTop);
            var mid = origin + (BoardView.CenterOf(a.Pos, a.W, a.H) + BoardView.CenterOf(b.Pos, b.W, b.H)) / 2f;
            bool rowLine = a.Pos.Y == b.Pos.Y;
            const float StageW = 1170f, StageH = 2532f;

            // Entry pattern (old US-145): counter sweep, mirrored, same-side chase, staggered one-two.
            float d1 = Random.value < 0.5f ? 1f : -1f, d2 = -d1, delay2 = 0f;
            switch (Random.Range(0, 4))
            {
                case 2: d2 = d1; delay2 = CutInStagger; break;
                case 3: delay2 = CutInStagger; break;
            }

            var shots = new List<CutInPortrait>();
            Unit[] who = { a, b };
            for (int i = 0; i < 2; i++)
            {
                var tex = Art.Portrait(who[i].Def.Id);
                float aspect = tex != null && tex.height > 0 ? (float)tex.width / tex.height : 0.7f;
                float h = rowLine ? 760f : 1040f, w = h * aspect;
                if (!rowLine && w > 540f) { w = 540f; h = w / aspect; }
                var el = new VisualElement { name = $"cutin-{who[i].Id}", pickingMode = PickingMode.Ignore };
                el.style.position = Position.Absolute;
                el.style.width = w; el.style.height = h;
                el.style.backgroundImage = new StyleBackground(tex);
                el.style.left = -w * 2; el.style.top = -h * 2;
                layer.Add(el);
                shots.Add(new CutInPortrait
                {
                    El = el, Horizontal = rowLine, Dir = i == 0 ? d1 : d2, Delay = i == 0 ? 0f : delay2, W = w, H = h,
                    Distance = rowLine ? StageW / 2f + w / 2f + 2f : StageH / 2f + h / 2f + 2f, Seed = Random.Range(0f, 100f),
                });
            }
            // Lanes flank the attacked line without covering it; at the board edge both shift into the open space.
            if (rowLine)
            {
                float gap = BoardView.Tile / 2f + 20f;
                float la = mid.y - gap - shots[0].H / 2f, lb = mid.y + gap + shots[1].H / 2f;
                float shift = Mathf.Max(0f, shots[0].H / 2f - la) - Mathf.Max(0f, lb + shots[1].H / 2f - StageH);
                shots[0].Lane = la + shift; shots[1].Lane = lb + shift;
            }
            else
            {
                float gap = BoardView.Tile / 2f + 10f;
                float la = mid.x - gap - shots[0].W / 2f, lb = mid.x + gap + shots[1].W / 2f;
                float shift = Mathf.Max(0f, shots[0].W / 2f - la) - Mathf.Max(0f, lb + shots[1].W / 2f - StageW);
                shots[0].Lane = la + shift; shots[1].Lane = lb + shift;
            }

            App.Audio.Sfx("dash", 0.7f, 0.9f);
            if (delay2 > 0f) App.StartCoroutine(DelayedSfx("dash", delay2, 1.05f));
            var cam = boardHost;
            cam.style.transformOrigin = new TransformOrigin(mid.x - origin.x, mid.y - origin.y);
            float total = CutInSeconds + delay2;
            float camSeed = Random.Range(0f, 100f);
            yield return Tween.Run(total, k =>
            {
                float elapsed = k * total;
                float fade = Mathf.Clamp01(Mathf.Min(elapsed / 0.15f, (total - elapsed) / 0.2f));
                dim.style.opacity = 0.5f * fade;
                // Board camera: slow push-in on the pincer with a hand-held drift.
                float zoom = 1f + 0.085f * Tween.EaseOut(Mathf.Clamp01(elapsed / 0.6f));
                float sx = (Mathf.PerlinNoise(camSeed, elapsed * 1.6f) - 0.5f) * 18f;
                float sy = (Mathf.PerlinNoise(camSeed + 7f, elapsed * 1.6f) - 0.5f) * 18f;
                cam.style.scale = new Scale(new Vector2(zoom, zoom));
                cam.style.translate = new Translate(sx, sy);
                cam.style.rotate = new Rotate((Mathf.PerlinNoise(camSeed + 13f, elapsed * 1.1f) - 0.5f) * 1.2f);
                foreach (var s in shots)
                {
                    float t = Mathf.Max(0f, elapsed - s.Delay) * CutInSpeed;
                    float v = t <= 0f ? -1f : CutInCurve.Evaluate(Mathf.Min(t, 1.993103f));
                    float along = s.Dir * s.Distance * v;
                    float hx = (Mathf.PerlinNoise(s.Seed, elapsed * 2.4f) - 0.5f) * 30f;
                    float hy = (Mathf.PerlinNoise(s.Seed + 3f, elapsed * 2.4f) - 0.5f) * 30f;
                    float cx = s.Horizontal ? StageW / 2f + along : s.Lane;
                    float cy = s.Horizontal ? s.Lane : StageH / 2f + along;
                    float push = 1f + 0.07f * Mathf.Clamp01(t / 1.993103f);
                    s.El.style.left = cx - s.W / 2f + hx;
                    s.El.style.top = cy - s.H / 2f + hy;
                    s.El.style.scale = new Scale(new Vector2(push, push));
                    s.El.style.rotate = new Rotate((Mathf.PerlinNoise(s.Seed + 9f, elapsed * 1.7f) - 0.5f) * 4f);
                }
            });
            layer.RemoveFromHierarchy();
            App.StartCoroutine(SettleCamera(cam));
        }

        private IEnumerator DelayedSfx(string cue, float delay, float pitch)
        {
            yield return Tween.Wait(delay);
            App.Audio.Sfx(cue, 0.7f, pitch);
        }

        private static IEnumerator SettleCamera(VisualElement cam)
        {
            float s0 = cam.resolvedStyle.scale.value.x;
            var t0 = cam.resolvedStyle.translate;
            float r0 = cam.resolvedStyle.rotate.angle.value;
            yield return Tween.Run(0.3f, k =>
            {
                float e = Tween.EaseInOut(k);
                float s = Mathf.Lerp(s0, 1f, e);
                cam.style.scale = new Scale(new Vector2(s, s));
                cam.style.translate = new Translate(t0.x * (1f - e), t0.y * (1f - e));
                cam.style.rotate = new Rotate(r0 * (1f - e));
            });
            cam.style.scale = new Scale(Vector2.one);
            cam.style.translate = new Translate(0, 0);
            cam.style.rotate = new Rotate(0);
        }

        private IEnumerator RespiteAnim(RespiteEvent rs)
        {
            foreach (var id in new[] { rs.Attacker1, rs.Attacker2 })
                if (board.Units.TryGetValue(id, out var v))
                    App.StartCoroutine(Tween.Run(0.45f, k => v.Root.style.rotate = new Rotate(k >= 1f ? 0f : 360f * Tween.EaseInOut(k))));
            yield return FloatText(rs.Attacker1, "Respite", new Color(0.8f, 0.95f, 1f), 0.45f, 56);
        }

        private IEnumerator AttackAnim(AttackEvent at)
        {
            var a = State.UnitById(at.AttackerId); var t = State.UnitById(at.TargetId);
            if (a == null || t == null || !board.Units.TryGetValue(a.Id, out var av)) yield break;
            Vector2 dir = (BoardView.CenterOf(t.Pos, t.W, t.H) - BoardView.CenterOf(a.Pos, a.W, a.H)).normalized;
            if (at.IsRanged)
            {
                // A hexed bolt flies down the lane.
                var from = BoardView.CenterOf(a.Pos, a.W, a.H); var to = BoardView.CenterOf(t.Pos, t.W, t.H);
                App.Audio.Sfx("magic", 0.7f);
                yield return Tween.Run(0.22f, k => { board.Lines.ClearLines(); board.Lines.Dot(ProjectilePoint(ProjectileMotion.Twist, from, to, k), 22f, new Color(0.7f, 0.35f, 1f)); });
                board.Lines.ClearLines();
                yield break;
            }
            float lunge = (at.IsSupport ? 18f : 34f) * Fx + 6f;
            yield return Tween.Run(0.07f, k => av.Root.style.translate = new Translate(dir.x * lunge * k, dir.y * lunge * k));
            App.StartCoroutine(Tween.Run(0.12f, k => av.Root.style.translate = new Translate(dir.x * lunge * (1 - k), dir.y * lunge * (1 - k))));
        }

        private IEnumerator DamageAnim(DamagedEvent d)
        {
            var t = State.UnitById(d.TargetId);
            if (t == null || !board.Units.TryGetValue(t.Id, out var v)) yield break;
            var at = BoardView.CenterOf(v.Tile, v.W, v.H);
            if (d.Hit == HitKind.Miss)
            {
                App.Audio.Sfx("miss");
                App.StartCoroutine(FloatAway(board.Float("MISS", at, new Color(0.8f, 0.85f, 1f), 56), 0.6f));
                yield return Tween.Wait(0.12f);
                yield break;
            }
            bool crit = d.Hit == HitKind.Critical;
            App.Audio.Sfx(d.SourceId < 0 ? "debuff" : crit ? "crit" : SfxFor(d.Type));
            var src = State.UnitById(d.SourceId);
            Feed($"{(d.SourceId < 0 ? "☠" : "†")} {(src != null ? src.Name + " → " : "")}{t.Name}: {d.Amount}{(crit ? " CRIT" : "")}{(d.Note != null ? " (" + d.Note + ")" : "")}", t.IsHero ? new Color(1f, 0.65f, 0.6f) : new Color(1f, 1f, 1f, 0.9f));
            if (d.Amount > 0 && !ReduceMotion)
            {
                if (d.Type == DamageType.Physical || d.SourceId < 0) Sparks(at, crit ? 12 : 7, crit ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.95f, 0.85f));
                else App.StartCoroutine(ElementBurst(at, d.Type, crit));
            }
            string text = d.Note == "Armored!" ? "ARMORED" : (crit ? $"{d.Amount}!" : d.Amount.ToString());
            var color = d.Note == "Armored!" ? new Color(0.7f, 0.7f, 0.75f) : crit ? new Color(1f, 0.85f, 0.2f) : t.IsHero ? new Color(1f, 0.5f, 0.45f) : Color.white;
            App.StartCoroutine(FloatAway(board.Float(text, at, color, crit ? 100 : 72), 0.75f));
            v.SetHp(d.HpAfter, t.MaxHp);
            v.Frame.style.unityBackgroundImageTintColor = new Color(1f, 0.4f, 0.4f);
            v.Art.style.unityBackgroundImageTintColor = new Color(1f, 0.55f, 0.55f);
            App.StartCoroutine(Tween.Shake(v.Root, 14f * Fx, 0.18f));
            if (crit || d.Amount >= t.MaxHp * 0.25f) App.StartCoroutine(Tween.Shake(shake, (crit ? 16f : 9f) * Fx, 0.2f));
            yield return Tween.Wait(crit ? 0.16f : 0.09f); // hit-stop
            v.Art.style.unityBackgroundImageTintColor = Color.white;
            v.Frame.style.unityBackgroundImageTintColor = Color.white;
        }

        private static string SfxFor(DamageType t)
        {
            switch (t)
            {
                case DamageType.Fire: return "fire";
                case DamageType.Ice: return "ice";
                case DamageType.Lightning: return "lightning";
                case DamageType.Poison: return "poison";
                case DamageType.Physical: return "hit";
                default: return "magic";
            }
        }

        private IEnumerator HealAnim(HealedEvent h)
        {
            var t = State.UnitById(h.TargetId);
            if (t == null || !board.Units.TryGetValue(t.Id, out var v)) yield break;
            Feed($"✚ {t.Name} +{h.Amount} HP", new Color(0.5f, 1f, 0.6f));
            App.Audio.Sfx("heal");
            if (!ReduceMotion) App.StartCoroutine(HealBurst(BoardView.CenterOf(v.Tile, v.W, v.H)));
            v.SetHp(h.HpAfter, t.MaxHp);
            v.SetGlow(new Color(0.4f, 1f, 0.5f), 0.8f);
            App.StartCoroutine(FloatAway(board.Float("+" + h.Amount, BoardView.CenterOf(v.Tile), new Color(0.45f, 1f, 0.55f), 72), 0.8f));
            yield return Tween.Wait(0.2f);
            v.SetGlow(Color.white, 0f);
        }

        private IEnumerator DeathAnim(DiedEvent d)
        {
            if (!board.Units.TryGetValue(d.UnitId, out var v)) yield break;
            var du = State.UnitById(d.UnitId);
            if (du != null) Feed($"★ {du.Name} is defeated", du.IsHero ? new Color(1f, 0.45f, 0.4f) : new Color(1f, 0.85f, 0.4f));
            App.Audio.Sfx("death");
            yield return Tween.Run(0.3f, k =>
            {
                v.Root.style.opacity = 1 - k;
                v.Root.style.scale = new Scale(Vector2.one * (1f - 0.4f * k));
                v.Root.style.rotate = new Rotate(Fx * 12f * k);
            });
            v.Root.RemoveFromHierarchy();
            v.Root.style.rotate = new Rotate(0);
            v.Dead = true;
        }

        private IEnumerator LootAnim(LootEvent l)
        {
            if (l.Coins <= 0 && l.Items.Count == 0) { SyncCoins(); yield break; }
            var at = BoardView.CenterOf(board.Units.TryGetValue(l.FromUnitId, out var v) ? v.Tile : new Coord(2, 2));
            if (l.Coins > 0)
            {
                App.StartCoroutine(FloatAway(board.Float($"+{l.Coins} G", at + new Vector2(0, -20), new Color(1f, 0.85f, 0.35f), 52), 0.9f));
                App.StartCoroutine(CoinBurst(at, l.Coins));
            }
            foreach (var it in l.Items)
            {
                var name = App.Content.TryItem(it.Item)?.Name ?? it.Item;
                App.StartCoroutine(FloatAway(board.Float($"{name} ×{it.Count}", at + new Vector2(0, 40), new Color(0.7f, 1f, 0.8f), 40), 1.1f));
            }
            App.Audio.Sfx("ui.buy", 0.6f);
            yield return Tween.Wait(0.08f);
        }

        private IEnumerator AmpSpawnAnim(AmplifierSpawnedEvent e)
        {
            board.SyncAmplifier(e.At);
            App.Audio.Sfx("orb", 0.8f, 0.7f);
            Feed("A Power Amplifier appeared — pincer from its tile", new Color(1f, 0.85f, 0.4f));
            yield return Tween.Pop(board.Amp, 1.6f, 0.3f);
        }

        private IEnumerator AmpMoveAnim(AmplifierMovedEvent e)
        {
            var from = BoardView.CenterOf(e.From); var to = BoardView.CenterOf(e.To);
            App.Audio.Sfx("slide", 0.6f, 1.3f);
            yield return Tween.Run(0.22f, k => board.PlaceAmp(Vector2.Lerp(from, to, Tween.EaseOut(k))));
            board.SyncAmplifier(e.To);
            var by = State.UnitById(e.ByUnitId);
            if (by != null) Feed($"{by.Name} shoved the amplifier", new Color(1f, 0.6f, 0.4f));
        }

        private IEnumerator AmpUsedAnim(AmplifierUsedEvent e)
        {
            var at = BoardView.CenterOf(e.At);
            App.Audio.Sfx("crit", 0.9f);
            yield return Tween.Run(0.3f, k => { board.Lines.Rings.Clear(); board.Lines.Ring(at, 40f + 160f * k, new Color(1f, 0.8f, 0.3f, 1f - k), 12f); });
            board.Lines.Rings.Clear(); board.Lines.MarkDirtyRepaint();
            board.SyncAmplifier(null);
            yield return FloatText(e.HeroId, "AMPLIFIED!", new Color(1f, 0.85f, 0.3f), 0.2f, 70);
        }

        /// <summary>Gold shown on the HUD: the party's purse plus what this battle has dropped so far.</summary>
        private int shownCoins = -1;
        public int CoinsShown => shownCoins;

        private void SyncCoins(int? value = null)
        {
            shownCoins = value ?? ((App.Save?.Gold ?? 0) + State.Results.Coins);
            coinLabel.text = $"{shownCoins} G";
        }

        /// <summary>
        /// The enemy "explodes" into gold (as in GridGame2026): coins burst out and bounce, then fly to the gold counter
        /// in the top-right, which counts up as each one lands. Runs alongside the next beats; never blocks play.
        /// </summary>
        private IEnumerator CoinBurst(Vector2 boardAt, int coins)
        {
            int n = Mathf.Clamp(coins, 3, ReduceMotion ? 4 : 12);
            int start = shownCoins < 0 ? (App.Save?.Gold ?? 0) + State.Results.Coins - coins : shownCoins;
            Vector2 from = App.Overlay.WorldToLocal(board.Root.LocalToWorld(boardAt));
            var wb = coinLabel.worldBound;
            Vector2 to = App.Overlay.WorldToLocal(new Vector2(wb.xMax - 40f, wb.center.y));
            int landed = 0;
            for (int i = 0; i < n; i++)
            {
                var coin = new VisualElement { name = "coin", pickingMode = PickingMode.Ignore };
                coin.style.position = Position.Absolute;
                coin.style.width = coin.style.height = 34;
                coin.style.backgroundImage = new StyleBackground(Art.Orb);
                coin.style.unityBackgroundImageTintColor = new Color(1f, 0.82f, 0.25f);
                App.Overlay.Add(coin);
                float ang = Random.Range(-Mathf.PI * 0.95f, -Mathf.PI * 0.05f), speed = Random.Range(260f, 520f);
                var vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * speed;
                float floor = from.y + Random.Range(30f, 70f), delay = i * 0.035f;
                int share = coins / n + (i < coins % n ? 1 : 0);
                App.StartCoroutine(OneCoin(coin, from, vel, floor, to, delay, () =>
                {
                    landed++;
                    SyncCoins(Mathf.Min(start + coins, shownCoins + share));
                    if (landed == n) SyncCoins();
                    App.Audio.Sfx("ui.buy", 0.35f, 1.1f + landed * 0.03f);
                    App.StartCoroutine(Tween.Pop(coinLabel, 1.15f, 0.08f));
                }));
            }
            yield break;
        }

        private IEnumerator OneCoin(VisualElement coin, Vector2 from, Vector2 vel, float floor, Vector2 to, float delay, System.Action arrive)
        {
            yield return Tween.Wait(delay);
            // Burst: ballistic arc with one bounce, ~0.45 s.
            var pos = from; float t = 0f; bool bounced = false;
            while (t < 0.45f)
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime * Tween.Speed, 0.05f);
                t += dt;
                vel.y += 1800f * dt;
                pos += vel * dt;
                if (pos.y > floor && vel.y > 0f && !bounced) { pos.y = floor; vel.y *= -0.45f; vel.x *= 0.7f; bounced = true; }
                coin.style.left = pos.x - 17; coin.style.top = pos.y - 17;
                yield return null;
            }
            // Fly to the counter, accelerating, shrinking a little.
            var start = pos;
            yield return Tween.Run(Random.Range(0.32f, 0.45f), k =>
            {
                float e = k * k;
                var p = Vector2.Lerp(start, to, e) + Vector2.up * (-80f * Mathf.Sin(k * Mathf.PI));
                coin.style.left = p.x - 17; coin.style.top = p.y - 17;
                coin.style.scale = new Scale(Vector2.one * (1f - 0.35f * k));
            });
            coin.RemoveFromHierarchy();
            arrive();
        }

        private IEnumerator OrbAnim(OrbMintedEvent o)
        {
            App.Audio.Sfx("orb", 0.7f, 1f + Random.Range(-0.05f, 0.1f));
            var src = State.UnitById(o.SourceUnitId);
            int index = o.Landed ? State.Bank.Count - 1 : orbViews.Count - 1;
            // Fly a small orb from the source to its slot in the line.
            var orb = new VisualElement { pickingMode = PickingMode.Ignore };
            orb.style.position = Position.Absolute;
            orb.style.width = orb.style.height = 60;
            orb.style.backgroundImage = new StyleBackground(Art.Orb);
            orb.style.unityBackgroundImageTintColor = Art.Mana;
            App.Overlay.Add(orb);
            Vector2 from = src != null && board.Units.TryGetValue(src.Id, out var v)
                ? new Vector2(BoardLeft, BoardTop) + BoardView.CenterOf(v.Tile) : new Vector2(585, 1200);
            var slot = orbViews[Mathf.Clamp(index, 0, orbViews.Count - 1)];
            Vector2 to = slot.worldBound.center - App.Stage.worldBound.position;
            if (float.IsNaN(to.x)) to = new Vector2(585, 2210);
            yield return Tween.Run(0.22f, k =>
            {
                var p = Vector2.Lerp(from, to, Tween.EaseInOut(k)) + Vector2.up * (-140f * Mathf.Sin(k * Mathf.PI));
                orb.style.left = p.x - 30; orb.style.top = p.y - 30;
                if (!o.Landed) orb.style.opacity = 1f - k;
            });
            orb.RemoveFromHierarchy();
            SyncOrbs();
            if (o.Landed && index >= 0 && index < orbViews.Count) App.StartCoroutine(Tween.Pop(orbViews[index], 1.3f, 0.15f));
        }

        private IEnumerator TimelineAnim(TimelineAdvancedEvent ta)
        {
            var starts = new Dictionary<int, float>();
            foreach (var kv in timelineIconViews)
                starts[kv.Key] = (kv.Value.resolvedStyle.left + kv.Value.resolvedStyle.width / 2f) / TimelineWidth;
            // Create icons for entries that appeared, then glide everything to the snapshot.
            foreach (var e in State.Timeline)
                if (!timelineIconViews.ContainsKey(e.Id)) { var icon = MakeTimelineIcon(e); timelineIconViews[e.Id] = icon; timelineIcons.Add(icon); starts[e.Id] = ta.U.TryGetValue(e.Id, out var u0) ? u0 : e.U; }
            float seconds = Mathf.Clamp(ta.Ticks / 40f, 0.18f, 0.45f);
            yield return Tween.Run(seconds, k =>
            {
                var at = new Dictionary<int, float>();
                foreach (var kv in ta.U) at[kv.Key] = Mathf.Lerp(starts.TryGetValue(kv.Key, out var v) ? v : kv.Value, kv.Value, k);
                SyncTimeline(at); // lanes, overtakes, badges and cast rings follow the glide
            }, Tween.EaseInOut);
            SyncTimeline(ta.U);
        }

        /// <summary>Glide one icon between two u positions while everything else holds still.</summary>
        private IEnumerator GlideOne(int entryId, float fromU, float toU, float seconds)
        {
            var hold = new Dictionary<int, float>();
            foreach (var e in State.Timeline) hold[e.Id] = lastAt != null && lastAt.TryGetValue(e.Id, out var u) ? u : e.U;
            yield return Tween.Run(seconds, k => { hold[entryId] = Mathf.Lerp(fromU, toU, Tween.EaseOut(k)); SyncTimeline(hold); });
        }

        private IEnumerator PushAnim(PushedBackEvent pb)
        {
            var entry = State.TurnEntryOf(pb.UnitId);
            App.Audio.Sfx("slide", 0.8f, 0.8f);
            if (entry != null && timelineIconViews.ContainsKey(entry.Id))
                App.StartCoroutine(GlideOne(entry.Id, pb.FromU, pb.ToU, 0.3f));
            yield return FloatText(pb.UnitId, "Pushback!", new Color(0.5f, 0.85f, 1f), 0.12f, 46);
        }

        private IEnumerator HastenAnim(HastenedEvent h)
        {
            if (timelineIconViews.ContainsKey(h.EntryId))
                App.StartCoroutine(GlideOne(h.EntryId, h.FromU, h.ToU, 0.3f));
            yield return FloatText(h.UnitId, "Haste!", new Color(1f, 0.9f, 0.4f), 0.2f, 50);
        }

        private IEnumerator TurnAnim(TurnStartedEvent ts)
        {
            var u = State.UnitById(ts.UnitId);
            if (u != null) Feed($"▶ {u.Name}'s turn", new Color(1f, 0.6f, 0.5f));
            App.Audio.Sfx("turn", 0.7f);
            var entry = State.TurnEntryOf(ts.UnitId);
            if (entry != null && timelineIconViews.TryGetValue(entry.Id, out var icon))
            {
                App.StartCoroutine(Tween.Pop(icon, 1.35f, 0.25f));
                var reset = new Dictionary<int, float>();
                foreach (var e in State.Timeline) reset[e.Id] = lastAt != null && lastAt.TryGetValue(e.Id, out var lu) ? lu : e.U;
                reset[entry.Id] = 0f;
                SyncTimeline(reset);
            }
            if (u != null && board.Units.TryGetValue(u.Id, out var v))
            {
                v.SetGlow(new Color(1f, 0.35f, 0.3f), 0.85f);
                ShowBanner($"{u.Name}'s turn");
                yield return Tween.Pop(v.Root, 1.1f, 0.2f);
                v.SetGlow(Color.white, 0f);
            }
        }

        private IEnumerator CastQueuedAnim(CastQueuedEvent cq)
        {
            SyncTimeline(null);
            App.Audio.Sfx("cast");
            var caster = State.UnitById(cq.CasterId);
            if (cq.LineTiles != null) foreach (var t in cq.LineTiles) board.Mark(t, new Color(1f, 0.2f, 0.15f), 0.4f);
            string what = App.Content.TryAbility(cq.AbilityId)?.Name ?? "spell";
            var entry = State.Timeline.FirstOrDefault(x => x.Id == cq.EntryId);
            var target = entry == null ? null : State.UnitById(entry.TargetUnitId) ?? State.UnitAt(entry.TargetTile);
            string on = target != null ? $" on {target.Name}" : cq.LineTiles != null ? " down a line" : "";
            float secs = cq.Ticks / (float)BattleConfig.TicksPerSecond;
            ShowBanner($"{caster?.Name} {(cq.IsCharge ? "charges" : "casts")} {what}{on} — lands in {secs:0.#}s");
            if (caster != null) yield return Gather(caster, Art.SpellColor(cq.AbilityId));
            yield return FloatText(cq.CasterId, cq.IsCharge ? "Charging…" : "Casting…", new Color(0.6f, 0.85f, 1f), 0.3f, 50);
        }

        private IEnumerator ClutchAnim(ClutchEvent c)
        {
            var flash = Ui.El("fill", App.Overlay);
            flash.style.backgroundColor = Color.white;
            flash.pickingMode = PickingMode.Ignore;
            App.Audio.Sfx("heal");
            yield return Tween.Fade(flash, 0.9f * Mathf.Max(0.3f, Fx), 0f, 0.35f);
            flash.RemoveFromHierarchy();
            yield return FloatText(c.CasterId, "CLUTCH!", new Color(1f, 1f, 0.6f), 0.3f, 90);
        }

        private IEnumerator AbilityAnim(AbilityResolvedEvent ar)
        {
            var a = App.Content.TryAbility(ar.AbilityId);
            var caster = State.UnitById(ar.CasterId);
            var hit = ar.Targets.Select(State.UnitById).Where(u => u != null).Select(u => u.Name).Distinct().ToList();
            string who = hit.Count == 0 ? "" : hit.Count <= 2 ? " → " + string.Join(" & ", hit) : $" → {hit.Count} targets";
            ShowBanner(a == null ? "" : caster != null ? $"{caster.Name}: {a.Name}{who}" : a.Name + who);
            board.ClearMarks();
            Color c = a == null ? Color.white : a.BaseHeal > 0 ? new Color(0.4f, 1f, 0.55f) : ColorFor(a.DamageType);
            foreach (var t in ar.Tiles) board.Mark(t, c, 0.45f);
            var targets = ar.Targets.Select(State.UnitById).Where(u => u != null).ToList();
            if (caster != null && a != null && a.Kind == AbilityKind.Spell && targets.Count > 0)
                yield return SpellVfx(caster, targets, a, c);
            else if (caster != null && a != null && a.Motion != ProjectileMotion.None && targets.Count > 0)
            {
                Vector2 from = BoardView.CenterOf(caster.Pos, caster.W, caster.H);
                App.Audio.Sfx("cast", 0.7f);
                yield return Tween.Run(a.Motion == ProjectileMotion.Strike ? 0.18f : 0.26f, k =>
                {
                    board.Lines.ClearLines();
                    foreach (var t in targets)
                    {
                        Vector2 to = BoardView.CenterOf(t.Pos, t.W, t.H);
                        var p = ProjectilePoint(a.Motion, from, to, k);
                        board.Lines.Dot(p, 26f, c);
                        board.Lines.Dot(ProjectilePoint(a.Motion, from, to, Mathf.Max(0f, k - 0.08f)), 16f, new Color(c.r, c.g, c.b, 0.5f));
                    }
                });
                board.Lines.ClearLines();
            }
            else yield return Tween.Wait(0.12f);
            App.StartCoroutine(ClearMarksLater(0.35f));
        }

        /// <summary>Where the last spell mote stopped above each target: the element strike (lightning…) starts there.</summary>
        private readonly Dictionary<int, Vector2> strikeFrom = new Dictionary<int, Vector2>();

        /// <summary>
        /// Every spell plays the same readable arc (owner direction 2026-10-10): the caster draws energy (motes spiral
        /// in), a cast burst, then one mote per target curves to the space above it; the impact (lightning strike,
        /// flame burst, frost ring, heal sparkles …) follows from that point when the damage/heal lands.
        /// </summary>
        private IEnumerator SpellVfx(Unit caster, List<Unit> targets, AbilityDef a, Color c)
        {
            Vector2 from = BoardView.CenterOf(caster.Pos, caster.W, caster.H);
            bool calm = ReduceMotion;
            board.Units.TryGetValue(caster.Id, out var cv);
            // 1. Drawing energy (a timed cast already drew it when it was queued).
            if (a.CastTime <= 0f) yield return Gather(caster, c);
            // 2. Cast burst.
            App.Audio.Sfx("cast", 0.9f, 1.2f);
            yield return Tween.Run(0.14f, k => { board.Lines.Rings.Clear(); board.Lines.Ring(from, 40f + 90f * k, new Color(c.r, c.g, c.b, 1f - k), 10f); });
            board.Lines.Rings.Clear(); board.Lines.MarkDirtyRepaint();
            if (cv != null) cv.SetGlow(Color.white, 0f);
            // 3. One mote per target curves up and over to the space above it.
            var ends = targets.ToDictionary(t => t.Id, t => BoardView.CenterOf(t.Pos, t.W, t.H) + new Vector2(0f, -BoardView.Tile * 0.85f));
            var bends = targets.ToDictionary(t => t.Id, t => (Random.value < 0.5f ? -1f : 1f) * Random.Range(90f, 160f));
            float travel = calm ? 0.2f : Mathf.Clamp(Vector2.Distance(from, ends.Values.First()) / 1600f, 0.26f, 0.5f);
            yield return Tween.Run(travel, k =>
            {
                board.Lines.ClearLines();
                foreach (var t in targets)
                {
                    Vector2 to = ends[t.Id];
                    Vector2 Mid(float kk)
                    {
                        var d = to - from; var n = new Vector2(-d.y, d.x).normalized;
                        Vector2 ctrl = (from + to) / 2f + n * bends[t.Id] + Vector2.up * -120f;
                        float e = Tween.EaseInOut(kk);
                        return (1 - e) * (1 - e) * from + 2 * (1 - e) * e * ctrl + e * e * to;
                    }
                    for (int tr = 4; tr >= 0; tr--)
                    {
                        float kk = Mathf.Max(0f, k - tr * 0.05f);
                        board.Lines.Dot(Mid(kk), 28f - tr * 4f, new Color(c.r, c.g, c.b, 1f - tr * 0.18f));
                    }
                    board.Lines.Dot(Mid(k), 12f, Color.white);
                }
            });
            board.Lines.ClearLines();
            foreach (var kv in ends) strikeFrom[kv.Key] = kv.Value;
            if (a.BaseHeal > 0 || a.Filter == TargetFilter.AllyOnly)
                foreach (var t in targets) App.StartCoroutine(HealBurst(BoardView.CenterOf(t.Pos, t.W, t.H)));
        }

        /// <summary>Projectile curves (bible §6.1). Reduce Motion flattens everything to a straight line.</summary>
        private Vector2 ProjectilePoint(ProjectileMotion m, Vector2 from, Vector2 to, float k)
        {
            if (ReduceMotion) m = ProjectileMotion.Straight;
            var d = to - from;
            var n = new Vector2(-d.y, d.x).normalized;
            switch (m)
            {
                case ProjectileMotion.Bezier: return Vector2.Lerp(from, to, k) + Vector2.up * (-160f * Mathf.Sin(k * Mathf.PI));
                case ProjectileMotion.Homing: return Vector2.Lerp(from, to, Tween.EaseInOut(k)) + n * (90f * Mathf.Sin(k * Mathf.PI) * (1f - k));
                case ProjectileMotion.Spiral: { float r = 70f * (1f - k); float ang = k * Mathf.PI * 6f; return Vector2.Lerp(from, to, k) + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r; }
                case ProjectileMotion.Twist: return Vector2.Lerp(from, to, k) + n * (40f * Mathf.Sin(k * Mathf.PI * 3f));
                case ProjectileMotion.Strike: { var top = new Vector2(to.x, to.y - 520f); return Vector2.Lerp(top, to, k * k); }
                default: return Vector2.Lerp(from, to, k);
            }
        }

        private IEnumerator ClearMarksLater(float s)
        {
            yield return Tween.Wait(s);
            if (TargetingSlot < 0) board.ClearMarks();
        }

        private static Color ColorFor(DamageType t)
        {
            switch (t)
            {
                case DamageType.Fire: return new Color(1f, 0.45f, 0.15f);
                case DamageType.Ice: return new Color(0.5f, 0.85f, 1f);
                case DamageType.Lightning: return new Color(1f, 0.95f, 0.35f);
                case DamageType.Poison: return new Color(0.6f, 1f, 0.3f);
                case DamageType.Holy: return new Color(1f, 1f, 0.8f);
                case DamageType.Dark: return new Color(0.7f, 0.35f, 1f);
                default: return new Color(0.8f, 0.6f, 1f);
            }
        }

        private IEnumerator BuffAnim(BuffAppliedEvent ba)
        {
            RefreshUnit(ba.UnitId);
            var def = App.Content.TryBuff(ba.BuffId);
            if (def == null) yield break;
            App.Audio.Sfx(def.Kind == BuffKind.Buff ? "cast" : "debuff", 0.6f);
            yield return FloatText(ba.UnitId, def.Name + "!", def.Kind == BuffKind.Buff ? new Color(0.5f, 0.9f, 1f) : new Color(0.85f, 0.5f, 1f), 0.15f, 46);
        }

        private IEnumerator ScanAnim(ScannedEvent sc)
        {
            var u = State.UnitById(sc.UnitId);
            if (u == null) yield break;
            panelUnitId = u.Id;
            SyncPanel();
            ShowBanner($"{u.Name}: HP {sc.Hp}/{sc.MaxHp}  STR {sc.Stats.Strength:0}  VIT {sc.Stats.Vitality:0}  INT {sc.Stats.Intelligence:0}");
            yield return Tween.Wait(0.8f);
        }

        private IEnumerator BossAnim(BossPhaseEvent bp)
        {
            App.Audio.Sfx("explosion");
            App.StartCoroutine(Tween.Shake(shake, 20f * Fx, 0.4f));
            yield return FloatText(bp.UnitId, string.IsNullOrEmpty(bp.Text) ? "ENRAGED!" : bp.Text, new Color(1f, 0.3f, 0.6f), 0.6f, 80);
        }

        private IEnumerator WaveAnim(WaveStartedEvent wv)
        {
            foreach (var id in wv.Spawned)
            {
                var u = State.UnitById(id);
                if (u == null || board.Units.ContainsKey(id)) continue;
                var v = board.Add(u);
                v.SetHp(u.Hp, u.MaxHp);
                v.Root.style.opacity = 0;
                App.StartCoroutine(Tween.Run(0.3f, k => { v.Root.style.opacity = k; v.Root.style.translate = new Translate(0, -80 * (1 - k)); }, Tween.EaseOut));
            }
            SyncTimeline(null);
            if (wv.Wave > 0 || wv.TotalWaves > 1)
            {
                ShowBanner($"Wave {wv.Wave + 1}/{wv.TotalWaves}");
                var f = board.Float($"WAVE {wv.Wave + 1}", new Vector2(BoardView.Tile * 3, BoardView.Tile * 4), new Color(1f, 0.9f, 0.6f), 120);
                App.StartCoroutine(FloatAway(f, 1.0f));
                yield return Tween.Wait(0.6f);
            }
        }

        private IEnumerator ReviveAnim(RevivedEvent rv)
        {
            var u = State.UnitById(rv.UnitId);
            if (u == null) yield break;
            if (!board.Units.TryGetValue(u.Id, out var v)) v = board.Add(u);
            if (v.Dead) { board.UnitLayer.Add(v.Root); v.Dead = false; }
            board.Place(v, rv.At);
            v.SetHp(rv.Hp, u.MaxHp);
            App.Audio.Sfx("heal");
            yield return Tween.Run(0.35f, k => { v.Root.style.opacity = k; v.Root.style.scale = new Scale(Vector2.one * (0.6f + 0.4f * k)); });
            yield return FloatText(u.Id, "Revived!", new Color(1f, 0.95f, 0.6f), 0.2f, 70);
        }

        private VisualElement Particle(float size, Color c)
        {
            var p = new VisualElement { pickingMode = PickingMode.Ignore };
            p.style.position = Position.Absolute;
            p.style.width = p.style.height = size;
            p.style.backgroundImage = new StyleBackground(Art.Glow);
            p.style.unityBackgroundImageTintColor = c;
            board.FxLayer.Add(p);
            return p;
        }

        /// <summary>Element-specific impact effects (bible §6.2) drawn with UI Toolkit — no particle system needed.</summary>
        private IEnumerator ElementBurst(Vector2 at, DamageType type, bool crit)
        {
            var c = ColorFor(type);
            float scale = crit ? 1.4f : 1f;
            switch (type)
            {
                case DamageType.Fire:
                    for (int i = 0; i < 12; i++)
                    {
                        var p = Particle(Random.Range(18f, 34f) * scale, Color.Lerp(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.25f, 0.05f), Random.value));
                        float x0 = Random.Range(-60f, 60f), rise = Random.Range(90f, 200f) * scale, wob = Random.Range(0f, 6.28f);
                        App.StartCoroutine(Tween.Run(Random.Range(0.35f, 0.6f), k =>
                        {
                            p.style.left = at.x + x0 + Mathf.Sin(wob + k * 9f) * 14f - 15; p.style.top = at.y + 40 - rise * k - 15;
                            p.style.opacity = 1f - k; if (k >= 1f) p.RemoveFromHierarchy();
                        }));
                    }
                    break;
                case DamageType.Ice:
                    for (int i = 0; i < 8; i++)
                    {
                        var p = Particle(16f * scale, new Color(0.8f, 0.95f, 1f));
                        float ang = i * Mathf.PI / 4f;
                        App.StartCoroutine(Tween.Run(0.3f, k =>
                        {
                            var pos = at + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (130f * scale * Tween.EaseOut(k));
                            p.style.left = pos.x - 8; p.style.top = pos.y - 8; p.style.opacity = 1f - k; if (k >= 1f) p.RemoveFromHierarchy();
                        }));
                    }
                    yield return Tween.Run(0.35f, k => { board.Lines.Rings.Clear(); board.Lines.Ring(at, 30f + 110f * scale * k, new Color(0.7f, 0.9f, 1f, 1f - k), 8f); });
                    break;
                case DamageType.Lightning:
                {
                    var origin = TakeStrikeOrigin(at) ?? at + new Vector2(Random.Range(-30f, 30f), -560f);
                    var pts = new List<Vector2> { origin };
                    while (pts[pts.Count - 1].y < at.y - 40) pts.Add(new Vector2(at.x + Random.Range(-46f, 46f), pts[pts.Count - 1].y + Random.Range(60f, 110f)));
                    pts.Add(at);
                    for (int flash = 0; flash < 2; flash++)
                    {
                        board.Lines.ClearLines();
                        for (int i = 1; i < pts.Count; i++) { board.Lines.Add(pts[i - 1], pts[i], new Color(1f, 1f, 0.75f, 0.95f), 10f * scale); board.Lines.Add(pts[i - 1], pts[i], new Color(1f, 1f, 1f, 1f), 4f); }
                        yield return Tween.Wait(0.06f);
                        board.Lines.ClearLines();
                        yield return Tween.Wait(0.04f);
                    }
                    Sparks(at, 8, new Color(1f, 1f, 0.6f));
                    break;
                }
                case DamageType.Poison:
                    for (int i = 0; i < 9; i++)
                    {
                        var p = Particle(Random.Range(14f, 26f), new Color(0.55f, 1f, 0.35f));
                        float x0 = Random.Range(-70f, 70f), delay = Random.Range(0f, 0.25f), rise = Random.Range(60f, 130f);
                        App.StartCoroutine(Tween.Run(0.7f, k =>
                        {
                            float kk = Mathf.Clamp01((k - delay) / (1f - delay));
                            float s2 = 0.6f + kk * 0.8f;
                            p.style.scale = new Scale(Vector2.one * s2);
                            p.style.left = at.x + x0 - 10; p.style.top = at.y + 30 - rise * kk - 10;
                            p.style.opacity = kk <= 0f ? 0f : 1f - kk; if (k >= 1f) p.RemoveFromHierarchy();
                        }));
                    }
                    break;
                case DamageType.Holy:
                    yield return Tween.Run(0.4f, k =>
                    {
                        board.Lines.ClearLines();
                        board.Lines.Ring(at, 40f + 120f * k, new Color(1f, 0.95f, 0.6f, 1f - k), 10f);
                        for (int i = 0; i < 8; i++) { float ang = i * Mathf.PI / 4f + k; var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)); board.Lines.Add(at + dir * 30f, at + dir * (60f + 120f * k), new Color(1f, 1f, 0.8f, 0.8f * (1f - k)), 6f); }
                    });
                    board.Lines.ClearLines();
                    break;
                case DamageType.Dark:
                    for (int i = 0; i < 12; i++)
                    {
                        var p = Particle(22f, new Color(0.6f, 0.25f, 0.95f));
                        float ang = Random.Range(0f, 6.28f), dist = Random.Range(120f, 190f);
                        App.StartCoroutine(Tween.Run(0.35f, k =>
                        {
                            var pos = at + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist * (1f - Tween.EaseInOut(k));
                            p.style.left = pos.x - 11; p.style.top = pos.y - 11; p.style.opacity = 0.3f + 0.7f * k; if (k >= 1f) p.RemoveFromHierarchy();
                        }));
                    }
                    break;
                default:
                    yield return Tween.Run(0.35f, k => { board.Lines.Rings.Clear(); board.Lines.Ring(at, 20f + 120f * k, new Color(c.r, c.g, c.b, 1f - k), 9f); });
                    break;
            }
            if (board.Lines.Rings.Count > 0) { board.Lines.Rings.Clear(); board.Lines.MarkDirtyRepaint(); }
        }

        /// <summary>"Drawing energy": motes spiral into the caster while its frame glows in the spell colour.</summary>
        private IEnumerator Gather(Unit caster, Color c)
        {
            Vector2 from = BoardView.CenterOf(caster.Pos, caster.W, caster.H);
            App.Audio.Sfx("cast", 0.5f);
            board.Units.TryGetValue(caster.Id, out var cv);
            cv?.SetGlow(c, 0.8f);
            int motes = ReduceMotion ? 4 : 12;
            for (int i = 0; i < motes; i++)
            {
                var p = Particle(Random.Range(14f, 24f), c);
                float ang = i * Mathf.PI * 2f / motes + Random.Range(-0.2f, 0.2f), dist = Random.Range(120f, 170f), spin = Random.Range(1.5f, 2.5f);
                App.StartCoroutine(Tween.Run(0.34f, k =>
                {
                    float r = dist * (1f - k * k), aa = ang + spin * k;
                    var pos = from + new Vector2(Mathf.Cos(aa), Mathf.Sin(aa)) * r;
                    p.style.left = pos.x - 10; p.style.top = pos.y - 10;
                    p.style.opacity = 0.4f + 0.6f * k; if (k >= 1f) p.RemoveFromHierarchy();
                }));
            }
            yield return Tween.Wait(0.34f);
            cv?.SetGlow(Color.white, 0f);
        }

        /// <summary>The mote end above the unit at <paramref name="at"/>, consumed once.</summary>
        private Vector2? TakeStrikeOrigin(Vector2 at)
        {
            foreach (var kv in strikeFrom)
                if (Mathf.Abs(kv.Value.x - at.x) < BoardView.Tile * 0.6f && kv.Value.y < at.y && at.y - kv.Value.y < BoardView.Tile * 2.2f)
                { strikeFrom.Remove(kv.Key); return kv.Value; }
            return null;
        }

        private IEnumerator HealBurst(Vector2 at)
        {
            for (int i = 0; i < 7; i++)
            {
                var p = Particle(Random.Range(16f, 26f), new Color(0.5f, 1f, 0.6f));
                float x0 = Random.Range(-60f, 60f), rise = Random.Range(70f, 140f);
                App.StartCoroutine(Tween.Run(0.6f, k =>
                {
                    p.style.left = at.x + x0 - 10; p.style.top = at.y + 20 - rise * Tween.EaseOut(k) - 10;
                    p.style.opacity = 1f - k; if (k >= 1f) p.RemoveFromHierarchy();
                }));
            }
            yield break;
        }

        /// <summary>Little particles radiating from a hit (UI Toolkit, no particle system needed).</summary>
        private void Sparks(Vector2 at, int count, Color color)
        {
            for (int i = 0; i < count; i++)
            {
                var p = new VisualElement { pickingMode = PickingMode.Ignore };
                p.style.position = Position.Absolute;
                float size = Random.Range(10f, 22f);
                p.style.width = p.style.height = size;
                p.style.backgroundImage = new StyleBackground(Art.Glow);
                p.style.unityBackgroundImageTintColor = color;
                board.FxLayer.Add(p);
                float ang = Random.Range(0f, Mathf.PI * 2f), dist = Random.Range(60f, 150f);
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                App.StartCoroutine(Tween.Run(Random.Range(0.25f, 0.4f), k =>
                {
                    var pos = at + dir * dist * Tween.EaseOut(k) + Vector2.up * (60f * k * k);
                    p.style.left = pos.x - size / 2; p.style.top = pos.y - size / 2;
                    p.style.opacity = 1f - k;
                    if (k >= 1f) p.RemoveFromHierarchy();
                }));
            }
        }

        // ── Shared helpers ────────────────────────────────────────────────────────

        private void RefreshUnit(int id)
        {
            var u = State.UnitById(id);
            if (u != null && board.Units.TryGetValue(id, out var v)) { v.SetBuffs(u, Colorblind, State.TurnEntryOf(u.Id)?.StunTicks > 0); v.SetHp(u.Hp, u.MaxHp); }
        }

        private IEnumerator FloatText(int unitId, string text, Color c, float hold, float size = 60)
        {
            var u = State.UnitById(unitId);
            Vector2 at = u != null && board.Units.TryGetValue(unitId, out var v) ? BoardView.CenterOf(v.Tile, v.W, v.H) : new Vector2(BoardView.Tile * 3, BoardView.Tile * 4);
            App.StartCoroutine(FloatAway(board.Float(text, at + new Vector2(0, -50), c, size), 0.8f));
            yield return Tween.Wait(hold);
        }

        private IEnumerator FloatAway(Label l, float seconds)
        {
            float top = l.resolvedStyle.top;
            if (float.IsNaN(top)) top = 0;
            var start = l.style.top.value.value;
            yield return Tween.Run(seconds, k =>
            {
                l.style.top = start - 90f * Tween.EaseOut(k);
                l.style.opacity = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
                float s = k < 0.15f ? 0.6f + k / 0.15f * 0.5f : 1.1f - 0.1f * Mathf.Min(1, (k - 0.15f) * 4);
                l.style.scale = new Scale(Vector2.one * s);
            });
            l.RemoveFromHierarchy();
        }

        private IEnumerator EndBattle()
        {
            ended = true;
            yield return Tween.Wait(0.6f);
            if (setupOverride != null || App.Save == null)
            {
                App.Router.Back();
                yield break;
            }
            var report = BattleFactory.Commit(App.Content, App.Save, State);
            App.Persist();
            App.Router.Replace(new PostBattleScreen(report, State.Stage.Id));
        }
    }
}
