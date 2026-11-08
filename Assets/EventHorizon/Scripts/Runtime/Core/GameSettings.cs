using System;
using UnityEngine;

namespace EventHorizon.Core
{
    public enum GraphicsPreset { Low, Medium, High, Custom }
    public enum QualityLevel { Low, Medium, High }
    public enum AntiAliasing { Off, FXAA, SMAA, MSAA2x, MSAA4x, TAA }
    public enum Difficulty { Casual, Standard, Scientist, Engineer }
    public enum TutorialMode { Full, Basic, None }
    public enum ScienceDetail { Basic, Standard, Expert }
    public enum ColorblindMode { Off, Protanopia, Deuteranopia, Tritanopia }

    /// <summary>Everything on the Settings screen. Saved as JSON in Application.persistentDataPath/settings.json.</summary>
    [Serializable]
    public class GameSettings
    {
        public Graphics graphics = new();
        public Audio audio = new();
        public Controls controls = new();
        public Gameplay gameplay = new();
        public Accessibility accessibility = new();
        public LanguageSettings language = new();

        [Serializable]
        public class Graphics
        {
            public int resolutionWidth;          // 0 = native
            public int resolutionHeight;
            public bool fullscreen = true;
            public bool vSync;
            /// <summary>0 = unlimited.</summary>
            public int fpsLimit = 120;
            public float renderScale = 1f;
            public GraphicsPreset preset = GraphicsPreset.High;
            public QualityLevel textures = QualityLevel.High;
            public QualityLevel shadows = QualityLevel.High;
            public AntiAliasing antiAliasing = AntiAliasing.SMAA;
            public bool ambientOcclusion = true;
            /// <summary>Metres the camera draws to.</summary>
            public float viewDistance = 2500f;
            public bool postProcessing = true;
        }

        [Serializable]
        public class Audio
        {
            public float master = 0.8f, music = 0.6f, effects = 0.8f, ambient = 0.7f, voice = 1f;
        }

        [Serializable]
        public class Controls
        {
            public string bindingOverridesJson = "";
            public float mouseSensitivity = 0.12f;
            public bool invertY;
        }

        [Serializable]
        public class Gameplay
        {
            public Difficulty difficulty = Difficulty.Standard;
            public TutorialMode tutorial = TutorialMode.Basic;
            public bool subtitles = true;
            [Tooltip("Seconds per subtitle line are multiplied by this (slower readers can raise it).")]
            public float subtitleSpeed = 1f;
            /// <summary>Game seconds per real second at "normal" speed.</summary>
            public float simulationSpeed = 60f;
            public bool firstPerson = true;
            public bool showHud = true;
            public ScienceDetail scienceDetail = ScienceDetail.Standard;
        }

        [Serializable]
        public class Accessibility
        {
            public float subtitleSize = 1f;
            public float uiScale = 1f;
            public ColorblindMode colorblind = ColorblindMode.Off;
            public bool screenShake = true;
            public bool motionEffects = true;
            public bool simplifiedInterface;
        }

        /// <summary>Applies a preset to the individual quality options (Custom leaves them alone).</summary>
        public void ApplyPreset(GraphicsPreset p)
        {
            graphics.preset = p;
            switch (p)
            {
                case GraphicsPreset.Low:
                    graphics.textures = QualityLevel.Low; graphics.shadows = QualityLevel.Low;
                    graphics.antiAliasing = AntiAliasing.FXAA; graphics.ambientOcclusion = false;
                    graphics.viewDistance = 1200f; graphics.renderScale = 0.85f; graphics.postProcessing = true;
                    break;
                case GraphicsPreset.Medium:
                    graphics.textures = QualityLevel.Medium; graphics.shadows = QualityLevel.Medium;
                    graphics.antiAliasing = AntiAliasing.SMAA; graphics.ambientOcclusion = true;
                    graphics.viewDistance = 1800f; graphics.renderScale = 1f; graphics.postProcessing = true;
                    break;
                case GraphicsPreset.High:
                    graphics.textures = QualityLevel.High; graphics.shadows = QualityLevel.High;
                    graphics.antiAliasing = AntiAliasing.SMAA; graphics.ambientOcclusion = true;
                    graphics.viewDistance = 2500f; graphics.renderScale = 1f; graphics.postProcessing = true;
                    break;
            }
        }
    }
}
