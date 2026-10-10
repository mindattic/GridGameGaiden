using System;
using System.Collections.Generic;
using Gaiden.Core.Battle;
using Gaiden.Core.Content;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gaiden.App
{
    /// <summary>
    /// The widget kit. Every screen composes these; nothing positions itself with magic pixel offsets
    /// except the battle board. All styling is in Theme.uss classes.
    /// </summary>
    public static class Ui
    {
        public static VisualElement El(string classes = null, VisualElement parent = null)
        {
            var e = new VisualElement();
            AddClasses(e, classes);
            parent?.Add(e);
            return e;
        }

        public static void AddClasses(VisualElement e, string classes)
        {
            if (string.IsNullOrEmpty(classes)) return;
            foreach (var c in classes.Split(' ')) if (c.Length > 0) e.AddToClassList(c);
        }

        public static Label Text(string text, string classes = null, VisualElement parent = null)
        {
            var l = new Label(text) { enableRichText = true };
            l.pickingMode = PickingMode.Ignore;
            AddClasses(l, classes);
            parent?.Add(l);
            return l;
        }

        public static VisualElement Window(string title = null, VisualElement parent = null, string classes = null)
        {
            var w = El("window " + (classes ?? ""), parent);
            w.style.backgroundImage = new StyleBackground(Art.Window);
            if (title != null) Text(title, "window-title", w);
            return w;
        }

        public static Button Button(string text, Action onClick, string classes = null, VisualElement parent = null)
        {
            var b = new Button(() =>
            {
                GameApp.I?.Audio?.Sfx("ui.click");
                onClick?.Invoke();
            }) { text = text };
            b.AddToClassList("btn");
            AddClasses(b, classes);
            parent?.Add(b);
            return b;
        }

        public static void SetEnabled(Button b, bool enabled)
        {
            b.SetEnabled(enabled);
            b.EnableInClassList("btn--disabled", !enabled);
        }

        /// <summary>Top bar: back button, title, live gold.</summary>
        public static VisualElement Header(string title, Action onBack, VisualElement parent, out Label gold)
        {
            var h = El("header", parent);
            if (onBack != null) Button("‹", onBack, "btn--small btn--icon", h).name = "back";
            Text(title, "header-title shadow", h);
            gold = Text("", "header-gold shadow", h);
            return h;
        }

        public static string Gold(int g) => $"{g:N0} G";

        /// <summary>FF-style menu row: cursor hand, optional icon, label, right-aligned value.</summary>
        public static VisualElement MenuRow(string label, string value, Action onClick, VisualElement parent, Texture2D icon = null, bool enabled = true)
        {
            var row = El("menu-row", parent);
            var cur = El("menu-cursor", row);
            cur.style.backgroundImage = new StyleBackground(Art.Cursor);
            if (icon != null)
            {
                var ic = El("menu-icon", row);
                ic.style.backgroundImage = new StyleBackground(icon);
            }
            var l = Text(label, "menu-label", row);
            l.name = "label";
            var v = Text(value ?? "", "menu-value", row);
            v.name = "value";
            row.EnableInClassList("menu-row--disabled", !enabled);
            row.RegisterCallback<PointerEnterEvent>(_ => Select(row));
            row.RegisterCallback<ClickEvent>(_ =>
            {
                try
                {
                    Select(row);
                    if (!enabled) { GameApp.I?.Audio?.Sfx("ui.error"); return; }
                    GameApp.I?.Audio?.Sfx("ui.select");
                    onClick?.Invoke();
                }
                catch (Exception e) { Debug.LogException(e); }
            });
            return row;
        }

        public static void Select(VisualElement row)
        {
            var p = row.parent;
            if (p != null) foreach (var sib in p.Children()) sib.RemoveFromClassList("menu-row--selected");
            row.AddToClassList("menu-row--selected");
        }

        /// <summary>Put the cursor on the first enabled row of a menu container.</summary>
        public static void SelectFirst(VisualElement menu)
        {
            foreach (var c in menu.Children())
                if (c.ClassListContains("menu-row") && !c.ClassListContains("menu-row--disabled")) { Select(c); return; }
        }

        public static ScrollView List(VisualElement parent)
        {
            var sv = new ScrollView(ScrollViewMode.Vertical);
            sv.AddToClassList("list");
            sv.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            sv.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
            parent?.Add(sv);
            return sv;
        }

        public static VisualElement Tabs(string[] names, int active, Action<int> onPick, VisualElement parent)
        {
            var bar = El("tabs", parent);
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var t = Text(names[i], "tab" + (i == active ? " tab--active" : ""), bar);
                t.pickingMode = PickingMode.Position;
                t.RegisterCallback<ClickEvent>(_ => { GameApp.I?.Audio?.Sfx("ui.click"); onPick(idx); });
            }
            return bar;
        }

        public static VisualElement Gauge(float frac, VisualElement parent, float width = -1)
        {
            var g = El("gauge", parent);
            if (width > 0) g.style.width = width;
            var f = El("gauge-fill", g);
            f.name = "fill";
            SetGauge(g, frac);
            return g;
        }

        public static void SetGauge(VisualElement gauge, float frac)
        {
            var f = gauge.Q("fill");
            frac = Mathf.Clamp01(frac);
            f.style.width = Length.Percent(frac * 100f);
            f.EnableInClassList("gauge-fill--mid", frac <= 0.5f && frac > 0.25f);
            f.EnableInClassList("gauge-fill--low", frac <= 0.25f);
        }

        public static VisualElement Thumb(string classId, VisualElement parent, float size = 150)
        {
            var t = El("thumb", parent);
            t.style.width = size;
            t.style.height = size;
            t.style.backgroundImage = new StyleBackground(Art.Thumb(classId));
            return t;
        }

        /// <summary>Mana cost as orb pips: 3 → three orbs.</summary>
        public static VisualElement CostPips(int cost, VisualElement parent, float size = 34)
        {
            var row = El("row", parent);
            row.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < cost; i++)
            {
                var p = Text("", "pip pip--mana", row);
                p.style.width = size; p.style.height = size; p.style.borderTopLeftRadius = p.style.borderTopRightRadius = p.style.borderBottomLeftRadius = p.style.borderBottomRightRadius = size / 2;
                p.style.fontSize = size * 0.6f;
            }
            return row;
        }

        public static string RarityClass(ItemRarity r) => "rarity-" + r;

        public static string StatLine(Stats s) =>
            $"STR {s.Strength:0}  VIT {s.Vitality:0}  AGI {s.Agility:0}  SPD {s.Speed:0}\nINT {s.Intelligence:0}  WIS {s.Wisdom:0}  LCK {s.Luck:0}";

        public static string Delta(float now, float then)
        {
            float d = then - now;
            if (Mathf.Abs(d) < 0.5f) return $"{then:0}";
            return d > 0 ? $"{then:0} <color=#78EC96>▲{d:0}</color>" : $"{then:0} <color=#FF6E6E>▼{-d:0}</color>";
        }

        /// <summary>What a buff does, in words, built from its mechanics so it never drifts from the numbers.</summary>
        public static string BuffEffect(BuffDef d)
        {
            var parts = new List<string>();
            if (d.DamagePerTick > 0) parts.Add($"{d.DamagePerTick} damage each tick");
            if (d.HealPerTick > 0) parts.Add($"heals {d.HealPerTick} each tick");
            if (d.Immobile) parts.Add("can't move or act");
            if (d.HaltsTimeline) parts.Add("timeline halted");
            if (d.BlocksSpells) parts.Add("can't cast spells");
            if (d.IncomingDamageMultiplier < 1f) parts.Add($"takes {Mathf.RoundToInt((1f - d.IncomingDamageMultiplier) * 100)}% less damage");
            if (d.IncomingDamageMultiplier > 1f) parts.Add($"takes {Mathf.RoundToInt((d.IncomingDamageMultiplier - 1f) * 100)}% more damage");
            if (d.TimelineSpeedMultiplier < 1f) parts.Add("acts slower");
            if (d.TimelineSpeedMultiplier > 1f) parts.Add("acts faster");
            if (d.AccuracyMultiplier < 1f) parts.Add("attacks miss more");
            if (d.BreaksOnDamage) parts.Add("ends when hit");
            if (d.BreaksOnMove) parts.Add("ends when moved");
            return parts.Count == 0 ? (d.Kind == BuffKind.Buff ? "beneficial" : "harmful") : string.Join(", ", parts);
        }

        /// <summary>An enemy's kit in a few words (§14.4) — shown on its card and in the bestiary.</summary>
        public static string KitTraits(ContentDb c, ClassDef d)
        {
            var t = new List<string>();
            if (d.MoveRange > 1) t.Add($"moves {d.MoveRange} tiles");
            if (d.AttackRange > 1) t.Add($"shoots {d.AttackRange} tiles");
            if (!string.IsNullOrEmpty(d.ChargeAbility)) t.Add("casts " + (c.TryAbility(d.ChargeAbility)?.Name ?? d.ChargeAbility));
            if (!string.IsNullOrEmpty(d.Trap) && c.Traps.TryGetValue(d.Trap, out var tr)) t.Add("lays " + tr.Name);
            if (!string.IsNullOrEmpty(d.Trail) && c.Traps.TryGetValue(d.Trail, out var tl)) t.Add("leaves a " + tl.Name);
            if (!string.IsNullOrEmpty(d.OnHitBuff)) t.Add($"hits may inflict {c.TryBuff(d.OnHitBuff)?.Name ?? d.OnHitBuff} ({Mathf.RoundToInt(d.OnHitChance * 100)}%)");
            if (d.DeathBurst > 0) t.Add($"explodes when killed ({d.DeathBurst})");
            foreach (var id in d.Abilities)
                if (c.TryAbility(id) is AbilityDef a && a.Kind == AbilityKind.Passive) t.Add(a.Name.ToLowerInvariant());
            if (d.ImmuneBuffs != null && d.ImmuneBuffs.Count > 0) t.Add("immune to " + string.Join("/", d.ImmuneBuffs));
            return t.Count == 0 ? "" : char.ToUpper(t[0][0]) + string.Join(" · ", t).Substring(1);
        }

        /// <summary>
        /// A status pip: the pixel-art icon (flame, snowflake, poison bottle …) on a dark disc ringed in the status colour.
        /// Falls back to the letter only when an effect has no icon yet.
        /// </summary>
        public static VisualElement StatusPip(string iconId, string letter, Color color, float size, VisualElement parent = null)
        {
            var pip = new VisualElement { pickingMode = PickingMode.Ignore, viewDataKey = iconId };
            pip.style.width = pip.style.height = size;
            pip.style.flexShrink = 0;
            pip.style.borderTopLeftRadius = pip.style.borderTopRightRadius = pip.style.borderBottomLeftRadius = pip.style.borderBottomRightRadius = size / 2f;
            pip.style.borderTopWidth = pip.style.borderBottomWidth = pip.style.borderLeftWidth = pip.style.borderRightWidth = 3;
            pip.style.borderTopColor = pip.style.borderBottomColor = pip.style.borderLeftColor = pip.style.borderRightColor = color;
            pip.style.backgroundColor = new Color(0.05f, 0.06f, 0.14f, 0.95f);
            var tex = Art.StatusIcon(iconId);
            if (tex != null)
            {
                var img = new VisualElement { pickingMode = PickingMode.Ignore };
                img.style.position = Position.Absolute;
                img.style.left = img.style.top = img.style.right = img.style.bottom = 1;
                img.style.backgroundImage = new StyleBackground(tex);
                img.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                pip.Add(img);
            }
            else
            {
                var l = new Label(letter) { pickingMode = PickingMode.Ignore };
                l.style.flexGrow = 1; l.style.unityTextAlign = TextAnchor.MiddleCenter;
                l.style.fontSize = size * 0.55f; l.style.color = color; l.style.unityFontStyleAndWeight = FontStyle.Bold;
                pip.Add(l);
            }
            parent?.Add(pip);
            return pip;
        }
    }
}
