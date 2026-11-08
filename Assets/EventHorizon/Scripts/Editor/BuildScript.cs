using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EventHorizon.EditorTools
{
    /// <summary>
    /// Command-line entry points, for CI or a quick local build:
    ///   Unity -batchmode -projectPath . -executeMethod EventHorizon.EditorTools.BuildScript.SetupAndBuildLinux -quit
    /// </summary>
    public static class BuildScript
    {
        const string MarsScene = "Assets/EventHorizon/Scenes/MarsColony.unity";
        const string SpaceScene = "Assets/EventHorizon/Scenes/SpaceLab.unity";
        public const string LinuxOutput = "Builds/Linux/EventHorizonMars.x86_64";

        /// <summary>Builds prefabs and both scenes, validates assets, then makes a Linux player.</summary>
        public static void SetupAndBuildLinux()
        {
            // In batch mode the editor quits before ProjectBootstrap's delayed first-load setup runs.
            if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;
            if (UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline == null) ProjectBootstrap.CreatePipeline();
            AssetIntegrationBuilder.BuildAll();
            DemoSceneBuilder.BuildMarsColony();
            DemoSceneBuilder.BuildSpaceLab();
            ThirdPartyValidator.Validate();
            BuildLinux();
        }

        [MenuItem("Event Horizon/Build/Linux Player", priority = 200)]
        public static void BuildLinux()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LinuxOutput));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { MarsScene, SpaceScene },
                locationPathName = LinuxOutput,
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[Event Horizon] Build {s.result}: {s.totalErrors} errors, {s.totalSize / 1e6:0} MB, {s.totalTime.TotalSeconds:0} s -> {LinuxOutput}");
            if (Application.isBatchMode && s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
