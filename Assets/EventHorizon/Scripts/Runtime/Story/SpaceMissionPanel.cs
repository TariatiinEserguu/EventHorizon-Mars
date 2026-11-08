using System.Text;
using EventHorizon.Core;
using EventHorizon.Game;
using EventHorizon.Space;
using EventHorizon.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace EventHorizon.Story
{
    /// <summary>
    /// Campaign actions in orbit and in the lab, where there is no astronaut to walk up to things: station
    /// operations, tracking a target, retrieving pods, returning to the colony. Select a target with Tab or 1-9.
    /// </summary>
    public class SpaceMissionPanel : MonoBehaviour
    {
        static SpaceMissionPanel s_instance;
        UIDocument _doc;
        VisualElement _list;
        string _signature;
        float _next;
        OrbitCamera _orbit;

        public static void Ensure()
        {
            if (s_instance != null || GameSession.Mode != GameMode.Campaign) return;
            var doc = UiRoot.Create("Mission Actions", 12);
            s_instance = doc.gameObject.AddComponent<SpaceMissionPanel>();
            s_instance._doc = doc;
            var panel = UiKit.Box(doc.rootVisualElement, "eh-panel", "eh-panel--glass");
            panel.style.position = Position.Absolute;
            panel.style.left = 24;
            panel.style.bottom = 24;
            panel.style.width = 470;
            UiKit.Text(panel, Loc.T("SPACE_ACTIONS"), "eh-kicker");
            s_instance._list = UiKit.Box(panel, "eh-col");
            Loc.Changed += s_instance.Invalidate;
        }

        void Invalidate() => _signature = null;

        void OnDestroy()
        {
            Loc.Changed -= Invalidate;
            if (s_instance == this) s_instance = null;
        }

        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.4f;
            _doc.rootVisualElement.style.display = Hud.Hidden || GameSession.Paused ? DisplayStyle.None : DisplayStyle.Flex;
            if (_orbit == null) _orbit = FindAnyObjectByType<OrbitCamera>();
            var selected = _orbit != null ? _orbit.CurrentTarget : null;
            var actions = StoryContent.SpaceActions(selected);
            var sig = new StringBuilder();
            foreach (var (label, _, enabled) in actions) sig.Append(label).Append(enabled);
            if (sig.ToString() == _signature) return;
            _signature = sig.ToString();
            _list.Clear();
            foreach (var (label, action, enabled) in actions)
            {
                var b = UiKit.Small(_list, label, action);
                b.style.marginTop = 4;
                b.style.whiteSpace = WhiteSpace.Normal;
                b.SetEnabled(enabled && !ExperimentPanel.IsOpen);
            }
        }
    }
}
