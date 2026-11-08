using EventHorizon.Core;
using EventHorizon.Interaction;
using UnityEngine;

namespace EventHorizon.Colony
{
    /// <summary>
    /// Makes oxygen for the colony. Two processes:
    /// - Solid-oxide CO2 electrolysis (MOXIE-type): 2 CO2 -> 2 CO + O2, using unlimited atmospheric CO2.
    ///   The stack runs at ~800 C and stops producing if power falls below the hold-heat threshold.
    /// - Water electrolysis: 2 H2O -> 2 H2 + O2, which uses 1.125 kg of water per kg of O2.
    /// </summary>
    public class OxygenGenerator : MonoBehaviour, IPowerConsumer, IInteractable
    {
        public enum Process
        {
            SolidOxideCO2,
            WaterElectrolysis,
        }

        public Process process = Process.SolidOxideCO2;
        public bool running = true;
        [Min(0f)] public float ratedPowerW = 25000f;
        [Tooltip("Electrical energy per kg of O2. MOXIE on Mars: ~50 kWh/kg (300 W for 6 g/h). " +
                 "Scaled-up ISRU plant estimate: ~11 kWh/kg. PEM water electrolysis: ~6.3 kWh/kg O2.")]
        [Min(0.1f)] public float kWhPerKgO2 = 11.4f;
        [Tooltip("Solid-oxide only: below this power fraction the stack cools and makes nothing.")]
        [Range(0f, 1f)] public float minimumPowerFraction = 0.5f;

        public const float WaterKgPerKgO2 = 36.03f / 32.00f; // 2 H2O (36.03 g) -> O2 (32.00 g)

        public float OutputKgPerHour { get; private set; }

        ColonyGrid _grid;

        void OnEnable()
        {
            _grid = ColonyGrid.Find(this);
            _grid?.Register(this);
        }

        void OnDisable() => _grid?.Unregister(this);

        public float DemandWatts => running ? ratedPowerW : 0f;

        public void Supply(float satisfaction, float dt)
        {
            if (!running || _grid == null || dt <= 0f)
            {
                OutputKgPerHour = 0f;
                return;
            }
            if (process == Process.SolidOxideCO2 && satisfaction < minimumPowerFraction)
            {
                OutputKgPerHour = 0f;
                return;
            }

            float energyKWh = ratedPowerW * satisfaction * dt / 3.6e6f;
            float kg = energyKWh / kWhPerKgO2;
            if (process == Process.WaterElectrolysis)
            {
                float needWater = kg * WaterKgPerKgO2;
                float gotWater = _grid.Take(ResourceType.Water, needWater);
                kg *= needWater > 0f ? gotWater / needWater : 0f;
            }
            float stored = _grid.Add(ResourceType.Oxygen, kg);
            OutputKgPerHour = stored / dt * 3600f;
        }

        public string Prompt => Loc.T(running ? "PROMPT_O2_STOP" : "PROMPT_O2_START", (OutputKgPerHour * 1000f).ToString("0"));
        public bool CanInteract(GameObject user) => true;
        public void Interact(GameObject user)
        {
            running = !running;
            Game.GameEvents.Raise("oxygen_toggled");
        }

        /// <summary>Sets the MOXIE values from the Perseverance rover's experiment.</summary>
        [ContextMenu("Preset: MOXIE (300 W, ~6 g/h)")]
        void PresetMoxie()
        {
            process = Process.SolidOxideCO2;
            ratedPowerW = 300f;
            kWhPerKgO2 = 50f;
        }

        [ContextMenu("Preset: Water electrolysis (PEM)")]
        void PresetWater()
        {
            process = Process.WaterElectrolysis;
            kWhPerKgO2 = 6.3f;
        }
    }
}
