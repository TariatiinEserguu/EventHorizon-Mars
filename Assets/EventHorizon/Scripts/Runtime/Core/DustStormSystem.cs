using UnityEngine;

namespace EventHorizon.Core
{
    /// <summary>
    /// Mars weather: wind, airborne dust and occasional regional dust storms. A storm raises the optical depth tau
    /// in <see cref="MarsEnvironment"/>, which dims sunlight (and solar output), thickens the haze and narrows the
    /// day/night temperature swing. Drives fog and a dust-mote particle system around the camera.
    /// </summary>
    public class DustStormSystem : MonoBehaviour
    {
        public bool stormsEnabled = true;
        [Tooltip("Optical depth on a clear day. Jezero typically sees 0.4-0.8.")]
        public float clearTau = 0.5f;
        public Vector2 stormTauRange = new Vector2(2f, 4.5f);
        [Tooltip("Average sols between regional storms (gameplay value; real regional storms cluster in dusty seasons).")]
        public float meanSolsBetweenStorms = 6f;
        public Vector2 stormDurationSols = new Vector2(0.3f, 1.2f);

        [Header("Atmosphere look")]
        public Color clearFog = new Color(0.78f, 0.58f, 0.42f);
        public Color stormFog = new Color(0.62f, 0.4f, 0.26f);
        public float clearFogDensity = 0.0011f;
        public float stormFogDensity = 0.022f;

        [Header("Dust motes")]
        public ParticleSystem dust;
        public float clearEmission = 40f;
        public float stormEmission = 900f;

        public float WindSpeed { get; private set; } = 5f;
        public Vector3 WindDirection { get; private set; } = new Vector3(0.6f, 0f, 0.8f);
        public bool StormActive => _stormEnd > _now;

        double _now, _nextStorm, _stormEnd;
        float _stormTau;
        System.Random _rng;

        void Start()
        {
            _rng = new System.Random(System.Environment.TickCount);
            _now = MarsEnvironment.TimeSeconds;
            ScheduleNext();
            if (dust != null)
            {
                var main = dust.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
            }
        }

        void ScheduleNext()
        {
            double sols = -System.Math.Log(1.0 - _rng.NextDouble()) * meanSolsBetweenStorms; // exponential gaps
            _nextStorm = _now + System.Math.Max(0.5, sols) * MarsEnvironment.SolSeconds;
        }

        /// <summary>Starts a storm now (science lab, testing, story events).</summary>
        public void TriggerStorm(float durationSols = 0.6f, float tau = 3.5f)
        {
            _stormTau = tau;
            _stormEnd = _now + durationSols * MarsEnvironment.SolSeconds;
        }

        void Update()
        {
            var env = MarsEnvironment.Instance;
            if (env == null) return;
            _now = MarsEnvironment.TimeSeconds;

            if (stormsEnabled && _now >= _nextStorm && !StormActive)
            {
                TriggerStorm(Mathf.Lerp(stormDurationSols.x, stormDurationSols.y, (float)_rng.NextDouble()),
                             Mathf.Lerp(stormTauRange.x, stormTauRange.y, (float)_rng.NextDouble()));
                ScheduleNext();
            }

            float target = StormActive && stormsEnabled ? _stormTau : clearTau;
            // Storms build and clear over a few hours of sim time.
            float rate = Mathf.Clamp01(env.SimDeltaSeconds / (3f * 3600f));
            env.opticalDepth = Mathf.Lerp(env.opticalDepth, target, rate);
            float storm = Mathf.InverseLerp(clearTau, stormTauRange.y, env.opticalDepth);

            float gust = Mathf.PerlinNoise((float)(_now / 600.0), 0.37f);
            WindSpeed = Mathf.Lerp(3f, 9f, gust) + storm * 18f;
            float angle = Mathf.PerlinNoise(0.81f, (float)(_now / 7200.0)) * 120f + 20f;
            WindDirection = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

            // Haze: denser and darker in storms; at night the sky-lit dust goes dark too.
            float daylight = Mathf.Clamp01(env.SunDirection.y * 4f + 0.25f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = Mathf.Lerp(clearFogDensity, stormFogDensity, storm);
            RenderSettings.fogColor = Color.Lerp(clearFog, stormFog, storm) * Mathf.Lerp(0.06f, 1f, daylight);

            if (dust != null)
            {
                var cam = Camera.main;
                if (cam != null) dust.transform.position = cam.transform.position + WindDirection * -8f;
                var emission = dust.emission;
                emission.rateOverTime = Mathf.Lerp(clearEmission, stormEmission, storm) * (SettingsService.Current.graphics.textures == QualityLevel.Low ? 0.4f : 1f);
                var vel = dust.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.World;
                vel.x = WindDirection.x * WindSpeed;
                vel.y = 0.2f;
                vel.z = WindDirection.z * WindSpeed;
            }
        }
    }
}
