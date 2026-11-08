using System.IO;
using EventHorizon.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace EventHorizon.EditorTools
{
    /// <summary>
    /// Creates the shared visual assets: UI panel settings and resources, the URP rendering features
    /// (SSAO, soft shadows), the post-processing profile and the sky/atmosphere/dust materials.
    /// </summary>
    public static class VisualSetup
    {
        const string ResDir = "Assets/EventHorizon/Resources/UI";
        const string RenderDir = AssetIntegrationBuilder.Out + "/Rendering";
        const string MatDir = AssetIntegrationBuilder.Out + "/Materials";
        const string SSS = "Assets/ThirdParty/SolarSystemScope/";
        const string SciFi = "Assets/ThirdParty/Kenney/SciFiSounds/Audio/";
        const string Impact = "Assets/ThirdParty/Kenney/ImpactSounds/Audio/";

        public const string PostProfilePath = RenderDir + "/EventHorizon_PostProcess.asset";

        [MenuItem("Event Horizon/Assets/0. Set Up UI and Rendering", priority = 0)]
        public static void SetupAll()
        {
            Directory.CreateDirectory(ResDir);
            Directory.CreateDirectory(RenderDir);
            Directory.CreateDirectory(MatDir);
            AssetDatabase.Refresh();
            PanelAsset();
            Resources();
            Rendering();
            PostProcess();
            Materials();
            AssetDatabase.SaveAssets();
            Debug.Log("[Event Horizon] UI and rendering assets ready.");
        }

        static void PanelAsset()
        {
            string path = ResDir + "/EventHorizonPanel.asset";
            var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (ps == null)
            {
                ps = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(ps, path);
            }
            ps.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/EventHorizon/UI/EventHorizonTheme.tss");
            // Responsive layout: authored at 1080p and scaled by height, so ultrawide screens just get more width.
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 1f;
            ps.clearColor = false;
            EditorUtility.SetDirty(ps);
        }

        static void Resources()
        {
            string path = ResDir + "/UiResources.asset";
            var r = AssetDatabase.LoadAssetAtPath<UiResources>(path);
            if (r == null)
            {
                r = ScriptableObject.CreateInstance<UiResources>();
                AssetDatabase.CreateAsset(r, path);
            }
            Texture2D T(string f) => AssetDatabase.LoadAssetAtPath<Texture2D>(SSS + f);
            AudioClip A(string f) => AssetDatabase.LoadAssetAtPath<AudioClip>(f);
            r.mars = T("2k_mars.jpg");
            r.earth = T("2k_earth_daymap.jpg");
            r.moon = T("2k_moon.jpg");
            r.sun = T("2k_sun.jpg");
            r.saturn = T("2k_saturn.jpg");
            r.stars = T("8k_stars_milky_way.jpg");
            r.phobos = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdParty/NASA/MarsMoons/Phobos.jpg");
            r.hover = A(Impact + "impactGlass_light_000.ogg");
            r.click = A(SciFi + "computerNoise_000.ogg");
            r.back = A(SciFi + "doorClose_000.ogg");
            r.confirm = A(SciFi + "forceField_000.ogg");
            r.error = A(Impact + "impactMetal_heavy_000.ogg");
            r.previewEffects = A(Impact + "impactMetal_medium_000.ogg");
            r.previewAmbient = A(SciFi + "spaceEngineLow_000.ogg");
            r.previewVoice = A(SciFi + "computerNoise_001.ogg");
            r.previewMusic = A(SciFi + "engineCircular_000.ogg");
            EditorUtility.SetDirty(r);
        }

        static void Rendering()
        {
            if (!(GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset urp)) return;
            var so = new SerializedObject(urp);
            so.FindProperty("m_SoftShadowsSupported").boolValue = true;
            so.FindProperty("m_SupportsHDR").boolValue = true;
            var depth = so.FindProperty("m_RequireDepthTexture");
            if (depth != null) depth.boolValue = true;
            so.ApplyModifiedProperties();

            var lightLimit = so.FindProperty("m_AdditionalLightsPerObjectLimit");
            if (lightLimit != null) { lightLimit.intValue = 8; so.ApplyModifiedProperties(); }
            foreach (var data in urp.rendererDataList)
            {
                if (data == null) continue;
                // Forward+: lights are culled in screen tiles, so many floodlights don't fight over 4 slots per object.
                var rso = new SerializedObject(data);
                var mode = rso.FindProperty("m_RenderingMode");
                if (mode != null && mode.intValue != 2) { mode.intValue = 2; rso.ApplyModifiedProperties(); }
                bool has = false;
                foreach (var f in data.rendererFeatures) has |= f is ScreenSpaceAmbientOcclusion;
                if (has) continue;
                var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                ssao.name = "SSAO";
                AssetDatabase.AddObjectToAsset(ssao, data);
                AssetDatabase.SaveAssets();
                var dso = new SerializedObject(data);
                var features = dso.FindProperty("m_RendererFeatures");
                var map = dso.FindProperty("m_RendererFeatureMap");
                features.InsertArrayElementAtIndex(features.arraySize);
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = ssao;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out _, out long localId);
                map.InsertArrayElementAtIndex(map.arraySize);
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                // Gentler than the default intensity of 3: Mars light is soft and dusty.
                var sso = new SerializedObject(ssao);
                var intensity = sso.FindProperty("m_Settings.Intensity");
                var radius = sso.FindProperty("m_Settings.Radius");
                if (intensity != null) intensity.floatValue = 1.4f;
                if (radius != null) radius.floatValue = 0.3f;
                sso.ApplyModifiedProperties();
                dso.ApplyModifiedProperties();
                EditorUtility.SetDirty(data);
            }
        }

        static void PostProcess()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, PostProfilePath);
            }
            T Get<T>() where T : VolumeComponent
            {
                if (!profile.TryGet(out T c))
                {
                    c = profile.Add<T>(true);
                    c.name = typeof(T).Name;
                    AssetDatabase.AddObjectToAsset(c, profile);
                }
                return c;
            }
            var tone = Get<Tonemapping>();
            tone.mode.Override(TonemappingMode.ACES);
            var bloom = Get<Bloom>();
            bloom.threshold.Override(1.1f);      // only genuinely bright things glow: sun, emissives, accretion disk
            bloom.intensity.Override(0.35f);
            bloom.scatter.Override(0.6f);
            var color = Get<ColorAdjustments>();
            color.postExposure.Override(0.15f);
            color.contrast.Override(8f);
            color.saturation.Override(-4f);
            var vignette = Get<Vignette>();
            vignette.intensity.Override(0.18f);
            vignette.smoothness.Override(0.45f);
            EditorUtility.SetDirty(profile);
        }

        static Material Mat(string name, string shader)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var s = Shader.Find(shader);
            if (s == null) { Debug.LogWarning($"[Event Horizon] Shader {shader} not found"); return null; }
            if (m == null) { m = new Material(s); AssetDatabase.CreateAsset(m, path); }
            else m.shader = s;
            return m;
        }

        static void Materials()
        {
            var stars = AssetDatabase.LoadAssetAtPath<Texture2D>(SSS + "8k_stars_milky_way.jpg");
            var sky = Mat("Sky_MarsDynamic", "EventHorizon/MarsSky");
            if (sky != null && stars != null) sky.SetTexture("_StarTex", stars);

            Mat("AtmosphereRim_Mars", "EventHorizon/AtmosphereRim");

            var dust = Mat("DustMotes", "Universal Render Pipeline/Particles/Unlit");
            if (dust != null)
            {
                dust.SetTexture("_BaseMap", SoftParticleTexture());
                dust.SetColor("_BaseColor", new Color(0.82f, 0.62f, 0.45f, 0.35f));
                dust.SetFloat("_Surface", 1f);
                dust.SetFloat("_Blend", 0f);
                dust.SetOverrideTag("RenderType", "Transparent");
                dust.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                dust.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                dust.SetFloat("_ZWrite", 0f);
                dust.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                dust.renderQueue = (int)RenderQueue.Transparent;
            }

            var clouds = Mat("Mars_DustClouds", "Universal Render Pipeline/Lit");
            if (clouds != null)
            {
                clouds.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(SSS + "2k_earth_clouds.jpg"));
                clouds.SetColor("_BaseColor", new Color(0.95f, 0.72f, 0.52f, 0.22f));
                MakeTransparent(clouds);
            }

            var beacon = Mat("Beacon", "Universal Render Pipeline/Unlit");
            if (beacon != null) beacon.SetColor("_BaseColor", new Color(0.3f, 0.85f, 1f) * 6f);

            var screen = Mat("TerminalScreen", "Universal Render Pipeline/Lit");
            if (screen != null)
            {
                screen.SetColor("_BaseColor", new Color(0.02f, 0.05f, 0.08f));
                screen.SetColor("_EmissionColor", new Color(0.15f, 0.6f, 0.85f) * 2.2f);
                screen.EnableKeyword("_EMISSION");
                screen.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                screen.SetFloat("_Smoothness", 0.9f);
            }
            var hull = Mat("SpacecraftHull", "Universal Render Pipeline/Lit");
            if (hull != null) { hull.SetColor("_BaseColor", new Color(0.82f, 0.82f, 0.8f)); hull.SetFloat("_Metallic", 0.7f); hull.SetFloat("_Smoothness", 0.55f); }
            var lamp = Mat("HabitatLamp", "Universal Render Pipeline/Unlit");
            if (lamp != null) lamp.SetColor("_BaseColor", new Color(1f, 0.85f, 0.6f) * 4f);
            foreach (var m in new[] { sky, dust, clouds, beacon, screen, hull, lamp }) if (m != null) EditorUtility.SetDirty(m);
        }

        /// <summary>64x64 soft round sprite for dust particles (otherwise they render as squares).</summary>
        static Texture2D SoftParticleTexture()
        {
            const string path = AssetIntegrationBuilder.Out + "/Rendering/SoftParticle.png";
            if (!File.Exists(path))
            {
                var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32f, 32f)) / 32f;
                    float a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * (3f - 2f * a)));
                }
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                if (AssetImporter.GetAtPath(path) is TextureImporter ti)
                {
                    ti.alphaIsTransparency = true;
                    ti.mipmapEnabled = true;
                    ti.wrapMode = TextureWrapMode.Clamp;
                    ti.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static void MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        /// <summary>Adds a global post-processing volume to the open scene.</summary>
        public static void AddGlobalVolume()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProfilePath);
            if (profile == null) return;
            var go = new GameObject("Global Volume");
            var v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.sharedProfile = profile;
        }
    }
}
