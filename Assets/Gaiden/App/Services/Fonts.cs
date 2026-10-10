using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Gaiden.App
{
    /// <summary>
    /// Dynamic SDF font assets built at runtime from the bundled TTFs, each with a fallback chain
    /// (Noto Sans → Noto Sans Symbols 2) so every glyph the UI uses renders — no missing-glyph boxes.
    /// </summary>
    public static class Fonts
    {
        private static FontAsset body, display, noto, symbols;

        public static FontAsset Body => body ??= Make("Fonts/Outfit", true);
        public static FontAsset Display => display ??= Make("Fonts/Cinzel", true);

        private static FontAsset Fallback(ref FontAsset slot, string path)
        {
            if (slot != null) return slot;
            var f = Resources.Load<Font>(path);
            slot = f != null ? FontAsset.CreateFontAsset(f) : null;
            return slot;
        }

        private static FontAsset Make(string path, bool withFallbacks)
        {
            var font = Resources.Load<Font>(path);
            if (font == null) { Debug.LogError($"[Fonts] missing {path}"); return null; }
            var fa = FontAsset.CreateFontAsset(font);
            if (fa == null) { Debug.LogError($"[Fonts] could not create font asset for {path}"); return null; }
            if (withFallbacks)
            {
                fa.fallbackFontAssetTable = new List<FontAsset>();
                var n = Fallback(ref noto, "Fonts/NotoSans");
                var s = Fallback(ref symbols, "Fonts/NotoSansSymbols2");
                if (n != null) fa.fallbackFontAssetTable.Add(n);
                if (s != null) fa.fallbackFontAssetTable.Add(s);
            }
            return fa;
        }

        public static void UseBody(VisualElement e) { if (Body != null) e.style.unityFontDefinition = FontDefinition.FromSDFFont(Body); }
        public static void UseDisplay(VisualElement e) { if (Display != null) e.style.unityFontDefinition = FontDefinition.FromSDFFont(Display); }
    }
}
