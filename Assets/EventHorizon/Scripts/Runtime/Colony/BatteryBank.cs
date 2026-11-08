using UnityEngine;

namespace EventHorizon.Colony
{
    /// <summary>Adds energy storage to the grid, so solar power carries the colony through the night.</summary>
    public class BatteryBank : MonoBehaviour
    {
        [Min(0f)] public float capacityWh = 100000f;
        [Range(0f, 1f)] public float initialCharge = 0.8f;

        ColonyGrid _grid;
        bool _seeded;

        void OnEnable()
        {
            _grid = ColonyGrid.Find(this);
            _grid?.AddBatteryCapacity(capacityWh, _seeded ? 0f : initialCharge);
            _seeded = true;
        }

        void OnDisable() => _grid?.RemoveBatteryCapacity(capacityWh);
    }
}
