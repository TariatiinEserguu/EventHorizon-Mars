using EventHorizon.Core;
using UnityEngine;

namespace EventHorizon.Colony
{
    /// <summary>Debug readout of the colony simulation. Replace it with the real UI later.</summary>
    public class ColonyStatusHUD : MonoBehaviour
    {
        public ColonyGrid grid;
        public bool visible = true;

        void OnGUI()
        {
            if (!visible) return;
            if (grid == null) grid = ColonyGrid.Find(this);
            var env = MarsEnvironment.Instance;
            var r = new Rect(Screen.width - 300, 10, 290, 170);
            GUI.Box(r, "Colony");
            float y = r.y + 22;
            void Line(string s) { GUI.Label(new Rect(r.x + 8, y, r.width - 16, 20), s); y += 18; }
            if (env != null)
            {
                Line($"Sol {env.Sol}  {env.LocalSolarHour:00.0} h   Ls {env.solarLongitudeDeg:0}  tau {env.opticalDepth:0.00}");
                Line($"Sun elevation {env.SunElevationDeg:0} deg");
            }
            if (grid == null) return;
            Line($"Power {grid.ProductionWatts / 1000f:0.0} / {grid.DemandWatts / 1000f:0.0} kW  ({grid.Satisfaction:P0})");
            Line($"Battery {grid.BatteryStoredWh / 1000f:0} / {grid.BatteryCapacityWh / 1000f:0} kWh");
            Line($"O2    {grid.Amount(ResourceType.Oxygen):0.0} / {grid.Capacity(ResourceType.Oxygen):0} kg");
            Line($"Water {grid.Amount(ResourceType.Water):0} / {grid.Capacity(ResourceType.Water):0} kg");
            Line($"Food  {grid.Amount(ResourceType.Food):0.0} / {grid.Capacity(ResourceType.Food):0} kg");
        }
    }
}
