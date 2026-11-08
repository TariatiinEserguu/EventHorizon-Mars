using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EventHorizon.EditorTools
{
    /// <summary>
    /// First-open setup: linear colour space and a URP pipeline asset, so a fresh clone renders with URP.
    /// Runs once per editor load; it only creates things that are missing.
    /// </summary>
    [InitializeOnLoad]
    internal static class ProjectBootstrap
    {
        const string SettingsDir = "Assets/EventHorizon/Settings";
        const string PipelinePath = SettingsDir + "/EventHorizon_URP.asset";
        const string RendererPath = SettingsDir + "/EventHorizon_Renderer.asset";
        const string DefaultPostProcessData = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

        static ProjectBootstrap() => EditorApplication.delayCall += Run;

        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;
            if (GraphicsSettings.defaultRenderPipeline == null) CreatePipeline();
        }

        [MenuItem("Event Horizon/Setup/Create or Assign URP Pipeline Asset", priority = 100)]
        internal static void CreatePipeline()
        {
            try
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
                if (asset == null)
                {
                    Directory.CreateDirectory(SettingsDir);
                    var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                    var ppd = AssetDatabase.LoadAssetAtPath<PostProcessData>(DefaultPostProcessData);
                    if (ppd != null) renderer.postProcessData = ppd;
                    AssetDatabase.CreateAsset(renderer, RendererPath);

                    asset = UniversalRenderPipelineAsset.Create(renderer);
                    asset.supportsHDR = true;
                    asset.msaaSampleCount = 2;
                    asset.renderScale = 1f;
                    asset.shadowDistance = 120f;
                    AssetDatabase.CreateAsset(asset, PipelinePath);
                    AssetDatabase.SaveAssets();
                }
                GraphicsSettings.defaultRenderPipeline = asset;
                QualitySettings.renderPipeline = asset;
                Debug.Log($"[Event Horizon] URP pipeline assigned: {PipelinePath}");
            }
            catch (Exception e)
            {
                Debug.LogError("[Event Horizon] Could not create the URP asset automatically. Create one with " +
                               "Assets > Create > Rendering > URP Asset (with Universal Renderer) and assign it in " +
                               "Project Settings > Graphics.\n" + e);
            }
        }
    }
}
