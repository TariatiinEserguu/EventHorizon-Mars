using System;
using System.Collections.Generic;
using System.Globalization;
using EventHorizon.Core;
using EventHorizon.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace EventHorizon.UI
{
    /// <summary>
    /// Science Laboratory / Scientific Database: five categories of topics, each with an explanation (basic, plus
    /// an expert paragraph), an interactive calculator, and a link to the in-game simulation where one exists.
    /// All text comes from the Science table (SCI_&lt;topic&gt;_TITLE, _BASIC, _EXPERT, _IN1.., _OUT). Formulas are
    /// standard physics; constants come from NASA fact sheets and the cited papers.
    /// </summary>
    public class SciencePanel
    {
        class Topic
        {
            public string id, simulationScene, simulationKey;
            public Action<VisualElement> calculator;
        }

        readonly VisualElement _list, _detail;
        readonly Dictionary<string, Button> _catButtons = new();
        readonly bool _allowLaunch;

        public VisualElement Element { get; }

        const double G = 6.674e-11, C = 299792458.0, Msun = 1.989e30, MEarth = 5.972e24;

        public SciencePanel(Action onBack, bool allowLaunch)
        {
            _allowLaunch = allowLaunch;
            Element = new VisualElement();
            Element.AddToClassList("eh-panel");
            Element.style.flexGrow = 1;
            var header = UiKit.Box(Element, "eh-row");
            UiKit.Text(header, Loc.T("MENU_SCIENCE_LAB"), "eh-panel-title");
            UiKit.Box(header, "eh-spacer");
            UiKit.Small(header, Loc.T("BTN_BACK"), () => { UiAudio.Back(); onBack?.Invoke(); });

            var cats = UiKit.Box(Element, "eh-row");
            foreach (var cat in Categories.Keys)
            {
                string c = cat;
                _catButtons[c] = UiKit.Button(cats, Loc.T("SCI_CAT_" + c), () => ShowCategory(c), "eh-tab");
            }
            var body = UiKit.Box(Element, "eh-row", "eh-grow");
            body.style.alignItems = Align.Stretch;
            body.style.marginTop = 8;
            _list = UiKit.Scroll(body);
            _list.style.width = 320;
            _detail = UiKit.Scroll(body);
            _detail.style.flexGrow = 1;
            _detail.style.paddingLeft = 18;
            ShowCategory("ASTRONOMY");
        }

        void ShowCategory(string cat)
        {
            foreach (var kv in _catButtons) kv.Value.EnableInClassList("eh-tab--active", kv.Key == cat);
            _list.Clear();
            var topics = Categories[cat];
            foreach (var t in topics)
            {
                var topic = t;
                UiKit.Button(_list, Loc.Upper(Loc.T($"SCI_{t.id}_TITLE")), () => ShowTopic(topic), "eh-btn");
            }
            ShowTopic(topics[0]);
        }

        void ShowTopic(Topic t)
        {
            _detail.Clear();
            UiKit.Text(_detail, Loc.Upper(Loc.T($"SCI_{t.id}_TITLE")), "eh-panel-title");
            var detail = SettingsService.Current.gameplay.scienceDetail;
            UiKit.Text(_detail, Loc.T($"SCI_{t.id}_BASIC"), "eh-body");
            if (detail != ScienceDetail.Basic && Loc.Has($"SCI_{t.id}_EXPERT"))
            {
                var e = UiKit.Text(_detail, Loc.T($"SCI_{t.id}_EXPERT"), "eh-body", "eh-dim");
                e.style.marginTop = 8;
            }
            if (t.calculator != null)
            {
                UiKit.Divider(_detail);
                UiKit.Text(_detail, Loc.T("SCI_INTERACTIVE"), "eh-kicker");
                var calc = UiKit.Box(_detail);
                t.calculator(calc);
            }
            if (_allowLaunch && t.simulationScene != null)
            {
                UiKit.Divider(_detail);
                UiKit.Small(_detail, Loc.T(t.simulationKey ?? "SCI_OPEN_SIM"), () => GameSession.StartScience(t.simulationScene), "eh-btn-primary");
            }
        }

        // ------------------------------------------------------------------ calculator helpers

        static string N(double v, string f) => v.ToString(f, CultureInfo.InvariantCulture);
        static string Sci(double v) => Math.Abs(v) >= 1e6 || (Math.Abs(v) < 1e-2 && v != 0) ? N(v, "0.###E+0") : N(v, "#,0.##");

        /// <summary>Sliders labelled SCI_&lt;id&gt;_IN1.. that recompute <paramref name="compute"/> on every change.</summary>
        static void Model(VisualElement p, string id, Func<string> compute, params (float min, float max, float value, Func<float, string> fmt, Action<float> set)[] inputs)
        {
            var output = UiKit.Text(null, "", "eh-mono", "eh-accent");
            output.style.whiteSpace = WhiteSpace.Normal;
            output.style.marginTop = 6;
            for (int i = 0; i < inputs.Length; i++)
            {
                var input = inputs[i];
                input.set(input.value);
                UiKit.Slider(p, Loc.T($"SCI_{id}_IN{i + 1}"), input.min, input.max, input.value, v => { input.set(v); output.text = compute(); }, input.fmt);
            }
            p.Add(output);
            output.text = compute();
        }

        static Topic T(string id, Action<VisualElement> calc = null, string scene = null, string simKey = null) =>
            new() { id = id, calculator = calc, simulationScene = scene, simulationKey = simKey };

        static string Out(string id, params object[] a) => Loc.T($"SCI_{id}_OUT", a);

        // ------------------------------------------------------------------ content

        static Dictionary<string, List<Topic>> s_categories;
        static Dictionary<string, List<Topic>> Categories => s_categories ??= BuildContent();

        static Dictionary<string, List<Topic>> BuildContent()
        {
            const string Planetarium = "SCI_OPEN_PLANETARIUM", Lab = "SCI_OPEN_LAB", Orbit = "SCI_OPEN_ORBIT";
            var d = new Dictionary<string, List<Topic>>();

            d["ASTRONOMY"] = new List<Topic>
            {
                T("solar_system", p => { float a = 1.52f; Model(p, "solar_system", () => Out("solar_system", N(Math.Pow(a, 1.5), "0.00"), N(1361 / (a * a), "0"), N(100 / (a * a), "0")),
                    (0.39f, 30f, 1.52f, v => N(v, "0.00") + " AU", v => a = v)); }, Scenes.SpaceLab, Planetarium),
                T("planets", null, Scenes.SpaceLab, Planetarium),
                T("moons", null, Scenes.MarsOrbit, Orbit),
                T("stars", p => { float t = 5772f; Model(p, "stars", () => Out("stars", N(2.898e-3 / t * 1e9, "0"),
                        Loc.T(t < 3500 ? "SCI_COLOR_RED" : t < 5000 ? "SCI_COLOR_ORANGE" : t < 6000 ? "SCI_COLOR_YELLOW" : t < 10000 ? "SCI_COLOR_WHITE" : "SCI_COLOR_BLUE")),
                    (2500f, 30000f, 5772f, v => N(v, "0") + " K", v => t = v)); }),
                T("black_holes", p => { float logM = 6.63f, rr = 3f; Model(p, "black_holes", () =>
                    {
                        double m = Math.Pow(10, logM) * Msun, rs = 2 * G * m / (C * C);
                        return Out("black_holes", Sci(Math.Pow(10, logM)), Sci(rs / 1000), Sci(3 * rs / 1000), Sci(2.598 * rs / 1000), N(rr, "0.0"), N(Math.Sqrt(Math.Max(0, 1 - 1 / rr)) * 100, "0.0"));
                    },
                    (-5f, 10f, 6.63f, v => N(v, "0.00"), v => logM = v),
                    (1.01f, 20f, 3f, v => N(v, "0.00"), v => rr = v)); }, Scenes.BlackHoleLab, Lab),
                T("kerr", p => { float a = 0.9f; Model(p, "kerr", () =>
                    {
                        double rp = Space.BlackHole.HorizonOuter(a) / 2, ip = Space.BlackHole.Isco(a) / 2, ir = Space.BlackHole.Isco(a, false) / 2;
                        double pp = Space.BlackHole.PhotonOrbit(a, true) / 2, eff = 1 - Math.Sqrt(1 - 2 / (3 * Space.BlackHole.Isco(a)));
                        return Out("kerr", N(rp, "0.000"), N(pp, "0.000"), N(ip, "0.000"), N(ir, "0.000"), N(eff * 100, "0.0"), N((1 - 1 / Math.Sqrt(2) * Math.Sqrt(1 + Math.Sqrt(1 - a * a))) * 100, "0.0"));
                    },
                    (0f, 0.998f, 0.9f, v => N(v, "0.000"), v => a = v)); }, Scenes.BlackHoleLab, Lab),
                T("lensing", p => { float logM = 0f, bR = 1f; Model(p, "lensing", () =>
                    {
                        double m = Math.Pow(10, logM) * Msun, b = bR * 6.957e8, alpha = 4 * G * m / (C * C * b);
                        return Out("lensing", N(alpha * 206264.8, "0.###"), N(alpha * 180 / Math.PI, "0.######"));
                    },
                    (0f, 9f, 0f, v => N(v, "0.0"), v => logM = v),
                    (1f, 100f, 1f, v => N(v, "0.0"), v => bR = v)); }, Scenes.BlackHoleLab, Lab),
                T("microlensing", p => { float u = 0.5f; Model(p, "microlensing", () => Out("microlensing", N((u * u + 2) / (u * Math.Sqrt(u * u + 4)), "0.00")),
                    (0.05f, 2f, 0.5f, v => N(v, "0.00"), v => u = v)); }),
                T("pbh", p => { float earths = 6f; Model(p, "pbh", () =>
                    {
                        double rs = 2 * G * earths * MEarth / (C * C);
                        return Out("pbh", N(rs * 100, "0.00"), N(earths * MEarth / Msun, "0.0E+0"), N(Space.BlackHole.IscoFrequencyHz(earths * MEarth / Msun, 0.8) / 1e6, "0"));
                    },
                    (0.1f, 20f, 6f, v => N(v, "0.0"), v => earths = v)); }, Scenes.BlackHoleLab, Lab),
            };

            d["PHYSICS"] = new List<Topic>
            {
                T("gravity", p => { float v = 2f, g = 3.721f; Model(p, "gravity", () => Out("gravity", N(v * v / (2 * g), "0.00"), N(2 * v / g, "0.00")),
                    (0.5f, 5f, 2f, x => N(x, "0.0") + " m/s", x => v = x),
                    (1.62f, 24.8f, 3.721f, x => N(x, "0.00"), x => g = x)); }),
                T("orbits", p => { float alt = 400f; Model(p, "orbits", () =>
                    {
                        double mu = 4.2828e13, r = (3389.5 + alt) * 1000;
                        return Out("orbits", N(Math.Sqrt(mu / r) / 1000, "0.00"), N(2 * Math.PI * Math.Sqrt(r * r * r / mu) / 60, "0"));
                    }, (100f, 20000f, 400f, x => N(x, "0") + " km", x => alt = x)); }, Scenes.MarsOrbit, Orbit),
                T("time_dilation", p => { float r = 3f; Model(p, "time_dilation", () =>
                    {
                        double rate = Math.Sqrt(Math.Max(0, 1 - 1 / r));
                        return Out("time_dilation", N(rate * 100, "0.000"), N(3600 * rate, "0.0"), N((1 / Math.Max(rate, 1e-9) - 1) * 86400, "#,0"));
                    }, (1.01f, 100f, 3f, x => N(x, "0.00") + " r_s", x => r = x)); }, Scenes.BlackHoleLab, Lab),
                T("tides", p => { float logM = 1f, r = 10f; Model(p, "tides", () =>
                    {
                        double m = Math.Pow(10, logM) * Msun, rs = 2 * G * m / (C * C), dist = r * rs, a = 2 * G * m * 2 / (dist * dist * dist);
                        double r10 = Math.Pow(2 * G * m * 2 / 98.1, 1.0 / 3.0);
                        return Out("tides", Sci(dist / 1000), Sci(a), Sci(a / 9.81), Loc.T(r10 > rs ? "LAB_TIDES_OUTSIDE" : "LAB_TIDES_INSIDE"));
                    },
                    (0f, 10f, 1f, x => N(Math.Pow(10, x), "0.##E+0"), x => logM = x),
                    (1.05f, 100f, 10f, x => N(x, "0.0") + " r_s", x => r = x)); }),
                T("motion", p => { float leg = 0.9f; Model(p, "motion", () => Out("motion", N(Mathf.Sqrt(0.5f * 3.721f * leg), "0.00"), N(Mathf.Sqrt(0.5f * 9.81f * leg), "0.00")),
                    (0.6f, 1.1f, 0.9f, x => N(x, "0.00") + " m", x => leg = x)); }),
                T("energy", p => { float load = 10f, hours = 12.3f; Model(p, "energy", () => Out("energy", N(load * hours / (0.8f * 0.9f), "0")),
                    (1f, 100f, 10f, x => N(x, "0") + " kW", x => load = x),
                    (8f, 16f, 12.3f, x => N(x, "0.0") + " h", x => hours = x)); }),
                T("thermo", p => { float au = 1.524f, albedo = 0.25f; Model(p, "thermo", () =>
                    {
                        double s = 1361 / (au * au), t = Math.Pow(s * (1 - albedo) / (4 * 5.670e-8), 0.25);
                        return Out("thermo", N(t, "0"), N(t - 273.15, "0"));
                    },
                    (0.39f, 5.2f, 1.524f, x => N(x, "0.00") + " AU", x => au = x),
                    (0.05f, 0.8f, 0.25f, x => N(x, "0.00"), x => albedo = x)); }),
            };

            d["CHEMISTRY"] = new List<Topic>
            {
                T("electrolysis", p => { float kg = 10f; Model(p, "electrolysis", () => Out("electrolysis", N(kg * 1.125f, "0.0"), N(kg * 6.3f, "0"), N(kg * 0.126f, "0.00")),
                    (1f, 100f, 10f, x => N(x, "0") + " kg", x => kg = x)); }, Scenes.MarsColony, "SCI_VISIT_O2"),
                T("moxie", p => { float kw = 25f, eff = 11.4f; Model(p, "moxie", () => Out("moxie", N(kw / eff, "0.00"), N(kw / eff * 24.66f, "0.0"), N(kw / eff * 24.66f / 0.84f, "0")),
                    (0.3f, 100f, 25f, x => N(x, "0.0") + " kW", x => kw = x),
                    (6f, 50f, 11.4f, x => N(x, "0.0"), x => eff = x)); }, Scenes.MarsColony, "SCI_VISIT_O2"),
                T("oxygen", p => { float crew = 4f; Model(p, "oxygen", () => Out("oxygen", N(crew * 0.84f, "0.0"), N(crew * 0.84f * 668.6f / 1000f, "0.00")),
                    (1f, 50f, 4f, x => N(x, "0"), x => crew = Mathf.Round(x))); }),
                T("reactions", p => { float ch4 = 100f; Model(p, "reactions", () => Out("reactions", N(ch4 * 44.01f / 16.04f, "0"), N(ch4 * 4 * 2.016f / 16.04f, "0.0"), N(ch4 * 2 * 18.02f / 16.04f, "0")),
                    (10f, 1000f, 100f, x => N(x, "0") + " kg", x => ch4 = x)); }),
            };

            d["BIOLOGY"] = new List<Topic>
            {
                T("photosynthesis", p => { float area = 40f, rate = 0.03f; Model(p, "photosynthesis", () => Out("photosynthesis", N(area * rate, "0.00"), N(area * rate / 0.84f * 100, "0")),
                    (2f, 200f, 40f, x => N(x, "0") + " m²", x => area = x),
                    (0.01f, 0.06f, 0.03f, x => N(x, "0.000"), x => rate = x)); }, Scenes.MarsColony, "SCI_VISIT_GH"),
                T("plant_growth", p => { float dli = 17f; Model(p, "plant_growth", () => Out("plant_growth", N(dli / 2.8f / 3.6f, "0.00")),
                    (5f, 60f, 17f, x => N(x, "0"), x => dli = x)); }, Scenes.MarsColony, "SCI_VISIT_GH"),
                T("hydroponics", p => { float area = 12f, rec = 0.95f; Model(p, "hydroponics", () => Out("hydroponics", N(area * 4f * (1 - rec), "0.00"), N(area * 4f, "0"), N(rec * 100, "0")),
                    (2f, 100f, 12f, x => N(x, "0") + " m²", x => area = x),
                    (0.5f, 0.99f, 0.95f, x => N(x * 100, "0") + "%", x => rec = x)); }, Scenes.MarsColony, "SCI_VISIT_GH"),
                T("ecosystems"),
            };

            d["ENGINEERING"] = new List<Topic>
            {
                T("solar_power", p => { float tau = 0.5f, dust = 0f, elev = 60f; Model(p, "solar_power", () =>
                    {
                        float mu = Mathf.Sin(elev * Mathf.Deg2Rad), s = 586f, slant = tau / Mathf.Max(mu, 0.05f);
                        float global = s * Mathf.Exp(-slant) * mu + s * mu * (1 - Mathf.Exp(-slant)) * 0.6f * Mathf.Exp(-0.25f * tau);
                        return Out("solar_power", N(global, "0"), N(global * 10 * 0.29f * 0.9f * (1 - dust), "0"));
                    },
                    (0.2f, 6f, 0.5f, x => N(x, "0.0"), x => tau = x),
                    (0f, 0.8f, 0f, x => N(x * 100, "0") + "%", x => dust = x),
                    (5f, 90f, 60f, x => N(x, "0") + "°", x => elev = x)); }, Scenes.MarsColony, "SCI_VISIT_SOLAR"),
                T("life_support", p => { float crew = 4f, sols = 500f; Model(p, "life_support", () => Out("life_support", N(crew * sols * 0.84f / 1000f, "0.0"), N(crew * sols * 3.5f / 1000f, "0.0"), N(crew * sols * 1.8f / 1000f, "0.0")),
                    (1f, 20f, 4f, x => N(x, "0"), x => crew = Mathf.Round(x)),
                    (30f, 1000f, 500f, x => N(x, "0"), x => sols = x)); }),
                T("water_recycling", p => { float crew = 4f, rec = 0.93f; Model(p, "water_recycling", () => Out("water_recycling", N(crew * 3.5f * (1 - rec), "0.00")),
                    (1f, 50f, 4f, x => N(x, "0"), x => crew = Mathf.Round(x)),
                    (0.5f, 0.99f, 0.93f, x => N(x * 100, "0") + "%", x => rec = x)); }),
                T("spacecraft", p => { float isp = 370f, ratio = 3f; Model(p, "spacecraft", () => Out("spacecraft", N(isp * 9.80665f * Mathf.Log(ratio) / 1000f, "0.00")),
                    (200f, 460f, 370f, x => N(x, "0") + " s", x => isp = x),
                    (1.1f, 12f, 3f, x => N(x, "0.0"), x => ratio = x)); }),
            };
            return d;
        }

        /// <summary>Every topic id, for the localization test.</summary>
        public static IEnumerable<(string id, int inputs)> AllTopics()
        {
            foreach (var kv in Categories)
                foreach (var t in kv.Value) yield return (t.id, 0);
        }
    }
}
