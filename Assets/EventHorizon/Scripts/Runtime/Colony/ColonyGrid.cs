using System.Collections.Generic;
using EventHorizon.Core;
using UnityEngine;

namespace EventHorizon.Colony
{
    /// <summary>
    /// The colony's electricity network and resource stores. Producers, consumers, tanks and batteries
    /// register themselves on enable. Each frame the grid balances power, charges or drains the battery
    /// pool, then lets every consumer do its work with the power it actually received.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class ColonyGrid : MonoBehaviour
    {
        public static ColonyGrid Instance { get; private set; }

        [Range(0.5f, 1f)] public float batteryRoundTripEfficiency = 0.9f;

        readonly List<IPowerProducer> _producers = new();
        readonly List<IPowerConsumer> _consumers = new();
        readonly Dictionary<ResourceType, float> _amount = new();
        readonly Dictionary<ResourceType, float> _capacity = new();

        public float ProductionWatts { get; private set; }
        public float DemandWatts { get; private set; }
        public float Satisfaction { get; private set; } = 1f;
        public float BatteryCapacityWh { get; private set; }
        public float BatteryStoredWh { get; private set; }
        public float BatteryFraction => BatteryCapacityWh > 0 ? BatteryStoredWh / BatteryCapacityWh : 0f;

        /// <summary>Finds the grid a component belongs to: a parent grid first, else the scene's main grid.</summary>
        public static ColonyGrid Find(Component from)
        {
            var parent = from != null ? from.GetComponentInParent<ColonyGrid>() : null;
            if (parent != null) return parent;
            if (Instance == null) Instance = FindAnyObjectByType<ColonyGrid>();
            return Instance;
        }

        void Awake()
        {
            if (Instance == null) Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Register(IPowerProducer p) { if (!_producers.Contains(p)) _producers.Add(p); }
        public void Unregister(IPowerProducer p) => _producers.Remove(p);
        public void Register(IPowerConsumer c) { if (!_consumers.Contains(c)) _consumers.Add(c); }
        public void Unregister(IPowerConsumer c) => _consumers.Remove(c);

        public void AddBatteryCapacity(float wh, float initialFraction)
        {
            BatteryCapacityWh += wh;
            BatteryStoredWh = Mathf.Min(BatteryCapacityWh, BatteryStoredWh + wh * initialFraction);
        }

        public void RemoveBatteryCapacity(float wh)
        {
            BatteryCapacityWh = Mathf.Max(0f, BatteryCapacityWh - wh);
            BatteryStoredWh = Mathf.Min(BatteryStoredWh, BatteryCapacityWh);
        }

        public void AddCapacity(ResourceType type, float kg, float initialKg)
        {
            _capacity[type] = Capacity(type) + kg;
            Add(type, initialKg);
        }

        public void RemoveCapacity(ResourceType type, float kg)
        {
            _capacity[type] = Mathf.Max(0f, Capacity(type) - kg);
            _amount[type] = Mathf.Min(Amount(type), _capacity[type]);
        }

        public float Amount(ResourceType type) => _amount.TryGetValue(type, out var v) ? v : 0f;
        public float Capacity(ResourceType type) => _capacity.TryGetValue(type, out var v) ? v : 0f;

        /// <summary>Stores up to <paramref name="kg"/>; returns how much fit. Without tanks, nothing is stored.</summary>
        public float Add(ResourceType type, float kg)
        {
            if (kg <= 0f) return 0f;
            float accepted = Mathf.Min(kg, Capacity(type) - Amount(type));
            _amount[type] = Amount(type) + accepted;
            return accepted;
        }

        /// <summary>Removes up to <paramref name="kg"/>; returns how much was available.</summary>
        public float Take(ResourceType type, float kg)
        {
            if (kg <= 0f) return 0f;
            float taken = Mathf.Min(kg, Amount(type));
            _amount[type] = Amount(type) - taken;
            return taken;
        }

        void Update()
        {
            float dt = MarsEnvironment.DeltaSeconds;
            if (dt <= 0f) return;

            float produced = 0f;
            for (int i = _producers.Count - 1; i >= 0; i--) produced += Mathf.Max(0f, _producers[i].ProduceWatts(dt));
            float demand = 0f;
            for (int i = _consumers.Count - 1; i >= 0; i--) demand += Mathf.Max(0f, _consumers[i].DemandWatts);

            float hours = dt / 3600f;
            float satisfaction = 1f;
            if (produced >= demand)
            {
                float surplusWh = (produced - demand) * hours * batteryRoundTripEfficiency;
                BatteryStoredWh = Mathf.Min(BatteryCapacityWh, BatteryStoredWh + surplusWh);
            }
            else
            {
                float deficitWh = (demand - produced) * hours;
                float fromBattery = Mathf.Min(deficitWh, BatteryStoredWh);
                BatteryStoredWh -= fromBattery;
                float deliveredWh = produced * hours + fromBattery;
                satisfaction = demand > 0f ? Mathf.Clamp01(deliveredWh / (demand * hours)) : 1f;
            }

            ProductionWatts = produced;
            DemandWatts = demand;
            Satisfaction = satisfaction;
            for (int i = _consumers.Count - 1; i >= 0; i--) _consumers[i].Supply(satisfaction, dt);
        }
    }
}
