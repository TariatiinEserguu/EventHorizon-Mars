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
        [Tooltip("Anti-dust coating upgrade lowers this below 1.")]
        public float dustMultiplier = 1f;
        [Tooltip("A damaged connection: the panel produces nothing until repaired.")]
        public bool faulted;

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

            dustCoverage = Mathf.Clamp01(dustCoverage + dustPerSol * dustMultiplier * dt / MarsEnvironment.SolSeconds);

            if (sunTracking && trackingPivot != null && env.SunDirection.y > 0f)
                trackingPivot.rotation = Quaternion.FromToRotation(surface.up, env.SunDirection) * trackingPivot.rotation;

            env.SurfaceIrradiance(out float direct, out float diffuseHorizontal);
            Vector3 n = surface.up;
            float cosIncidence = Mathf.Max(0f, Vector3.Dot(n, env.SunDirection));
            // Isotropic sky: a panel tilted by angle b sees (1 + cos b) / 2 of the diffuse light.
            float skyView = (1f + Mathf.Clamp(n.y, -1f, 1f)) * 0.5f;
            float irradiance = direct * cosIncidence + diffuseHorizontal * skyView;

            CurrentWatts = faulted ? 0f : irradiance * areaM2 * cellEfficiency * (1f - systemLosses) * (1f - dustCoverage);
            return CurrentWatts;
        }

        public string Prompt => faulted
            ? Loc.T("PROMPT_PANEL_REPAIR")
            : Loc.T("PROMPT_PANEL_CLEAN", CurrentWatts.ToString("0"), (dustCoverage * 100f).ToString("0"));
        public bool CanInteract(GameObject user) => faulted || dustCoverage > 0.01f;

        public void Interact(GameObject user)
        {
            if (faulted)
            {
                // A repair takes a few seconds of work; StoryWork shows the progress and calls back.
                Story.StoryWork.Begin(transform, 3f, Loc.T("WORK_REPAIRING"), () =>
                {
                    faulted = false;
                    Story.Story.SetFlag("panel_fixed_" + name);
                    Game.GameEvents.Raise("panel_repaired");
                });
                return;
            }
            Clean();
            Game.GameEvents.Raise("panel_cleaned");
        }

        public void Clean()
        {
            dustCoverage = 0f;
            UpdateDustVisual();
        }

        [Header("Dust visual")]
        [Tooltip("Renderers of the cell surface, tinted toward dust colour as dust builds up.")]
        public Renderer[] cellRenderers = new Renderer[0];
        public Color cleanColor = new Color(0.05f, 0.08f, 0.2f);
        public Color dustColor = new Color(0.62f, 0.42f, 0.28f);

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        MaterialPropertyBlock _mpb;
        float _shownDust = -1f;

        void LateUpdate()
        {
            if (Mathf.Abs(dustCoverage - _shownDust) > 0.01f) UpdateDustVisual();
        }

        void UpdateDustVisual()
        {
            _shownDust = dustCoverage;
            if (cellRenderers == null || cellRenderers.Length == 0) return;
            _mpb ??= new MaterialPropertyBlock();
            // Dust settles as a matte layer: colour shifts toward regolith and the glass stops reflecting.
            float d = Mathf.Sqrt(dustCoverage);
            foreach (var r in cellRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, Color.Lerp(cleanColor, dustColor, d));
                _mpb.SetFloat(SmoothnessId, Mathf.Lerp(0.9f, 0.15f, d));
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
