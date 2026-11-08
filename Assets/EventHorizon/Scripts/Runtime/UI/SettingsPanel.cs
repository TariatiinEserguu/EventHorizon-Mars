using System;
using System.Collections.Generic;
using System.Linq;
using EventHorizon.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace EventHorizon.UI
{
    /// <summary>
    /// Graphics, Audio, Controls, Gameplay and Accessibility tabs. Every change applies immediately and is saved
    /// to settings.json. Used by both the main menu and the pause menu.
    /// </summary>
    public class SettingsPanel
    {
        readonly VisualElement _root;      // modal layer for dialogs
        readonly VisualElement _content;
        readonly Dictionary<string, Button> _tabs = new();
        string _tab = "GRAPHICS";
        InputActionRebindingExtensions.RebindingOperation _rebind;

        static GameSettings S => SettingsService.Current;

        public VisualElement Element { get; }

        public SettingsPanel(VisualElement modalRoot, Action onBack)
        {
            _root = modalRoot;
            Element = new VisualElement();
            Element.AddToClassList("eh-panel");
            Element.style.flexGrow = 1;

            var header = UiKit.Box(Element, "eh-row");
            UiKit.Text(header, "SETTINGS", "eh-panel-title");
            UiKit.Box(header, "eh-spacer");
            UiKit.Small(header, "RESET TAB", ResetTab, "eh-btn-danger");
            UiKit.Small(header, "BACK", () => { CancelRebind(); UiAudio.Back(); onBack?.Invoke(); });

            var tabs = UiKit.Box(Element, "eh-row");
            tabs.style.marginBottom = 8;
            foreach (var t in new[] { "GRAPHICS", "AUDIO", "CONTROLS", "GAMEPLAY", "ACCESSIBILITY" })
            {
                string name = t;
                _tabs[t] = UiKit.Button(tabs, t, () => Show(name), "eh-tab");
            }
            _content = UiKit.Scroll(Element);
            _content.style.flexGrow = 1;
            Show(_tab);
        }

        public void Show(string tab)
        {
            CancelRebind();
            _tab = tab;
            foreach (var kv in _tabs) kv.Value.EnableInClassList("eh-tab--active", kv.Key == tab);
            _content.Clear();
            switch (tab)
            {
                case "GRAPHICS": BuildGraphics(); break;
                case "AUDIO": BuildAudio(); break;
                case "CONTROLS": BuildControls(); break;
                case "GAMEPLAY": BuildGameplay(); break;
                default: BuildAccessibility(); break;
            }
        }

        static void Changed(bool customPreset = false)
        {
            if (customPreset) S.graphics.preset = GraphicsPreset.Custom;
            SettingsService.SaveAndApply();
        }

        void BuildGraphics()
        {
            var g = S.graphics;
            var c = _content;

            var res = Screen.resolutions.Select(r => (r.width, r.height)).Distinct().OrderBy(r => r.width * r.height).ToList();
            if (res.Count == 0) res.Add((Screen.width, Screen.height));
            var names = new List<string> { "NATIVE" };
            names.AddRange(res.Select(r => $"{r.width} x {r.height}"));
            int idx = g.resolutionWidth == 0 ? 0 : 1 + res.FindIndex(r => r.width == g.resolutionWidth && r.height == g.resolutionHeight);
            UiKit.Choice(c, "Resolution", names.ToArray(), Mathf.Max(0, idx), i =>
            {
                g.resolutionWidth = i == 0 ? 0 : res[i - 1].width;
                g.resolutionHeight = i == 0 ? 0 : res[i - 1].height;
                SettingsService.Save();
                SettingsService.Apply(applyResolution: true);
            });
            UiKit.Choice(c, "Display mode", new[] { "FULLSCREEN", "WINDOWED" }, g.fullscreen ? 0 : 1, i =>
            {
                g.fullscreen = i == 0;
                SettingsService.Save();
                SettingsService.Apply(applyResolution: true);
            });
            UiKit.Toggle(c, "VSync", g.vSync, v => { g.vSync = v; Changed(); });
            int[] fps = { 30, 60, 90, 120, 0 };
            UiKit.Choice(c, "FPS limit", new[] { "30", "60", "90", "120", "UNLIMITED" }, Array.IndexOf(fps, g.fpsLimit) < 0 ? 3 : Array.IndexOf(fps, g.fpsLimit),
                i => { g.fpsLimit = fps[i]; Changed(); });
            UiKit.Divider(c);

            UiKit.EnumChoice(c, "Graphics preset", g.preset, p =>
            {
                if (p != GraphicsPreset.Custom) S.ApplyPreset(p); else g.preset = p;
                Changed();
                Show("GRAPHICS"); // refresh the individual options
            });
            UiKit.Slider(c, "Render scale", 0.5f, 1f, g.renderScale, v => { g.renderScale = v; Changed(true); }, v => $"{v * 100f:0}%");
            UiKit.EnumChoice(c, "Texture quality", g.textures, v => { g.textures = v; Changed(true); });
            UiKit.EnumChoice(c, "Shadow quality", g.shadows, v => { g.shadows = v; Changed(true); });
            UiKit.EnumChoice(c, "Anti-aliasing", g.antiAliasing, v => { g.antiAliasing = v; Changed(true); }, v => v switch
            {
                AntiAliasing.MSAA2x => "MSAA 2X", AntiAliasing.MSAA4x => "MSAA 4X", _ => v.ToString().ToUpperInvariant(),
            });
            UiKit.Toggle(c, "Ambient occlusion (SSAO)", g.ambientOcclusion, v => { g.ambientOcclusion = v; Changed(true); });
            UiKit.Slider(c, "View distance", 800f, 3000f, g.viewDistance, v => { g.viewDistance = v; Changed(true); }, v => $"{v:0} m");
            UiKit.Toggle(c, "Post-processing", g.postProcessing, v => { g.postProcessing = v; Changed(true); });
        }

        void BuildAudio()
        {
            var a = S.audio;
            var r = UiResources.Instance;
            void Preview(AudioClip clip, AudioCategory cat) => UiAudio.Play(clip, cat);
            UiKit.Slider(_content, "Master volume", 0f, 1f, a.master, v => { a.master = v; Changed(); }, Pct, () => Preview(r?.confirm, AudioCategory.Effects));
            UiKit.Slider(_content, "Music", 0f, 1f, a.music, v => { a.music = v; Changed(); }, Pct, () => Preview(r?.previewMusic, AudioCategory.Music));
            UiKit.Slider(_content, "Sound effects", 0f, 1f, a.effects, v => { a.effects = v; Changed(); }, Pct, () => Preview(r?.previewEffects, AudioCategory.Effects));
            UiKit.Slider(_content, "Ambient sound", 0f, 1f, a.ambient, v => { a.ambient = v; Changed(); }, Pct, () => Preview(r?.previewAmbient, AudioCategory.Ambient));
            UiKit.Slider(_content, "Voice", 0f, 1f, a.voice, v => { a.voice = v; Changed(); }, Pct, () => Preview(r?.previewVoice, AudioCategory.Voice));
            UiKit.Text(_content, "Release a slider to hear a preview at the new volume.", "eh-dim", "eh-small");
        }

        static string Pct(float v) => $"{v * 100f:0}%";

        void BuildControls()
        {
            var c = _content;
            UiKit.Text(c, "Select REBIND, then press the new key. Esc cancels.", "eh-dim", "eh-small");
            foreach (var name in GameInput.Rebindable)
            {
                var action = GameInput.Player.FindAction(name);
                var right = UiKit.Row(c, ActionLabel(name));
                var value = UiKit.Text(right, GameInput.DisplayBinding(action), "eh-choice-value");
                if (name != "Move")
                    UiKit.Small(right, "REBIND", () => StartRebind(action, value));
                else
                    UiKit.Text(right, "fixed", "eh-dim", "eh-small");
            }
            var look = UiKit.Row(c, "Look");
            UiKit.Text(look, "MOUSE", "eh-choice-value");
            var pause = UiKit.Row(c, "Pause");
            UiKit.Text(pause, "ESC", "eh-choice-value");
            UiKit.Divider(c);
            UiKit.Slider(c, "Mouse sensitivity", 0.02f, 0.4f, S.controls.mouseSensitivity, v => { S.controls.mouseSensitivity = v; Changed(); }, v => $"{v / 0.12f:0.00}x");
            UiKit.Toggle(c, "Invert vertical look", S.controls.invertY, v => { S.controls.invertY = v; Changed(); });
            var row = UiKit.Box(c, "eh-row");
            row.style.marginTop = 10;
            UiKit.Small(row, "RESET KEYS TO DEFAULT", () => UiKit.Confirm(_root, "RESET CONTROLS",
                "Restore all default key bindings?", "RESET", () => { GameInput.ResetOverrides(); Show("CONTROLS"); }), "eh-btn-danger");
        }

        static string ActionLabel(string action) => action switch
        {
            "ToggleView" => "First / third person",
            "Missions" => "Missions",
            "Map" => "Map",
            "Inventory" => "Inventory",
            _ => action,
        };

        void StartRebind(InputAction action, Label value)
        {
            CancelRebind();
            int index = GameInput.KeyboardBindingIndex(action);
            if (index < 0) return;
            value.text = "PRESS A KEY...";
            action.Disable();
            _rebind = action.PerformInteractiveRebinding(index)
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Gamepad>")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnComplete(op => FinishRebind(action, value))
                .OnCancel(op => FinishRebind(action, value))
                .Start();
        }

        void FinishRebind(InputAction action, Label value)
        {
            _rebind?.Dispose();
            _rebind = null;
            if (!GameInput.GameplayBlocked) action.Enable();
            value.text = GameInput.DisplayBinding(action);
            GameInput.SaveOverrides();
        }

        void CancelRebind()
        {
            _rebind?.Cancel();
            _rebind?.Dispose();
            _rebind = null;
        }

        void BuildGameplay()
        {
            var gp = S.gameplay;
            var c = _content;
            UiKit.EnumChoice(c, "Difficulty", gp.difficulty, v => { gp.difficulty = v; Changed(); });
            UiKit.Text(c, "Changing difficulty affects new sessions. Running games keep their difficulty, except for supply consumption.", "eh-dim", "eh-small");
            UiKit.EnumChoice(c, "Tutorial assistance", gp.tutorial, v => { gp.tutorial = v; Changed(); });
            UiKit.Toggle(c, "Subtitles", gp.subtitles, v => { gp.subtitles = v; Changed(); });
            float[] speeds = { 30f, 60f, 120f, 300f };
            int si = Array.IndexOf(speeds, gp.simulationSpeed);
            UiKit.Choice(c, "Simulation speed", new[] { "SLOW (x30)", "NORMAL (x60)", "FAST (x120)", "VERY FAST (x300)" }, si < 0 ? 1 : si,
                i => { gp.simulationSpeed = speeds[i]; Changed(); });
            UiKit.Slider(c, "Camera sensitivity", 0.02f, 0.4f, S.controls.mouseSensitivity, v => { S.controls.mouseSensitivity = v; Changed(); }, v => $"{v / 0.12f:0.00}x");
            UiKit.Choice(c, "Default camera", new[] { "FIRST PERSON", "THIRD PERSON" }, gp.firstPerson ? 0 : 1, i => { gp.firstPerson = i == 0; Changed(); });
            UiKit.Toggle(c, "Show HUD", gp.showHud, v => { gp.showHud = v; Changed(); });
            UiKit.EnumChoice(c, "Scientific explanations", gp.scienceDetail, v => { gp.scienceDetail = v; Changed(); });
        }

        void BuildAccessibility()
        {
            var a = S.accessibility;
            var c = _content;
            UiKit.Slider(c, "Subtitle size", 0.75f, 1.75f, a.subtitleSize, v => { a.subtitleSize = v; Changed(); }, v => $"{v * 100f:0}%");
            UiKit.Slider(c, "UI scale", 0.75f, 1.5f, a.uiScale, v => { a.uiScale = v; Changed(); }, v => $"{v * 100f:0}%");
            UiKit.EnumChoice(c, "Colourblind-friendly indicators", a.colorblind, v => { a.colorblind = v; Changed(); });
            UiKit.Slider(c, "Camera sensitivity", 0.02f, 0.4f, S.controls.mouseSensitivity, v => { S.controls.mouseSensitivity = v; Changed(); }, v => $"{v / 0.12f:0.00}x");
            UiKit.Toggle(c, "Screen shake", a.screenShake, v => { a.screenShake = v; Changed(); });
            UiKit.Toggle(c, "Motion effects (camera drift, UI motion)", a.motionEffects, v => { a.motionEffects = v; Changed(); });
            UiKit.Toggle(c, "Simplified interface", a.simplifiedInterface, v => { a.simplifiedInterface = v; Changed(); });
        }

        void ResetTab()
        {
            UiKit.Confirm(_root, "RESET " + _tab, $"Restore the default {_tab.ToLowerInvariant()} settings?", "RESET", () =>
            {
                var d = new GameSettings();
                switch (_tab)
                {
                    case "GRAPHICS": S.graphics = d.graphics; S.ApplyPreset(GraphicsPreset.High); break;
                    case "AUDIO": S.audio = d.audio; break;
                    case "CONTROLS": S.controls.mouseSensitivity = d.controls.mouseSensitivity; S.controls.invertY = false; GameInput.ResetOverrides(); break;
                    case "GAMEPLAY": S.gameplay = d.gameplay; break;
                    default: S.accessibility = d.accessibility; break;
                }
                SettingsService.Save();
                SettingsService.Apply(applyResolution: _tab == "GRAPHICS");
                Show(_tab);
            }, danger: true);
        }
    }
}
