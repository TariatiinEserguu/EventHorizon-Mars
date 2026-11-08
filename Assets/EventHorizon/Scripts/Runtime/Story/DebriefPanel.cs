using System.Collections.Generic;
using EventHorizon.Core;
using EventHorizon.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace EventHorizon.Story
{
    /// <summary>
    /// Mission debriefing card: what happened, the science covered and the rewards. Doesn't block play; closes by
    /// itself after a while or with its button.
    /// </summary>
    public class DebriefPanel : MonoBehaviour
    {
        static DebriefPanel s_instance;
        static readonly Queue<QuestDef> s_queue = new();
        UIDocument _doc;
        VisualElement _card;
        float _hideAt;

        public static void Show(QuestDef q)
        {
            if (StoryTest.Running) return;
            if (s_instance == null)
            {
                var doc = UiRoot.Create("Debrief", 33);
                s_instance = doc.gameObject.AddComponent<DebriefPanel>();
                s_instance._doc = doc;
            }
            s_queue.Enqueue(q);
            if (s_instance._card == null) s_instance.ShowNext();
        }

        void ShowNext()
        {
            _card?.RemoveFromHierarchy();
            _card = null;
            if (s_queue.Count == 0) return;
            var q = s_queue.Dequeue();
            var root = _doc.rootVisualElement;
            _card = UiKit.Box(root, "eh-panel");
            _card.style.position = Position.Absolute;
            _card.style.right = 24;
            _card.style.top = 170;
            _card.style.width = 560;
            var head = UiKit.Box(_card, "eh-row");
            UiKit.Text(head, Loc.T("DEBRIEF_KICKER"), "eh-kicker");
            UiKit.Box(head, "eh-spacer");
            UiKit.Small(head, Loc.T("BTN_CLOSE"), ShowNext);
            UiKit.Text(_card, Loc.T(q.TitleKey), "eh-panel-title").style.whiteSpace = WhiteSpace.Normal;
            UiKit.Text(_card, Loc.T(q.DebriefKey), "eh-body");
            if (Loc.Has(q.LearnKey))
            {
                UiKit.Text(_card, Loc.T("DEBRIEF_SCIENCE"), "eh-kicker").style.marginTop = 10;
                UiKit.Text(_card, Loc.T(q.LearnKey), "eh-body", "eh-small");
            }
            var rewards = JournalPanel.RewardLines(q.reward);
            if (rewards.Count > 0)
            {
                UiKit.Text(_card, Loc.T("JOURNAL_REWARDS"), "eh-kicker").style.marginTop = 10;
                foreach (var r in rewards) UiKit.Text(_card, "+ " + r, "eh-reward");
            }
            _hideAt = Time.unscaledTime + 22f;
        }

        void Update()
        {
            if (_card != null && Time.unscaledTime > _hideAt) ShowNext();
        }

        void OnDestroy()
        {
            if (s_instance == this) { s_instance = null; s_queue.Clear(); }
        }
    }
}
