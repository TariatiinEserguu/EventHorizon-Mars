using EventHorizon.Colony;
using EventHorizon.Core;
using UnityEngine;

namespace EventHorizon.Astronaut
{
    /// <summary>
    /// The backpack (PLSS). Oxygen is sized like NASA's EMU suit: ~0.6 kg of O2 used at ~0.07 kg/h during active EVA
    /// work, so about 8 hours outside. Energy is the suit battery (heating, fans, radio). Integrity drops slowly
    /// while dust storms sandblast the suit. Refill oxygen at any oxygen <see cref="ResourceTank"/>.
    /// </summary>
    public class SuitLifeSupport : MonoBehaviour
    {
        public float oxygenCapacityKg = 0.6f;
        public float oxygenKg = 0.6f;
        public float useKgPerHour = 0.07f;
        [Tooltip("Suit battery, Wh. The EMU's battery is ~1.1 kWh.")]
        public float energyCapacityWh = 1100f;
        public float energyWh = 1100f;
        [Tooltip("Average draw: heaters, fans, pump, radio, lights. More when it is very cold.")]
        public float baseDrawW = 90f;
        [Range(0f, 1f)] public float integrity = 1f;
        [Tooltip("Set by habitat/airlock triggers: no suit consumables are used inside.")]
        public bool insidePressurisedVolume;
        public bool infiniteOxygen;
        /// <summary>Difficulty multiplier on oxygen use.</summary>
        public float consumptionMultiplier = 1f;

        public float HoursLeft => useKgPerHour > 0f ? oxygenKg / (useKgPerHour * consumptionMultiplier) : float.PositiveInfinity;
        public float OxygenFraction => oxygenCapacityKg > 0f ? oxygenKg / oxygenCapacityKg : 0f;
        public float EnergyFraction => energyCapacityWh > 0f ? energyWh / energyCapacityWh : 0f;
        public bool OutOfOxygen => oxygenKg <= 0f;

        void Update()
        {
            if (insidePressurisedVolume || infiniteOxygen) return;
            float hours = MarsEnvironment.DeltaSeconds / 3600f;
            oxygenKg = Mathf.Max(0f, oxygenKg - useKgPerHour * consumptionMultiplier * hours);

            var env = MarsEnvironment.Instance;
            float cold = env != null ? Mathf.Clamp01((-20f - env.SurfaceTemperatureC) / 60f) : 0f;
            energyWh = Mathf.Max(0f, energyWh - baseDrawW * (1f + cold) * hours);
            if (env != null && env.opticalDepth > 2f)
                integrity = Mathf.Max(0f, integrity - 0.02f * (env.opticalDepth - 2f) * hours);
        }

        /// <summary>Tops up oxygen from the colony stock and recharges the battery. Returns true if anything was added.</summary>
        public bool Refill(ColonyGrid grid)
        {
            float need = oxygenCapacityKg - oxygenKg;
            float got = grid.Take(ResourceType.Oxygen, need);
            oxygenKg += got;
            bool charged = energyWh < energyCapacityWh - 1f;
            energyWh = energyCapacityWh;
            return got > 0.0001f || charged;
        }

        public void Repair() => integrity = 1f;
    }
}
