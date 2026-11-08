using System;
using System.Collections.Generic;
using System.Linq;
using EventHorizon.Core;
using EventHorizon.Game;
using EventHorizon.Story;
using UnityEngine;
using UnityEngine.UIElements;

namespace EventHorizon.UI
{
    /// <summary>
    /// Mission journal: active missions by category (current mission, description, active and optional objectives,
    /// rewards, who is involved), the archive of completed missions with their debriefs, the colony record
    /// (decisions, reputation, relationships, research and upgrades) and the documents collected so far.
    /// </summary>
    public class JournalPanel
    {
        public VisualElement Element { get; }
        readonly VisualElement _content;
        readonly Dictionary<string, Button> _tabs = new();
        string _tab = "CURRENT";
        string _selected;

        static StoryState S => Story.Story.State;

        public JournalPanel(Action onBack, string tab = "CURRENT")
        {
            Element = new VisualElement();
            Element.AddToClassList("eh-panel");
            Element.style.flexGrow = 1;
            var head = UiKit.Box(Element, "eh-row");
            UiKit.Text(head, Loc.T("JOURNAL_TITLE"), "eh-panel-title");
            UiKit.Box(head, "eh-spacer");
            UiKit.Text(head, Loc.T("JOURNAL_RESEARCH", S.research), "eh-reward").style.marginRight = 16;
            UiKit.Small(head, Loc.T("BTN_BACK"), () => { UiAudio.Back(); onBack?.Invoke(); });
            var tabs = UiKit.Box(Element, "eh-row");
            foreach (var t in new[] { "CURRENT", "ARCHIVE", "RECORD", "DOCUMENTS" })
            {
                string name = t;
                _tabs[t] = UiKit.Button(tabs, Loc.T("JOURNAL_TAB_" + t), () => Show(name), "eh-tab");
            }
            _content = UiKit.Box(Element, "eh-row", "eh-grow");
            _content.style.alignItems = Align.Stretch;
            _content.style.marginTop = 8;
            Show(tab);
        }

        void Show(string tab)
        {
            _tab = tab;
            foreach (var kv in _tabs) kv.Value.EnableInClassList("eh-tab--active", kv.Key == tab);
            _content.Clear();
            switch (tab)
            {
                case "CURRENT": BuildQuestList(S.active, current: true); break;
                case "ARCHIVE": BuildQuestList(S.completed.Concat(S.failed).ToList(), current: false); break;
                case "RECORD": BuildRecord(); break;
                default: BuildDocuments(); break;
            }
        }

        // ------------------------------------------------------------------ missions

        void BuildQuestList(List<string> ids, bool current)
        {
            var list = UiKit.Scroll(_content);
            list.style.width = 380;
            var detail = UiKit.Scroll(_content);
            detail.style.flexGrow = 1;
            detail.style.paddingLeft = 18;
            var quests = ids.Select(Story.Story.Quest).Where(q => q != null).ToList();
            if (quests.Count == 0)
            {
                UiKit.Text(list, Loc.T(current ? "JOURNAL_NONE_ACTIVE" : "JOURNAL_NONE_DONE"), "eh-dim");
                return;
            }
            QuestDef first = null;
            foreach (QuestCategory cat in Enum.GetValues(typeof(QuestCategory)))
            {
                var inCat = quests.Where(q => q.category == cat).OrderBy(q => q.chapter).ToList();
                if (inCat.Count == 0) continue;
                UiKit.Text(list, Loc.T("CAT_" + cat.ToString().ToUpperInvariant()), "eh-kicker").style.marginTop = 10;
                foreach (var q in inCat)
                {
                    var quest = q;
                    string label = Loc.T(q.TitleKey);
                    if (S.failed.Contains(q.id)) label += "  " + Loc.T("JOURNAL_FAILED_TAG");
                    if (current && Story.Story.Tracked == q) label = "> " + label;
                    UiKit.Button(list, label, () => { _selected = quest.id; ShowQuest(detail, quest, current); }, "eh-btn");
                    if (first == null || q.id == _selected) first = q;
                }
            }
            if (first != null) ShowQuest(detail, first, current);
        }

        void ShowQuest(VisualElement detail, QuestDef q, bool current)
        {
            detail.Clear();
            UiKit.Text(detail, Loc.T(current ? "JOURNAL_CURRENT_MISSION" : "JOURNAL_COMPLETED_MISSION"), "eh-kicker");
            var titleRow = UiKit.Box(detail, "eh-row");
            var badge = UiKit.Text(titleRow, Loc.T("CAT_" + q.category.ToString().ToUpperInvariant()), "eh-cat-badge");
            if (q.IsMain) badge.AddToClassList("eh-cat-badge--main");
            if (q.IsMain) UiKit.Text(titleRow, MissionTracker.ChapterLabel(q.chapter) + "  ", "eh-kicker");
            UiKit.Text(detail, Loc.T(q.TitleKey), "eh-panel-title").style.whiteSpace = WhiteSpace.Normal;
            UiKit.Text(detail, Loc.T(q.DescKey), "eh-body");

            var people = q.characters.Length > 0 ? q.characters : new[] { q.giver };
            UiKit.Text(detail, Loc.T("JOURNAL_PEOPLE") + "  " + string.Join(", ", people.Select(c => Loc.T($"CHAR_{c.ToString().ToUpperInvariant()}_NAME"))), "eh-small", "eh-dim").style.marginTop = 6;

            if (q.timeLimitSols > 0f && current && MarsEnvironment.Instance != null)
            {
                float left = q.timeLimitSols - (MarsEnvironment.Instance.Sol - StoryState.Get(S.startSol, q.id));
                UiKit.Text(detail, Loc.T("JOURNAL_TIME_LEFT", Mathf.Max(0f, left).ToString("0.0")), "eh-warn");
            }

            if (current)
            {
                var views = MissionTracker.Objectives(q, includeHidden: false);
                UiKit.Text(detail, Loc.T("JOURNAL_ACTIVE_OBJECTIVES"), "eh-kicker").style.marginTop = 12;
                foreach (var v in views.Where(v => !v.optional)) ObjectiveRow(detail, v);
                if (views.Any(v => v.optional))
                {
                    UiKit.Text(detail, Loc.T("JOURNAL_OPTIONAL_OBJECTIVES"), "eh-kicker").style.marginTop = 12;
                    foreach (var v in views.Where(v => v.optional)) ObjectiveRow(detail, v);
                }
            }
            else
            {
                UiKit.Text(detail, Loc.T("DEBRIEF_KICKER"), "eh-kicker").style.marginTop = 12;
                UiKit.Text(detail, Loc.T(S.failed.Contains(q.id) ? q.FailKey : q.DebriefKey), "eh-body");
            }
            if (Loc.Has(q.LearnKey))
            {
                UiKit.Text(detail, Loc.T("DEBRIEF_SCIENCE"), "eh-kicker").style.marginTop = 12;
                UiKit.Text(detail, Loc.T(q.LearnKey), "eh-body", "eh-small");
            }
            var rewards = RewardLines(q.reward);
            if (rewards.Count > 0)
            {
                UiKit.Text(detail, Loc.T("JOURNAL_REWARDS"), "eh-kicker").style.marginTop = 12;
                foreach (var r in rewards) UiKit.Text(detail, "+ " + r, "eh-reward");
            }
            if (current && Story.Story.Tracked != q)
            {
                var track = UiKit.Small(detail, Loc.T("JOURNAL_TRACK"), () => { S.tracked = q.id; Story.Story.NotifyChanged(); Show("CURRENT"); }, "eh-btn-primary");
                track.style.marginTop = 14;
            }
        }

        static void ObjectiveRow(VisualElement parent, ObjectiveView v)
        {
            var row = UiKit.Box(parent, "eh-row");
            row.style.marginBottom = 6;
            string text = (v.done ? "[x]  " : "[ ]  ") + v.text + (v.elsewhere != null ? "  [" + v.elsewhere + "]" : "");
            var l = UiKit.Text(row, text, "eh-body");
            if (v.done) l.AddToClassList("eh-ok");
            l.style.flexGrow = 1;
            var bar = UiKit.Bar(row, out var fill);
            bar.style.width = 150;
            UiKit.SetFill(fill, v.done ? 1f : v.progress);
        }

        /// <summary>Human-readable reward list.</summary>
        public static List<string> RewardLines(Reward r)
        {
            var list = new List<string>();
            if (r == null) return list;
            if (r.research > 0) list.Add(Loc.T("REWARD_RESEARCH", r.research));
            foreach (var t in r.tech) list.Add(Loc.T("REWARD_TECH", Loc.T("TECH_" + t)));
            foreach (var u in r.upgrades) list.Add(Loc.T("REWARD_UPGRADE", Loc.T("UPG_" + u)));
            foreach (var i in r.items) list.Add(Loc.T("REWARD_ITEM", Loc.T("ITEM_" + i)));
            foreach (var c in r.codex) list.Add(Loc.T("REWARD_CODEX", Loc.T("CODEX_" + c + "_TITLE")));
            foreach (var (who, d) in r.relationships)
                list.Add(Loc.T("REWARD_RELATION", Loc.T($"CHAR_{who.ToString().ToUpperInvariant()}_NAME"), d.ToString("+0;-0")));
            return list;
        }

        // ------------------------------------------------------------------ colony record

        void BuildRecord()
        {
            var left = UiKit.Scroll(_content);
            left.style.width = Length.Percent(50);
            var right = UiKit.Scroll(_content);
            right.style.flexGrow = 1;
            right.style.paddingLeft = 18;

            UiKit.Text(left, Loc.T("RECORD_REPUTATION"), "eh-kicker");
            foreach (var axis in Axis.All) Meter(left, Loc.T("AXIS_" + axis.ToUpperInvariant()), S.Rep(axis));
            UiKit.Text(left, Loc.T("RECORD_RELATIONSHIPS"), "eh-kicker").style.marginTop = 14;
            foreach (var c in new[] { Character.Reyes, Character.Chen, Character.Erdene, Character.Okafor, Character.Park, Character.Ortiz, Character.Sorensen })
                Meter(left, Loc.T($"CHAR_{c.ToString().ToUpperInvariant()}_NAME"), S.Relation(c));

            UiKit.Text(right, Loc.T("RECORD_DECISIONS"), "eh-kicker");
            if (S.decisions.Count == 0) UiKit.Text(right, Loc.T("RECORD_NO_DECISIONS"), "eh-dim");
            foreach (var d in S.decisions)
            {
                string value = d.key == "ship_upgrades"
                    ? string.Join(", ", d.value.Split('+').Select(v => Loc.T("SHIP_" + v.ToUpperInvariant())))
                    : Loc.T($"DEC_{d.key.ToUpperInvariant()}_{d.value.ToUpperInvariant()}");
                UiKit.Text(right, $"{Loc.T("DEC_" + d.key.ToUpperInvariant())}:  {value}", "eh-body", "eh-small");
            }
            UiKit.Text(right, Loc.T("RECORD_TECH"), "eh-kicker").style.marginTop = 14;
            foreach (var t in S.tech) UiKit.Text(right, "+ " + Loc.T("TECH_" + t), "eh-small");
            foreach (var u in S.upgrades) UiKit.Text(right, "+ " + Loc.T("UPG_" + u), "eh-small");
            var endings = Profile.Current.endings;
            UiKit.Text(right, Loc.T("RECORD_ENDINGS", endings.Count, Endings.All.Length), "eh-kicker").style.marginTop = 14;
            foreach (var e in endings) UiKit.Text(right, "+ " + Loc.T("ENDING_" + e + "_TITLE"), "eh-small");
            UiKit.Text(right, Loc.T("RECORD_ARCHIVES", Story.Story.Archives), "eh-small", "eh-dim").style.marginTop = 8;
        }

        static void Meter(VisualElement parent, string label, int value)
        {
            var row = UiKit.Box(parent, "eh-row");
            row.style.marginTop = 4;
            var l = UiKit.Text(row, label, "eh-small");
            l.style.width = 230;
            var bar = UiKit.Bar(row, out var fill);
            bar.style.flexGrow = 1;
            UiKit.SetFill(fill, (value + 100f) / 200f, value < 0);
            UiKit.Text(row, value.ToString("+0;-0;0"), "eh-mono", "eh-small").style.width = 50;
        }

        // ------------------------------------------------------------------ documents

        void BuildDocuments()
        {
            var list = UiKit.Scroll(_content);
            list.style.width = 380;
            var detail = UiKit.Scroll(_content);
            detail.style.flexGrow = 1;
            detail.style.paddingLeft = 18;
            if (S.codex.Count == 0) { UiKit.Text(list, Loc.T("JOURNAL_NO_DOCUMENTS"), "eh-dim"); return; }
            void ShowDoc(string id)
            {
                detail.Clear();
                UiKit.Text(detail, Loc.T(id.StartsWith("bio_") ? "DOC_KIND_PERSON" : "DOC_KIND_RECORD"), "eh-kicker");
                UiKit.Text(detail, Loc.T("CODEX_" + id + "_TITLE"), "eh-panel-title").style.whiteSpace = WhiteSpace.Normal;
                UiKit.Text(detail, Loc.T("CODEX_" + id + "_BODY"), "eh-body");
            }
            foreach (var group in new[] { true, false })
            {
                var ids = S.codex.Where(c => c.StartsWith("bio_") == group).ToList();
                if (ids.Count == 0) continue;
                UiKit.Text(list, Loc.T(group ? "DOC_PEOPLE" : "DOC_RECORDS"), "eh-kicker").style.marginTop = 10;
                foreach (var id in ids)
                {
                    string doc = id;
                    UiKit.Button(list, Loc.T("CODEX_" + id + "_TITLE"), () => ShowDoc(doc), "eh-btn");
                }
            }
            ShowDoc(S.codex[0]);
        }
    }
}
