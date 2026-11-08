using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace EventHorizon.Core
{
    /// <summary>Loads, saves and applies <see cref="GameSettings"/>. Re-applies camera settings on every scene load.</summary>
    public static class SettingsService
    {
        static GameSettings s_current;
        static bool s_hooked;

        public static string FilePath => Path.Combine(Application.persistentDataPath, "settings.json");
        public static event Action Changed;

        public static GameSettings Current
        {
            get
            {
                if (s_current == null) Load();
                return s_current;
            }
        }

        public static void Load()
        {
            try
            {
                s_current = File.Exists(FilePath) ? JsonUtility.FromJson<GameSettings>(File.ReadAllText(FilePath)) : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Event Horizon] Settings file unreadable, using defaults: {e.Message}");
                s_current = null;
            }
            if (s_current == null)
            {
                s_current = new GameSettings();
                s_current.ApplyPreset(GraphicsPreset.High);
            }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(FilePath, JsonUtility.ToJson(Current, true));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Event Horizon] Could not save settings: {e.Message}");
            }
        }

        public static void SaveAndApply()
        {
            Save();
            Apply();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!s_hooked)
            {
                SceneManager.sceneLoaded += (_, _) => ApplySceneDependent();
                s_hooked = true;
            }
            // The screenshot tour runs in a fixed window, so it must not switch to the saved fullscreen mode.
            Apply(applyResolution: Game.ScreenshotDirector.Folder == null);
        }

        public static void Apply(bool applyResolution = false)
        {
            var g = Current.graphics;

            if (applyResolution || !Application.isEditor)
            {
                var native = Screen.currentResolution;
                int w = g.resolutionWidth > 0 ? g.resolutionWidth : native.width;
                int h = g.resolutionHeight > 0 ? g.resolutionHeight : native.height;
                var mode = g.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                if (Screen.width != w || Screen.height != h || Screen.fullScreenMode != mode) Screen.SetResolution(w, h, mode);
            }

            QualitySettings.vSyncCount = g.vSync ? 1 : 0;
            Application.targetFrameRate = g.vSync || g.fpsLimit <= 0 ? -1 : g.fpsLimit;

            // Textures: Low drops the top mip, so 2K textures render at 1K and 1K at 512.
            QualitySettings.globalTextureMipmapLimit = g.textures == QualityLevel.Low ? 1 : 0;
            QualitySettings.anisotropicFiltering = g.textures == QualityLevel.High ? AnisotropicFiltering.ForceEnable
                : g.textures == QualityLevel.Medium ? AnisotropicFiltering.Enable : AnisotropicFiltering.Disable;
            QualitySettings.lodBias = Mathf.Lerp(0.7f, 2f, Mathf.InverseLerp(800f, 3000f, g.viewDistance));

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = Mathf.Clamp(g.renderScale, 0.5f, 1f);
                urp.msaaSampleCount = g.antiAliasing == AntiAliasing.MSAA4x ? 4 : g.antiAliasing == AntiAliasing.MSAA2x ? 2 : 1;
                switch (g.shadows)
                {
                    case QualityLevel.Low: urp.shadowDistance = 50f; urp.shadowCascadeCount = 1; urp.mainLightShadowmapResolution = 1024; break;
                    case QualityLevel.Medium: urp.shadowDistance = 90f; urp.shadowCascadeCount = 2; urp.mainLightShadowmapResolution = 2048; break;
                    default: urp.shadowDistance = 150f; urp.shadowCascadeCount = 4; urp.mainLightShadowmapResolution = 2048; break;
                }
                foreach (var data in urp.rendererDataList)
                {
                    if (data == null) continue;
                    foreach (var feature in data.rendererFeatures)
                        if (feature is ScreenSpaceAmbientOcclusion) feature.SetActive(g.ambientOcclusion);
                }
            }

            var a = Current.audio;
            AudioListener.volume = Mathf.Clamp01(a.master);

            ApplySceneDependent();
            Changed?.Invoke();
        }

        /// <summary>Settings that live on scene objects: cameras, sun shadows, simulation speed.</summary>
        static void ApplySceneDependent()
        {
            var g = Current.graphics;
            foreach (var cam in Camera.allCameras) ApplyToCamera(cam);
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>())
                if (light.type == LightType.Directional && light.shadows != LightShadows.None)
                    light.shadows = g.shadows == QualityLevel.Low ? LightShadows.Hard : LightShadows.Soft;
            if (MarsEnvironment.Instance != null) MarsEnvironment.Instance.timeScale = Current.gameplay.simulationSpeed;
        }

        public static void ApplyToCamera(Camera cam)
        {
            if (cam == null || cam.targetTexture != null) return;
            var g = Current.graphics;
            if (cam.cameraType == CameraType.Game && cam.CompareTag("MainCamera")) cam.farClipPlane = Mathf.Max(cam.farClipPlane > 5000f ? cam.farClipPlane : 0f, g.viewDistance);
            var data = cam.GetUniversalAdditionalCameraData();
            if (data == null) return;
            data.renderPostProcessing = g.postProcessing || g.antiAliasing == AntiAliasing.TAA;
            data.antialiasing = g.antiAliasing switch
            {
                AntiAliasing.FXAA => AntialiasingMode.FastApproximateAntialiasing,
                AntiAliasing.SMAA => AntialiasingMode.SubpixelMorphologicalAntiAliasing,
                AntiAliasing.TAA => AntialiasingMode.TemporalAntiAliasing,
                _ => AntialiasingMode.None,
            };
            data.antialiasingQuality = AntialiasingQuality.High;
        }

        /// <summary>Volume multiplier for a sound category, including the master volume via AudioListener.</summary>
        public static float CategoryVolume(AudioCategory c)
        {
            var a = Current.audio;
            return c switch
            {
                AudioCategory.Music => a.music,
                AudioCategory.Ambient => a.ambient,
                AudioCategory.Voice => a.voice,
                _ => a.effects,
            };
        }
    }

    public enum AudioCategory { Effects, Music, Ambient, Voice }
}
