using System;
using System.Collections.Generic;
using EventHorizon.Core;
using EventHorizon.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace EventHorizon.UI
{
    /// <summary>
    /// Science Laboratory / Scientific Database: five categories of topics, each with an explanation,
    /// an interactive calculator, and a link to the in-game simulation where one exists.
    /// Formulas are standard physics. Constants come from NASA fact sheets and the cited papers.
    /// </summary>
    public class SciencePanel
    {
        class Topic
        {
            public string title, basic, expert, simulationScene, simulationLabel;
            public Action<VisualElement> calculator;
        }

        readonly VisualElement _list, _detail;
        readonly Dictionary<string, Button> _catButtons = new();
        readonly bool _allowLaunch;

        public VisualElement Element { get; }

        const double G = 6.674e-11, C = 299792458.0, Msun = 1.989e30;

        public SciencePanel(Action onBack, bool allowLaunch)
        {
            _allowLaunch = allowLaunch;
            Element = new VisualElement();
            Element.AddToClassList("eh-panel");
            Element.style.flexGrow = 1;
            var header = UiKit.Box(Element, "eh-row");
            UiKit.Text(header, "SCIENCE LABORATORY", "eh-panel-title");
            UiKit.Box(header, "eh-spacer");
            UiKit.Small(header, "BACK", () => { UiAudio.Back(); onBack?.Invoke(); });

            var cats = UiKit.Box(Element, "eh-row");
            foreach (var cat in Categories.Keys)
            {
                string c = cat;
                _catButtons[c] = UiKit.Button(cats, c, () => ShowCategory(c), "eh-tab");
            }
            var body = UiKit.Box(Element, "eh-row", "eh-grow");
            body.style.alignItems = Align.Stretch;
            body.style.marginTop = 8;
            _list = UiKit.Scroll(body);
            _list.style.width = 300;
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
                UiKit.Button(_list, topic.title.ToUpperInvariant(), () => ShowTopic(topic), "eh-btn");
            }
            ShowTopic(topics[0]);
        }

        void ShowTopic(Topic t)
        {
            _detail.Clear();
            UiKit.Text(_detail, t.title.ToUpperInvariant(), "eh-panel-title");
            var detail = SettingsService.Current.gameplay.scienceDetail;
            UiKit.Text(_detail, t.basic, "eh-body");
            if (detail != ScienceDetail.Basic && !string.IsNullOrEmpty(t.expert))
            {
                var e = UiKit.Text(_detail, t.expert, "eh-body", "eh-dim");
                e.style.marginTop = 8;
            }
            if (t.calculator != null)
            {
                UiKit.Divider(_detail);
                UiKit.Text(_detail, "INTERACTIVE MODEL", "eh-kicker");
                var calc = UiKit.Box(_detail);
                t.calculator(calc);
            }
            if (_allowLaunch && t.simulationScene != null)
            {
                UiKit.Divider(_detail);
                UiKit.Small(_detail, t.simulationLabel ?? "OPEN SIMULATION", () => GameSession.StartScience(t.simulationScene), "eh-btn-primary");
            }
        }

        // ------------------------------------------------------------------ calculator helpers

        static Label Out(VisualElement parent)
        {
            var l = UiKit.Text(parent, "", "eh-mono", "eh-accent");
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.marginTop = 6;
            return l;
        }

        static void Model(VisualElement p, Func<string> compute, params (string label, float min, float max, float value, Func<float, string> fmt, Action<float> set)[] inputs)
        {
            var output = Out(null);
            foreach (var i in inputs)
            {
                var input = i;
                input.set(input.value);
                UiKit.Slider(p, input.label, input.min, input.max, input.value, v => { input.set(v); output.text = compute(); }, input.fmt);
            }
            p.Add(output);
            output.text = compute();
        }

        static string Sci(double v, string unit) => v >= 1e6 || v < 1e-2 ? $"{v:0.###E+0} {unit}" : $"{v:#,0.##} {unit}";

        // ------------------------------------------------------------------ content

        static Dictionary<string, List<Topic>> s_categories;
        static Dictionary<string, List<Topic>> Categories => s_categories ??= BuildContent();

        static Dictionary<string, List<Topic>> BuildContent()
        {
            var d = new Dictionary<string, List<Topic>>();

            // ---------------- ASTRONOMY
            d["ASTRONOMY"] = new List<Topic>
            {
                new()
                {
                    title = "Solar system",
                    basic = "Eight planets orbit the Sun. Mars is the fourth, 1.52 times farther out than Earth (1.52 AU), so it receives only 43% of Earth's sunlight. A Mars year lasts 687 Earth days (668.6 sols).",
                    expert = "Kepler's third law: T^2 = a^3, with T in years and a in AU. Mars' orbit is eccentric (e = 0.093), so sunlight at the top of its atmosphere varies from ~492 W/m^2 at aphelion to ~717 W/m^2 at perihelion. That swing drives its seasons and global dust storms.",
                    simulationScene = Scenes.SpaceLab, simulationLabel = "OPEN PLANETARIUM",
                    calculator = p => { float a = 1.52f; Model(p, () => $"Orbital period: {Mathf.Pow(a, 1.5f):0.00} Earth years  |  Sunlight: {1361f / (a * a):0} W/m^2 ({100f / (a * a):0}% of Earth)",
                        ("Distance from Sun (AU)", 0.39f, 30f, 1.52f, v => $"{v:0.00} AU", v => a = v)); },
                },
                new()
                {
                    title = "Planets",
                    basic = "Rocky planets (Mercury, Venus, Earth, Mars) are small and dense. Gas and ice giants (Jupiter, Saturn, Uranus, Neptune) are mostly hydrogen, helium and ices. Mars is 53% of Earth's diameter and has 11% of its mass.",
                    expert = "Surface gravity g = GM/r^2. For Mars: M = 6.417e23 kg and r = 3389.5 km, so g = 3.72 m/s^2 (0.38 g). Escape velocity sqrt(2GM/r) is 5.03 km/s, against Earth's 11.19 km/s.",
                    simulationScene = Scenes.SpaceLab, simulationLabel = "OPEN PLANETARIUM",
                },
                new()
                {
                    title = "Moons",
                    basic = "Mars has two small moons. Phobos (27 x 22 x 18 km) orbits only 6,000 km above the surface, three times per sol. Deimos (15 x 12 x 10 km) orbits farther out. Both may be captured asteroids or debris from an ancient impact.",
                    expert = "Phobos orbits inside synchronous altitude, so tides drag it inward by about 1.8 m per century. In 30 to 50 million years it will hit Mars or break up into a ring.",
                    simulationScene = Scenes.SpaceLab, simulationLabel = "VIEW PHOBOS AND DEIMOS",
                },
                new()
                {
                    title = "Stars",
                    basic = "The Sun is a G-type main-sequence star with a surface temperature of about 5,772 K. A star's colour shows its temperature: cooler stars look red and hotter stars blue-white.",
                    expert = "Wien's law: peak wavelength = 2.898e-3 / T (m). Stefan-Boltzmann law: L = 4 pi R^2 sigma T^4. The accretion-disk colours in the black-hole lab use the same blackbody physics.",
                    calculator = p => { float t = 5772f; Model(p, () => $"Peak wavelength: {2.898e-3 / t * 1e9:0} nm  |  {(t < 3500 ? "red" : t < 5000 ? "orange" : t < 6000 ? "yellow-white" : t < 10000 ? "white" : "blue-white")}",
                        ("Surface temperature (K)", 2500f, 30000f, 5772f, v => $"{v:0} K", v => t = v)); },
                },
                new()
                {
                    title = "Black holes",
                    basic = "A black hole is mass compressed inside its Schwarzschild radius, r_s = 2GM/c^2. Inside that radius not even light can escape. The black-hole lab renders one by tracing how light bends near it.",
                    expert = "Key radii: photon sphere at 1.5 r_s (light can orbit), innermost stable circular orbit (ISCO) at 3 r_s (the inner edge of the accretion disk), and shadow radius 2.6 r_s. A clock at radius r runs slow by a factor sqrt(1 - r_s/r).",
                    simulationScene = Scenes.SpaceLab, simulationLabel = "OPEN BLACK HOLE LAB",
                    calculator = p => { float logM = 6.63f, rr = 3f; Model(p, () =>
                        {
                            double m = Math.Pow(10, logM) * Msun, rs = 2 * G * m / (C * C);
                            return $"Mass: {Math.Pow(10, logM):0.##E+0} solar masses\nSchwarzschild radius: {Sci(rs / 1000, "km")}\nISCO: {Sci(3 * rs / 1000, "km")}   Shadow radius: {Sci(2.598 * rs / 1000, "km")}\nClock at {rr:0.0} r_s runs at {Math.Sqrt(Math.Max(0, 1 - 1 / rr)) * 100:0.0}% of the rate far away";
                        },
                        ("log10(mass / Sun)", 0f, 10f, 6.63f, v => $"{v:0.00}", v => logM = v),
                        ("Distance (r_s)", 1.01f, 20f, 3f, v => $"{v:0.00}", v => rr = v)); },
                },
                new()
                {
                    title = "Gravitational lensing",
                    basic = "Mass bends the path of light. Stars behind a black hole appear shifted and stretched, and a star directly behind it becomes a ring of light, an Einstein ring.",
                    expert = "Light passing a mass M at distance b is deflected by 4GM/(c^2 b), twice the Newtonian value. Eddington confirmed this in 1919. Einstein-ring angle: theta_E = sqrt(4GM/c^2 * D_ls / (D_l D_s)).",
                    simulationScene = Scenes.SpaceLab, simulationLabel = "OPEN BLACK HOLE LAB",
                    calculator = p => { float logM = 0f, bR = 1f; Model(p, () =>
                        {
                            double m = Math.Pow(10, logM) * Msun, b = bR * 6.957e8;
                            double alpha = 4 * G * m / (C * C * b);
                            return $"Deflection: {alpha * 206264.8:0.###} arcseconds  ({alpha * 180 / Math.PI:0.######} deg)";
                        },
                        ("log10(mass / Sun)", 0f, 9f, 0f, v => $"{v:0.0}", v => logM = v),
                        ("Closest approach (solar radii)", 1f, 100f, 1f, v => $"{v:0.0}", v => bR = v)); },
                },
            };

            // ---------------- PHYSICS
            d["PHYSICS"] = new List<Topic>
            {
                new()
                {
                    title = "Gravity",
                    basic = "Mars' gravity is 3.72 m/s^2, 38% of Earth's. A jump that lifts you 20 cm on Earth lifts you about 54 cm on Mars, and falls take longer.",
                    expert = "Jump height h = v^2 / 2g. Hang time t = 2v / g. A suited astronaut with life support weighs ~200 kg, which is 745 N on Mars, about what a 76 kg person weighs on Earth.",
                    calculator = p => { float v = 2f, gIdx = 3.721f; Model(p, () => $"Jump height: {v * v / (2 * gIdx):0.00} m   Hang time: {2 * v / gIdx:0.00} s",
                        ("Take-off speed (m/s)", 0.5f, 5f, 2f, x => $"{x:0.0} m/s", x => v = x),
                        ("Gravity (m/s^2)", 1.62f, 24.8f, 3.721f, x => $"{x:0.00}", x => gIdx = x)); },
                },
                new()
                {
                    title = "Orbital mechanics",
                    basic = "A spacecraft in orbit is falling around the planet. Higher orbits are slower and take longer. Mars' areostationary orbit, which matches the planet's spin, sits 17,030 km above the surface.",
                    expert = "Circular speed v = sqrt(mu/r); period T = 2 pi sqrt(r^3/mu). For Mars, mu = 4.2828e13 m^3/s^2 and the radius is 3389.5 km.",
                    calculator = p => { float alt = 400f; Model(p, () =>
                        {
                            double mu = 4.2828e13, r = (3389.5 + alt) * 1000;
                            return $"Orbital speed: {Math.Sqrt(mu / r) / 1000:0.00} km/s   Period: {2 * Math.PI * Math.Sqrt(r * r * r / mu) / 60:0} min";
                        }, ("Altitude above Mars (km)", 100f, 20000f, 400f, x => $"{x:0} km", x => alt = x)); },
                },
                new()
                {
                    title = "Motion",
                    basic = "Newton's laws are the same on Mars, but lower gravity means less friction. Rovers need more distance to stop and astronauts tend to lope instead of walking.",
                    expert = "Walking speed is limited by the Froude number Fr = v^2 / (g L). Humans switch to running near Fr ~ 0.5, so on Mars the walk-to-run transition happens near 1.0 m/s instead of 2.0 m/s.",
                    calculator = p => { float leg = 0.9f; Model(p, () => $"Walk-run transition on Mars: {Mathf.Sqrt(0.5f * 3.721f * leg):0.00} m/s  (Earth: {Mathf.Sqrt(0.5f * 9.81f * leg):0.00} m/s)",
                        ("Leg length (m)", 0.6f, 1.1f, 0.9f, x => $"{x:0.00} m", x => leg = x)); },
                },
                new()
                {
                    title = "Energy",
                    basic = "Everything in the colony runs on energy budgets. One kilowatt-hour runs a 1,000 W heater for one hour. Solar panels make power only in daylight, so batteries carry the colony through the night.",
                    expert = "Battery size needed: E = P_night x t_night / (depth of discharge x round-trip efficiency). A Mars night lasts about 12.3 hours at the equinox.",
                    calculator = p => { float load = 10f, hours = 12.3f; Model(p, () => $"Battery needed: {load * hours / (0.8f * 0.9f):0} kWh (80% depth of discharge, 90% efficiency)",
                        ("Night-time load (kW)", 1f, 100f, 10f, x => $"{x:0} kW", x => load = x),
                        ("Night length (h)", 8f, 16f, 12.3f, x => $"{x:0.0} h", x => hours = x)); },
                },
                new()
                {
                    title = "Thermodynamics",
                    basic = "Mars is cold because it is far from the Sun and its thin atmosphere holds little heat. At Jezero crater the air swings from about -85 C before dawn to about -10 C in the afternoon.",
                    expert = "Radiative equilibrium: T = [S (1 - A) / (4 sigma)]^(1/4). With S = 586 W/m^2 and albedo 0.25, T is about 210 K (-63 C). The 0.6 kPa CO2 atmosphere adds only ~5 K of greenhouse warming, against ~33 K on Earth.",
                    calculator = p => { float au = 1.524f, albedo = 0.25f; Model(p, () =>
                        {
                            double s = 1361 / (au * au), t = Math.Pow(s * (1 - albedo) / (4 * 5.670e-8), 0.25);
                            return $"Equilibrium temperature: {t:0} K ({t - 273.15:0} C)";
                        },
                        ("Distance from Sun (AU)", 0.39f, 5.2f, 1.524f, x => $"{x:0.00} AU", x => au = x),
                        ("Albedo", 0.05f, 0.8f, 0.25f, x => $"{x:0.00}", x => albedo = x)); },
                },
            };

            // ---------------- CHEMISTRY
            d["CHEMISTRY"] = new List<Topic>
            {
                new()
                {
                    title = "Electrolysis",
                    basic = "An electric current splits water into hydrogen and oxygen: 2 H2O -> 2 H2 + O2. Each kilogram of oxygen needs 1.125 kg of water.",
                    expert = "Thermodynamic minimum: 39.4 kWh per kg H2 (higher heating value). Practical PEM cells need ~50 kWh/kg H2, or ~6.3 kWh per kg O2. The colony's oxygen plant can switch to this mode.",
                    simulationScene = Scenes.MarsColony, simulationLabel = "VISIT THE OXYGEN PLANT",
                    calculator = p => { float kg = 10f; Model(p, () => $"Water needed: {kg * 1.125f:0.0} kg   Energy: {kg * 6.3f:0} kWh   Hydrogen by-product: {kg * 0.126f:0.00} kg",
                        ("Oxygen wanted (kg)", 1f, 100f, 10f, x => $"{x:0} kg", x => kg = x)); },
                },
                new()
                {
                    title = "CO2 processing (MOXIE)",
                    basic = "Mars' air is 95% carbon dioxide. Perseverance's MOXIE experiment heated CO2 to ~800 C and split it: 2 CO2 -> 2 CO + O2. Between 2021 and 2023 it made 122 g of oxygen, up to 12 g per hour.",
                    expert = "MOXIE used ~300 W for ~6 g/h, about 50 kWh/kg including compressor and heaters. A scaled-up plant for a crewed ascent vehicle is estimated at ~25 kW for ~2 kg/h, roughly 11 kWh/kg (Hecht et al., 2021).",
                    simulationScene = Scenes.MarsColony, simulationLabel = "VISIT THE OXYGEN PLANT",
                    calculator = p => { float kw = 25f, eff = 11.4f; Model(p, () => $"Oxygen: {kw / eff:0.00} kg/h = {kw / eff * 24.66f:0.0} kg per sol, enough for {kw / eff * 24.66f / 0.84f:0} people",
                        ("Plant power (kW)", 0.3f, 100f, 25f, x => $"{x:0.0} kW", x => kw = x),
                        ("Energy per kg O2 (kWh)", 6f, 50f, 11.4f, x => $"{x:0.0}", x => eff = x)); },
                },
                new()
                {
                    title = "Oxygen generation",
                    basic = "A resting adult uses about 0.84 kg of oxygen per day. The colony makes oxygen in three ways: solid-oxide CO2 electrolysis, water electrolysis and photosynthesis in the greenhouse.",
                    expert = "NASA's Baseline Values and Assumptions Document (NASA/TP-2015-218570) gives 0.82 kg O2 per crew member per day at nominal activity, rising during heavy EVA work.",
                    calculator = p => { float crew = 4f; Model(p, () => $"Daily need: {crew * 0.84f:0.0} kg O2 per sol  |  {crew * 0.84f * 668.6f / 1000f:0.00} t per Mars year",
                        ("Crew size", 1f, 50f, 4f, x => $"{x:0}", x => crew = Mathf.Round(x))); },
                },
                new()
                {
                    title = "Chemical reactions",
                    basic = "The Sabatier reaction turns CO2 and hydrogen into methane rocket fuel and water: CO2 + 4 H2 -> CH4 + 2 H2O. It releases heat, and Mars has plenty of CO2.",
                    expert = "Sabatier is exothermic (dH = -165 kJ/mol) and runs at 300-400 C over a nickel or ruthenium catalyst. With electrolysis it closes the loop that makes methane/oxygen propellant on Mars.",
                    calculator = p => { float ch4 = 100f; Model(p, () => $"Needs {ch4 * 44.01f / 16.04f:0} kg CO2 + {ch4 * 4 * 2.016f / 16.04f:0.0} kg H2; produces {ch4 * 2 * 18.02f / 16.04f:0} kg water",
                        ("Methane wanted (kg)", 10f, 1000f, 100f, x => $"{x:0} kg", x => ch4 = x)); },
                },
            };

            // ---------------- BIOLOGY
            d["BIOLOGY"] = new List<Topic>
            {
                new()
                {
                    title = "Photosynthesis",
                    basic = "Plants use light to turn carbon dioxide and water into sugar and oxygen: 6 CO2 + 6 H2O -> C6H12O6 + 6 O2. A greenhouse makes food and also recycles air.",
                    expert = "Producing 1 kg of dry biomass releases roughly 1.07 kg of O2. About 20 to 50 m^2 of high-light crop area per person can supply most of one person's oxygen.",
                    simulationScene = Scenes.MarsColony, simulationLabel = "VISIT THE GREENHOUSE",
                    calculator = p => { float area = 40f, rate = 0.03f; Model(p, () => $"Oxygen from crops: {area * rate:0.00} kg per sol, {area * rate / 0.84f * 100:0}% of one person's need",
                        ("Growing area (m^2)", 2f, 200f, 40f, x => $"{x:0} m^2", x => area = x),
                        ("O2 per m^2 per sol (kg)", 0.01f, 0.06f, 0.03f, x => $"{x:0.000}", x => rate = x)); },
                },
                new()
                {
                    title = "Plant growth",
                    basic = "Under LEDs, lettuce is ready in about 6 weeks, wheat in 10 weeks and potatoes in 15 weeks. Light, water, nutrients, CO2 and temperature all set how fast plants grow.",
                    expert = "Growth tracks the daily light integral (DLI, mol of photons per m^2 per day). Lettuce needs ~14-17 mol/m^2/d. Wheat yields keep rising up to ~60. At 2.8 umol/J LED efficacy, every 1 mol/m^2/d costs ~0.1 kWh/m^2.",
                    simulationScene = Scenes.MarsColony, simulationLabel = "VISIT THE GREENHOUSE",
                    calculator = p => { float dli = 17f; Model(p, () => $"LED energy: {dli / 2.8f / 3.6f:0.00} kWh per m^2 per day",
                        ("Daily light integral (mol/m^2/d)", 5f, 60f, 17f, x => $"{x:0}", x => dli = x)); },
                },
                new()
                {
                    title = "Hydroponics",
                    basic = "Hydroponic crops grow in nutrient solution instead of soil. Martian regolith contains toxic perchlorates, so a colony grows food hydroponically at first.",
                    expert = "A closed greenhouse condenses transpired water and reuses about 95% of it. Nutrient film technique (NFT) recirculates a thin film of solution past the roots, using far less water than field farming.",
                    simulationScene = Scenes.MarsColony, simulationLabel = "VISIT THE GREENHOUSE",
                    calculator = p => { float area = 12f, rec = 0.95f; Model(p, () => $"Net water use: {area * 4f * (1 - rec):0.00} kg per sol (transpiration {area * 4f:0} kg, recovered {rec * 100:0}%)",
                        ("Growing area (m^2)", 2f, 100f, 12f, x => $"{x:0} m^2", x => area = x),
                        ("Water recovery", 0.5f, 0.99f, 0.95f, x => $"{x * 100:0}%", x => rec = x)); },
                },
                new()
                {
                    title = "Ecosystems",
                    basic = "A long-term colony needs a balanced loop: plants make oxygen and food, people make CO2 and waste, and microbes recycle the waste into nutrients.",
                    expert = "Projects like Biosphere 2 and MELiSSA (ESA) showed how hard closed ecosystems are to balance: soil microbes ate oxygen faster than predicted. That is why the colony keeps machine life support as a backup.",
                },
            };

            // ---------------- ENGINEERING
            d["ENGINEERING"] = new List<Topic>
            {
                new()
                {
                    title = "Solar power",
                    basic = "Sunlight on Mars is weaker than on Earth, and dust in the air and on the panels cuts it further. Mars Pathfinder's panels lost about 0.3% of their output every sol to settling dust.",
                    expert = "Panel output = irradiance x area x efficiency x (1 - dust). Direct sunlight falls off as exp(-tau / cos z). The colony model fits Appelbaum and Flood's (1990) diffuse-light tables to within ~5% for tau 0.5 to 5.",
                    simulationScene = Scenes.MarsColony, simulationLabel = "VISIT THE SOLAR FIELD",
                    calculator = p => { float tau = 0.5f, dust = 0f, elev = 60f; Model(p, () =>
                        {
                            float mu = Mathf.Sin(elev * Mathf.Deg2Rad), s = 586f, slant = tau / Mathf.Max(mu, 0.05f);
                            float global = s * Mathf.Exp(-slant) * mu + s * mu * (1 - Mathf.Exp(-slant)) * 0.6f * Mathf.Exp(-0.25f * tau);
                            return $"Ground sunlight: {global:0} W/m^2   10 m^2 GaAs panel: {global * 10 * 0.29f * 0.9f * (1 - dust):0} W";
                        },
                        ("Dust optical depth (tau)", 0.2f, 6f, 0.5f, x => $"{x:0.0}", x => tau = x),
                        ("Dust on panel", 0f, 0.8f, 0f, x => $"{x * 100:0}%", x => dust = x),
                        ("Sun elevation (deg)", 5f, 90f, 60f, x => $"{x:0}", x => elev = x)); },
                },
                new()
                {
                    title = "Life-support systems",
                    basic = "Life support keeps air breathable, pressure safe and temperature comfortable. It scrubs CO2, adds oxygen, removes humidity and recycles water.",
                    expert = "Per crew member per day: 0.84 kg O2, ~1.0 kg CO2 removed, ~2.5 kg drinking water, ~1.8 kg food including packaging water (NASA BVAD). The ISS recovers ~98% of its water.",
                    calculator = p => { float crew = 4f, sols = 500f; Model(p, () => $"Without recycling: {crew * sols * 0.84f / 1000f:0.0} t O2, {crew * sols * 3.5f / 1000f:0.0} t water, {crew * sols * 1.8f / 1000f:0.0} t food",
                        ("Crew", 1f, 20f, 4f, x => $"{x:0}", x => crew = Mathf.Round(x)),
                        ("Mission length (sols)", 30f, 1000f, 500f, x => $"{x:0}", x => sols = x)); },
                },
                new()
                {
                    title = "Water recycling",
                    basic = "Water is too heavy to ship from Earth, so a colony recycles almost all of it: urine, humidity from the air and wash water are all purified and reused.",
                    expert = "The ISS Water Recovery System uses vapour compression distillation for urine plus multifiltration and catalytic oxidation. It reached 98% recovery in 2023.",
                    calculator = p => { float crew = 4f, rec = 0.93f; Model(p, () => $"Top-up water needed: {crew * 3.5f * (1 - rec):0.00} kg per sol",
                        ("Crew", 1f, 50f, 4f, x => $"{x:0}", x => crew = Mathf.Round(x)),
                        ("Recovery rate", 0.5f, 0.99f, 0.93f, x => $"{x * 100:0}%", x => rec = x)); },
                },
                new()
                {
                    title = "Spacecraft systems",
                    basic = "A rocket's change in velocity (delta-v) depends on its engine efficiency and how much of its mass is propellant. Leaving Mars for low orbit takes about 4.1 km/s; leaving Earth takes about 9.4 km/s.",
                    expert = "Tsiolkovsky's rocket equation: delta-v = Isp x g0 x ln(m0 / m1). Methane/oxygen engines reach Isp ~360-380 s in vacuum.",
                    calculator = p => { float isp = 370f, ratio = 3f; Model(p, () => $"Delta-v: {isp * 9.80665f * Mathf.Log(ratio) / 1000f:0.00} km/s",
                        ("Specific impulse (s)", 200f, 460f, 370f, x => $"{x:0} s", x => isp = x),
                        ("Mass ratio (full / empty)", 1.1f, 12f, 3f, x => $"{x:0.0}", x => ratio = x)); },
                },
            };
            return d;
        }
    }
}
