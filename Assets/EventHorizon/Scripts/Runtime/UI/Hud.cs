using System.Collections.Generic;
using EventHorizon.Astronaut;
using EventHorizon.Colony;
using EventHorizon.Core;
using EventHorizon.Game;
using EventHorizon.Interaction;
using EventHorizon.Story;
using UnityEngine;
using UnityEngine.UIElements;

namespace EventHorizon.UI
{
    /// <summary>
    /// Astronaut HUD: suit oxygen, integrity, energy, outside temperature, the task panel (current mission,
    /// chapter progress, objectives), a waypoint to the current objective, Martian time, compass, coordinates,
    /// colony summary, interaction prompt with work progress, colonists' name tags and toasts. Every label is
    /// localized and the whole HUD rebuilds when the language changes.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        // Jezero crater landing site; the colony pad origin sits here.
        const double SiteLatitude = 18.38, SiteLongitude = 77.58;
        const double MetresPerDegree = 2 * System.Math.PI * 3389500.0 / 360.0;

        static Hud s_instance;
        /// <summary>Hides the HUD during cinematics and endings.</summary>
        public static bool Hidden { get; set; }
        /// <summary>Shows info cards bottom-left instead of bottom-right (the black-hole lab's panel is on the right).</summary>
        public static bool InfoOnLeft { get; set; }

        UIDocument _doc;
        VisualElement _root, _hud, _toasts, _info, _prompt, _compassStrip, _objectives, _top, _suitBlock, _missionBlock, _tags;
        Label _promptLabel, _infoTitle, _infoBody, _o2Val, _intVal, _energyVal, _tempVal, _clock, _coords, _mission, _colony, _hint;
        VisualElement _o2Fill, _intFill, _energyFill, _chapterFill, _marker, _holdBar, _holdFill;
        Label _chapterLabel, _markerLabel;
        string _objectiveSignature;
        float _nextObjectiveRefresh;
        Vector3? _waypoint;
        readonly List<(Label label, float angle)> _compassMarks = new();
        readonly Dictionary<Npc, VisualElement> _nametags = new();
        float _infoHideAt;
        AstronautController _player;
        SuitLifeSupport _suit;
        Interactor _interactor;

        public static VisualElement ModalRoot => s_instance != null ? s_instance._root : null;

        public static Hud Ensure()
        {
            if (s_instance != null) return s_instance;
            var doc = UiRoot.Create("HUD", 10);
            s_instance = doc.gameObject.AddComponent<Hud>();
            s_instance._doc = doc;
            s_instance.Build();
            Loc.Changed += s_instance.Rebuild;
            return s_instance;
        }

        public static void Toast(string text, bool warning = false)
        {
            if (StoryTest.Running || string.IsNullOrEmpty(text)) return;
            var h = Ensure();
            var t = UiKit.Text(h._toasts, text, "eh-toast", "eh-toast--hidden");
            if (warning) t.AddToClassList("eh-toast--warn");
            t.schedule.Execute(() => t.RemoveFromClassList("eh-toast--hidden")).StartingIn(20);
            t.schedule.Execute(() => t.AddToClassList("eh-toast--hidden")).StartingIn(4500);
            t.schedule.Execute(() => t.RemoveFromHierarchy()).StartingIn(5000);
        }

        public static void ShowInfo(string title, string body, float seconds = 9f)
        {
            if (StoryTest.Running) return;
            var h = Ensure();
            h._info.style.right = InfoOnLeft ? StyleKeyword.Auto : 24;
            h._info.style.left = InfoOnLeft ? 24 : StyleKeyword.Auto;
            h._infoTitle.text = Loc.Upper(title);
            h._infoBody.text = body;
            h._info.style.display = DisplayStyle.Flex;
            h._infoHideAt = Time.unscaledTime + seconds;
        }

        void Rebuild()
        {
            var toasts = _toasts;
            toasts?.RemoveFromHierarchy();
            _root.Clear();
            _compassMarks.Clear();
            _nametags.Clear();
            _objectiveSignature = null;
            Build();
        }

        void OnDestroy()
        {
            Loc.Changed -= Rebuild;
            if (s_instance == this) s_instance = null;
        }

        void Build()
        {
            _root = _doc.rootVisualElement;
            _hud = UiKit.Box(_root, "eh-fill");
            _hud.pickingMode = PickingMode.Ignore;
            _tags = UiKit.Box(_hud, "eh-fill");
            _tags.pickingMode = PickingMode.Ignore;

            // Top centre: compass
            var top = _top = UiKit.Box(_hud, "eh-col");
            top.style.position = Position.Absolute;
            top.style.top = 14;
            top.style.left = 0;
            top.style.right = 0;
            top.style.alignItems = Align.Center;
            _compassStrip = UiKit.Box(top, "eh-compass");
            foreach (var (key, angle) in new[] { ("N", 0f), ("NE", 45f), ("E", 90f), ("SE", 135f), ("S", 180f), ("SW", 225f), ("W", 270f), ("NW", 315f) })
            {
                var l = UiKit.Text(_compassStrip, Loc.T("COMPASS_" + key), "eh-compass-mark");
                if (key.Length == 1) l.AddToClassList("eh-compass-mark--cardinal");
                _compassMarks.Add((l, angle));
            }
            for (int a = 15; a < 360; a += 15)
                if (a % 45 != 0) _compassMarks.Add((UiKit.Text(_compassStrip, "|", "eh-compass-mark", "eh-dim"), a));
            _coords = UiKit.Text(top, "", "eh-hud-label");
            _coords.style.marginTop = 4;

            // Task panel: mission title, chapter progress, then each objective with its own progress.
            var mission = _missionBlock = UiKit.Box(_hud, "eh-hud-block", "eh-task-panel");
            mission.style.position = Position.Absolute;
            mission.style.left = 24;
            mission.style.top = 20;
            mission.style.width = 460;
            var head = UiKit.Box(mission, "eh-row");
            UiKit.Text(head, Loc.T("HUD_CURRENT_MISSION"), "eh-hud-label");
            UiKit.Box(head, "eh-spacer");
            _chapterLabel = UiKit.Text(head, "", "eh-hud-label");
            _mission = UiKit.Text(mission, "", "eh-card-title");
            _mission.style.whiteSpace = WhiteSpace.Normal;
            var chapterBar = UiKit.Bar(mission, out _chapterFill);
            chapterBar.style.marginTop = 4;
            chapterBar.style.marginBottom = 6;
            _objectives = UiKit.Box(mission, "eh-col");
            _hint = UiKit.Text(mission, Loc.T("HUD_JOURNAL_HINT", GameInput.DisplayBinding(GameInput.Missions)), "eh-hud-label");
            _hint.style.marginTop = 6;

            // Waypoint marker for the current objective.
            _marker = UiKit.Box(_hud, "eh-marker");
            UiKit.Box(_marker, "eh-marker-diamond");
            _markerLabel = UiKit.Text(_marker, "", "eh-marker-label");
            _marker.style.display = DisplayStyle.None;

            // Top right: clock + colony summary
            var status = UiKit.Box(_hud, "eh-hud-block");
            status.style.position = Position.Absolute;
            status.style.right = 24;
            status.style.top = 20;
            status.style.alignItems = Align.FlexEnd;
            _clock = UiKit.Text(status, "", "eh-hud-value");
            _colony = UiKit.Text(status, "", "eh-hud-label");
            _colony.style.unityTextAlign = TextAnchor.UpperRight;

            // Bottom left: suit
            var suit = _suitBlock = UiKit.Box(_hud, "eh-hud-block");
            suit.style.position = Position.Absolute;
            suit.style.left = 24;
            suit.style.bottom = 24;
            suit.style.width = 360;
            UiKit.Text(suit, Loc.T("HUD_SUIT_STATUS"), "eh-hud-label");
            _o2Val = Gauge(suit, Loc.T("HUD_OXYGEN"), out _o2Fill);
            _intVal = Gauge(suit, Loc.T("HUD_INTEGRITY"), out _intFill);
            _energyVal = Gauge(suit, Loc.T("HUD_ENERGY"), out _energyFill);
            var temp = UiKit.Box(suit, "eh-row");
            UiKit.Text(temp, Loc.T("HUD_EXT_TEMP"), "eh-hud-label").style.width = 120;
            _tempVal = UiKit.Text(temp, "", "eh-hud-value");

            UiKit.Box(_hud, "eh-crosshair");
            _prompt = UiKit.Box(_hud, "eh-prompt");
            _promptLabel = UiKit.Text(_prompt, "", "eh-prompt-label");
            _holdBar = UiKit.Box(_prompt, "eh-hold");
            _holdFill = UiKit.Box(_holdBar, "eh-hold-fill");
            _prompt.style.display = DisplayStyle.None;

            _info = UiKit.Box(_hud, "eh-panel");
            _info.style.position = Position.Absolute;
            _info.style.right = 24;
            _info.style.bottom = 24;
            _info.style.width = 540;
            _infoTitle = UiKit.Text(_info, "", "eh-panel-title");
            _infoBody = UiKit.Text(_info, "", "eh-body");
            _info.style.display = DisplayStyle.None;

            _toasts = UiKit.Box(_root, "eh-toasts");
            _toasts.pickingMode = PickingMode.Ignore;
        }

        static Label Gauge(VisualElement parent, string label, out VisualElement fill)
        {
            var row = UiKit.Box(parent, "eh-row");
            row.style.marginTop = 4;
            UiKit.Text(row, label, "eh-hud-label").style.width = 120;
            var bar = UiKit.Bar(row, out fill);
            bar.style.flexGrow = 1;
            var v = UiKit.Text(row, "", "eh-hud-value");
            v.style.width = 86;
            v.style.unityTextAlign = TextAnchor.MiddleRight;
            return v;
        }

        void Update()
        {
            bool show = SettingsService.Current.gameplay.showHud && !GameSession.Paused && !Hidden;
            _hud.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (_info.style.display == DisplayStyle.Flex && Time.unscaledTime > _infoHideAt) _info.style.display = DisplayStyle.None;
            if (!show) return;

            if (_player == null)
            {
                _player = FindAnyObjectByType<AstronautController>();
                if (_player != null) { _suit = _player.GetComponent<SuitLifeSupport>(); _interactor = _player.GetComponent<Interactor>(); }
            }

            _suitBlock.style.display = _suit != null ? DisplayStyle.Flex : DisplayStyle.None;
            _top.style.display = _player != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (_suit != null)
            {
                float o2 = _suit.OxygenFraction;
                UiKit.SetFill(_o2Fill, o2, o2 < 0.25f);
                _o2Val.text = _suit.infiniteOxygen ? Loc.T("HUD_INFINITE") : Loc.T("HUD_HOURS", _suit.HoursLeft.ToString("0.0"));
                _o2Val.EnableInClassList("eh-warn", o2 < 0.25f && !_suit.infiniteOxygen);
                UiKit.SetFill(_intFill, _suit.integrity, _suit.integrity < 0.5f);
                _intVal.text = $"{_suit.integrity * 100f:0}%";
                UiKit.SetFill(_energyFill, _suit.EnergyFraction, _suit.EnergyFraction < 0.2f);
                _energyVal.text = $"{_suit.EnergyFraction * 100f:0}%";
            }

            var env = MarsEnvironment.Instance;
            if (env != null)
            {
                _clock.text = Loc.T("HUD_CLOCK", env.Sol.ToString("000"), env.ClockText);
                float t = env.SurfaceTemperatureC;
                _tempVal.text = $"{t:0} °C  /  {t + 273.15f:0} K";
                _colony.text = Loc.T("HUD_TAU", env.opticalDepth.ToString("0.0")) + (env.opticalDepth > 2f ? "  " + Loc.T("HUD_DUST_STORM") : "");
                _colony.EnableInClassList("eh-warn", env.opticalDepth > 2f);
            }
            var grid = ColonyGrid.Instance;
            if (grid != null)
                _colony.text += "\n" + Loc.T("HUD_COLONY", (grid.ProductionWatts / 1000f).ToString("0.0"), (grid.DemandWatts / 1000f).ToString("0.0"),
                    (grid.BatteryFraction * 100f).ToString("0"), grid.Amount(ResourceType.Oxygen).ToString("0"),
                    grid.Amount(ResourceType.Water).ToString("0"), grid.Amount(ResourceType.Food).ToString("0"));

            UpdateTasks();
            UpdateNametags();

            // Compass + coordinates
            var cam = Camera.main;
            if (cam != null)
            {
                float heading = cam.transform.eulerAngles.y;
                const float pxPerDeg = 4f, width = 520f;
                foreach (var (label, angle) in _compassMarks)
                {
                    float delta = Mathf.DeltaAngle(heading, angle);
                    label.style.display = Mathf.Abs(delta) < 62f ? DisplayStyle.Flex : DisplayStyle.None;
                    label.style.left = width * 0.5f + delta * pxPerDeg;
                }
                var p = _player != null ? _player.transform.position : cam.transform.position;
                double lat = SiteLatitude + p.z / MetresPerDegree;
                double lon = SiteLongitude + p.x / (MetresPerDegree * System.Math.Cos(lat * System.Math.PI / 180.0));
                _coords.text = Loc.T("HUD_COORDS", System.Math.Abs(lat).ToString("0.00000"), Loc.T(lat >= 0 ? "COMPASS_N" : "COMPASS_S"),
                    lon.ToString("0.00000"), Loc.T("COMPASS_E"), heading.ToString("000"), p.y.ToString("+0;-0"));
            }

            // Prompt, or the progress of work in hand.
            if (StoryWork.Busy)
            {
                _prompt.style.display = DisplayStyle.Flex;
                _promptLabel.text = $"{StoryWork.Label}  {StoryWork.Progress01 * 100f:0}%";
                _holdBar.style.display = DisplayStyle.Flex;
                _holdFill.style.width = Length.Percent(StoryWork.Progress01 * 100f);
            }
            else
            {
                string prompt = _interactor != null ? _interactor.CurrentPrompt : null;
                _prompt.style.display = string.IsNullOrEmpty(prompt) ? DisplayStyle.None : DisplayStyle.Flex;
                _holdBar.style.display = DisplayStyle.None;
                if (!string.IsNullOrEmpty(prompt)) _promptLabel.text = $"[{GameInput.DisplayBinding(GameInput.Interact)}]  {prompt}";
            }
        }

        /// <summary>Rebuilds the task rows only when something changed (4x per second at most).</summary>
        void UpdateTasks()
        {
            _mission.text = MissionTracker.CurrentTitle;
            if (Time.unscaledTime < _nextObjectiveRefresh) { UpdateMarker(); return; }
            _nextObjectiveRefresh = Time.unscaledTime + 0.25f;
            var list = MissionTracker.CurrentObjectives();
            int doneCount = 0, required = 0;
            _waypoint = null;
            int currentIndex = -1;
            var sig = new System.Text.StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                var v = list[i];
                if (!v.optional) { required++; if (v.done) doneCount++; }
                if (!v.done && currentIndex < 0 && !v.optional) { currentIndex = i; _waypoint = v.waypoint; }
                sig.Append(v.text).Append(v.done).Append(v.elsewhere).Append(Mathf.RoundToInt(v.progress * 20f));
            }
            if (currentIndex < 0) for (int i = 0; i < list.Count; i++) if (!list[i].done) { currentIndex = i; _waypoint = list[i].waypoint; break; }
            bool campaign = MissionTracker.CampaignActive;
            UiKit.SetFill(_chapterFill, required > 0 && campaign ? doneCount / (float)required : 0f);
            _chapterFill.parent.style.display = campaign ? DisplayStyle.Flex : DisplayStyle.None;
            _chapterLabel.text = campaign ? $"{doneCount}/{required}" : "";
            _hint.style.display = campaign ? DisplayStyle.Flex : DisplayStyle.None;
            if (sig.ToString() != _objectiveSignature)
            {
                _objectiveSignature = sig.ToString();
                _objectives.Clear();
                for (int i = 0; i < list.Count; i++)
                {
                    var v = list[i];
                    var row = UiKit.Box(_objectives, "eh-obj-row");
                    if (i == currentIndex) row.AddToClassList("eh-obj-row--current");
                    UiKit.Text(row, v.done ? "+" : i == currentIndex ? ">" : v.optional ? "o" : "-", "eh-obj-icon");
                    var col = UiKit.Box(row, "eh-col", "eh-grow");
                    string text = v.optional ? Loc.T("HUD_OPTIONAL", v.text) : v.text;
                    if (v.elsewhere != null) text += "  [" + v.elsewhere + "]";
                    var label = UiKit.Text(col, text, "eh-small");
                    label.style.whiteSpace = WhiteSpace.Normal;
                    if (v.done) label.AddToClassList("eh-ok");
                    else if (v.optional) label.AddToClassList("eh-optional");
                    if (!v.done && v.progress > 0f && v.progress < 1f)
                    {
                        var bar = UiKit.Bar(col, out var fill);
                        bar.style.height = 4;
                        bar.style.marginTop = 2;
                        UiKit.SetFill(fill, v.progress);
                    }
                }
            }
            UpdateMarker();
        }

        /// <summary>Places the waypoint diamond over the objective, or on the screen edge when it's off-screen.</summary>
        void UpdateMarker()
        {
            var cam = Camera.main;
            if (_waypoint == null || cam == null || _root.panel == null || _player == null)
            {
                _marker.style.display = DisplayStyle.None;
                return;
            }
            var target = _waypoint.Value + Vector3.up * 2f;
            float dist = Vector3.Distance(_player.transform.position, _waypoint.Value);
            if (dist < 4f) { _marker.style.display = DisplayStyle.None; return; }
            _marker.style.display = DisplayStyle.Flex;
            _markerLabel.text = Loc.T("HUD_METRES", dist.ToString("0"));

            var view = cam.WorldToViewportPoint(target);
            bool behind = view.z < 0f;
            if (behind) view = new Vector3(1f - view.x, 0.02f, 0f); // behind you: show at the bottom edge
            var clamped = new Vector2(Mathf.Clamp(view.x, 0.04f, 0.96f), Mathf.Clamp(view.y, 0.06f, 0.92f));
            var size = _root.layout.size;
            if (float.IsNaN(size.x) || size.x <= 0f) return;
            _marker.style.left = clamped.x * size.x - 30f;
            _marker.style.top = (1f - clamped.y) * size.y - 10f;
            _marker.EnableInClassList("eh-marker--edge", behind || clamped.x != view.x || clamped.y != view.y);
        }

        /// <summary>Name and role over colonists within 18 m.</summary>
        void UpdateNametags()
        {
            var cam = Camera.main;
            var size = _root.layout.size;
            foreach (var npc in Npc.All)
            {
                if (!_nametags.TryGetValue(npc, out var tag))
                {
                    tag = UiKit.Box(_tags, "eh-nametag");
                    tag.pickingMode = PickingMode.Ignore;
                    UiKit.Text(tag, npc.DisplayName, "eh-nametag-name");
                    UiKit.Text(tag, npc.Role, "eh-nametag-role");
                    _nametags[npc] = tag;
                }
                bool visible = false;
                if (cam != null && _player != null && !float.IsNaN(size.x) && size.x > 0f)
                {
                    float d = Vector3.Distance(_player.transform.position, npc.transform.position);
                    var vp = cam.WorldToViewportPoint(npc.transform.position + Vector3.up * 2.25f);
                    visible = d < 18f && vp.z > 0f && vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f;
                    if (visible)
                    {
                        tag.style.left = vp.x * size.x - 90f;
                        tag.style.top = (1f - vp.y) * size.y - 40f;
                        tag.style.width = 180f;
                        tag.style.opacity = Mathf.Clamp01((18f - d) / 4f);
                    }
                }
                tag.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}
