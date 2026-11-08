using EventHorizon.Core;
using EventHorizon.Interaction;
using UnityEngine;

namespace EventHorizon.Colony
{
    /// <summary>
    /// Photovoltaic array connected to the <see cref="ColonyGrid"/>. Output depends on sun angle, season,
    /// atmospheric dust and dust settling on the panel. Interact to clean it.
    /// </summary>
    public class SolarPanel : MonoBehaviour, IPowerProducer, IInteractable
    {
        [Tooltip("Transform whose up axis is the panel's surface normal. Defaults to this transform.")]
        public Transform surface;
        [Tooltip("Pivot rotated toward the sun when tracking is enabled.")]
        public Transform trackingPivot;
        public bool sunTracking;

        [Min(0f)] public float areaM2 = 10f;
        [Tooltip("Triple-junction GaAs cells like InSight's reach ~0.29; ordinary silicon ~0.20.")]
        [Range(0.05f, 0.4f)] public float cellEfficiency = 0.29f;
        [Tooltip("Wiring, MPPT and thermal losses.")]
        [Range(0f, 0.5f)] public float systemLosses = 0.1f;

        [Header("Dust on the panel")]
        [Tooltip("Fraction of light blocked by settled dust. Mars Pathfinder lost ~0.28% per sol.")]
        [Range(0f, 1f)] public float dustCoverage;
        public float dustPerSol = 0.0028f;

        public float CurrentWatts { get; private set; }

        ColonyGrid _grid;

        void OnEnable()
        {
            if (surface == null) surface = transform;
            _grid = ColonyGrid.Find(this);
            _grid?.Register(this);
        }

        void OnDisable() => _grid?.Unregister(this);

        public float ProduceWatts(float dt)
        {
            var env = MarsEnvironment.Instance;
            if (env == null) return CurrentWatts = 0f;

            dustCoverage = Mathf.Clamp01(dustCoverage + dustPerSol * dt / MarsEnvironment.SolSeconds);

            if (sunTracking && trackingPivot != null && env.SunDirection.y > 0f)
                trackingPivot.rotation = Quaternion.FromToRotation(surface.up, env.SunDirection) * trackingPivot.rotation;

            env.SurfaceIrradiance(out float direct, out float diffuseHorizontal);
            Vector3 n = surface.up;
            float cosIncidence = Mathf.Max(0f, Vector3.Dot(n, env.SunDirection));
            // Isotropic sky: a panel tilted by angle b sees (1 + cos b) / 2 of the diffuse light.
            float skyView = (1f + Mathf.Clamp(n.y, -1f, 1f)) * 0.5f;
            float irradiance = direct * cosIncidence + diffuseHorizontal * skyView;

            CurrentWatts = irradiance * areaM2 * cellEfficiency * (1f - systemLosses) * (1f - dustCoverage);
            return CurrentWatts;
        }

        public string Prompt => $"Clean solar panel ({CurrentWatts:0} W, {dustCoverage:P0} dust)";
        public bool CanInteract(GameObject user) => dustCoverage > 0.01f;
        public void Interact(GameObject user) => dustCoverage = 0f;
    }
}
