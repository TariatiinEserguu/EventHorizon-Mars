using System;
using System.Collections.Generic;
using System.Globalization;
using EventHorizon.Colony;
using EventHorizon.Core;
using EventHorizon.Game;
using UnityEngine;

namespace EventHorizon.Story
{
    public static partial class StoryContent
    {
        // Physical constants (SI) and the HORIZON-1 object used by chapters 8-10.
        const double G = 6.674e-11, C = 299792458.0, EarthMass = 5.972e24, SunMass = 1.989e30;
        public const double Horizon1EarthMasses = 6.0;
        public const double Horizon1Spin = 0.80;
        static double Horizon1GM => G * Horizon1EarthMasses * EarthMass;

        static string N(double v, string f = "0.##") => v.ToString(f, CultureInfo.InvariantCulture);
        static string T(string key, params object[] a) => Loc.T(key, a);

        static ExperimentDef X(ExperimentDef e)
        {
            s_experiments[e.id] = e;
            return e;
        }

        static ExperimentDef Choice(string id, int options, int correct, Func<string> readout = null, Action<int> onChoice = null, Action solved = null) =>
            X(new ExperimentDef { id = id, kind = ExperimentDef.Kind.Choice, options = options, correct = correct, readout = readout, onChoice = onChoice, onSolved = solved });

        static ExperimentDef Numeric(string id, Func<double> answer, double tolerance, Func<string> readout = null, Action solved = null) =>
            X(new ExperimentDef { id = id, kind = ExperimentDef.Kind.Numeric, answer = answer, tolerance = tolerance, readout = readout, onSolved = solved });

        static ExperimentDef Order(string id, int items, Func<string> readout = null) =>
            X(new ExperimentDef { id = id, kind = ExperimentDef.Kind.Order, items = items, readout = readout });

        static ExperimentDef Readout(string id, Func<string> readout, Action solved = null) =>
            X(new ExperimentDef { id = id, kind = ExperimentDef.Kind.Readout, readout = readout, onSolved = solved });

        static SliderDef Sl(string id, int n, float min, float max, float value, Func<float, string> fmt) =>
            new() { key = $"X_{id}_S{n}", min = min, max = max, value = value, format = fmt };

        static ExperimentDef Sliders(string id, SliderDef[] sliders, Func<float[], string> readout, Func<float[], bool> goal, Func<string> data = null) =>
            X(new ExperimentDef { id = id, kind = ExperimentDef.Kind.Sliders, sliders = sliders, sliderReadout = readout, goal = goal, readout = data });

        static string Data(string id) => T($"X_{id}_DATA");

        static void BuildExperiments()
        {
            // ================================================================ prologue
            X(new ExperimentDef
            {
                id = "suit_check", kind = ExperimentDef.Kind.Checklist, items = 5,
                readout = () =>
                {
                    var suit = UnityEngine.Object.FindAnyObjectByType<Astronaut.SuitLifeSupport>();
                    float hours = suit != null ? suit.HoursLeft : 8.5f, battery = suit != null ? suit.EnergyFraction * 100f : 100f;
                    return T("X_suit_check_DATA", N(hours, "0.0"), N(battery, "0"));
                },
                checklistGoal = c => Array.TrueForAll(c, x => x),
            });

            // ================================================================ 1
            Choice("priority", 3, -1, () =>
            {
                var g = ColonyGrid.Instance;
                return T("X_priority_DATA", g != null ? N(g.BatteryFraction * 100f, "0") : "18", g != null ? N(g.ProductionWatts / 1000f, "0.0") : "4.2");
            }, i =>
            {
                switch (i)
                {
                    case 0: Story.Decide("power_priority", "greenhouse", new[] { (Axis.Community, 5) }, new[] { (Character.Okafor, 10) }); break;
                    case 1: Story.Decide("power_priority", "research", new[] { (Axis.Science, 5) }, new[] { (Character.Chen, 10), (Character.Okafor, -5) }); break;
                    default: Story.Decide("power_priority", "mining", new[] { (Axis.Independence, 5) }, new[] { (Character.Erdene, 10), (Character.Okafor, -5) }); break;
                }
                React("C01_REACT_" + (char)('A' + i));
            });

            // ================================================================ 2
            Readout("intake", () => Data("intake"));
            Choice("o2diag", 4, 0, () => Data("o2diag"));
            Choice("purity", 3, 0, () => Data("purity"));

            // ================================================================ 3
            Choice("icesite", 3, 1, () => Data("icesite"), solved: () => Story.SetFlag("ice_site_b"));
            Order("purify", 5);

            // ================================================================ 4
            Sliders("nutrients", new[]
                {
                    Sl("nutrients", 1, 4f, 8f, 7.4f, v => N(v, "0.0")),
                    Sl("nutrients", 2, 0.2f, 3.5f, 0.6f, v => N(v, "0.0") + " mS/cm"),
                    Sl("nutrients", 3, 0f, 100f, 20f, v => N(v, "0") + "%"),
                },
                v =>
                {
                    // Iron and phosphorus lock out above pH ~6.5; too little salt starves, too much burns roots.
                    float ph = Mathf.Clamp01(1f - Mathf.Max(0f, Mathf.Abs(v[0] - 6f) - 0.5f) / 1.5f);
                    float ec = Mathf.Clamp01(1f - Mathf.Max(0f, Mathf.Abs(v[1] - 1.8f) - 0.6f) / 1.2f);
                    float flow = Mathf.Clamp01(v[2] / 60f);
                    return T("X_nutrients_OUT", N(ph * ec * flow * 100f, "0"));
                },
                v => v[0] >= 5.5f && v[0] <= 6.5f && v[1] >= 1.2f && v[1] <= 2.4f && v[2] >= 60f);

            Sliders("lights", new[]
                {
                    Sl("lights", 1, 5f, 60f, 8f, v => N(v, "0") + " mol/m²/d"),
                    Sl("lights", 2, 8f, 24f, 12f, v => N(v, "0") + " h"),
                },
                v =>
                {
                    // LEDs at 2.8 µmol/J: kWh per m² per day = DLI / 2.8 / 3.6. Twelve m² of beds.
                    float kwh = v[0] / 2.8f / 3.6f * 12f;
                    float peak = kwh / v[1];
                    float growth = Mathf.Clamp01(v[0] / 17f);
                    return T("X_lights_OUT", N(kwh, "0.0"), N(peak, "0.00"), N(growth * 100f, "0"));
                },
                v => v[0] >= 14f && v[0] <= 30f && v[0] / 2.8f / 3.6f * 12f / v[1] <= 2.0f);

            Choice("crops", 3, -1, () => Data("crops"), i =>
                Story.Decide("crop_priority", i == 0 ? "calories" : i == 1 ? "variety" : "oxygen",
                    new[] { (i == 0 ? Axis.Independence : i == 1 ? Axis.Community : Axis.Science, 3) }, new[] { (Character.Okafor, i == 1 ? 5 : 2) }));

            Sliders("climate", new[]
                {
                    Sl("climate", 1, 10f, 35f, 14f, v => N(v, "0") + " °C"),
                    Sl("climate", 2, 30f, 95f, 45f, v => N(v, "0") + "%"),
                },
                v =>
                {
                    // Vapour pressure deficit (kPa): how hard the air pulls water out of the leaves.
                    float es = 0.6108f * Mathf.Exp(17.27f * v[0] / (v[0] + 237.3f));
                    float vpd = es * (1f - v[1] / 100f);
                    string mold = v[1] > 80f ? T("X_climate_MOLD_HIGH") : v[1] > 75f ? T("X_climate_MOLD_MED") : T("X_climate_MOLD_LOW");
                    return T("X_climate_OUT", N(vpd, "0.00"), mold);
                },
                v => v[0] >= 20f && v[0] <= 25f && v[1] >= 60f && v[1] <= 75f);

            Readout("humidity", () => Data("humidity"));

            // ================================================================ 5
            Readout("waste", () => Data("waste"));
            Order("routing", 5);
            Sliders("optimize", new[]
                {
                    Sl("optimize", 1, 0f, 12f, 4f, v => N(v, "0.0") + " kW"),
                    Sl("optimize", 2, 2f, 12f, 4f, v => N(v, "0.0") + " bar"),
                    Sl("optimize", 3, 20f, 45f, 25f, v => N(v, "0") + " °C"),
                    Sl("optimize", 4, 0f, 90f, 20f, v => N(v, "0") + "%"),
                },
                v =>
                {
                    Optimize(v, out float water, out float nutrients, out float power);
                    return T("X_optimize_OUT", N(water * 100f, "0.0"), N(nutrients * 100f, "0"), N(power, "0.0"));
                },
                v => { Optimize(v, out float w, out float n, out float p); return w >= 0.95f && n >= 0.8f && p <= 14f; });

            // ================================================================ 7 (orbit)
            Sliders("obs_power", new[] { Sl("obs_power", 1, 0f, 180f, 20f, v => N(v, "0") + "°") },
                v => T("X_obs_power_OUT", N(18f * Mathf.Max(0f, Mathf.Cos((v[0] - 112f) * Mathf.Deg2Rad)), "0.0")),
                v => Mathf.Abs(v[0] - 112f) <= 8f, () => Data("obs_power"));
            Sliders("obs_antenna", new[]
                {
                    Sl("obs_antenna", 1, 0f, 360f, 40f, v => N(v, "0") + "°"),
                    Sl("obs_antenna", 2, 0f, 90f, 10f, v => N(v, "0") + "°"),
                },
                v => T("X_obs_antenna_OUT", N(Signal(v[0], v[1]), "0")),
                v => Signal(v[0], v[1]) >= 90f, () => Data("obs_antenna"));
            X(new ExperimentDef
            {
                id = "obs_instruments", kind = ExperimentDef.Kind.Checklist, items = 5,
                costs = new[] { 1.2f, 2.0f, 0.3f, 1.5f, 1.8f }, budget = 4.0f,
                readout = () => Data("obs_instruments"),
                checklistGoal = c =>
                {
                    float used = (c[0] ? 1.2f : 0f) + (c[1] ? 2f : 0f) + (c[2] ? 0.3f : 0f) + (c[3] ? 1.5f : 0f) + (c[4] ? 1.8f : 0f);
                    return c[0] && c[1] && c[2] && used <= 4.0f;
                },
            });
            Readout("obs_archive", () => Data("obs_archive"));
            Choice("lensing1", 4, 0, () => Data("lensing1"));
            Choice("classified", 4, 2, () => Data("classified"), solved: () => Story.SetFlag("archive_3"));

            // ================================================================ 8
            Sliders("signal", new[]
                {
                    Sl("signal", 1, 250f, 450f, 380f, v => N(v, "0.0") + " MHz"),
                    Sl("signal", 2, 0.1f, 20f, 12f, v => N(v, "0.0") + " MHz"),
                    Sl("signal", 3, 1f, 120f, 5f, v => N(v, "0") + " s"),
                },
                v => T("X_signal_OUT", N(Snr(v), "0.0")),
                v => Snr(v) >= 10f, () => Data("signal"));
            Order("timeline", 5);
            Choice("compare", 3, 1, () =>
            {
                // Predictions for each candidate mass: Einstein angle grows as sqrt(M); flyby turn from the hyperbola.
                string Row(double m) => T("X_compare_ROW", N(m, "0.0"), N(0.253 * Math.Sqrt(m), "0.00"), N(FlybyTurnDeg(m, 3e7, 12e3), "0"));
                return Data("compare") + "\n" + Row(0.6) + "\n" + Row(6) + "\n" + Row(60);
            });
            Numeric("decode", () => 2 * Horizon1GM / (C * C) * 100.0, 0.03, () => Data("decode"));
            Choice("hidden", 3, 1, () => Data("hidden"), solved: () => Story.SetFlag("archive_4"));

            // ================================================================ 9 (lab)
            Numeric("rs", () => 2 * Horizon1GM / (C * C) * 1000.0, 0.03, () => Data("rs"));
            Sliders("bending", new[] { Sl("bending", 1, 0.5f, 20f, 2f, v => N(v, "0.0")) },
                v => T("X_bending_OUT", N(BendingMas(v[0]), "0.00")),
                v => Math.Abs(BendingMas(v[0]) - BendingMas(Horizon1EarthMasses)) / BendingMas(Horizon1EarthMasses) <= 0.05, () => Data("bending"));
            Sliders("trajectory", new[] { Sl("trajectory", 1, 0.1f, 10f, 3f, v => N(v, "0.00")) },
                v => T("X_trajectory_OUT", N(FlybyTurnDeg(v[0], 3e7, 12e3), "0.0"), N(FlybyTurnDeg(Horizon1EarthMasses, 3e7, 12e3), "0.0")),
                v => Mathf.Abs(v[0] - 0.6f) <= 0.08f, () => Data("trajectory"));

            // ================================================================ 10
            X(new ExperimentDef
            {
                id = "upgrade", kind = ExperimentDef.Kind.Checklist, items = 4, costs = new[] { 1f, 1f, 1f, 1f }, budget = 2f,
                readout = () => Data("upgrade"),
                checklistGoal = c =>
                {
                    int n = 0;
                    foreach (var x in c) if (x) n++;
                    if (!(c[1] && n == 2)) return false;
                    var picks = new List<string>();
                    string[] names = { "shielding", "navigation", "lifesupport", "drive" };
                    for (int i = 0; i < 4; i++) if (c[i]) picks.Add(names[i]);
                    Story.Decide("ship_upgrades", string.Join("+", picks),
                        new[] { (c[3] ? Axis.Exploration : Axis.Community, 5) });
                    return true;
                },
            });
            Numeric("provision", () => 4 * 0.84 * 690, 0.03, () => Data("provision"));
            Numeric("nav", () => Math.Sqrt(Horizon1GM / 4e7) / 1000.0, 0.03, () => Data("nav"));
            double[] podRadii = { 2e7, 3.5e7, 6e7 };
            for (int i = 0; i < 3; i++)
            {
                double r = podRadii[i];
                int pod = i + 1;
                string id = "pod" + pod;
                double vc = Math.Sqrt(Horizon1GM / r) / 1000.0;
                Sliders(id, new[] { Sl(id, 1, 2f, 16f, 5f, v => N(v, "0.00") + " km/s") },
                    v => T("X_pod_OUT", N((v[0] - vc) * 1000.0, "+0;-0;0")),
                    v => Math.Abs(v[0] - vc) / vc <= 0.03, () => T("X_pod_DATA", pod, N(r / 1000.0, "#,0")));
            }
            Choice("final", 4, 0, () => Data("final"));
            Choice("ergo", 2, -1, () => Data("ergo"), i =>
                Story.Decide("open_ergosphere", i == 0 ? "open" : "seal", new[] { (i == 0 ? Axis.Science : Axis.Earth, 10) }));

            // ================================================================ side missions
            Choice("rover", 4, 1, () => Data("rover"));
            Choice("cropsave", 4, 2, () => Data("cropsave"));
            Choice("glitch", 4, 0, () => Data("glitch"));
            Choice("minerals", 4, 0, () => Data("minerals"));
            Numeric("quake", () => 42.0 / (1.0 / 4.2 - 1.0 / 7.5), 0.03, () => Data("quake"));
            Choice("cave", 4, 1, () => Data("cave"));
            Numeric("solarcmp", () => (1.0 - 612.0 / 845.0) * 100.0, 0.04, () => Data("solarcmp"));
            Choice("plants", 4, 1, () => Data("plants"));
            Numeric("gravity", () => 4 * Math.PI * Math.PI * 1.0 / (3.26 * 3.26), 0.02, () => Data("gravity"));
            Numeric("co2", () => 0.61 * 0.951, 0.03, () => Data("co2"));
            Numeric("filter", () => (1.0 - 12.0 / 480.0) * 100.0, 0.01, () => Data("filter"));
            Numeric("binary", () => Math.Pow(3.1, 3) / (2.4 * 2.4), 0.03, () => Data("binary"));
            Numeric("asteroid", () => 299792.458 * 31.2 / 2.0 / 1e6, 0.03, () => Data("asteroid"));
            Choice("pulsar", 4, 2, () => Data("pulsar"));
            Numeric("microlens", () => { double u = 0.5; return (u * u + 2) / (u * Math.Sqrt(u * u + 4)); }, 0.03, () => Data("microlens"));
            Numeric("accretion", () => 2.898e-3 / 2.9e-9 / 1e6, 0.05, () => Data("accretion"));
            Numeric("telescope", () => 4.0 * 8000.0 / 2.0 / 3600.0, 0.03, () => Data("telescope"));
        }

        // ------------------------------------------------------------------ models behind the puzzles

        static void Optimize(float[] v, out float water, out float nutrients, out float power)
        {
            float distil = Mathf.Clamp01(v[0] / 8f);
            float memb = Mathf.Clamp01((v[1] - 2f) / 6f) - Mathf.Max(0f, v[1] - 10f) * 0.03f; // above ~10 bar the membrane fouls
            float brine = v[3] / 90f;
            water = Mathf.Min(0.98f, 0.78f + 0.08f * memb + 0.08f * distil + 0.04f * brine * distil);
            nutrients = Mathf.Clamp01(1f - Mathf.Abs(v[2] - 37f) / 17f); // the bioreactor's microbes like ~37 °C
            power = 1.5f + v[0] + 0.4f * v[1] + 0.05f * Mathf.Max(0f, v[2] - 20f) + brine * 3f;
        }

        static float Signal(float az, float el)
        {
            float da = Mathf.DeltaAngle(az, 214f), de = el - 37f;
            return 100f * Mathf.Exp(-(da * da) / (2f * 36f) - (de * de) / (2f * 16f));
        }

        /// <summary>The periodic radio signal from HORIZON-1: a hot spot orbiting at the ISCO (about 311 MHz).</summary>
        public static float Horizon1SignalMHz => (float)(Space.BlackHoleLab.Horizon1SignalHz / 1e6);

        static float Snr(float[] v) =>
            30f * Mathf.Exp(-Mathf.Pow((v[0] - Horizon1SignalMHz) / 3f, 2f)) * Mathf.Sqrt(Mathf.Min(v[2], 120f) / 60f) / Mathf.Sqrt(Mathf.Max(v[1], 0.1f));

        /// <summary>Starlight deflection 4GM/(c² b) in milliarcseconds, for b = 7,000 km.</summary>
        static double BendingMas(double earthMasses) => 4 * G * earthMasses * EarthMass / (C * C * 7e6) * 206264806.0;

        /// <summary>How far a hyperbolic flyby turns a ship: 2 asin(1/e), e = 1 + r v² / GM.</summary>
        static double FlybyTurnDeg(double earthMasses, double periapsisM, double vInfinity)
        {
            double e = 1 + periapsisM * vInfinity * vInfinity / (G * earthMasses * EarthMass);
            return 2 * Math.Asin(1 / e) * 180 / Math.PI;
        }
    }
}
