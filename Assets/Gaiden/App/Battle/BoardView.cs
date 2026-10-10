using System.Collections.Generic;
using Gaiden.Core;
using Gaiden.Core.Battle;
using Gaiden.Core.Content;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gaiden.App
{
    /// <summary>One actor on the board: framed portrait crop, HP strip, buff pips (turn order lives on the timeline).</summary>
    public sealed class UnitView
    {
        public readonly int Id;
        public readonly VisualElement Root;
        public readonly VisualElement Frame;
        public readonly VisualElement Art;
        public readonly VisualElement Keyline;
        public readonly VisualElement Hp;
        public readonly VisualElement HpFill;
        public readonly VisualElement Buffs;
        public readonly VisualElement Glow;
        public readonly CastRing Cast;
        public readonly VisualElement CastChip;
        public Coord Tile;
        public int W = 1, H = 1;
        public bool Dead;

        public UnitView(Unit u, float tile)
        {
            Id = u.Id;
            W = u.W; H = u.H;
            Root = new VisualElement { name = "unit-" + u.Id, pickingMode = PickingMode.Ignore };
            Root.style.position = Position.Absolute;
            Root.style.width = tile * u.W;
            Root.style.height = tile * u.H;

            Glow = new VisualElement { pickingMode = PickingMode.Ignore };
            Glow.style.position = Position.Absolute;
            Glow.style.left = Glow.style.top = -tile * 0.18f;
            Glow.style.right = Glow.style.bottom = -tile * 0.18f;
            Glow.style.backgroundImage = new StyleBackground(App.Art.Glow);
            Glow.style.opacity = 0f;
            Root.Add(Glow);

            // Same frame as every menu portrait (.thumb): white rounded border over the window gradient.
            Frame = new VisualElement { pickingMode = PickingMode.Ignore };
            Frame.AddToClassList("unit-frame");
            Frame.style.position = Position.Absolute;
            Frame.style.left = Frame.style.top = 6;
            Frame.style.right = Frame.style.bottom = 6;
            Frame.style.backgroundImage = new StyleBackground(App.Art.Window);
            Root.Add(Frame);

            Art = new VisualElement { pickingMode = PickingMode.Ignore };
            Art.style.position = Position.Absolute;
            Art.style.left = Art.style.top = Art.style.right = Art.style.bottom = 0;
            Art.style.backgroundImage = new StyleBackground(App.Art.Thumb(u.Def.Id));
            Art.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            if (!u.IsHero) Art.style.scale = new Scale(new Vector2(-1, 1)); // enemies face the party
            Frame.Add(Art);

            // Team keyline just inside the white border, so heroes, foes and bosses still read apart.
            Keyline = new VisualElement { name = "keyline", pickingMode = PickingMode.Ignore };
            Keyline.AddToClassList("unit-keyline");
            Keyline.AddToClassList(u.IsHero ? "unit-keyline--hero" : (u.Def.HasTag("Boss") || u.IsMultiTile) ? "unit-keyline--boss" : "unit-keyline--foe");
            Frame.Add(Keyline);

            Hp = new VisualElement { pickingMode = PickingMode.Ignore };
            Hp.AddToClassList("gauge");
            Hp.style.position = Position.Absolute;
            Hp.style.left = 18; Hp.style.right = 18; Hp.style.bottom = 12; Hp.style.height = 18;
            HpFill = new VisualElement { name = "fill" };
            HpFill.AddToClassList("gauge-fill");
            Hp.Add(HpFill);
            Root.Add(Hp);

            Buffs = new VisualElement { name = "buffs-" + u.Id, pickingMode = PickingMode.Ignore };
            Buffs.style.position = Position.Absolute;
            Buffs.style.flexDirection = FlexDirection.Row;
            Buffs.style.right = 10; Buffs.style.top = 10;
            Root.Add(Buffs);


            // Casting: countdown traced around the caster's frame. Targeted: the incoming spell's chip in the corner.
            Cast = new CastRing { name = "castring-" + u.Id, pickingMode = PickingMode.Ignore };
            Cast.style.display = DisplayStyle.None;
            Root.Add(Cast);
            CastChip = new VisualElement { name = "castchip-" + u.Id, pickingMode = PickingMode.Ignore };
            CastChip.style.position = Position.Absolute;
            CastChip.style.left = 4; CastChip.style.top = 4;
            CastChip.style.width = CastChip.style.height = 50;
            CastChip.style.borderTopLeftRadius = CastChip.style.borderTopRightRadius = CastChip.style.borderBottomLeftRadius = CastChip.style.borderBottomRightRadius = 12;
            CastChip.style.borderTopWidth = CastChip.style.borderBottomWidth = CastChip.style.borderLeftWidth = CastChip.style.borderRightWidth = 3;
            CastChip.style.backgroundColor = new Color(0.06f, 0.07f, 0.17f, 1f);
            CastChip.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            CastChip.style.display = DisplayStyle.None;
            CastChip.Add(new Label { name = "castchip-letter", pickingMode = PickingMode.Ignore });
            CastChip[0].AddToClassList("shadow");
            CastChip[0].style.fontSize = 30; CastChip[0].style.unityFontStyleAndWeight = FontStyle.Bold;
            CastChip[0].style.unityTextAlign = TextAnchor.MiddleCenter; CastChip[0].style.flexGrow = 1;
            Root.Add(CastChip);

            if (u.IsSnakeSegment)
            {
                Frame.style.borderTopLeftRadius = Frame.style.borderTopRightRadius = Frame.style.borderBottomLeftRadius = Frame.style.borderBottomRightRadius = tile * 0.4f;
                Keyline.style.borderTopLeftRadius = Keyline.style.borderTopRightRadius = Keyline.style.borderBottomLeftRadius = Keyline.style.borderBottomRightRadius = tile * 0.4f - 4;
            }
        }

        public void SetHp(int hp, int max)
        {
            float f = max <= 0 ? 0 : Mathf.Clamp01(hp / (float)max);
            HpFill.style.width = Length.Percent(f * 100f);
            HpFill.EnableInClassList("gauge-fill--mid", f <= 0.5f && f > 0.25f);
            HpFill.EnableInClassList("gauge-fill--low", f <= 0.25f);
        }

        private readonly List<(string letter, Color color, float frac)> statuses = new List<(string, Color, float)>();
        private int statusBeat;

        /// <summary>
        /// One status slot per unit (owner direction 2026-10-10): it flashes through every buff, debuff and stun on
        /// the 1.2 s beat; the selected unit's card explains each one.
        /// </summary>
        public void SetBuffs(Unit u, bool colorblind, bool stunned = false)
        {
            statuses.Clear();
            if (stunned) statuses.Add(("!", new Color(1f, 0.85f, 0.3f), 1f)); // pushback stun isn't a buff, but it's why it isn't moving
            foreach (var b in u.Buffs)
                statuses.Add((string.IsNullOrEmpty(b.Def.Letter) ? b.Def.Name.Substring(0, 1) : b.Def.Letter,
                    App.Art.HexColor(b.Def.Color, b.Def.Kind == BuffKind.Buff ? new Color(0.4f, 0.8f, 1f) : new Color(0.8f, 0.4f, 0.9f)),
                    Mathf.Clamp01(b.Remaining / (float)Mathf.Max(1, b.Def.Duration))));
            ShowStatus();
        }

        public void NextStatus()
        {
            if (statuses.Count < 2) return;
            statusBeat++;
            ShowStatus();
        }

        private void ShowStatus()
        {
            Buffs.Clear();
            if (statuses.Count == 0) return;
            var (letter, color, frac) = statuses[statusBeat % statuses.Count];
            var pip = new Label(letter) { name = "statuspip-" + Id, pickingMode = PickingMode.Ignore };
            pip.AddToClassList("pip");
            pip.style.width = pip.style.height = 44;
            pip.style.fontSize = 26;
            pip.style.backgroundColor = color;
            pip.Add(new RingTimer(frac) { pickingMode = PickingMode.Ignore });
            Buffs.Add(pip);
            if (statuses.Count > 1)
            {
                var more = new Label((statuses.Count).ToString()) { pickingMode = PickingMode.Ignore };
                more.AddToClassList("shadow");
                more.style.position = Position.Absolute; more.style.right = -6; more.style.bottom = -10;
                more.style.fontSize = 20; more.style.unityFontStyleAndWeight = FontStyle.Bold;
                pip.Add(more);
            }
        }

        /// <summary>Caster side: show (or hide, with null) the countdown ring; <paramref name="left"/> = fraction of cast time remaining.</summary>
        public void SetCastRing(Color? color, float left)
        {
            Cast.style.display = color.HasValue ? DisplayStyle.Flex : DisplayStyle.None;
            if (color.HasValue) Cast.Set(color.Value, left);
        }

        /// <summary>Target side: the incoming spell's icon (or initial) in its colour, top-left of the tile.</summary>
        public void SetCastChip(Color? color, Texture2D icon, string name)
        {
            CastChip.style.display = color.HasValue ? DisplayStyle.Flex : DisplayStyle.None;
            if (!color.HasValue) return;
            var c = color.Value;
            CastChip.style.borderTopColor = CastChip.style.borderBottomColor = CastChip.style.borderLeftColor = CastChip.style.borderRightColor = c;
            CastChip.style.backgroundImage = icon != null ? new StyleBackground(icon) : new StyleBackground((Texture2D)null);
            var letter = (Label)CastChip[0];
            letter.text = icon == null && !string.IsNullOrEmpty(name) ? name.Substring(0, 1) : "";
            letter.style.color = c;
        }

        public void SetGlow(Color c, float alpha)
        {
            Glow.style.unityBackgroundImageTintColor = c;
            Glow.style.opacity = alpha;
        }
    }

    /// <summary>The 6×8 board: tiles, trap marks, highlight overlay, units, floating fx.</summary>
    public sealed class BoardView
    {
        public const float Tile = 180f;
        public readonly VisualElement Root;
        public readonly VisualElement TileLayer;
        public readonly VisualElement MarkLayer;
        public readonly VisualElement UnitLayer;
        public readonly VisualElement FxLayer;
        public readonly Dictionary<int, UnitView> Units = new Dictionary<int, UnitView>();
        private readonly VisualElement[,] tiles = new VisualElement[BattleConfig.Cols, BattleConfig.Rows];
        private readonly VisualElement[,] marks = new VisualElement[BattleConfig.Cols, BattleConfig.Rows];
        private readonly List<VisualElement> trapMarks = new List<VisualElement>();
        public readonly LineLayer Lines;

        private readonly string theme;

        public BoardView(VisualElement parent, string theme = null)
        {
            this.theme = theme;
            Root = new VisualElement { name = "board" };
            Root.style.position = Position.Absolute;
            Root.style.width = Tile * BattleConfig.Cols;
            Root.style.height = Tile * BattleConfig.Rows;
            parent.Add(Root);

            // Order matters: highlights and lines draw OVER the units so targets and pincer previews read clearly.
            TileLayer = Layer("tiles");
            UnitLayer = Layer("units");
            MarkLayer = Layer("marks");
            Lines = new LineLayer();
            Lines.style.position = Position.Absolute;
            Lines.style.left = Lines.style.top = Lines.style.right = Lines.style.bottom = 0;
            Lines.pickingMode = PickingMode.Ignore;
            Root.Add(Lines);
            FxLayer = Layer("fx");

            for (int y = 0; y < BattleConfig.Rows; y++)
                for (int x = 0; x < BattleConfig.Cols; x++)
                {
                    var t = new VisualElement { pickingMode = PickingMode.Ignore };
                    t.style.position = Position.Absolute;
                    t.style.left = x * Tile; t.style.top = y * Tile;
                    t.style.width = Tile; t.style.height = Tile;
                    // GridGame2026's tile art recoloured per biome (tools/make_tiles.py), tinted light/dark for the checkerboard.
                    bool light = (x + y) % 2 == 0;
                    t.style.backgroundImage = new StyleBackground(App.Art.BoardTile(theme));
                    t.style.unityBackgroundImageTintColor = light ? new Color(1.0f, 1.0f, 1.0f, 0.8f) : new Color(0.42f, 0.46f, 0.56f, 0.6f);
                    TileLayer.Add(t);
                    tiles[x, y] = t;

                    var m = new VisualElement { pickingMode = PickingMode.Ignore };
                    m.style.position = Position.Absolute;
                    m.style.left = x * Tile + 4; m.style.top = y * Tile + 4;
                    m.style.width = Tile - 8; m.style.height = Tile - 8;
                    m.style.borderTopLeftRadius = m.style.borderTopRightRadius = m.style.borderBottomLeftRadius = m.style.borderBottomRightRadius = 18;
                    m.style.opacity = 0;
                    MarkLayer.Add(m);
                    marks[x, y] = m;
                }
        }

        private VisualElement Layer(string name)
        {
            var l = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            l.style.position = Position.Absolute;
            l.style.left = l.style.top = l.style.right = l.style.bottom = 0;
            Root.Add(l);
            return l;
        }

        public static Vector2 PosOf(Coord c) => new Vector2(c.X * Tile, c.Y * Tile);
        public static Vector2 CenterOf(Coord c, int w = 1, int h = 1) => new Vector2((c.X + w / 2f) * Tile, (c.Y + h / 2f) * Tile);

        public Coord TileAt(Vector2 local)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt(local.x / Tile), 0, BattleConfig.Cols - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(local.y / Tile), 0, BattleConfig.Rows - 1);
            return new Coord(x, y);
        }

        // ── Physical blocks (§1.1.1): raised stone that nothing can enter ──
        private readonly HashSet<Coord> drawnBlocks = new HashSet<Coord>();

        public void SyncBlocks(IEnumerable<Coord> blocks)
        {
            foreach (var c in blocks)
            {
                if (!drawnBlocks.Add(c)) continue;
                var shadow = new VisualElement { pickingMode = PickingMode.Ignore };
                shadow.style.position = Position.Absolute;
                shadow.style.left = c.X * Tile + 14; shadow.style.top = c.Y * Tile + 22;
                shadow.style.width = Tile - 22; shadow.style.height = Tile - 22;
                shadow.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f);
                SetRadius(shadow, 26);
                TileLayer.Add(shadow);
                var rock = new VisualElement { name = $"block-{c.X}-{c.Y}", pickingMode = PickingMode.Ignore };
                rock.style.position = Position.Absolute;
                rock.style.left = c.X * Tile + 8; rock.style.top = c.Y * Tile + 8;
                rock.style.width = Tile - 16; rock.style.height = Tile - 22;
                rock.style.backgroundImage = new StyleBackground(App.Art.BoardTile(theme));
                rock.style.unityBackgroundImageTintColor = new Color(0.85f, 0.85f, 0.9f, 1f);
                rock.style.backgroundColor = new Color(0.32f, 0.33f, 0.38f, 1f);
                SetRadius(rock, 26);
                rock.style.borderTopWidth = 6; rock.style.borderLeftWidth = rock.style.borderRightWidth = 4; rock.style.borderBottomWidth = 12;
                rock.style.borderTopColor = new Color(0.78f, 0.8f, 0.86f, 0.9f);
                rock.style.borderLeftColor = rock.style.borderRightColor = new Color(0.45f, 0.47f, 0.53f, 1f);
                rock.style.borderBottomColor = new Color(0.16f, 0.17f, 0.2f, 1f);
                TileLayer.Add(rock);
            }
        }

        private static void SetRadius(VisualElement e, float r) =>
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;

        // ── Power Amplifier marker (§1.2.6): a pulsing gold diamond on its tile ──
        private VisualElement amp;
        public VisualElement Amp => amp;

        public void SyncAmplifier(Coord? at)
        {
            if (amp == null)
            {
                amp = new VisualElement { name = "amplifier", pickingMode = PickingMode.Ignore };
                amp.style.position = Position.Absolute;
                amp.style.width = amp.style.height = Tile * 0.5f;
                amp.style.rotate = new Rotate(45f);
                amp.style.borderTopWidth = amp.style.borderBottomWidth = amp.style.borderLeftWidth = amp.style.borderRightWidth = 6;
                amp.style.borderTopColor = amp.style.borderBottomColor = amp.style.borderLeftColor = amp.style.borderRightColor = new Color(1f, 0.95f, 0.7f);
                amp.style.backgroundColor = new Color(1f, 0.72f, 0.15f, 0.85f);
                amp.style.borderTopLeftRadius = amp.style.borderTopRightRadius = amp.style.borderBottomLeftRadius = amp.style.borderBottomRightRadius = 10;
                var glow = new VisualElement { pickingMode = PickingMode.Ignore };
                glow.style.position = Position.Absolute;
                glow.style.left = glow.style.top = glow.style.right = glow.style.bottom = -30;
                glow.style.backgroundImage = new StyleBackground(App.Art.Glow);
                glow.style.unityBackgroundImageTintColor = new Color(1f, 0.8f, 0.3f, 0.8f);
                amp.Add(glow);
                MarkLayer.Add(amp);
                amp.schedule.Execute(() =>
                {
                    float k = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 5f);
                    amp.style.scale = new Scale(new Vector2(k, k));
                }).Every(33);
            }
            amp.style.display = at.HasValue ? DisplayStyle.Flex : DisplayStyle.None;
            if (at.HasValue) PlaceAmp(CenterOf(at.Value));
        }

        public void PlaceAmp(Vector2 centre)
        {
            amp.style.left = centre.x - Tile * 0.25f;
            amp.style.top = centre.y - Tile * 0.25f;
        }

        /// <summary>Board-space rect around every tile of a group (a pincer's attackers + opponents), slightly outside the frames.</summary>
        public static Rect GroupRect(IEnumerable<(Coord pos, int w, int h)> members)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var (pos, w, h) in members)
            {
                x0 = Mathf.Min(x0, pos.X * Tile); y0 = Mathf.Min(y0, pos.Y * Tile);
                x1 = Mathf.Max(x1, (pos.X + w) * Tile); y1 = Mathf.Max(y1, (pos.Y + h) * Tile);
            }
            return Rect.MinMaxRect(x0 - 4f, y0 - 4f, x1 + 4f, y1 + 4f);
        }

        /// <summary>Short link bridging the gap between a supporter's frame and its partner's (edge to edge, never over the art).</summary>
        public static (Vector2 a, Vector2 b) EdgeLink(Coord from, Coord to)
        {
            var d = new Vector2(to.X - from.X, to.Y - from.Y);
            if (d.sqrMagnitude > 0f) d.Normalize();
            var ca = CenterOf(from); var cb = CenterOf(to);
            return (ca + d * (Tile * 0.5f - 16f), cb - d * (Tile * 0.5f - 16f));
        }

        public UnitView Add(Unit u)
        {
            var v = new UnitView(u, Tile);
            Units[u.Id] = v;
            UnitLayer.Add(v.Root);
            Place(v, u.Pos);
            return v;
        }

        public void Place(UnitView v, Coord c)
        {
            v.Tile = c;
            var p = PosOf(c);
            v.Root.style.left = p.x;
            v.Root.style.top = p.y;
        }

        public void ClearMarks()
        {
            foreach (var m in marks) m.style.opacity = 0;
        }

        public void Mark(Coord c, Color color, float alpha = 0.55f)
        {
            if (!BattleState.InBounds(c)) return;
            var m = marks[c.X, c.Y];
            m.style.backgroundColor = color;
            m.style.opacity = alpha;
        }

        public void SyncTraps(BattleState s)
        {
            foreach (var t in trapMarks) t.RemoveFromHierarchy();
            trapMarks.Clear();
            foreach (var trap in s.Traps)
            {
                var e = new Label("☠") { pickingMode = PickingMode.Ignore };
                e.style.position = Position.Absolute;
                e.style.left = trap.At.X * Tile; e.style.top = trap.At.Y * Tile;
                e.style.width = Tile; e.style.height = Tile;
                e.style.unityTextAlign = TextAnchor.MiddleCenter;
                e.style.fontSize = 90;
                e.style.color = new Color(0.75f, 0.35f, 1f, 0.85f);
                e.style.backgroundColor = new Color(0.45f, 0.1f, 0.6f, 0.25f);
                MarkLayer.Add(e);
                trapMarks.Add(e);
            }
        }

        /// <summary>Floating combat text (damage numbers etc.), rising and fading.</summary>
        public Label Float(string text, Vector2 at, Color color, float size = 64)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.AddToClassList("shadow");
            l.style.position = Position.Absolute;
            l.style.left = at.x - 150; l.style.top = at.y - 60;
            l.style.width = 300;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.fontSize = size;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.color = color;
            FxLayer.Add(l);
            return l;
        }
    }

    /// <summary>Radial countdown ring drawn around a buff pip: the arc shrinks clockwise as the buff runs out.</summary>
    public sealed class RingTimer : VisualElement
    {
        private readonly float frac;
        public RingTimer(float frac)
        {
            this.frac = frac;
            style.position = Position.Absolute;
            style.left = style.top = style.right = style.bottom = -4;
            generateVisualContent += ctx =>
            {
                var p = ctx.painter2D;
                var r = contentRect;
                var c = r.center;
                float radius = Mathf.Min(r.width, r.height) / 2f - 2f;
                p.lineWidth = 4f;
                p.strokeColor = new Color(1f, 0.92f, 0.35f, 0.95f);
                p.lineCap = LineCap.Round;
                p.BeginPath();
                p.Arc(c, radius, Angle.Degrees(-90f), Angle.Degrees(-90f + 360f * this.frac));
                p.Stroke();
            };
        }
    }

    /// <summary>
    /// Radial countdown traced around a unit's rounded frame, clockwise from top-centre: the lit part is the cast
    /// time still to run, so the line shrinks toward the moment the spell lands (bible §2.6, owner request).
    /// </summary>
    public sealed class CastRing : VisualElement
    {
        private Color color = Color.white;
        private float left = 1f;
        public float Left => left;
        public Color Color => color;

        public CastRing()
        {
            style.position = Position.Absolute;
            style.left = style.top = 2; style.right = style.bottom = 2;
            generateVisualContent += Draw;
        }

        public void Set(Color c, float remaining)
        {
            color = c; left = Mathf.Clamp01(remaining);
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width <= 0 || left <= 0f) return;
            var pts = Perimeter(r, 22f, left);
            var p = ctx.painter2D;
            p.lineJoin = LineJoin.Round; p.lineCap = LineCap.Round;
            p.strokeColor = new Color(0f, 0f, 0f, 0.55f); p.lineWidth = 13f;
            Stroke(p, pts);
            p.strokeColor = color; p.lineWidth = 8f;
            Stroke(p, pts);
        }

        private static void Stroke(Painter2D p, List<Vector2> pts)
        {
            if (pts.Count < 2) return;
            p.BeginPath(); p.MoveTo(pts[0]);
            for (int i = 1; i < pts.Count; i++) p.LineTo(pts[i]);
            p.Stroke();
        }

        /// <summary>Points along a rounded rect's border from top-centre clockwise, covering fraction <paramref name="f"/> of it.</summary>
        public static List<Vector2> Perimeter(Rect r, float rad, float f)
        {
            float inset = 5f;
            float x0 = r.xMin + inset, y0 = r.yMin + inset, x1 = r.xMax - inset, y1 = r.yMax - inset;
            rad = Mathf.Min(rad, (x1 - x0) / 2f, (y1 - y0) / 2f);
            var full = new List<Vector2>();
            void Line(Vector2 a, Vector2 b, int n) { for (int i = 0; i < n; i++) full.Add(Vector2.Lerp(a, b, i / (float)n)); }
            void Arc(Vector2 c, float a0, int n) { for (int i = 0; i < n; i++) { float a = (a0 + 90f * i / n) * Mathf.Deg2Rad; full.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad); } }
            float cx = (x0 + x1) / 2f;
            Line(new Vector2(cx, y0), new Vector2(x1 - rad, y0), 12);
            Arc(new Vector2(x1 - rad, y0 + rad), -90f, 8);
            Line(new Vector2(x1, y0 + rad), new Vector2(x1, y1 - rad), 24);
            Arc(new Vector2(x1 - rad, y1 - rad), 0f, 8);
            Line(new Vector2(x1 - rad, y1), new Vector2(x0 + rad, y1), 24);
            Arc(new Vector2(x0 + rad, y1 - rad), 90f, 8);
            Line(new Vector2(x0, y1 - rad), new Vector2(x0, y0 + rad), 24);
            Arc(new Vector2(x0 + rad, y0 + rad), 180f, 8);
            Line(new Vector2(x0 + rad, y0), new Vector2(cx, y0), 12);
            full.Add(new Vector2(cx, y0));
            int keep = Mathf.Clamp(Mathf.CeilToInt(full.Count * f), 2, full.Count);
            return full.GetRange(0, keep);
        }
    }

    /// <summary>Vector overlay: pincer lines, projectile trails, line-charge telegraphs.</summary>
    public sealed class LineLayer : VisualElement
    {
        public struct Seg { public Vector2 A, B; public Color Color; public float Width; }
        public readonly List<Seg> Segs = new List<Seg>();
        public readonly List<(Vector2 c, float r, Color color)> Dots = new List<(Vector2, float, Color)>();
        public readonly List<(Vector2 c, float r, Color color, float w)> Rings = new List<(Vector2, float, Color, float)>();
        /// <summary>Rounded outlines drawn around a group of tiles; <c>f</c> is how much of the border has drawn itself (0..1).</summary>
        public readonly List<(Rect r, Color color, float w, float f)> Outlines = new List<(Rect, Color, float, float)>();

        public LineLayer() { generateVisualContent += Draw; }

        public void ClearLines() { Segs.Clear(); Dots.Clear(); Rings.Clear(); Outlines.Clear(); MarkDirtyRepaint(); }
        public void Outline(Rect r, Color color, float w, float f) { Outlines.Add((r, color, w, f)); MarkDirtyRepaint(); }
        public void Ring(Vector2 c, float r, Color color, float w) { Rings.Add((c, r, color, w)); MarkDirtyRepaint(); }
        public void Add(Vector2 a, Vector2 b, Color c, float w) { Segs.Add(new Seg { A = a, B = b, Color = c, Width = w }); MarkDirtyRepaint(); }
        public void Dot(Vector2 c, float r, Color color) { Dots.Add((c, r, color)); MarkDirtyRepaint(); }

        private void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            p.lineCap = LineCap.Round;
            foreach (var s in Segs)
            {
                p.strokeColor = s.Color;
                p.lineWidth = s.Width;
                p.BeginPath();
                p.MoveTo(s.A);
                p.LineTo(s.B);
                p.Stroke();
            }
            foreach (var (rect, color, w, f) in Outlines)
            {
                var pts = CastRing.Perimeter(rect, 30f, f);
                if (pts.Count < 2) continue;
                p.lineJoin = LineJoin.Round; p.lineCap = LineCap.Round;
                p.strokeColor = new Color(0f, 0f, 0f, 0.5f * color.a); p.lineWidth = w + 6f;
                p.BeginPath(); p.MoveTo(pts[0]); for (int i = 1; i < pts.Count; i++) p.LineTo(pts[i]); p.Stroke();
                p.strokeColor = color; p.lineWidth = w;
                p.BeginPath(); p.MoveTo(pts[0]); for (int i = 1; i < pts.Count; i++) p.LineTo(pts[i]); p.Stroke();
            }
            foreach (var (c, r, color, w) in Rings)
            {
                p.strokeColor = color;
                p.lineWidth = w;
                p.BeginPath();
                p.Arc(c, r, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Stroke();
            }
            foreach (var (c, r, color) in Dots)
            {
                p.fillColor = color;
                p.BeginPath();
                p.Arc(c, r, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Fill();
            }
        }
    }
}
