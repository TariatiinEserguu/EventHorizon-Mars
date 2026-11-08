using System;
using System.Collections.Generic;
using EventHorizon.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace EventHorizon.UI
{
    /// <summary>Small factory for the menu's reusable building blocks, so every screen looks and behaves the same.</summary>
    public static class UiKit
    {
        public static T Add<T>(VisualElement parent, T child, params string[] classes) where T : VisualElement
        {
            foreach (var c in classes) child.AddToClassList(c);
            parent?.Add(child);
            return child;
        }

        public static VisualElement Box(VisualElement parent, params string[] classes) => Add(parent, new VisualElement(), classes);

        public static Label Text(VisualElement parent, string text, params string[] classes) => Add(parent, new Label(text), classes);

        public static Button Button(VisualElement parent, string text, Action onClick, params string[] classes)
        {
            var b = new Button(() => { UiAudio.Click(); onClick?.Invoke(); }) { text = text };
            b.RegisterCallback<PointerEnterEvent>(_ => { if (b.enabledInHierarchy) UiAudio.Hover(); });
            if (classes.Length == 0) classes = new[] { "eh-btn" };
            return Add(parent, b, classes);
        }

        public static Button Small(VisualElement parent, string text, Action onClick, params string[] extra)
        {
            var classes = new List<string> { "eh-btn-small" };
            classes.AddRange(extra);
            return Button(parent, text, onClick, classes.ToArray());
        }

        public static VisualElement Divider(VisualElement parent) => Box(parent, "eh-divider");

        public static ScrollView Scroll(VisualElement parent, params string[] classes)
        {
            var s = Add(parent, new ScrollView(ScrollViewMode.Vertical), classes);
            s.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            return s;
        }

        /// <summary>A labelled settings row. Returns the right-hand container for the control.</summary>
        public static VisualElement Row(VisualElement parent, string label, string tooltip = null)
        {
            var row = Box(parent, "eh-setting");
            if (tooltip != null) row.tooltip = tooltip;
            Text(row, label, "eh-setting-label");
            return Box(row, "eh-setting-value");
        }

        /// <summary>Left/right selector, easier to read at a glance than a dropdown.</summary>
        public static Label Choice(VisualElement parent, string label, string[] options, int index, Action<int> changed)
        {
            var right = Row(parent, label);
            int current = Mathf.Clamp(index, 0, options.Length - 1);
            Label value = null;
            void Set(int i)
            {
                current = (i + options.Length) % options.Length;
                value.text = options[current];
                changed?.Invoke(current);
            }
            Button(right, "<", () => Set(current - 1), "eh-arrow");
            value = Text(right, options[current], "eh-choice-value");
            Button(right, ">", () => Set(current + 1), "eh-arrow");
            return value;
        }

        public static Label EnumChoice<T>(VisualElement parent, string label, T value, Action<T> changed, Func<T, string> name = null) where T : Enum
        {
            var values = (T[])Enum.GetValues(typeof(T));
            var names = Array.ConvertAll(values, v => name != null ? name(v) : Pretty(v.ToString()));
            return Choice(parent, label, names, Array.IndexOf(values, value), i => changed(values[i]));
        }

        public static Slider Slider(VisualElement parent, string label, float min, float max, float value,
                                    Action<float> changed, Func<float, string> format = null, Action released = null)
        {
            var right = Row(parent, label);
            var s = Add(right, new Slider(min, max) { value = value });
            var readout = Text(right, format != null ? format(value) : value.ToString("0.00"), "eh-choice-value");
            readout.style.minWidth = 90;
            s.RegisterValueChangedCallback(e =>
            {
                readout.text = format != null ? format(e.newValue) : e.newValue.ToString("0.00");
                changed?.Invoke(e.newValue);
            });
            if (released != null) s.RegisterCallback<PointerCaptureOutEvent>(_ => released());
            return s;
        }

        public static Toggle Toggle(VisualElement parent, string label, bool value, Action<bool> changed)
        {
            var right = Row(parent, label);
            var t = Add(right, new Toggle { value = value });
            t.RegisterValueChangedCallback(e => { UiAudio.Click(); changed?.Invoke(e.newValue); });
            return t;
        }

        public static TextField Field(VisualElement parent, string label, string value, Action<string> changed, int maxLength = 24)
        {
            var right = Row(parent, label);
            var f = Add(right, new TextField { value = value, maxLength = maxLength });
            f.RegisterValueChangedCallback(e => changed?.Invoke(e.newValue));
            return f;
        }

        public static VisualElement Bar(VisualElement parent, out VisualElement fill)
        {
            var bar = Box(parent, "eh-bar");
            fill = Box(bar, "eh-bar-fill");
            return bar;
        }

        public static void SetFill(VisualElement fill, float t, bool warn = false)
        {
            fill.style.width = Length.Percent(Mathf.Clamp01(t) * 100f);
            fill.EnableInClassList("eh-bar-fill--warn", warn);
        }

        // ------------------------------------------------------------------ dialogs

        /// <summary>Yes/No dialog over everything in <paramref name="root"/>.</summary>
        public static void Confirm(VisualElement root, string title, string message, string okText, Action onOk, bool danger = false)
        {
            var bg = Box(root, "eh-modal-bg");
            bg.pickingMode = PickingMode.Position;
            var m = Box(bg, "eh-panel", "eh-modal");
            Text(m, title, "eh-panel-title");
            Text(m, message, "eh-body");
            var row = Box(m, "eh-row");
            row.style.justifyContent = Justify.FlexEnd;
            row.style.marginTop = 16;
            Small(row, "CANCEL", () => { UiAudio.Back(); bg.RemoveFromHierarchy(); });
            Small(row, okText, () => { bg.RemoveFromHierarchy(); onOk?.Invoke(); }, danger ? "eh-btn-danger" : "eh-btn-primary");
        }

        public static void Message(VisualElement root, string title, string message)
        {
            var bg = Box(root, "eh-modal-bg");
            bg.pickingMode = PickingMode.Position;
            var m = Box(bg, "eh-panel", "eh-modal");
            Text(m, title, "eh-panel-title");
            Text(m, message, "eh-body");
            var row = Box(m, "eh-row");
            row.style.justifyContent = Justify.FlexEnd;
            row.style.marginTop = 16;
            Small(row, "OK", () => bg.RemoveFromHierarchy(), "eh-btn-primary");
        }

        /// <summary>Text input dialog (rename save, name a save).</summary>
        public static void Prompt(VisualElement root, string title, string initial, Action<string> onOk, int maxLength = 32)
        {
            var bg = Box(root, "eh-modal-bg");
            bg.pickingMode = PickingMode.Position;
            var m = Box(bg, "eh-panel", "eh-modal");
            Text(m, title, "eh-panel-title");
            var field = Add(m, new TextField { value = initial, maxLength = maxLength });
            var row = Box(m, "eh-row");
            row.style.justifyContent = Justify.FlexEnd;
            row.style.marginTop = 16;
            Small(row, "CANCEL", () => bg.RemoveFromHierarchy());
            Small(row, "OK", () => { bg.RemoveFromHierarchy(); onOk?.Invoke(field.value); }, "eh-btn-primary");
            field.schedule.Execute(() => field.Focus()).StartingIn(50);
        }

        public static string Pretty(string enumName)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < enumName.Length; i++)
            {
                if (i > 0 && char.IsUpper(enumName[i]) && !char.IsUpper(enumName[i - 1])) sb.Append(' ');
                sb.Append(char.ToUpperInvariant(enumName[i]));
            }
            return sb.ToString();
        }

        public static string Duration(double seconds)
        {
            var t = TimeSpan.FromSeconds(seconds);
            return t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes:00}m" : $"{t.Minutes}m {t.Seconds:00}s";
        }
    }

    /// <summary>Menu sounds, scaled by the Effects volume.</summary>
    public static class UiAudio
    {
        static AudioSource s_source;

        static AudioSource Source
        {
            get
            {
                if (s_source != null) return s_source;
                var go = new GameObject("UI Audio");
                UnityEngine.Object.DontDestroyOnLoad(go);
                s_source = go.AddComponent<AudioSource>();
                s_source.playOnAwake = false;
                s_source.spatialBlend = 0f;
                s_source.ignoreListenerPause = true;
                return s_source;
            }
        }

        public static void Play(AudioClip clip, AudioCategory category = AudioCategory.Effects, float volume = 1f)
        {
            if (clip == null) return;
            Source.PlayOneShot(clip, volume * SettingsService.CategoryVolume(category));
        }

        public static void Hover() => Play(UiResources.Instance?.hover, AudioCategory.Effects, 0.25f);
        public static void Click() => Play(UiResources.Instance?.click, AudioCategory.Effects, 0.5f);
        public static void Back() => Play(UiResources.Instance?.back, AudioCategory.Effects, 0.5f);
        public static void Confirm() => Play(UiResources.Instance?.confirm, AudioCategory.Effects, 0.6f);
    }
}
