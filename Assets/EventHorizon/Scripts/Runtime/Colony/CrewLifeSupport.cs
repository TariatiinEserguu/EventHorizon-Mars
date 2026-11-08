using EventHorizon.Core;
using UnityEngine;

namespace EventHorizon.Colony
{
    /// <summary>
    /// Life support for the crew inside a habitat: oxygen, water and food use plus the habitat's base power load.
    /// Per-person rates follow NASA's Baseline Values and Assumptions Document (NASA/TP-2015-218570).
    /// </summary>
    public class CrewLifeSupport : MonoBehaviour, IPowerConsumer
    {
        [Min(0)] public int crew = 4;
        public float oxygenKgPerPersonPerSol = 0.84f;
        [Tooltip("Drinking, food preparation and hygiene water.")]
        public float waterKgPerPersonPerSol = 3.5f;
        [Tooltip("Fraction of used water recovered by the habitat's recycling (ISS: ~0.93-0.98).")]
        [Range(0f, 1f)] public float waterRecovery = 0.93f;
        public float foodKgPerPersonPerSol = 1.5f;
        [Tooltip("Heating, air circulation, CO2 scrubbing and lighting.")]
        public float baseLoadW = 3000f;
        public float loadPerPersonW = 500f;

        /// <summary>0..1: the worst of oxygen, water and food delivery on the last tick.</summary>
        public float Wellbeing { get; private set; } = 1f;
        public float OxygenShortSeconds { get; private set; }

        ColonyGrid _grid;

        void OnEnable()
        {
            _grid = ColonyGrid.Find(this);
            _grid?.Register(this);
        }

        void OnDisable() => _grid?.Unregister(this);

        public float DemandWatts => crew > 0 ? baseLoadW + loadPerPersonW * crew : 0f;

        public void Supply(float satisfaction, float dt)
        {
            if (_grid == null || crew == 0) return;
            float sols = dt / MarsEnvironment.SolSeconds;
            float o2 = Consume(ResourceType.Oxygen, oxygenKgPerPersonPerSol * crew * sols);
            float water = Consume(ResourceType.Water, waterKgPerPersonPerSol * (1f - waterRecovery) * crew * sols);
            float food = Consume(ResourceType.Food, foodKgPerPersonPerSol * crew * sols);
            Wellbeing = Mathf.Min(o2, Mathf.Min(water, Mathf.Min(food, Mathf.Lerp(0.5f, 1f, satisfaction))));
            OxygenShortSeconds = o2 < 0.999f ? OxygenShortSeconds + dt : 0f;
        }

        float Consume(ResourceType type, float kg)
        {
            if (kg <= 0f) return 1f;
            return _grid.Take(type, kg) / kg;
        }
    }
}
