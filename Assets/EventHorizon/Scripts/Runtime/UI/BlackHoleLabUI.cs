using System;
using System.Collections.Generic;
using System.Globalization;
using EventHorizon.Core;
using EventHorizon.Game;
using EventHorizon.Space;
using UnityEngine;
using UnityEngine.UIElements;

namespace EventHorizon.UI
{
    /// <summary>
    /// Research console for the black-hole lab. Modes: Beginner (plain language, guides and labels), Research
    /// (formulas and full numbers), Experiment (save two settings and compare them), Observation (free view, no panel).
    /// Tabs: Simulation (presets, mass, spin, viewing angle and distance, disk, background star, speed, quality),
    /// Orbits, Time, Tides and Guides. Every number shown is computed from the current parameters.
    /// </summary>
    public class BlackHoleLabUI : MonoBehaviour
    {
        public BlackHole blackHole;
        public OrbitCamera orbit;
        BlackHoleLab _lab;

        UIDocument _doc;
        VisualElement _root, _panel, _content, _labels;
        Label _readout, _status;
        string _tab = "SIM";
        bool _collapsed;
        float _next;
        readonly Dictionary<string, Label> _labelElements = new();
        Snapshot? _a, _b;

        struct Snapshot
        {
            public double mass, spin, rs, horizon, isco, photon, freq, dilation, view;
        }

        static string N(double v, string f = "0.###") => v.ToString(f, CultureInfo.InvariantCulture);
        static string Sci(double v) => Math.Abs(v) >= 1e5 || (Math.Abs(v) < 0.01 && v != 0) ? v.ToString("0.00E+0", CultureInfo.InvariantCulture) : v.ToString("#,0.##", CultureInfo.InvariantCulture);

        void Start()
        {
            if (blackHole == null) blackHole = FindAnyObjectByType<BlackHole>();
            if (orbit == null) orbit = FindAnyObjectByType<OrbitCamera>();
            _lab = FindAnyObjectByType<BlackHoleLab>();
            if (blackHole == null) return;
            Hud.InfoOnLeft = true;
            _doc = UiRoot.Create("Black Hole Lab", 20);
            _root = _doc.rootVisualElement;
            Build();
            Loc.Changed += Build;
        }

        void OnDestroy()
        {
            Loc.Changed -= Build;
            Hud.InfoOnLeft = false;
        }

        /// <summary>Switches tab: SIM, ORBITS, TIME, TIDES, GUIDES (used by the screenshot tour).</summary>
        public void ShowTab(string tab)
        {
            _tab = tab;
            Build();
        }

        BlackHoleLab.Mode Mode => _lab != null ? _lab.mode : BlackHoleLab.Mode.Research;

        void Build()
        {
            _root.Clear();
            _labelElements.Clear();
            _labels = UiKit.Box(_root, "eh-fill");
            _labels.pickingMode = PickingMode.Ignore;

            _panel = UiKit.Box(_root, "eh-panel");
            _panel.style.position = Position.Absolute;
            _panel.style.right = 24;
            _panel.style.top = 120;
            _panel.style.bottom = 24;
            _panel.style.width = 540;

            var head = UiKit.Box(_panel, "eh-row");
            UiKit.Text(head, Loc.T("LAB_TITLE"), "eh-kicker");
            UiKit.Box(head, "eh-spacer");
            UiKit.Small(head, Loc.T(blackHole.paused ? "LAB_RESUME" : "LAB_PAUSE"), () => { blackHole.paused = !blackHole.paused; Build(); });
            UiKit.Small(head, Loc.T("LAB_RESET"), () => { _lab?.ResetSimulation(); Build(); });
            UiKit.Small(head, Loc.T(_collapsed ? "LAB_SHOW" : "LAB_HIDE"), () => { _collapsed = !_collapsed; Build(); });

            if (_lab != null)
            {
                var modes = UiKit.Box(_panel, "eh-row");
                modes.style.flexWrap = Wrap.Wrap;
                foreach (BlackHoleLab.Mode m in Enum.GetValues(typeof(BlackHoleLab.Mode)))
                {
                    var mode = m;
                    var b = UiKit.Button(modes, Loc.T("LAB_MODE_" + m.ToString().ToUpperInvariant()), () => { _lab.mode = mode; Build(); }, "eh-tab");
                    b.EnableInClassList("eh-tab--active", _lab.mode == m);
                }
            }
            bool observing = Mode == BlackHoleLab.Mode.Observation || _collapsed;
            _panel.style.bottom = observing ? StyleKeyword.Auto : 24;
            if (observing)
            {
                UiKit.Text(_panel, Loc.T(Mode == BlackHoleLab.Mode.Observation ? "LAB_OBSERVATION_HINT" : "LAB_COLLAPSED_HINT"), "eh-small", "eh-dim");
                return;
            }

            var tabs = UiKit.Box(_panel, "eh-row");
            tabs.style.flexWrap = Wrap.Wrap;
            foreach (var t in new[] { "SIM", "ORBITS", "TIME", "TIDES", "GUIDES" })
            {
                string tab = t;
                var b = UiKit.Button(tabs, Loc.T("LAB_TAB_" + t), () => { _tab = tab; Build(); }, "eh-tab");
                b.EnableInClassList("eh-tab--active", _tab == t);
            }
            var scroll = UiKit.Scroll(_panel);
            scroll.style.flexGrow = 1;
            _content = scroll;
            switch (_tab)
            {
                case "ORBITS": BuildOrbits(); break;
                case "TIME": BuildTime(); break;
                case "TIDES": BuildTides(); break;
                case "GUIDES": BuildGuides(); break;
                default: BuildSimulation(); break;
            }
            UiKit.Divider(_content);
            _readout = UiKit.Text(_content, "", "eh-mono", "eh-small");
            _readout.style.whiteSpace = WhiteSpace.Normal;
            _status = UiKit.Text(_content, "", "eh-small", "eh-dim");
            _status.style.whiteSpace = WhiteSpace.Normal;
            if (Mode == BlackHoleLab.Mode.Experiment) BuildExperiment();
            var note = UiKit.Text(_content, Loc.T("LAB_NOTE"), "eh-small", "eh-dim");
            note.style.whiteSpace = WhiteSpace.Normal;
            note.style.marginTop = 8;
            Refresh();
        }

        // ------------------------------------------------------------------ tabs

        void BuildSimulation()
        {
            var c = _content;
            if (_lab != null)
            {
                var row = UiKit.Box(c, "eh-row");
                row.style.flexWrap = Wrap.Wrap;
                foreach (BlackHoleLab.Preset p in Enum.GetValues(typeof(BlackHoleLab.Preset)))
                {
                    var preset = p;
                    var b = UiKit.Small(row, Loc.T("LAB_PRESET_" + p.ToString().ToUpperInvariant()), () => { _lab.ApplyPreset(preset); Build(); },
                        _lab.preset == p ? "eh-btn-primary" : "eh-btn-small");
                    b.style.marginRight = 4;
                    b.style.marginBottom = 4;
                }
                var desc = UiKit.Text(c, Loc.T("LAB_PRESET_" + _lab.preset.ToString().ToUpperInvariant() + "_DESC"), "eh-small");
                desc.style.whiteSpace = WhiteSpace.Normal;
            }
            UiKit.Slider(c, Loc.T("LAB_MASS"), -5.5f, 10.5f, Mathf.Log10((float)blackHole.massSolar), v => blackHole.massSolar = Math.Pow(10, v), v => Loc.T("LAB_SOLAR_MASSES", Sci(Math.Pow(10, v))));
            UiKit.Slider(c, Loc.T("LAB_SPIN"), 0f, 0.998f, blackHole.spin, v => blackHole.spin = v, v => N(v, "0.000"));
            if (orbit != null)
            {
                UiKit.Slider(c, Loc.T("LAB_VIEW_ANGLE"), -85f, 85f, orbit.pitch, v => orbit.pitch = v, v => N(v, "0") + "°");
                UiKit.Slider(c, Loc.T("LAB_DISTANCE"), 25f, 400f, orbit.distance, v => orbit.distance = v, v => N(v / blackHole.WorldUnitsPerRs, "0") + " r_s");
            }
            UiKit.Slider(c, Loc.T("LAB_DISK_TEMP"), 2000f, 40000f, blackHole.diskTemperature, v => blackHole.diskTemperature = v, v => N(v, "#,0") + " K");
            UiKit.Slider(c, Loc.T("LAB_DISK_DENSITY"), 0f, 1f, blackHole.diskDensity, v => blackHole.diskDensity = v, v => N(v * 100, "0") + "%");
            UiKit.Slider(c, Loc.T("LAB_DISK_INTENSITY"), 0.3f, 8f, blackHole.diskIntensity, v => blackHole.diskIntensity = v, v => N(v, "0.0"));
            UiKit.Text(c, Loc.T("LAB_SOURCE"), "eh-kicker").style.marginTop = 8;
            var dir = blackHole.sourceDirection.normalized;
            float az = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, el = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
            float curAz = az, curEl = el;
            UiKit.Slider(c, Loc.T("LAB_SOURCE_AZ"), -180f, 180f, az, v => { curAz = v; _lab?.SetSource(curAz, curEl); }, v => N(v, "0") + "°");
            UiKit.Slider(c, Loc.T("LAB_SOURCE_EL"), -89f, 89f, el, v => { curEl = v; _lab?.SetSource(curAz, curEl); }, v => N(v, "0") + "°");
            var srcRow = UiKit.Box(c, "eh-row");
            UiKit.Small(srcRow, Loc.T("LAB_SOURCE_ALIGN"), () => { _lab?.AlignSource(); Build(); }, "eh-btn-primary");
            UiKit.Small(srcRow, Loc.T(blackHole.sourceBrightness > 0f ? "LAB_SOURCE_OFF" : "LAB_SOURCE_ON"), () => { blackHole.sourceBrightness = blackHole.sourceBrightness > 0f ? 0f : 6f; Build(); });
            UiKit.Text(c, Loc.T("LAB_RUN"), "eh-kicker").style.marginTop = 8;
            UiKit.Slider(c, Loc.T("LAB_SIM_SPEED"), 0f, 4f, blackHole.simulationSpeed, v => blackHole.simulationSpeed = v, v => N(v, "0.0") + "x");
            if (_lab != null)
                UiKit.Choice(c, Loc.T("LAB_QUALITY"), new[] { Loc.T("ENUM_LOW"), Loc.T("ENUM_MEDIUM"), Loc.T("ENUM_HIGH") }, (int)_lab.quality,
                    i => { _lab.ApplyQuality((BlackHoleLab.Quality)i); Build(); });
            UiKit.Slider(c, Loc.T("LAB_STEPS"), 32f, 600f, blackHole.steps, v => blackHole.steps = Mathf.RoundToInt(v), v => N(v, "0"));
            UiKit.Toggle(c, Loc.T("LAB_FRAME_DRAG"), blackHole.frameDragging > 0.5f, v => blackHole.frameDragging = v ? 1f : 0f);
        }

        void BuildOrbits()
        {
            if (_lab == null) return;
            var c = _content;
            UiKit.Text(c, Loc.T("LAB_ORBITS_INTRO"), "eh-small").style.whiteSpace = WhiteSpace.Normal;
            UiKit.Slider(c, Loc.T("LAB_ORBIT_R0"), 1.2f, 30f, _lab.orbitStartRs, v => _lab.orbitStartRs = v, v => N(v, "0.0") + " r_s");
            UiKit.Slider(c, Loc.T("LAB_ORBIT_SPEED"), 0f, 1.6f, _lab.orbitSpeedFactor, v => _lab.orbitSpeedFactor = v, v => N(v * 100, "0") + "%");
            UiKit.Slider(c, Loc.T("LAB_ORBIT_RADIAL"), -0.5f, 0.5f, _lab.orbitRadialSpeed, v => _lab.orbitRadialSpeed = v, v => N(v, "0.00") + " c");
            var row = UiKit.Box(c, "eh-row");
            UiKit.Small(row, Loc.T("LAB_ORBIT_LAUNCH"), () => _lab.LaunchOrbit(), "eh-btn-primary");
            UiKit.Small(row, Loc.T("LAB_ORBIT_PRESET_ISCO"), () => { _lab.orbitStartRs = (float)blackHole.IscoRs * 1.02f; _lab.orbitSpeedFactor = 1f; _lab.orbitRadialSpeed = 0f; _lab.LaunchOrbit(); Build(); });
            UiKit.Small(row, Loc.T("LAB_ORBIT_PRESET_PRECESS"), () => { _lab.orbitStartRs = 12f; _lab.orbitSpeedFactor = 0.92f; _lab.orbitRadialSpeed = 0f; _lab.LaunchOrbit(); Build(); });
            UiKit.Small(row, Loc.T("LAB_ORBIT_PRESET_PLUNGE"), () => { _lab.orbitStartRs = 10f; _lab.orbitSpeedFactor = 0.55f; _lab.orbitRadialSpeed = 0f; _lab.LaunchOrbit(); Build(); });
        }

        void BuildTime()
        {
            if (_lab == null) return;
            var c = _content;
            UiKit.Text(c, Loc.T("LAB_TIME_INTRO"), "eh-small").style.whiteSpace = WhiteSpace.Normal;
            UiKit.Slider(c, Loc.T("LAB_TIME_RADIUS"), 1.05f, 50f, _lab.clockRadiusRs, v => _lab.clockRadiusRs = v, v => N(v, "0.00") + " r_s");
            UiKit.Choice(c, Loc.T("LAB_TIME_OBSERVER"), new[] { Loc.T("LAB_TIME_STATIC"), Loc.T("LAB_TIME_ORBITING") }, _lab.clockOrbiting ? 1 : 0, i => _lab.clockOrbiting = i == 1);
            UiKit.Small(c, Loc.T("LAB_TIME_RESET"), _lab.ResetClocks);
        }

        void BuildTides()
        {
            if (_lab == null) return;
            var c = _content;
            UiKit.Text(c, Loc.T("LAB_TIDES_INTRO"), "eh-small").style.whiteSpace = WhiteSpace.Normal;
            UiKit.Toggle(c, Loc.T("LAB_TIDES_SHOW"), _lab.showTides, v => _lab.showTides = v);
            UiKit.Slider(c, Loc.T("LAB_TIDES_RADIUS"), 0f, 2.5f, Mathf.Log10(_lab.tideRadiusRs), v => _lab.tideRadiusRs = Mathf.Max(1.02f, Mathf.Pow(10f, v)), v => N(Math.Max(1.02, Math.Pow(10, v)), "0.0") + " r_s");
            UiKit.Slider(c, Loc.T("LAB_TIDES_LENGTH"), 0.5f, 100f, _lab.tideLengthM, v => _lab.tideLengthM = v, v => N(v, "0.0") + " m");
        }

        void BuildGuides()
        {
            if (_lab == null) return;
            var c = _content;
            UiKit.Toggle(c, Loc.T("LAB_GUIDES_SHOW"), _lab.showGuides, v => _lab.showGuides = v);
            UiKit.Toggle(c, Loc.T("LAB_LABELS_SHOW"), _lab.showLabels, v => _lab.showLabels = v);
            UiKit.Toggle(c, Loc.T("LAB_GRID_SHOW"), _lab.showGrid, v => _lab.SetGrid(v));
            UiKit.Text(c, Loc.T("LAB_GUIDES_LEGEND"), "eh-small").style.whiteSpace = WhiteSpace.Normal;
        }

        void BuildExperiment()
        {
            var row = UiKit.Box(_content, "eh-row");
            row.style.marginTop = 8;
            UiKit.Small(row, Loc.T("LAB_SAVE_A"), () => { _a = Take(); Refresh(); });
            UiKit.Small(row, Loc.T("LAB_SAVE_B"), () => { _b = Take(); Refresh(); });
        }

        Snapshot Take() => new()
        {
            mass = blackHole.massSolar, spin = blackHole.spin, rs = blackHole.SchwarzschildRadiusM / 1000.0,
            horizon = blackHole.HorizonRs, isco = blackHole.IscoRs, photon = blackHole.PhotonOrbitRs, freq = blackHole.IscoFrequency,
            dilation = blackHole.TimeDilationFactor((_lab != null ? _lab.ViewingDistanceRs : 30.0) * blackHole.SchwarzschildRadiusM),
            view = _lab != null ? _lab.ViewingDistanceRs : 30.0,
        };

        // ------------------------------------------------------------------ live readouts

        void Update()
        {
            if (blackHole == null || _doc == null) return;
            UpdateLabels();
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.2f;
            Refresh();
        }

        void Refresh()
        {
            if (_readout == null || _readout.panel == null) return;
            var bh = blackHole;
            double rs = bh.SchwarzschildRadiusM;
            double km(double rsUnits) => rsUnits * rs / 1000.0;
            double view = _lab != null ? _lab.ViewingDistanceRs : 30.0;
            bool research = Mode == BlackHoleLab.Mode.Research || Mode == BlackHoleLab.Mode.Experiment;
            string text;
            switch (_tab)
            {
                case "ORBITS":
                {
                    var o = _lab?.Orbit;
                    if (o == null) { text = Loc.T("LAB_ORBIT_NONE"); break; }
                    double tUnit = BlackHole.TimeUnitS(bh.massSolar);
                    text = Loc.T("LAB_ORBIT_RESULT",
                        Loc.T("LAB_OUTCOME_" + o.outcome), N(o.E, "0.0000"), N(o.L, "0.000"), N(o.bindingFraction * 100, "0.00"),
                        double.IsNaN(o.peri) ? "-" : N(o.peri / 2, "0.00"), double.IsNaN(o.apo) ? "-" : N(o.apo / 2, "0.00"),
                        o.periodT > 0 ? Sci(o.periodT * tUnit) : "-", N(o.precessionDeg, "0.0"), Loc.T("LAB_OUTCOME_" + o.newtonOutcome),
                        Sci(_lab.PlayT * tUnit), Sci(_lab.PlayTau * tUnit));
                    if (research) text += "\n" + Loc.T("LAB_ORBIT_FORMULA");
                    break;
                }
                case "TIME":
                {
                    double rate = _lab.ClockRate(_lab.clockRadiusRs, _lab.clockOrbiting);
                    text = double.IsNaN(rate)
                        ? Loc.T(_lab.clockOrbiting ? "LAB_TIME_NO_ORBIT" : "LAB_TIME_NO_STATIC")
                        : Loc.T("LAB_TIME_RESULT", N(_lab.DistantClock, "0.0"), N(_lab.LocalClock, "0.0"), N(rate * 100, "0.000"),
                            N((1.0 / rate - 1.0) * 86400.0, "#,0"));
                    text += "\n" + Loc.T(_lab.clockOrbiting ? "LAB_TIME_ORBIT_NOTE" : "LAB_TIME_STATIC_NOTE");
                    break;
                }
                case "TIDES":
                {
                    double r = _lab.tideRadiusRs * rs;
                    double stretch = bh.TidalAcceleration(r, _lab.tideLengthM);
                    double r10 = bh.TidalRadiusM(_lab.tideLengthM, 98.1);
                    text = Loc.T("LAB_TIDES_RESULT", Sci(r / 1000.0), Sci(stretch), Sci(stretch / 9.81), Sci(stretch / 2.0),
                        Sci(r10 / 1000.0), Loc.T(r10 > bh.HorizonRs * rs ? "LAB_TIDES_OUTSIDE" : "LAB_TIDES_INSIDE"));
                    break;
                }
                default:
                {
                    double dil = bh.TimeDilationFactor(view * rs);
                    text = Loc.T(research ? "LAB_READOUT_RESEARCH" : "LAB_READOUT_BEGINNER",
                        Sci(bh.massSolar), Sci(rs / 1000.0), Sci(km(bh.HorizonRs)), N(bh.HorizonRs, "0.000"),
                        N(bh.PhotonOrbitRs, "0.000"), N(bh.PhotonOrbitRetroRs, "0.000"), N(bh.IscoRs, "0.000"), N(bh.IscoRetroRs, "0.000"),
                        N(bh.ErgoEquatorRs, "0.000"), N(bh.spin, "0.000"), Sci(bh.AngularMomentum), Sci(bh.IscoFrequency), Sci(bh.IscoPeriodS),
                        N(view, "0"), N(dil * 100, "0.000"), Sci(bh.ShadowRadiusM / 1000.0), N((1.0 - IscoEnergy(bh.spin)) * 100, "0.0"));
                    break;
                }
            }
            if (Mode == BlackHoleLab.Mode.Experiment && (_a.HasValue || _b.HasValue)) text += "\n\n" + CompareText();
            _readout.text = text;
            if (GameSession.Mode == GameMode.Campaign && Story.Story.State.IsActive("C09"))
                _status.text = Loc.T("LAB_CAMPAIGN_STATUS", N(BlackHoleLab.Horizon1SignalHz / 1e6, "0.0"), N(bh.IscoFrequency / 1e6, "0.0"),
                    N(_lab != null ? _lab.YawTravelled : 0, "0"), N(_lab != null ? _lab.SourceMisalignmentDeg : 180, "0.0"));
            else _status.text = "";
        }

        /// <summary>Specific energy at the prograde ISCO: E = sqrt(1 - 2/(3 r_isco)) for any spin (M = 1).</summary>
        static double IscoEnergy(double a) => Math.Sqrt(1.0 - 2.0 / (3.0 * BlackHole.Isco(a)));

        string CompareText()
        {
            string Col(Snapshot? s, Func<Snapshot, string> f) => s.HasValue ? f(s.Value) : "-";
            return Loc.T("LAB_COMPARE",
                Col(_a, s => Sci(s.mass)), Col(_b, s => Sci(s.mass)),
                Col(_a, s => N(s.spin, "0.000")), Col(_b, s => N(s.spin, "0.000")),
                Col(_a, s => Sci(s.rs)), Col(_b, s => Sci(s.rs)),
                Col(_a, s => N(s.horizon, "0.000")), Col(_b, s => N(s.horizon, "0.000")),
                Col(_a, s => N(s.isco, "0.000")), Col(_b, s => N(s.isco, "0.000")),
                Col(_a, s => N(s.photon, "0.000")), Col(_b, s => N(s.photon, "0.000")),
                Col(_a, s => Sci(s.freq)), Col(_b, s => Sci(s.freq)),
                Col(_a, s => N(s.dilation * 100, "0.00")), Col(_b, s => N(s.dilation * 100, "0.00")));
        }

        /// <summary>Names next to the guides (Beginner mode, or when labels are on).</summary>
        void UpdateLabels()
        {
            var cam = Camera.main;
            var size = _root.layout.size;
            var used = new HashSet<string>();
            if (_lab != null && cam != null && !float.IsNaN(size.x) && size.x > 0 && Mode != BlackHoleLab.Mode.Observation)
                foreach (var (key, world) in _lab.Labels)
                {
                    var vp = cam.WorldToViewportPoint(world);
                    if (vp.z <= 0f) continue;
                    if (!_labelElements.TryGetValue(key, out var l))
                    {
                        l = UiKit.Text(_labels, Loc.T(key), "eh-hud-label");
                        l.style.position = Position.Absolute;
                        l.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f);
                        _labelElements[key] = l;
                    }
                    l.style.left = vp.x * size.x + 6f;
                    l.style.top = (1f - vp.y) * size.y - 10f;
                    l.style.display = DisplayStyle.Flex;
                    used.Add(key);
                }
            foreach (var kv in _labelElements) if (!used.Contains(kv.Key)) kv.Value.style.display = DisplayStyle.None;
        }
    }
}
