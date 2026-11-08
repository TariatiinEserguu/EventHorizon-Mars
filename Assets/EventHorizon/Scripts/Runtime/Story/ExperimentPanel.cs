using System;
using System.Collections.Generic;
using System.Globalization;
using EventHorizon.Core;
using EventHorizon.Game;
using EventHorizon.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace EventHorizon.Story
{
    /// <summary>
    /// Science tasks and puzzles: read instrument data, then answer a question, compute a value, tune sliders to a
    /// target, put steps in order or pick a set of systems within a power budget. Wrong answers get an explanation
    /// instead of a penalty. Solving raises exp_&lt;id&gt;.
    /// </summary>
    public class ExperimentPanel : MonoBehaviour
    {
        static ExperimentPanel s_open;
        public static bool IsOpen => s_open != null;
        /// <summary>Raised with the experiment id when one is solved.</summary>
        public static event Action<string> Solved;

        UIDocument _doc;
        ExperimentDef _def;
        Label _readout, _feedback, _status;
        VisualElement _task, _footer;
        float[] _values;
        bool[] _checks;
        readonly List<int> _order = new();
        int _attempts;
        bool _solved;
        float _next;

        public static void Open(string id)
        {
            if (StoryTest.Running) return;
            if (s_open != null || !StoryContent.Experiments.TryGetValue(id, out var def))
            {
                if (s_open == null) Debug.LogWarning($"[Event Horizon] Unknown experiment {id}");
                return;
            }
            var doc = UiRoot.Create("Experiment", 32);
            s_open = doc.gameObject.AddComponent<ExperimentPanel>();
            s_open._doc = doc;
            s_open._def = def;
            s_open.Build();
            ModalState.Open(s_open);
            GameInput.UpdateCursor();
        }

        public static void Close()
        {
            if (s_open == null) return;
            var p = s_open;
            s_open = null;
            ModalState.Close(p);
            Destroy(p.gameObject);
            GameInput.UpdateCursor();
        }

        void Build()
        {
            var root = _doc.rootVisualElement;
            root.Clear();
            var bg = UiKit.Box(root, "eh-modal-bg");
            bg.pickingMode = PickingMode.Position;
            var panel = UiKit.Box(bg, "eh-panel");
            panel.style.width = 980;
            panel.style.maxHeight = Length.Percent(90);
            var head = UiKit.Box(panel, "eh-row");
            UiKit.Text(head, Loc.T("EXP_KICKER"), "eh-kicker");
            UiKit.Box(head, "eh-spacer");
            UiKit.Small(head, Loc.T("BTN_CLOSE_ESC"), Close);
            UiKit.Text(panel, Loc.T(_def.TitleKey), "eh-panel-title");
            var scroll = UiKit.Scroll(panel);
            scroll.style.flexGrow = 1;
            UiKit.Text(scroll, Loc.T(_def.IntroKey), "eh-body");
            _readout = UiKit.Text(scroll, "", "eh-mono", "eh-accent");
            _readout.style.whiteSpace = WhiteSpace.Normal;
            _readout.style.marginTop = 10;
            _readout.style.display = _def.readout != null ? DisplayStyle.Flex : DisplayStyle.None;
            UiKit.Divider(scroll);
            _task = UiKit.Box(scroll, "eh-col");
            _status = UiKit.Text(scroll, "", "eh-mono", "eh-small");
            _feedback = UiKit.Text(scroll, "", "eh-body");
            _feedback.style.marginTop = 8;
            _footer = UiKit.Box(panel, "eh-row");
            _footer.style.justifyContent = Justify.FlexEnd;
            _footer.style.marginTop = 10;
            BuildTask();
            Refresh();
        }

        void BuildTask()
        {
            _task.Clear();
            _footer.Clear();
            if (Loc.Has(_def.QuestionKey)) UiKit.Text(_task, Loc.T(_def.QuestionKey), "eh-card-title").style.whiteSpace = WhiteSpace.Normal;
            switch (_def.kind)
            {
                case ExperimentDef.Kind.Readout:
                    UiKit.Small(_footer, Loc.T("EXP_LOG_DATA"), Succeed, "eh-btn-primary");
                    break;

                case ExperimentDef.Kind.Choice:
                    for (int i = 0; i < _def.options; i++)
                    {
                        int pick = i;
                        UiKit.Button(_task, $"{(char)('A' + i)}.  {Loc.T(_def.Option(i))}", () => Choose(pick), "eh-choice-btn");
                    }
                    break;

                case ExperimentDef.Kind.Numeric:
                {
                    var row = UiKit.Box(_task, "eh-row");
                    row.style.marginTop = 8;
                    var field = UiKit.Add(row, new TextField { maxLength = 16 });
                    field.style.width = 260;
                    field.style.flexGrow = 0;
                    if (Loc.Has(_def.UnitKey)) UiKit.Text(row, "  " + Loc.T(_def.UnitKey), "eh-mono");
                    UiKit.Small(_footer, Loc.T("EXP_SUBMIT"), () => Submit(field.value), "eh-btn-primary");
                    field.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) Submit(field.value); });
                    field.schedule.Execute(() => field.Focus()).StartingIn(60);
                    break;
                }

                case ExperimentDef.Kind.Sliders:
                    _values ??= Array.ConvertAll(_def.sliders, s => s.value);
                    for (int i = 0; i < _def.sliders.Length; i++)
                    {
                        int idx = i;
                        var s = _def.sliders[i];
                        UiKit.Slider(_task, Loc.T(s.key), s.min, s.max, _values[i], v => { _values[idx] = v; Refresh(); }, s.format);
                    }
                    UiKit.Small(_footer, Loc.T("EXP_CONFIRM"), ConfirmSliders, "eh-btn-primary");
                    break;

                case ExperimentDef.Kind.Order:
                {
                    var picked = UiKit.Text(_task, "", "eh-mono", "eh-accent");
                    picked.style.whiteSpace = WhiteSpace.Normal;
                    var pool = new List<int>();
                    for (int i = 0; i < _def.items; i++) pool.Add(i);
                    var rng = new System.Random(_def.id.GetHashCode());
                    for (int i = pool.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (pool[i], pool[j]) = (pool[j], pool[i]); }
                    foreach (var item in pool)
                    {
                        int it = item;
                        Button b = null;
                        b = UiKit.Button(_task, Loc.T(_def.Item(it)), () =>
                        {
                            if (_order.Contains(it)) return;
                            _order.Add(it);
                            b.SetEnabled(false);
                            ShowOrder(picked);
                            if (_order.Count == _def.items) CheckOrder();
                        }, "eh-choice-btn");
                    }
                    UiKit.Small(_footer, Loc.T("EXP_RESET"), () => { _order.Clear(); BuildTask(); });
                    ShowOrder(picked);
                    break;
                }

                case ExperimentDef.Kind.Checklist:
                    _checks ??= new bool[_def.items];
                    for (int i = 0; i < _def.items; i++)
                    {
                        int idx = i;
                        string label = Loc.T(_def.Item(i));
                        if (_def.costs != null && i < _def.costs.Length && _def.costs[i] != 0f) label += $"   [{_def.costs[i]:0.#}]";
                        UiKit.Toggle(_task, label, _checks[i], v => { _checks[idx] = v; Refresh(); });
                    }
                    UiKit.Small(_footer, Loc.T("EXP_CONFIRM"), ConfirmChecklist, "eh-btn-primary");
                    break;
            }
        }

        void ShowOrder(Label picked)
        {
            var parts = new List<string>();
            for (int i = 0; i < _order.Count; i++) parts.Add($"{i + 1}. {Loc.T(_def.Item(_order[i]))}");
            picked.text = parts.Count > 0 ? string.Join("\n", parts) : Loc.T("EXP_ORDER_PROMPT");
        }

        void Choose(int i)
        {
            if (_solved) return;
            _attempts++;
            if (_def.correct < 0 || i == _def.correct)
            {
                _def.onChoice?.Invoke(i);
                if (Loc.Has(_def.Feedback(i))) _feedback.text = Loc.T(_def.Feedback(i));
                Succeed();
            }
            else
            {
                _feedback.text = Loc.TOr(_def.Feedback(i), Loc.T("EXP_WRONG"));
                _feedback.AddToClassList("eh-warn");
            }
        }

        void Submit(string text)
        {
            if (_solved || _def.answer == null) return;
            text = (text ?? "").Trim().Replace(',', '.').Replace(" ", "");
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            {
                _feedback.text = Loc.T("EXP_NOT_A_NUMBER");
                _feedback.AddToClassList("eh-warn");
                return;
            }
            _attempts++;
            double expected = _def.answer();
            double rel = expected != 0 ? Math.Abs(v - expected) / Math.Abs(expected) : Math.Abs(v);
            if (rel <= _def.tolerance) { Succeed(); return; }
            _feedback.text = Loc.T(v > expected ? "EXP_TOO_HIGH" : "EXP_TOO_LOW") +
                             (_attempts >= 2 ? "\n" + Loc.TOr($"X_{_def.id}_HINT", "") : "");
            _feedback.AddToClassList("eh-warn");
        }

        void ConfirmSliders()
        {
            if (_solved) return;
            _attempts++;
            if (_def.goal == null || _def.goal(_values)) Succeed();
            else { _feedback.text = Loc.TOr($"X_{_def.id}_HINT", Loc.T("EXP_NOT_YET")); _feedback.AddToClassList("eh-warn"); }
        }

        void ConfirmChecklist()
        {
            if (_solved) return;
            _attempts++;
            if (_def.checklistGoal == null || _def.checklistGoal(_checks)) Succeed();
            else { _feedback.text = Loc.TOr($"X_{_def.id}_HINT", Loc.T("EXP_NOT_YET")); _feedback.AddToClassList("eh-warn"); }
        }

        void CheckOrder()
        {
            _attempts++;
            for (int i = 0; i < _order.Count; i++)
                if (_order[i] != i)
                {
                    _feedback.text = Loc.T("EXP_ORDER_WRONG", i + 1);
                    _feedback.AddToClassList("eh-warn");
                    _order.Clear();
                    schedule.Execute(BuildTask).StartingIn(1200);
                    return;
                }
            Succeed();
        }

        IVisualElementScheduler schedule => _doc.rootVisualElement.schedule;

        void Succeed()
        {
            if (_solved) return;
            _solved = true;
            _feedback.RemoveFromClassList("eh-warn");
            string ok = Loc.TOr(_def.OkKey, "");
            _feedback.text = string.IsNullOrEmpty(_feedback.text) ? ok : _feedback.text + (ok.Length > 0 ? "\n\n" + ok : "");
            _feedback.AddToClassList("eh-ok");
            _task.SetEnabled(false);
            _footer.Clear();
            if (_attempts <= 1) Story.SetFlag("firsttry_" + _def.id);
            UiAudio.Confirm();
            UiKit.Small(_footer, Loc.T("BTN_CONTINUE"), () =>
            {
                var def = _def;
                Close();
                def.onSolved?.Invoke();
                GameEvents.Raise("exp_" + def.id);
                Solved?.Invoke(def.id);
                Story.NotifyChanged();
            }, "eh-btn-primary");
        }

        void Refresh()
        {
            _next = Time.unscaledTime + 0.25f;
            if (_def.readout != null) _readout.text = _def.readout();
            if (_def.kind == ExperimentDef.Kind.Sliders && _def.sliderReadout != null && _values != null)
            {
                bool ok = _def.goal == null || _def.goal(_values);
                _status.text = _def.sliderReadout(_values) + "\n" + Loc.T(ok ? "EXP_IN_TARGET" : "EXP_OUT_OF_TARGET");
                _status.EnableInClassList("eh-ok", ok);
            }
            if (_def.kind == ExperimentDef.Kind.Checklist && _checks != null)
            {
                string s = _def.checklistReadout != null ? _def.checklistReadout(_checks) : "";
                if (_def.costs != null && _def.budget > 0f)
                {
                    float used = 0f;
                    for (int i = 0; i < _checks.Length && i < _def.costs.Length; i++) if (_checks[i]) used += _def.costs[i];
                    s = Loc.T("EXP_BUDGET", used.ToString("0.#", CultureInfo.InvariantCulture), _def.budget.ToString("0.#", CultureInfo.InvariantCulture)) + (s.Length > 0 ? "\n" + s : "");
                    _status.EnableInClassList("eh-warn", used > _def.budget);
                }
                _status.text = s;
            }
        }

        void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true) { Close(); return; }
            if (Time.unscaledTime >= _next) Refresh();
        }

        void OnDestroy()
        {
            if (s_open == this) { s_open = null; ModalState.Close(this); }
        }
    }
}
