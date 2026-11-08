using System.Text;
using EventHorizon.Agriculture;
using EventHorizon.Colony;
using EventHorizon.Core;
using EventHorizon.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace EventHorizon.UI
{
    /// <summary>Live control screen for a <see cref="ColonyTerminal"/>. Blocks gameplay input while open; Esc closes.</summary>
    public class TerminalScreen : MonoBehaviour
    {
        static TerminalScreen s_open;
        UIDocument _doc;
        ColonyTerminal _terminal;
        Label _readout;
        VisualElement _actions;
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
        }

        public static void Close()
        {
            if (s_open == null) return;
            GameInput.Unblock(s_open);
            Destroy(s_open.gameObject);
            s_open = null;
            LastClosedFrame = Time.frameCount;
        }

        void Build()
        {
            var root = _doc.rootVisualElement;
            var bg = UiKit.Box(root, "eh-modal-bg");
            bg.pickingMode = PickingMode.Position;
            var panel = UiKit.Box(bg, "eh-panel");
            panel.style.width = 900;
            panel.style.maxHeight = Length.Percent(85);
            var head = UiKit.Box(panel, "eh-row");
            UiKit.Text(head, "ARES-1 // " + _terminal.Title, "eh-panel-title");
            UiKit.Box(head, "eh-spacer");
            UiKit.Small(head, "CLOSE [ESC]", Close);
            UiKit.Divider(panel);
            _readout = UiKit.Text(panel, "", "eh-mono", "eh-body");
            _readout.style.fontSize = 19;
            UiKit.Divider(panel);
            _actions = UiKit.Box(panel, "eh-row");
            _actions.style.flexWrap = Wrap.Wrap;
            BuildActions();
            Refresh();
        }

        void BuildActions()
        {
            switch (_terminal.type)
            {
                case TerminalType.Solar:
                    UiKit.Small(_actions, "DISPATCH CLEANING ROVER", () =>
                    {
                        foreach (var p in FindObjectsByType<SolarPanel>()) p.Clean();
                        Hud.Toast("Cleaning rover swept the solar field");
                    });
                    UiKit.Small(_actions, "TOGGLE SUN TRACKING", () =>
                    {
                        var panels = FindObjectsByType<SolarPanel>();
                        bool on = panels.Length > 0 && !panels[0].sunTracking;
                        foreach (var p in panels) p.sunTracking = on;
                        Hud.Toast(on ? "Dual-axis sun tracking enabled" : "Panels locked at fixed tilt");
                    });
                    break;
                case TerminalType.Oxygen:
                    UiKit.Small(_actions, "START / STOP PLANT", () =>
                    {
                        foreach (var o in FindObjectsByType<OxygenGenerator>()) o.running = !o.running;
                    });
                    UiKit.Small(_actions, "SWITCH PROCESS (CO2 / H2O)", () =>
                    {
                        foreach (var o in FindObjectsByType<OxygenGenerator>())
                        {
                            bool toWater = o.process == OxygenGenerator.Process.SolidOxideCO2;
                            o.process = toWater ? OxygenGenerator.Process.WaterElectrolysis : OxygenGenerator.Process.SolidOxideCO2;
                            o.kWhPerKgO2 = toWater ? 6.3f : 11.4f;
                        }
                    });
                    break;
                case TerminalType.Greenhouse:
                    UiKit.Small(_actions, "HARVEST ALL READY", () =>
                    {
                        int n = 0;
                        foreach (var b in FindObjectsByType<HydroponicBed>())
                            if (b.Planted && !b.Dead && b.Growth01 >= 1f) { b.Interact(null); n++; }
                        Hud.Toast(n > 0 ? $"Harvested {n} beds" : "Nothing is ready to harvest");
                    });
                    UiKit.Small(_actions, "REPLANT EMPTY BEDS", () =>
                    {
                        foreach (var b in FindObjectsByType<HydroponicBed>())
                            if ((!b.Planted || b.Dead) && b.crop != null) b.Plant(b.crop);
                    });
                    break;
                case TerminalType.Water:
                    UiKit.Small(_actions, "RUN PURIFICATION CYCLE", () => Hud.Toast("Purification cycle: 99.9% of contaminants removed"));
                    break;
                default:
                    UiKit.Small(_actions, "SAVE COLONY LOG", () => Hud.Toast("Colony log archived"));
                    break;
            }
        }

        void Update()
        {
            if (GameInput.Pause.WasPressedThisFrame()) { Close(); return; }
            if (Time.unscaledTime >= _next) Refresh();
        }

        void Refresh()
        {
            _next = Time.unscaledTime + 0.25f;
            var grid = ColonyGrid.Instance;
            var env = MarsEnvironment.Instance;
            var sb = new StringBuilder();
            if (grid == null) { _readout.text = "NO CONNECTION TO COLONY NETWORK"; return; }

            switch (_terminal.type)
            {
                case TerminalType.Solar:
                {
                    var panels = FindObjectsByType<SolarPanel>();
                    float total = 0f, dust = 0f, area = 0f;
                    foreach (var p in panels) { total += p.CurrentWatts; dust += p.dustCoverage; area += p.areaM2; }
                    float avgDust = panels.Length > 0 ? dust / panels.Length : 0f;
                    env?.SurfaceIrradiance(out _, out _);
                    sb.AppendLine($"ARRAYS ONLINE ........ {panels.Length}  ({area:0} m^2)");
                    sb.AppendLine($"ELECTRICITY OUTPUT ... {total / 1000f:0.00} kW");
                    sb.AppendLine($"COLONY CONSUMPTION ... {grid.DemandWatts / 1000f:0.00} kW   (supply {grid.Satisfaction * 100f:0}%)");
                    sb.AppendLine($"BATTERY STORAGE ...... {grid.BatteryStoredWh / 1000f:0.0} / {grid.BatteryCapacityWh / 1000f:0} kWh  ({grid.BatteryFraction * 100f:0}%)");
                    sb.AppendLine($"PANEL EFFICIENCY ..... {(1f - avgDust) * 100f:0}%  (dust cover {avgDust * 100f:0.0}%)");
                    if (env != null)
                        sb.AppendLine($"SUN ELEVATION ........ {env.SunElevationDeg:0} deg   TAU {env.opticalDepth:0.00}   TOA {env.TopOfAtmosphereIrradiance:0} W/m^2");
                    sb.AppendLine($"TRACKING ............. {(panels.Length > 0 && panels[0].sunTracking ? "DUAL-AXIS" : "FIXED TILT 18.4 deg S")}");
                    break;
                }
                case TerminalType.Oxygen:
                {
                    var plants = FindObjectsByType<OxygenGenerator>();
                    float rate = 0f, power = 0f; bool running = false; string proc = "-";
                    foreach (var o in plants) { rate += o.OutputKgPerHour; power += o.DemandWatts; running |= o.running; proc = o.process == OxygenGenerator.Process.SolidOxideCO2 ? "SOLID-OXIDE CO2 ELECTROLYSIS" : "WATER ELECTROLYSIS"; }
                    float co2 = rate * (88.02f / 32f);
                    sb.AppendLine($"PROCESS .............. {proc}");
                    sb.AppendLine($"STATUS ............... {(running ? (rate > 0f ? "PRODUCING" : "STANDBY: INSUFFICIENT POWER") : "OFFLINE")}");
                    sb.AppendLine($"CO2 INTAKE ........... {(proc.StartsWith("SOLID") ? co2 : 0f):0.00} kg/h");
                    sb.AppendLine($"OXYGEN PRODUCTION .... {rate:0.000} kg/h  ({rate * 24.66f:0.0} kg/sol)");
                    sb.AppendLine($"POWER DRAW ........... {power / 1000f:0.0} kW");
                    sb.AppendLine($"STACK TEMPERATURE .... {(running && rate > 0f ? 800 : 25)} C");
                    sb.AppendLine($"OXYGEN STORAGE ....... {grid.Amount(ResourceType.Oxygen):0.0} / {grid.Capacity(ResourceType.Oxygen):0} kg");
                    break;
                }
                case TerminalType.Greenhouse:
                {
                    var beds = FindObjectsByType<HydroponicBed>();
                    float o2 = 0f, area = 0f;
                    foreach (var b in beds) { o2 += b.OxygenKgPerSol; area += b.AreaM2; }
                    float t = 22f + (env != null ? Mathf.Clamp((env.SurfaceTemperatureC + 50f) * 0.03f, -1.5f, 1.5f) : 0f);
                    sb.AppendLine($"GROWING AREA ......... {area:0.0} m^2 in {beds.Length} beds");
                    sb.AppendLine($"TEMPERATURE .......... {t:0.0} C     HUMIDITY 68%     CO2 1,200 ppm");
                    sb.AppendLine($"WATER CONSUMPTION .... net {beds.Length * 0.4f:0.0} kg/sol (95% condensate recovery)");
                    sb.AppendLine($"NUTRIENT SOLUTION .... EC 1.8 mS/cm   pH 5.9");
                    sb.AppendLine($"OXYGEN RELEASED ...... {o2:0.00} kg/sol");
                    sb.AppendLine();
                    foreach (var b in beds)
                    {
                        string crop = b.crop != null ? b.crop.displayName : "(empty)";
                        string state = !b.Planted ? "EMPTY" : b.Dead ? "DEAD" : b.Growth01 >= 1f ? "READY" : $"{b.Growth01 * 100f:0}%";
                        sb.AppendLine($"  {b.name,-10} {crop,-9} {state,-6} health {b.Health * 100f:0}%");
                    }
                    break;
                }
                case TerminalType.Water:
                {
                    float ice = 0f;
                    var deposits = FindObjectsByType<IceDeposit>();
                    foreach (var d in deposits) ice += d.iceKg;
                    sb.AppendLine($"WATER STORAGE ........ {grid.Amount(ResourceType.Water):0} / {grid.Capacity(ResourceType.Water):0} kg");
                    sb.AppendLine($"KNOWN ICE DEPOSITS ... {deposits.Length}  ({ice:0} kg remaining)");
                    sb.AppendLine("ICE EXTRACTION ....... manual drill, 150 kg per session, 90% purity");
                    sb.AppendLine("PURIFICATION ......... multifiltration + catalytic oxidation");
                    sb.AppendLine("RECYCLING RATE ....... 93% (habitat), 95% (greenhouse condensate)");
                    break;
                }
                default:
                {
                    sb.AppendLine($"POPULATION ........... {ColonyStats.Population()}");
                    sb.AppendLine($"DEVELOPMENT .......... {ColonyStats.DevelopmentPercent(grid):0}%");
                    sb.AppendLine($"INFRASTRUCTURE ....... {FindObjectsByType<SolarPanel>().Length} solar arrays, {FindObjectsByType<HydroponicBed>().Length} crop beds, {FindObjectsByType<OxygenGenerator>().Length} O2 plant");
                    sb.AppendLine($"RESOURCES ............ O2 {grid.Amount(ResourceType.Oxygen):0} kg   H2O {grid.Amount(ResourceType.Water):0} kg   FOOD {grid.Amount(ResourceType.Food):0} kg");
                    sb.AppendLine($"POWER ................ {grid.ProductionWatts / 1000f:0.0} kW generated / {grid.DemandWatts / 1000f:0.0} kW used");
                    sb.AppendLine($"RESEARCH ............. {Profile.Current.discoveries.Count} discoveries logged");
                    sb.AppendLine($"MISSION STATUS ....... {MissionTracker.CurrentTitle}");
                    if (env != null) sb.AppendLine($"MARS TIME ............ Sol {env.Sol}, {env.ClockText} LMST, Ls {env.solarLongitudeDeg:0}");
                    break;
                }
            }
            _readout.text = sb.ToString();
        }

        void OnDestroy()
        {
            if (s_open == this) { GameInput.Unblock(this); s_open = null; }
        }
    }
}
