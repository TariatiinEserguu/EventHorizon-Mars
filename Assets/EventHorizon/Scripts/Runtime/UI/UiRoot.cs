using EventHorizon.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;

namespace EventHorizon.UI
{
    /// <summary>Creates UI Toolkit documents that share one panel, the theme and the stylesheet.</summary>
    public static class UiRoot
    {
        static PanelSettings s_panel;
        static StyleSheet s_style;

        public static PanelSettings Panel => s_panel != null ? s_panel : s_panel = Resources.Load<PanelSettings>("UI/EventHorizonPanel");
        public static StyleSheet Style => s_style != null ? s_style : s_style = Resources.Load<StyleSheet>("UI/EventHorizon");

        /// <summary>Makes a full-screen document. Higher <paramref name="sortOrder"/> draws on top.</summary>
        public static UIDocument Create(string name, int sortOrder, bool persistent = false)
        {
            EnsureEventSystem();
            var go = new GameObject(name);
            if (persistent) Object.DontDestroyOnLoad(go);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = Panel;
            doc.sortingOrder = sortOrder;
            var root = doc.rootVisualElement;
            if (Style != null) root.styleSheets.Add(Style);
            root.AddToClassList("eh-root");
            root.pickingMode = PickingMode.Ignore;
            ApplyAccessibility(root);
            SettingsService.Changed += () => { if (doc != null) ApplyAccessibility(doc.rootVisualElement); };
            Loc.Changed += () => { if (doc != null) ApplyAccessibility(doc.rootVisualElement); };
            return doc;
        }

        public static void ApplyAccessibility(VisualElement root)
        {
            if (root == null) return;
            var a = SettingsService.Current.accessibility;
            if (Panel != null) Panel.scale = Mathf.Clamp(a.uiScale, 0.75f, 1.5f);
            foreach (var cls in new[] { "cb-protanopia", "cb-deuteranopia", "cb-tritanopia" }) root.RemoveFromClassList(cls);
            if (a.colorblind != ColorblindMode.Off) root.AddToClassList("cb-" + a.colorblind.ToString().ToLowerInvariant());
            root.EnableInClassList("eh-simple", a.simplifiedInterface);
            // Language class: Mongolian swaps in the Cyrillic fonts (see EventHorizon.uss), every language gets its own hook.
            foreach (var l in Loc.Languages) root.EnableInClassList("lang-" + l.code, l.code == Loc.Current);
        }

        /// <summary>UI Toolkit routes pointer and keyboard input through the Input System's UI module.</summary>
        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Object.DontDestroyOnLoad(es);
        }
    }
}
