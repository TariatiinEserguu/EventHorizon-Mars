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
            UiKit.Text(header, Loc.T("SET_TITLE"), "eh-panel-title");
            UiKit.Box(header, "eh-spacer");
            UiKit.Small(header, Loc.T("SET_RESET_TAB"), ResetTab, "eh-btn-danger");
            UiKit.Small(header, Loc.T("BTN_BACK"), () => { CancelRebind(); UiAudio.Back(); onBack?.Invoke(); });

            var tabs = UiKit.Box(Element, "eh-row");
            tabs.style.marginBottom = 8;
            foreach (var t in new[] { "GRAPHICS", "AUDIO", "CONTROLS", "GAMEPLAY", "LANGUAGE", "ACCESSIBILITY" })
            {
                string name = t;
                _tabs[t] = UiKit.Button(tabs, Loc.T("SET_TAB_" + t), () => Show(name), "eh-tab");
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
                case "LANGUAGE": BuildLanguage(); break;
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
            var names = new List<string> { Loc.T("SET_NATIVE") };
            names.AddRange(res.Select(r => $"{r.width} x {r.height}"));
            int idx = g.resolutionWidth == 0 ? 0 : 1 + res.FindIndex(r => r.width == g.resolutionWidth && r.height == g.resolutionHeight);
            UiKit.Choice(c, Loc.T("SET_RESOLUTION"), names.ToArray(), Mathf.Max(0, idx), i =>
            {
                g.resolutionWidth = i == 0 ? 0 : res[i - 1].width;
                g.resolutionHeight = i == 0 ? 0 : res[i - 1].height;
                SettingsService.Save();
                SettingsService.Apply(applyResolution: true);
            });
            UiKit.Choice(c, Loc.T("SET_DISPLAY_MODE"), new[] { Loc.T("SET_FULLSCREEN"), Loc.T("SET_WINDOWED") }, g.fullscreen ? 0 : 1, i =>
            {
                g.fullscreen = i == 0;
                SettingsService.Save();
                SettingsService.Apply(applyResolution: true);
            });
            UiKit.Toggle(c, Loc.T("SET_VSYNC"), g.vSync, v => { g.vSync = v; Changed(); });
            int[] fps = { 30, 60, 90, 120, 0 };
            UiKit.Choice(c, Loc.T("SET_FPS_LIMIT"), new[] { "30", "60", "90", "120", Loc.T("SET_UNLIMITED") }, Array.IndexOf(fps, g.fpsLimit) < 0 ? 3 : Array.IndexOf(fps, g.fpsLimit),
                i => { g.fpsLimit = fps[i]; Changed(); });
            UiKit.Divider(c);

            UiKit.EnumChoice(c, Loc.T("SET_PRESET"), g.preset, p =>
            {
                if (p != GraphicsPreset.Custom) S.ApplyPreset(p); else g.preset = p;
                Changed();
                Show("GRAPHICS"); // refresh the individual options
            }, EnumName);
            UiKit.Slider(c, Loc.T("SET_RENDER_SCALE"), 0.5f, 1f, g.renderScale, v => { g.renderScale = v; Changed(true); }, v => $"{v * 100f:0}%");
            UiKit.EnumChoice(c, Loc.T("SET_TEXTURES"), g.textures, v => { g.textures = v; Changed(true); }, EnumName);
            UiKit.EnumChoice(c, Loc.T("SET_SHADOWS"), g.shadows, v => { g.shadows = v; Changed(true); }, EnumName);
            UiKit.EnumChoice(c, Loc.T("SET_AA"), g.antiAliasing, v => { g.antiAliasing = v; Changed(true); }, v => v switch
            {
                AntiAliasing.MSAA2x => "MSAA 2X", AntiAliasing.MSAA4x => "MSAA 4X", AntiAliasing.Off => Loc.T("SET_OFF"), _ => v.ToString().ToUpperInvariant(),
            });
            UiKit.Toggle(c, Loc.T("SET_SSAO"), g.ambientOcclusion, v => { g.ambientOcclusion = v; Changed(true); });
            UiKit.Slider(c, Loc.T("SET_VIEW_DISTANCE"), 800f, 3000f, g.viewDistance, v => { g.viewDistance = v; Changed(true); }, v => Loc.T("HUD_METRES", v.ToString("0")));
            UiKit.Toggle(c, Loc.T("SET_POST"), g.postProcessing, v => { g.postProcessing = v; Changed(true); });
        }

        void BuildAudio()
        {
            var a = S.audio;
            var r = UiResources.Instance;
            void Preview(AudioClip clip, AudioCategory cat) => UiAudio.Play(clip, cat);
            UiKit.Slider(_content, Loc.T("SET_MASTER"), 0f, 1f, a.master, v => { a.master = v; Changed(); }, Pct, () => Preview(r?.confirm, AudioCategory.Effects));
            UiKit.Slider(_content, Loc.T("SET_MUSIC"), 0f, 1f, a.music, v => { a.music = v; Changed(); }, Pct, () => Preview(r?.previewMusic, AudioCategory.Music));
            UiKit.Slider(_content, Loc.T("SET_EFFECTS"), 0f, 1f, a.effects, v => { a.effects = v; Changed(); }, Pct, () => Preview(r?.previewEffects, AudioCategory.Effects));
            UiKit.Slider(_content, Loc.T("SET_AMBIENT"), 0f, 1f, a.ambient, v => { a.ambient = v; Changed(); }, Pct, () => Preview(r?.previewAmbient, AudioCategory.Ambient));
            UiKit.Slider(_content, Loc.T("SET_VOICE"), 0f, 1f, a.voice, v => { a.voice = v; Changed(); }, Pct, () => Preview(r?.previewVoice, AudioCategory.Voice));
            UiKit.Text(_content, Loc.T("SET_AUDIO_HINT"), "eh-dim", "eh-small");
        }

        static string Pct(float v) => $"{v * 100f:0}%";

        void BuildControls()
        {
            var c = _content;
            UiKit.Text(c, Loc.T("SET_REBIND_HINT"), "eh-dim", "eh-small");
            foreach (var name in GameInput.Rebindable)
            {
                var action = GameInput.Player.FindAction(name);
                var right = UiKit.Row(c, ActionLabel(name));
                var value = UiKit.Text(right, GameInput.DisplayBinding(action), "eh-choice-value");
                if (name != "Move")
                    UiKit.Small(right, Loc.T("SET_REBIND"), () => StartRebind(action, value));
                else
                    UiKit.Text(right, Loc.T("SET_FIXED"), "eh-dim", "eh-small");
            }
            var look = UiKit.Row(c, Loc.T("ACTION_LOOK"));
            UiKit.Text(look, Loc.T("KEY_MOUSE"), "eh-choice-value");
            var pause = UiKit.Row(c, Loc.T("ACTION_PAUSE"));
            UiKit.Text(pause, "ESC", "eh-choice-value");
            UiKit.Divider(c);
            UiKit.Slider(c, Loc.T("SET_MOUSE_SENS"), 0.02f, 0.4f, S.controls.mouseSensitivity, v => { S.controls.mouseSensitivity = v; Changed(); }, v => $"{v / 0.12f:0.00}x");
            UiKit.Toggle(c, Loc.T("SET_INVERT_Y"), S.controls.invertY, v => { S.controls.invertY = v; Changed(); });
            var row = UiKit.Box(c, "eh-row");
            row.style.marginTop = 10;
            UiKit.Small(row, Loc.T("SET_RESET_KEYS"), () => UiKit.Confirm(_root, Loc.T("SET_RESET_CONTROLS"),
                Loc.T("SET_RESET_CONTROLS_Q"), Loc.T("BTN_RESET"), () => { GameInput.ResetOverrides(); Show("CONTROLS"); }), "eh-btn-danger");
        }

        static string ActionLabel(string action) => Loc.T("ACTION_" + action.ToUpperInvariant());

        /// <summary>Localized enum value (Low, Medium, High, Custom, Casual ...).</summary>
        static string EnumName<T>(T v) where T : Enum => Loc.T("ENUM_" + v.ToString().ToUpperInvariant());

        void StartRebind(InputAction action, Label value)
        {
            CancelRebind();
            int index = GameInput.KeyboardBindingIndex(action);
            if (index < 0) return;
            value.text = Loc.T("SET_PRESS_KEY");
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
            UiKit.EnumChoice(c, Loc.T("SET_DIFFICULTY"), gp.difficulty, v => { gp.difficulty = v; Changed(); }, EnumName);
            UiKit.Text(c, Loc.T("SET_DIFFICULTY_HINT"), "eh-dim", "eh-small");
            UiKit.EnumChoice(c, Loc.T("SET_TUTORIAL"), gp.tutorial, v => { gp.tutorial = v; Changed(); }, EnumName);
            UiKit.Toggle(c, Loc.T("SET_SUBTITLES"), gp.subtitles, v => { gp.subtitles = v; Changed(); });
            UiKit.Text(c, Loc.T("SET_SUBTITLES_HINT"), "eh-dim", "eh-small");
            float[] speeds = { 30f, 60f, 120f, 300f };
            int si = Array.IndexOf(speeds, gp.simulationSpeed);
            UiKit.Choice(c, Loc.T("SET_SIM_SPEED"), new[] { Loc.T("SET_SPEED_SLOW"), Loc.T("SET_SPEED_NORMAL"), Loc.T("SET_SPEED_FAST"), Loc.T("SET_SPEED_VERY_FAST") }, si < 0 ? 1 : si,
                i => { gp.simulationSpeed = speeds[i]; Changed(); });
            UiKit.Slider(c, Loc.T("SET_CAMERA_SENS"), 0.02f, 0.4f, S.controls.mouseSensitivity, v => { S.controls.mouseSensitivity = v; Changed(); }, v => $"{v / 0.12f:0.00}x");
            UiKit.Choice(c, Loc.T("SET_DEFAULT_CAMERA"), new[] { Loc.T("SET_FIRST_PERSON"), Loc.T("SET_THIRD_PERSON") }, gp.firstPerson ? 0 : 1, i => { gp.firstPerson = i == 0; Changed(); });
            UiKit.Toggle(c, Loc.T("SET_SHOW_HUD"), gp.showHud, v => { gp.showHud = v; Changed(); });
            UiKit.EnumChoice(c, Loc.T("SET_SCIENCE_DETAIL"), gp.scienceDetail, v => { gp.scienceDetail = v; Changed(); }, EnumName);
        }

        /// <summary>Interface, subtitle and voice languages, set separately. Changes apply immediately.</summary>
        void BuildLanguage()
        {
            var c = _content;
            UiKit.Text(c, Loc.T("LANG_INTERFACE"), "eh-kicker");
            LanguagePicker.Cards(c);
            UiKit.Divider(c);
            var langs = Loc.Languages;
            string[] names = new string[langs.Count];
            for (int i = 0; i < langs.Count; i++) names[i] = langs[i].nativeName;
            int Index(string code) { for (int i = 0; i < langs.Count; i++) if (langs[i].code == code) return i; return 0; }
            UiKit.Choice(c, Loc.T("LANG_SUBTITLES"), names, Index(Loc.SubtitleLanguage), i => Loc.SetSubtitleLanguage(langs[i].code));
            UiKit.Choice(c, Loc.T("LANG_VOICE"), names, Index(S.language.voice), i => Loc.SetVoiceLanguage(langs[i].code));
            UiKit.Text(c, Loc.T("LANG_VOICE_HINT"), "eh-dim", "eh-small");
            UiKit.Toggle(c, Loc.T("SET_SUBTITLES"), S.gameplay.subtitles, v => { S.gameplay.subtitles = v; Changed(); });
            UiKit.Slider(c, Loc.T("SET_SUBTITLE_SPEED"), 0.5f, 2.5f, S.gameplay.subtitleSpeed, v => { S.gameplay.subtitleSpeed = v; Changed(); }, v => $"{v:0.0}x");
            UiKit.Text(c, Loc.T("LANG_PROGRESS_SAFE"), "eh-dim", "eh-small");
        }

        void BuildAccessibility()
        {
            var a = S.accessibility;
            var c = _content;
            UiKit.Slider(c, Loc.T("SET_SUBTITLE_SIZE"), 0.75f, 1.75f, a.subtitleSize, v => { a.subtitleSize = v; Changed(); }, v => $"{v * 100f:0}%");
            UiKit.Slider(c, Loc.T("SET_UI_SCALE"), 0.75f, 1.5f, a.uiScale, v => { a.uiScale = v; Changed(); }, v => $"{v * 100f:0}%");
            UiKit.EnumChoice(c, Loc.T("SET_COLORBLIND"), a.colorblind, v => { a.colorblind = v; Changed(); }, EnumName);
            UiKit.Slider(c, Loc.T("SET_CAMERA_SENS"), 0.02f, 0.4f, S.controls.mouseSensitivity, v => { S.controls.mouseSensitivity = v; Changed(); }, v => $"{v / 0.12f:0.00}x");
            UiKit.Toggle(c, Loc.T("SET_SCREEN_SHAKE"), a.screenShake, v => { a.screenShake = v; Changed(); });
            UiKit.Toggle(c, Loc.T("SET_MOTION"), a.motionEffects, v => { a.motionEffects = v; Changed(); });
            UiKit.Toggle(c, Loc.T("SET_SIMPLE_UI"), a.simplifiedInterface, v => { a.simplifiedInterface = v; Changed(); });
        }

        void ResetTab()
        {
            UiKit.Confirm(_root, Loc.T("SET_RESET_TAB"), Loc.T("SET_RESET_TAB_Q", Loc.T("SET_TAB_" + _tab)), Loc.T("BTN_RESET"), () =>
            {
                var d = new GameSettings();
                switch (_tab)
                {
                    case "GRAPHICS": S.graphics = d.graphics; S.ApplyPreset(GraphicsPreset.High); break;
                    case "AUDIO": S.audio = d.audio; break;
                    case "CONTROLS": S.controls.mouseSensitivity = d.controls.mouseSensitivity; S.controls.invertY = false; GameInput.ResetOverrides(); break;
                    case "GAMEPLAY": S.gameplay = d.gameplay; break;
                    case "LANGUAGE": Loc.SetLanguage(Loc.English); Loc.SetSubtitleLanguage(Loc.English); Loc.SetVoiceLanguage(Loc.English); break;
                    default: S.accessibility = d.accessibility; break;
                }
                SettingsService.Save();
                SettingsService.Apply(applyResolution: _tab == "GRAPHICS");
                Show(_tab);
            }, danger: true);
        }
    }
}
