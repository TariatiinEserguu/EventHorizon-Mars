using System.Collections.Generic;
using EventHorizon.Astronaut;
using EventHorizon.Colony;
using EventHorizon.Core;
using EventHorizon.Game;
using EventHorizon.Interaction;
using UnityEngine;
using UnityEngine.UIElements;

namespace EventHorizon.UI
{
    /// <summary>
    /// Astronaut HUD: suit oxygen, integrity, energy, outside temperature, mission and objectives, Martian time,
    /// compass, coordinates, colony summary, interaction prompt and toasts. Kept to the screen edges so it
    /// doesn't cover the view.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        // Jezero crater landing site; the colony pad origin sits here.
        const double SiteLatitude = 18.38, SiteLongitude = 77.58;
        const double MetresPerDegree = 2 * System.Math.PI * 3389500.0 / 360.0;

        static Hud s_instance;
        UIDocument _doc;
        VisualElement _root, _hud, _toasts, _info, _prompt, _compassStrip, _objectives, _top, _suitBlock;
        Label _promptLabel, _infoTitle, _infoBody, _o2Val, _intVal, _energyVal, _tempVal, _clock, _coords, _mission, _colony;
        VisualElement _o2Fill, _intFill, _energyFill;
        readonly List<(Label label, float angle)> _compassMarks = new();
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
            return s_instance;
        }

        public static void Toast(string text, bool warning = false)
        {
            var h = Ensure();
            var t = UiKit.Text(h._toasts, text, "eh-toast", "eh-toast--hidden");
            if (warning) t.AddToClassList("eh-toast--warn");
            t.schedule.Execute(() => t.RemoveFromClassList("eh-toast--hidden")).StartingIn(20);
            t.schedule.Execute(() => t.AddToClassList("eh-toast--hidden")).StartingIn(4500);
            t.schedule.Execute(() => t.RemoveFromHierarchy()).StartingIn(5000);
        }

        public static void ShowInfo(string title, string body, float seconds = 9f)
        {
            var h = Ensure();
            h._infoTitle.text = title.ToUpperInvariant();
            h._infoBody.text = body;
            h._info.style.display = DisplayStyle.Flex;
            h._infoHideAt = Time.unscaledTime + seconds;
        }

        void Build()
        {
            _root = _doc.rootVisualElement;
            _hud = UiKit.Box(_root, "eh-fill");
            _hud.pickingMode = PickingMode.Ignore;

            // Top centre: compass
            var top = _top = UiKit.Box(_hud, "eh-col");
            top.style.position = Position.Absolute;
            top.style.top = 14;
            top.style.left = 0;
            top.style.right = 0;
            top.style.alignItems = Align.Center;
            _compassStrip = UiKit.Box(top, "eh-compass");
            foreach (var (name, angle) in new[] { ("N", 0f), ("NE", 45f), ("E", 90f), ("SE", 135f), ("S", 180f), ("SW", 225f), ("W", 270f), ("NW", 315f) })
            {
                var l = UiKit.Text(_compassStrip, name, "eh-compass-mark");
                if (name.Length == 1) l.AddToClassList("eh-compass-mark--cardinal");
                _compassMarks.Add((l, angle));
            }
            for (int a = 15; a < 360; a += 15)
                if (a % 45 != 0) _compassMarks.Add((UiKit.Text(_compassStrip, "|", "eh-compass-mark", "eh-dim"), a));
            _coords = UiKit.Text(top, "", "eh-hud-label");
            _coords.style.marginTop = 4;

            // Top left: mission + objectives
            var mission = UiKit.Box(_hud, "eh-hud-block");
            mission.style.position = Position.Absolute;
            mission.style.left = 24;
            mission.style.top = 20;
            mission.style.width = 420;
            UiKit.Text(mission, "CURRENT MISSION", "eh-hud-label");
            _mission = UiKit.Text(mission, "", "eh-card-title");
            _objectives = UiKit.Box(mission, "eh-col");

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
            suit.style.width = 340;
            UiKit.Text(suit, "SUIT STATUS", "eh-hud-label");
            _o2Val = Gauge(suit, "OXYGEN", out _o2Fill);
            _intVal = Gauge(suit, "INTEGRITY", out _intFill);
            _energyVal = Gauge(suit, "ENERGY", out _energyFill);
            var temp = UiKit.Box(suit, "eh-row");
            UiKit.Text(temp, "EXT TEMP", "eh-hud-label").style.width = 110;
            _tempVal = UiKit.Text(temp, "", "eh-hud-value");

            UiKit.Box(_hud, "eh-crosshair");
            _prompt = UiKit.Box(_hud, "eh-prompt");
            _promptLabel = UiKit.Text(_prompt, "", "eh-prompt-label");
            _prompt.style.display = DisplayStyle.None;

            _info = UiKit.Box(_hud, "eh-panel");
            _info.style.position = Position.Absolute;
            _info.style.right = 24;
            _info.style.bottom = 24;
            _info.style.width = 520;
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
            UiKit.Text(row, label, "eh-hud-label").style.width = 110;
            var bar = UiKit.Bar(row, out fill);
            bar.style.flexGrow = 1;
            var v = UiKit.Text(row, "", "eh-hud-value");
            v.style.width = 86;
            v.style.unityTextAlign = TextAnchor.MiddleRight;
            return v;
        }

        void Update()
        {
            bool show = SettingsService.Current.gameplay.showHud && !GameSession.Paused;
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
                _o2Val.text = _suit.infiniteOxygen ? "INF" : $"{_suit.HoursLeft:0.0} h";
                _o2Val.EnableInClassList("eh-warn", o2 < 0.25f && !_suit.infiniteOxygen);
                UiKit.SetFill(_intFill, _suit.integrity, _suit.integrity < 0.5f);
                _intVal.text = $"{_suit.integrity * 100f:0}%";
                UiKit.SetFill(_energyFill, _suit.EnergyFraction, _suit.EnergyFraction < 0.2f);
                _energyVal.text = $"{_suit.EnergyFraction * 100f:0}%";
            }

            var env = MarsEnvironment.Instance;
            if (env != null)
            {
                _clock.text = $"SOL {env.Sol:000}  {env.ClockText} LMST";
                float t = env.SurfaceTemperatureC;
                _tempVal.text = $"{t:0} C  /  {t + 273.15f:0} K";
                string storm = env.opticalDepth > 2f ? "  DUST STORM" : "";
                _colony.text = $"TAU {env.opticalDepth:0.0}{storm}";
                _colony.EnableInClassList("eh-warn", env.opticalDepth > 2f);
            }
            var grid = ColonyGrid.Instance;
            if (grid != null)
                _colony.text += $"\nPWR {grid.ProductionWatts / 1000f:0.0}/{grid.DemandWatts / 1000f:0.0} kW  BAT {grid.BatteryFraction * 100f:0}%\nO2 {grid.Amount(ResourceType.Oxygen):0} kg  H2O {grid.Amount(ResourceType.Water):0} kg  FOOD {grid.Amount(ResourceType.Food):0} kg";

            // Mission
            _mission.text = MissionTracker.CurrentTitle;
            _objectives.Clear();
            foreach (var (text, progress, done) in MissionTracker.CurrentObjectives())
            {
                var l = UiKit.Text(_objectives, $"{(done ? "[x]" : "[ ]")} {text}{(progress > 0f && progress < 1f && !done ? $"  {progress * 100f:0}%" : "")}", "eh-small");
                if (done) l.AddToClassList("eh-ok");
            }

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
                _coords.text = $"{System.Math.Abs(lat):0.00000} {(lat >= 0 ? "N" : "S")}   {lon:0.00000} E   HDG {heading:000}   ALT {p.y:+0;-0} m";
            }

            // Prompt
            string prompt = _interactor != null ? _interactor.CurrentPrompt : null;
            _prompt.style.display = string.IsNullOrEmpty(prompt) ? DisplayStyle.None : DisplayStyle.Flex;
            if (!string.IsNullOrEmpty(prompt)) _promptLabel.text = $"[{GameInput.DisplayBinding(GameInput.Interact)}]  {prompt}";
        }

        void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }
    }
}
