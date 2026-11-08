using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace EventHorizon.EditorTools
{
    /// <summary>
    /// Builds dynamic font assets for Fira Sans Condensed and Fira Mono and registers them as the panel's global
    /// fallback fonts, so any glyph missing from Rajdhani or Share Tech Mono (Cyrillic, ², √, π, θ ...) is drawn from
    /// Fira. The atlases fill in at runtime (dynamic population) and are cleared from the saved asset on build.
    /// </summary>
    public static class FontSetup
    {
        const string FontDir = "Assets/EventHorizon/Fonts";
        const string OutDir = "Assets/EventHorizon/Fonts/Generated";
        const string SettingsPath = "Assets/EventHorizon/Resources/UI/EventHorizonText.asset";

        public static PanelTextSettings EnsureTextSettings()
        {
            System.IO.Directory.CreateDirectory(OutDir);
            var ui = MakeFontAsset("FiraSansCondensed-Medium");
            var mono = MakeFontAsset("FiraMono-Regular");
            var settings = AssetDatabase.LoadAssetAtPath<PanelTextSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelTextSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            var fallbacks = new List<FontAsset>();
            if (ui != null) fallbacks.Add(ui);
            if (mono != null) fallbacks.Add(mono);
            settings.fallbackFontAssets = fallbacks;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return settings;
        }

        static FontAsset MakeFontAsset(string name)
        {
            string path = $"{OutDir}/{name} SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<FontAsset>(path);
            if (existing != null) return existing;
            var font = AssetDatabase.LoadAssetAtPath<Font>($"{FontDir}/{name}.ttf");
            if (font == null) { Debug.LogWarning($"[Event Horizon] Font {name}.ttf not found"); return null; }
            var fa = FontAsset.CreateFontAsset(font, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (fa == null) { Debug.LogWarning($"[Event Horizon] Could not create a font asset for {name}"); return null; }
            fa.name = name + " SDF";
            AssetDatabase.CreateAsset(fa, path);
            if (fa.atlasTextures != null)
                foreach (var tex in fa.atlasTextures)
                    if (tex != null && !AssetDatabase.Contains(tex)) { tex.name = name + " Atlas"; AssetDatabase.AddObjectToAsset(tex, fa); }
            if (fa.material != null && !AssetDatabase.Contains(fa.material)) { fa.material.name = name + " Material"; AssetDatabase.AddObjectToAsset(fa.material, fa); }
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            return fa;
        }
    }
}
