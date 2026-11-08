using System.Text;
using EventHorizon.Agriculture;
using EventHorizon.Colony;
using EventHorizon.Core;
using EventHorizon.Game;
using EventHorizon.Story;
using UnityEngine;
using UnityEngine.UIElements;

namespace EventHorizon.UI
{
    /// <summary>
    /// Live control screen for a <see cref="ColonyTerminal"/>: readings from the simulation, the facility's own
    /// controls, and story actions (diagnostics, experiments, the black-hole lab) when a mission needs them.
    /// Blocks gameplay input while open; Esc closes.
    /// </summary>
    public class TerminalScreen : MonoBehaviour
    {
        static TerminalScreen s_open;
        UIDocument _doc;
        ColonyTerminal _terminal;
        Label _readout;
        VisualElement _actions, _story;
        float _next;

        public static bool IsOpen => s_open != null;
        /// <summary>Frame the terminal closed on, so the same Esc press doesn't also open the pause menu.</summary>
        public static int LastClosedFrame { get; private set; } = -1;

        public static void Open(ColonyTerminal terminal)
        {
            if (s_open != null) return;
            var doc = UiRoot.Create("Terminal", 30);
            s_open = doc.gameObject.AddComponent<TerminalScreen>();
            s_open._doc = doc;
            s_open._terminal = terminal;
            s_open.Build();
            GameInput.Block(s_open);
            GameInput.UpdateCursor();
            Loc.Changed += s_open.Rebuild;
        }

        public static void Close()
        {
            if (s_open == null) return;
            GameInput.Unblock(s_open);
            Loc.Changed -= s_open.Rebuild;
            Destroy(s_open.gameObject);
            s_open = null;
            LastClosedFrame = Time.frameCount;
            GameInput.UpdateCursor();
        }

        void Rebuild()
        {
            _doc.rootVisualElement.Clear();
            Build();
        }

        void Build()
        {
            var root = _doc.rootVisualElement;
            var bg = UiKit.Box(root, "eh-modal-bg");
            bg.pickingMode = PickingMode.Position;
            var panel = UiKit.Box(bg, "eh-panel");
            panel.style.width = 960;
            panel.style.maxHeight = Length.Percent(88);
            var head = UiKit.Box(panel, "eh-row");
            UiKit.Text(head, "ARES-1 // " + _terminal.Title, "eh-panel-title");
            UiKit.Box(head, "eh-spacer");
            UiKit.Small(head, Loc.T("BTN_CLOSE_ESC"), Close);
            UiKit.Divider(panel);
            _readout = UiKit.Text(panel, "", "eh-mono", "eh-body");
            _readout.style.fontSize = 19;
            UiKit.Divider(panel);
            _actions = UiKit.Box(panel, "eh-row");
            _actions.style.flexWrap = Wrap.Wrap;
            BuildActions();
            _story = UiKit.Box(panel, "eh-col");
            BuildStoryActions();
            Refresh();
        }

        void BuildStoryActions()
        {
            _story.Clear();
            var actions = StoryContent.TerminalActions(_terminal.type);
            if (actions.Count == 0) return;
            UiKit.Divider(_story);
            UiKit.Text(_story, Loc.T("TERM_MISSION_TASKS"), "eh-kicker");
            var row = UiKit.Box(_story, "eh-row");
            row.style.flexWrap = Wrap.Wrap;
            foreach (var (label, action) in actions) UiKit.Small(row, label, action, "eh-btn-primary").style.marginRight = 6;
        }

        void BuildActions()
        {
            switch (_terminal.type)
            {
                case TerminalType.Solar:
                    UiKit.Small(_actions, Loc.T("TERM_SOLAR_CLEAN"), () =>
                    {
                        foreach (var p in FindObjectsByType<SolarPanel>()) p.Clean();
                        Hud.Toast(Loc.T("TERM_SOLAR_CLEANED"));
                    });
                    UiKit.Small(_actions, Loc.T("TERM_SOLAR_TRACKING"), () =>
                    {
                        var panels = FindObjectsByType<SolarPanel>();
                        bool on = panels.Length > 0 && !panels[0].sunTracking;
                        foreach (var p in panels) p.sunTracking = on;
                        if (on) GameEvents.Raise("tracking_on");
                        Hud.Toast(Loc.T(on ? "TERM_TRACKING_ON" : "TERM_TRACKING_OFF"));
                    });
                    break;
                case TerminalType.Oxygen:
                    UiKit.Small(_actions, Loc.T("TERM_O2_TOGGLE"), () =>
                    {
                        foreach (var o in FindObjectsByType<OxygenGenerator>()) o.running = !o.running;
                    });
                    UiKit.Small(_actions, Loc.T("TERM_O2_PROCESS"), () =>
                    {
                        foreach (var o in FindObjectsByType<OxygenGenerator>())
                        {
                            bool toWater = o.process == OxygenGenerator.Process.SolidOxideCO2;
                            o.process = toWater ? OxygenGenerator.Process.WaterElectrolysis : OxygenGenerator.Process.SolidOxideCO2;
                            o.kWhPerKgO2 = toWater ? 6.3f : 11.4f;
                        }
                        StoryWorld.Apply();
                    });
                    break;
                case TerminalType.Greenhouse:
                    UiKit.Small(_actions, Loc.T("TERM_GH_HARVEST"), () =>
                    {
                        int n = 0;
                        foreach (var b in FindObjectsByType<HydroponicBed>())
                            if (b.Planted && !b.Dead && b.Growth01 >= 1f) { b.Interact(null); n++; }
                        Hud.Toast(n > 0 ? Loc.T("TERM_GH_HARVESTED", n) : Loc.T("TERM_GH_NOTHING"));
                    });
                    UiKit.Small(_actions, Loc.T("TERM_GH_REPLANT"), () =>
                    {
                        foreach (var b in FindObjectsByType<HydroponicBed>())
                            if ((!b.Planted || b.Dead) && b.crop != null) b.Plant(b.crop);
                    });
                    break;
                case TerminalType.Water:
                    UiKit.Small(_actions, Loc.T("TERM_WATER_PURIFY"), () => Hud.Toast(Loc.T("TERM_WATER_PURIFIED")));
                    break;
                case TerminalType.Observatory:
                    UiKit.Small(_actions, Loc.T("TERM_OBS_SKY"), () => Hud.Toast(Loc.T("TERM_OBS_SKY_DONE")));
                    break;
                case TerminalType.Recycling:
                    UiKit.Small(_actions, Loc.T("TERM_REC_FLUSH"), () => Hud.Toast(Loc.T("TERM_REC_FLUSHED")));
                    break;
                default:
                    UiKit.Small(_actions, Loc.T("TERM_CMD_LOG"), () => Hud.Toast(Loc.T("TERM_CMD_LOGGED")));
                    break;
            }
        }

        void Update()
        {
            if (GameInput.Pause.WasPressedThisFrame()) { Close(); return; }
            if (Time.unscaledTime >= _next) Refresh();
        }

        static string L(string key, params object[] a) => Loc.T(key, a);

        void Refresh()
        {
            _next = Time.unscaledTime + 0.25f;
            var grid = ColonyGrid.Instance;
            var env = MarsEnvironment.Instance;
            var sb = new StringBuilder();
            if (grid == null) { _readout.text = Loc.T("TERM_NO_CONNECTION"); return; }

            switch (_terminal.type)
            {
                case TerminalType.Solar:
                {
                    var panels = FindObjectsByType<SolarPanel>();
                    float total = 0f, dust = 0f, area = 0f;
                    int faulted = 0;
                    foreach (var p in panels) { total += p.CurrentWatts; dust += p.dustCoverage; area += p.areaM2; if (p.faulted) faulted++; }
                    float avgDust = panels.Length > 0 ? dust / panels.Length : 0f;
                    sb.AppendLine(L("TERM_SOLAR_ARRAYS", panels.Length, area.ToString("0")));
                    if (faulted > 0) sb.AppendLine(L("TERM_SOLAR_FAULTS", faulted));
                    sb.AppendLine(L("TERM_SOLAR_OUTPUT", (total / 1000f).ToString("0.00")));
                    sb.AppendLine(L("TERM_SOLAR_DEMAND", (grid.DemandWatts / 1000f).ToString("0.00"), (grid.Satisfaction * 100f).ToString("0")));
                    sb.AppendLine(L("TERM_SOLAR_BATTERY", (grid.BatteryStoredWh / 1000f).ToString("0.0"), (grid.BatteryCapacityWh / 1000f).ToString("0"), (grid.BatteryFraction * 100f).ToString("0")));
                    sb.AppendLine(L("TERM_SOLAR_EFFICIENCY", ((1f - avgDust) * 100f).ToString("0"), (avgDust * 100f).ToString("0.0")));
                    if (env != null)
                        sb.AppendLine(L("TERM_SOLAR_SUN", env.SunElevationDeg.ToString("0"), env.opticalDepth.ToString("0.00"), env.TopOfAtmosphereIrradiance.ToString("0")));
                    sb.AppendLine(L("TERM_SOLAR_TRACK", Loc.T(panels.Length > 0 && panels[0].sunTracking ? "TERM_TRACK_DUAL" : "TERM_TRACK_FIXED")));
                    break;
                }
                case TerminalType.Oxygen:
                {
                    var plants = FindObjectsByType<OxygenGenerator>();
                    float rate = 0f, power = 0f; bool running = false, soxe = true;
                    foreach (var o in plants) { rate += o.OutputKgPerHour; power += o.DemandWatts; running |= o.running; soxe = o.process == OxygenGenerator.Process.SolidOxideCO2; }
                    float co2 = rate * (88.02f / 32f);
                    sb.AppendLine(L("TERM_O2_PROCESS_LINE", Loc.T(soxe ? "TERM_O2_SOXE" : "TERM_O2_WATER")));
                    sb.AppendLine(L("TERM_O2_STATUS", Loc.T(running ? (rate > 0f ? "TERM_O2_PRODUCING" : "TERM_O2_STANDBY") : "TERM_O2_OFFLINE")));
                    sb.AppendLine(L("TERM_O2_CO2", (soxe ? co2 : 0f).ToString("0.00")));
                    sb.AppendLine(L("TERM_O2_RATE", rate.ToString("0.000"), (rate * 24.66f).ToString("0.0")));
                    sb.AppendLine(L("TERM_O2_POWER", (power / 1000f).ToString("0.0")));
                    sb.AppendLine(L("TERM_O2_STACK", running && rate > 0f ? 800 : 25));
                    sb.AppendLine(L("TERM_O2_STORE", grid.Amount(ResourceType.Oxygen).ToString("0.0"), grid.Capacity(ResourceType.Oxygen).ToString("0")));
                    break;
                }
                case TerminalType.Greenhouse:
                {
                    var beds = FindObjectsByType<HydroponicBed>();
                    float o2 = 0f, area = 0f;
                    foreach (var b in beds) { o2 += b.OxygenKgPerSol; area += b.AreaM2; }
                    float t = 22f + (env != null ? Mathf.Clamp((env.SurfaceTemperatureC + 50f) * 0.03f, -1.5f, 1.5f) : 0f);
                    sb.AppendLine(L("TERM_GH_AREA", area.ToString("0.0"), beds.Length));
                    sb.AppendLine(L("TERM_GH_CLIMATE", t.ToString("0.0")));
                    sb.AppendLine(L("TERM_GH_WATER", (beds.Length * 0.4f).ToString("0.0")));
                    sb.AppendLine(L("TERM_GH_NUTRIENT"));
                    sb.AppendLine(L("TERM_GH_O2", o2.ToString("0.00")));
                    if (HydroponicBed.GrowthMultiplier <= 0f) sb.AppendLine(L("TERM_GH_PUMPS_DOWN"));
                    sb.AppendLine();
                    foreach (var b in beds)
                    {
                        string crop = b.crop != null ? Loc.TOr("CROP_" + b.crop.displayName.ToUpperInvariant(), b.crop.displayName) : Loc.T("TERM_GH_EMPTY_BED");
                        string state = !b.Planted ? Loc.T("TERM_GH_STATE_EMPTY") : b.Dead ? Loc.T("TERM_GH_STATE_DEAD") : b.Growth01 >= 1f ? Loc.T("TERM_GH_STATE_READY") : $"{b.Growth01 * 100f:0}%";
                        sb.AppendLine(L("TERM_GH_BED", b.name, crop, state, (b.Health * 100f).ToString("0")));
                    }
                    break;
                }
                case TerminalType.Water:
                {
                    float ice = 0f;
                    var deposits = FindObjectsByType<IceDeposit>();
                    foreach (var d in deposits) ice += d.iceKg;
                    sb.AppendLine(L("TERM_WATER_STORE", grid.Amount(ResourceType.Water).ToString("0"), grid.Capacity(ResourceType.Water).ToString("0")));
                    sb.AppendLine(L("TERM_WATER_ICE", deposits.Length, ice.ToString("0")));
                    sb.AppendLine(L("TERM_WATER_EXTRACTION"));
                    sb.AppendLine(L("TERM_WATER_PURIFICATION"));
                    sb.AppendLine(L("TERM_WATER_RECYCLING"));
                    break;
                }
                case TerminalType.Observatory:
                {
                    var s = Story.Story.State;
                    sb.AppendLine(L("TERM_OBS_DISH"));
                    sb.AppendLine(L("TERM_OBS_OLYMPUS", Loc.T(s.tech.Contains("astro_adv") ? "TERM_OBS_ONLINE" : "TERM_OBS_SILENT")));
                    sb.AppendLine(L("TERM_OBS_LAB", Loc.T(s.tech.Contains("lab_blackhole") ? "TERM_OBS_ONLINE" : "TERM_OBS_LOCKED")));
                    sb.AppendLine(L("TERM_OBS_RELAY"));
                    break;
                }
                case TerminalType.Recycling:
                {
                    float recovery = 0.93f;
                    foreach (var c in FindObjectsByType<CrewLifeSupport>()) recovery = c.waterRecovery;
                    sb.AppendLine(L("TERM_REC_RECOVERY", (recovery * 100f).ToString("0")));
                    sb.AppendLine(L("TERM_REC_STAGES"));
                    sb.AppendLine(L("TERM_REC_NUTRIENTS"));
                    sb.AppendLine(L("TERM_REC_WATER", grid.Amount(ResourceType.Water).ToString("0")));
                    break;
                }
                default:
                {
                    var s = Story.Story.State;
                    sb.AppendLine(L("TERM_CMD_POP", ColonyStats.Population()));
                    sb.AppendLine(L("TERM_CMD_DEV", ColonyStats.DevelopmentPercent(grid).ToString("0")));
                    sb.AppendLine(L("TERM_CMD_INFRA", FindObjectsByType<SolarPanel>().Length, FindObjectsByType<HydroponicBed>().Length, FindObjectsByType<OxygenGenerator>().Length));
                    sb.AppendLine(L("TERM_CMD_RES", grid.Amount(ResourceType.Oxygen).ToString("0"), grid.Amount(ResourceType.Water).ToString("0"), grid.Amount(ResourceType.Food).ToString("0")));
                    sb.AppendLine(L("TERM_CMD_POWER", (grid.ProductionWatts / 1000f).ToString("0.0"), (grid.DemandWatts / 1000f).ToString("0.0")));
                    sb.AppendLine(L("TERM_CMD_RESEARCH", s.research, s.codex.Count));
                    sb.AppendLine(L("TERM_CMD_MISSION", MissionTracker.CurrentTitle));
                    if (env != null) sb.AppendLine(L("TERM_CMD_TIME", env.Sol, env.ClockText, env.solarLongitudeDeg.ToString("0")));
                    break;
                }
            }
            _readout.text = sb.ToString();
        }

        void OnDestroy()
        {
            if (s_open == this) { GameInput.Unblock(this); Loc.Changed -= Rebuild; s_open = null; }
        }
    }
}
