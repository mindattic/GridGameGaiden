using System.Collections.Generic;
using Gaiden.Core;
using Gaiden.Core.Battle;
using Gaiden.Core.Content;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gaiden.App
{
    /// <summary>One actor on the board: framed portrait crop, HP strip, buff pips, turn countdown badge.</summary>
    public sealed class UnitView
    {
        public readonly int Id;
        public readonly VisualElement Root;
        public readonly VisualElement Frame;
        public readonly VisualElement Art;
        public readonly VisualElement Hp;
        public readonly VisualElement HpFill;
        public readonly VisualElement Buffs;
        public readonly Label Countdown;
        public readonly VisualElement Glow;
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

            Frame = new VisualElement { pickingMode = PickingMode.Ignore };
            Frame.style.position = Position.Absolute;
            Frame.style.left = Frame.style.top = 6;
            Frame.style.right = Frame.style.bottom = 6;
            float r = 22;
            Frame.style.borderTopLeftRadius = Frame.style.borderTopRightRadius = Frame.style.borderBottomLeftRadius = Frame.style.borderBottomRightRadius = r;
            Frame.style.borderTopWidth = Frame.style.borderBottomWidth = Frame.style.borderLeftWidth = Frame.style.borderRightWidth = 6;
            var c = u.IsHero ? new Color(0.98f, 0.86f, 0.45f) : new Color(0.92f, 0.28f, 0.3f);
            if (u.Def.HasTag("Boss")) c = new Color(0.86f, 0.4f, 1f);
            Frame.style.borderTopColor = Frame.style.borderBottomColor = Frame.style.borderLeftColor = Frame.style.borderRightColor = c;
            Frame.style.backgroundColor = u.IsHero ? new Color(0.12f, 0.2f, 0.45f) : new Color(0.3f, 0.08f, 0.12f);
            Frame.style.overflow = Overflow.Hidden;
            Root.Add(Frame);

            Art = new VisualElement { pickingMode = PickingMode.Ignore };
            Art.style.position = Position.Absolute;
            Art.style.left = Art.style.top = Art.style.right = Art.style.bottom = 0;
            Art.style.backgroundImage = new StyleBackground(App.Art.Thumb(u.Def.Id));
            Art.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            if (!u.IsHero) Art.style.scale = new Scale(new Vector2(-1, 1)); // enemies face the party
            Frame.Add(Art);

            Hp = new VisualElement { pickingMode = PickingMode.Ignore };
            Hp.AddToClassList("gauge");
            Hp.style.position = Position.Absolute;
            Hp.style.left = 18; Hp.style.right = 18; Hp.style.bottom = 12; Hp.style.height = 18;
            HpFill = new VisualElement { name = "fill" };
            HpFill.AddToClassList("gauge-fill");
            Hp.Add(HpFill);
            Root.Add(Hp);

            Buffs = new VisualElement { pickingMode = PickingMode.Ignore };
            Buffs.style.position = Position.Absolute;
            Buffs.style.flexDirection = FlexDirection.Row;
            Buffs.style.right = 10; Buffs.style.top = 10;
            Root.Add(Buffs);

            Countdown = new Label { pickingMode = PickingMode.Ignore };
            Countdown.AddToClassList("shadow");
            Countdown.style.position = Position.Absolute;
            Countdown.style.left = 8; Countdown.style.top = 2;
            Countdown.style.fontSize = 58;
            Countdown.style.unityFontStyleAndWeight = FontStyle.Bold;
            Countdown.style.color = new Color(1f, 0.95f, 0.8f);
            Root.Add(Countdown);

            if (u.IsSnakeSegment)
            {
                Frame.style.borderTopLeftRadius = Frame.style.borderTopRightRadius = Frame.style.borderBottomLeftRadius = Frame.style.borderBottomRightRadius = tile * 0.4f;
            }
        }

        public void SetHp(int hp, int max)
        {
            float f = max <= 0 ? 0 : Mathf.Clamp01(hp / (float)max);
            HpFill.style.width = Length.Percent(f * 100f);
            HpFill.EnableInClassList("gauge-fill--mid", f <= 0.5f && f > 0.25f);
            HpFill.EnableInClassList("gauge-fill--low", f <= 0.25f);
        }

        public void SetBuffs(Unit u, bool colorblind)
        {
            Buffs.Clear();
            int n = 0;
            foreach (var b in u.Buffs)
            {
                if (n++ >= 3) break;
                var pip = new Label(string.IsNullOrEmpty(b.Def.Letter) ? b.Def.Name.Substring(0, 1) : b.Def.Letter) { pickingMode = PickingMode.Ignore };
                pip.AddToClassList("pip");
                pip.style.width = pip.style.height = 40;
                pip.style.fontSize = 24;
                pip.style.backgroundColor = App.Art.HexColor(b.Def.Color, b.Def.Kind == BuffKind.Buff ? new Color(0.4f, 0.8f, 1f) : new Color(0.8f, 0.4f, 0.9f));
                Buffs.Add(pip);
            }
        }

        public void SetCountdown(int actions)
        {
            Countdown.text = actions < 0 ? "" : actions.ToString();
            Countdown.style.color = actions == 0 ? new Color(1f, 0.35f, 0.3f) : new Color(1f, 0.95f, 0.8f);
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

        public BoardView(VisualElement parent)
        {
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
                    t.style.backgroundImage = new StyleBackground((x + y) % 2 == 0 ? App.Art.TileLight : App.Art.TileDark);
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

    /// <summary>Vector overlay: pincer lines, projectile trails, line-charge telegraphs.</summary>
    public sealed class LineLayer : VisualElement
    {
        public struct Seg { public Vector2 A, B; public Color Color; public float Width; }
        public readonly List<Seg> Segs = new List<Seg>();
        public readonly List<(Vector2 c, float r, Color color)> Dots = new List<(Vector2, float, Color)>();

        public LineLayer() { generateVisualContent += Draw; }

        public void ClearLines() { Segs.Clear(); Dots.Clear(); MarkDirtyRepaint(); }
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
