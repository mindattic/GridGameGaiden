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
    }
}
