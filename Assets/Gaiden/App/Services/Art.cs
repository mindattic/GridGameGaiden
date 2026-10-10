using System.Linq;
using System.Collections.Generic;
using Gaiden.Core.Content;
using UnityEngine;

namespace Gaiden.App
{
    /// <summary>
    /// Every texture the game draws: imported art by key (Resources) plus procedurally generated UI
    /// pieces (window gradient, cursor, orbs). Missing art never throws — it falls back to a
    /// generated placeholder and logs once, so a gap is visible instead of fatal.
    /// </summary>
    public static class Art
    {
        private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        private static readonly HashSet<string> warned = new HashSet<string>();

        public static Texture2D Load(string path)
        {
            if (string.IsNullOrEmpty(path)) return Placeholder;
            if (cache.TryGetValue(path, out var t) && t != null) return t;
            t = Resources.Load<Texture2D>(path);
            if (t == null)
            {
                if (warned.Add(path)) Debug.LogWarning($"[Art] missing texture '{path}'");
                t = Placeholder;
            }
            cache[path] = t;
            return t;
        }

        public static bool Exists(string path) => Resources.Load<Texture2D>(path) != null;

        public static Texture2D Thumb(string classId) => Load("Thumbs/" + classId);
        private static readonly Dictionary<string, int> spellIndex = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);

        /// <summary>Called once content loads: every ability gets an ordinal, so every spell gets its own colour.</summary>
        public static void IndexSpells(IEnumerable<string> ids)
        {
            spellIndex.Clear();
            foreach (var id in ids.OrderBy(x => x, System.StringComparer.OrdinalIgnoreCase)) spellIndex[id] = spellIndex.Count;
        }

        /// <summary>Each spell's own colour (golden-ratio hue walk over the ability list) — its countdown ring and timeline frame.</summary>
        public static Color SpellColor(string abilityId)
        {
            int i = abilityId != null && spellIndex.TryGetValue(abilityId, out var k) ? k : 0;
            float hue = (0.03f + i * 0.61803398f) % 1f;
            return Color.HSVToRGB(hue, 0.72f, 1f);
        }

        /// <summary>Pixel-art full-body sprite (tools/make_pixel_icons.py; enemies only) — null when the class has none.</summary>
        public static Texture2D Pixel(string classId)
        {
            if (string.IsNullOrEmpty(classId)) return null;
            string path = "Pixel/" + classId;
            if (cache.TryGetValue(path, out var t)) return t;
            return cache[path] = Resources.Load<Texture2D>(path);
        }
        public static Texture2D Portrait(string classId) => Load("Portraits/" + classId);
        public static Texture2D BoardTile => Load("Tiles/tile");
        public static Texture2D Icon(string key) => string.IsNullOrEmpty(key) ? null : Load("Icons/" + key);

        public static Texture2D Background(string theme, int index = 0)
        {
            var key = $"Backgrounds/{theme ?? "greenvalley"}_{index % 4}";
            var t = Resources.Load<Texture2D>(key);
            return t != null ? t : Load("Backgrounds/greenvalley_0");
        }

        // ── Generated pieces ────────────────────────────────────────────────────────

        private static Texture2D placeholder, window, windowDark, cursor, glow, orb;

        public static Texture2D Placeholder => placeholder ??= Solid(new Color(0.35f, 0.2f, 0.45f), "placeholder");

        /// <summary>The Final Fantasy window body: a deep vertical blue gradient (border drawn by USS).</summary>
        public static Texture2D Window => window ??= Gradient(new Color32(44, 70, 170, 255), new Color32(10, 18, 70, 255), "window");
        public static Texture2D WindowDark => windowDark ??= Gradient(new Color32(30, 36, 80, 255), new Color32(6, 8, 30, 255), "window-dark");

        public static Texture2D Cursor => cursor ??= MakeCursor();
        public static Texture2D Glow => glow ??= MakeGlow();
        public static Texture2D Orb => orb ??= MakeOrb();

        private static Texture2D NewTex(int w, int h, string name)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
        }

        private static Texture2D Solid(Color c, string name)
        {
            var t = NewTex(4, 4, name);
            var px = new Color[16];
            for (int i = 0; i < px.Length; i++) px[i] = c;
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        private static Texture2D Gradient(Color32 top, Color32 bottom, string name)
        {
            const int h = 128;
            var t = NewTex(2, h, name);
            for (int y = 0; y < h; y++)
            {
                var c = Color32.Lerp(bottom, top, y / (float)(h - 1));
                t.SetPixel(0, y, c);
                t.SetPixel(1, y, c);
            }
            t.Apply();
            return t;
        }

        private static Texture2D MakeCursor()
        {
            // A white right-pointing gauntlet-ish arrow with a dark outline, like the classic menu hand.
            const int s = 64;
            var t = NewTex(s, s, "cursor");
            var clear = new Color(0, 0, 0, 0);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float fx = x / (float)s, fy = y / (float)s;
                    // arrow head
                    float head = fx > 0.45f ? (1f - fx) * 1.8f : 0f;
                    bool inHead = fx > 0.45f && Mathf.Abs(fy - 0.5f) < head * 0.5f;
                    bool inShaft = fx >= 0.08f && fx <= 0.5f && Mathf.Abs(fy - 0.5f) < 0.13f;
                    bool inCuff = fx >= 0.04f && fx <= 0.16f && Mathf.Abs(fy - 0.5f) < 0.22f;
                    bool inside = inHead || inShaft || inCuff;
                    bool edge = false;
                    if (!inside)
                    {
                        for (int k = 0; k < 8 && !edge; k++)
                        {
                            float ox = fx + Mathf.Cos(k * Mathf.PI / 4f) * 0.04f, oy = fy + Mathf.Sin(k * Mathf.PI / 4f) * 0.04f;
                            float h2 = ox > 0.45f ? (1f - ox) * 1.8f : 0f;
                            edge = (ox > 0.45f && Mathf.Abs(oy - 0.5f) < h2 * 0.5f) || (ox >= 0.08f && ox <= 0.5f && Mathf.Abs(oy - 0.5f) < 0.13f) || (ox >= 0.04f && ox <= 0.16f && Mathf.Abs(oy - 0.5f) < 0.22f);
                        }
                    }
                    var shade = Mathf.Lerp(1f, 0.78f, fy);
                    t.SetPixel(x, y, inside ? new Color(shade, shade, 1f, 1f) : edge ? new Color(0.05f, 0.05f, 0.15f, 1f) : clear);
                }
            t.Apply();
            return t;
        }

        /// <summary>GLSL-style smoothstep (Unity's Mathf.SmoothStep is an interpolator, not an edge function).</summary>
        private static float Edge(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        private static Texture2D MakeGlow()
        {
            const int s = 64;
            var t = NewTex(s, s, "glow");
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x - s / 2f + 0.5f) / (s / 2f), dy = (y - s / 2f + 0.5f) / (s / 2f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    // Soft ring that stays bright out to the frame edge (≈0.75) then falls off.
                    float a = d < 0.7f ? 1f : 1f - Edge(0.7f, 1f, d);
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            return t;
        }

        private static Texture2D MakeOrb()
        {
            const int s = 64;
            var t = NewTex(s, s, "orb");
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x - s / 2f + 0.5f) / (s / 2f), dy = (y - s / 2f + 0.5f) / (s / 2f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > 1f) { t.SetPixel(x, y, new Color(0, 0, 0, 0)); continue; }
                    float hl = Mathf.Clamp01(1f - Mathf.Sqrt((dx + 0.35f) * (dx + 0.35f) + (dy - 0.35f) * (dy - 0.35f)) * 1.6f);
                    float rim = Edge(0.8f, 1f, d);
                    float v = Mathf.Lerp(0.85f, 0.55f, rim) + hl * 0.5f;
                    float a = 1f - Edge(0.94f, 1f, d);
                    t.SetPixel(x, y, new Color(v, v, v, a));
                }
            t.Apply();
            return t;
        }

        /// <summary>The one mana orb color (generic mana, AMENDMENTS 2026-10-10).</summary>
        public static readonly Color Mana = new Color(0.42f, 0.78f, 1f);

        public static Color HexColor(string hex, Color fallback)
        {
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out var c)) return c;
            return fallback;
        }
    }
}
