using UnityEngine;

namespace EventHorizon.Core
{
    /// <summary>
    /// Mars clock, sun position and surface sunlight. Every simulation system reads
    /// <see cref="SimDeltaSeconds"/> from here, so a single time scale controls the whole colony.
    /// World axes: +X east, +Y up, +Z north.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class MarsEnvironment : MonoBehaviour
    {
        public const float SolSeconds = 88775.244f;          // one Mars solar day
        public const float SolsPerYear = 668.6f;
        public const float Gravity = 3.721f;                  // m/s^2
        public const float Obliquity = 25.19f;                // degrees
        public const float Eccentricity = 0.0934f;
        public const float PerihelionLs = 251f;               // solar longitude of perihelion, degrees
        public const float MeanSolarConstant = 586.2f;        // W/m^2 at 1.524 AU (1361 / 1.524^2)
        public const float SurfacePressurePa = 610f;

        public static MarsEnvironment Instance { get; private set; }

        [Tooltip("Game seconds that pass per real second.")]
        public float timeScale = 60f;
        [Tooltip("Site latitude. Jezero crater is 18.4 N.")]
        [Range(-90f, 90f)] public float latitudeDeg = 18.4f;
        [Tooltip("Season as solar longitude Ls. 0 = northern spring equinox, 251 = perihelion.")]
        [Range(0f, 360f)] public float solarLongitudeDeg = 0f;
        public bool advanceSeasons = true;
        [Tooltip("Atmospheric dust optical depth tau. Clear sky ~0.3-0.6, regional storm 2-5, global storm up to ~10.")]
        [Range(0f, 10f)] public float opticalDepth = 0.5f;
        [Range(0f, 24f)] public float startHour = 9f;

        [Header("Scene")]
        public Light sun;
        [Tooltip("Directional light intensity when the sun is overhead in clear air.")]
        public float sunIntensityAtZenith = 1.3f;
        [Tooltip("Drive the ambient light from the sun position (dark nights, blue twilight).")]
        public bool driveAmbient = true;

        public int Sol { get; private set; }
        public double SimTimeSeconds { get; private set; }
        public float SimDeltaSeconds { get; private set; }
        /// <summary>Local mean solar time in Mars "hours" (1/24 sol).</summary>
        public float LocalSolarHour { get; private set; }
        /// <summary>Unit vector pointing toward the sun.</summary>
        public Vector3 SunDirection { get; private set; }
        public float SunElevationDeg => Mathf.Asin(Mathf.Clamp(SunDirection.y, -1f, 1f)) * Mathf.Rad2Deg;

        double _solClock; // seconds into the current sol

        void Awake()
        {
            Instance = this;
            Physics.gravity = new Vector3(0f, -Gravity, 0f);
            _solClock = startHour / 24.0 * SolSeconds;
            UpdateSun();
            Shader.SetGlobalVector(SunDirId, SunDirection);
        }

        static readonly int SunDirId = Shader.PropertyToID("_EH_SunDirection");
        static readonly int TauId = Shader.PropertyToID("_EH_OpticalDepth");

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            SimDeltaSeconds = Time.deltaTime * timeScale;
            SimTimeSeconds += SimDeltaSeconds;
            _solClock += SimDeltaSeconds;
            while (_solClock >= SolSeconds)
            {
                _solClock -= SolSeconds;
                Sol++;
            }
            if (advanceSeasons)
                solarLongitudeDeg = Mathf.Repeat(solarLongitudeDeg + SimDeltaSeconds / SolSeconds * 360f / SolsPerYear, 360f);
            UpdateSun();
            Shader.SetGlobalVector(SunDirId, SunDirection);
            Shader.SetGlobalFloat(TauId, opticalDepth);
        }

        void UpdateSun()
        {
            LocalSolarHour = (float)(_solClock / SolSeconds * 24.0);
            float phi = latitudeDeg * Mathf.Deg2Rad;
            float dec = Mathf.Asin(Mathf.Sin(Obliquity * Mathf.Deg2Rad) * Mathf.Sin(solarLongitudeDeg * Mathf.Deg2Rad));
            float hourAngle = (LocalSolarHour - 12f) * 15f * Mathf.Deg2Rad;

            // Sun direction in East-North-Up from latitude, declination and hour angle.
            float east = -Mathf.Cos(dec) * Mathf.Sin(hourAngle);
            float north = Mathf.Cos(phi) * Mathf.Sin(dec) - Mathf.Sin(phi) * Mathf.Cos(dec) * Mathf.Cos(hourAngle);
            float up = Mathf.Sin(phi) * Mathf.Sin(dec) + Mathf.Cos(phi) * Mathf.Cos(dec) * Mathf.Cos(hourAngle);
            SunDirection = new Vector3(east, up, north).normalized;

            if (sun != null)
            {
                sun.transform.rotation = Quaternion.LookRotation(-SunDirection, Vector3.up);
                SurfaceIrradiance(out float direct, out float diffuse);
                float global = direct * Mathf.Max(SunDirection.y, 0f) + diffuse;
                sun.intensity = sunIntensityAtZenith * global / MeanSolarConstant;
                sun.enabled = SunDirection.y > -0.05f;
            }

            if (driveAmbient)
            {
                // Sky-lit ambient follows the sun: warm dusty daylight, cool blue twilight, near-black night.
                float day = Mathf.Clamp01(SunDirection.y * 4f + 0.2f);
                float twilight = Mathf.Clamp01(1f - Mathf.Abs(SunDirection.y) * 6f);
                float dust = Mathf.Exp(-0.15f * opticalDepth);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.02f, 0.025f, 0.04f), new Color(0.62f, 0.47f, 0.36f), day) * dust
                                                 + new Color(0.05f, 0.08f, 0.14f) * twilight;
                RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.015f, 0.015f, 0.02f), new Color(0.55f, 0.38f, 0.27f), day) * dust;
                RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.01f, 0.008f, 0.006f), new Color(0.3f, 0.19f, 0.12f), day);
            }
        }

        /// <summary>Solar irradiance above the atmosphere for the current season (W/m^2).</summary>
        public float TopOfAtmosphereIrradiance
        {
            get
            {
                float nu = (solarLongitudeDeg - PerihelionLs) * Mathf.Deg2Rad;
                float f = (1f + Eccentricity * Mathf.Cos(nu)) / (1f - Eccentricity * Eccentricity);
                return MeanSolarConstant * f * f; // ~492 at aphelion, ~717 at perihelion
            }
        }

        /// <summary>
        /// Splits surface sunlight into direct-normal and diffuse-horizontal parts.
        /// The beam follows Beer-Lambert extinction exp(-tau/mu). Dust scatters mostly forward, and the diffuse
        /// term is fitted to the Appelbaum &amp; Flood (1990) Mars tables: within ~5% of their global flux for tau 0.5-5.
        /// </summary>
        public void SurfaceIrradiance(out float directNormal, out float diffuseHorizontal)
        {
            float mu = SunDirection.y;
            if (mu <= 0.01f)
            {
                directNormal = 0f;
                diffuseHorizontal = 0f;
                return;
            }
            float s = TopOfAtmosphereIrradiance;
            float slant = opticalDepth / Mathf.Max(mu, 0.05f);
            directNormal = s * Mathf.Exp(-slant);
            diffuseHorizontal = s * mu * (1f - Mathf.Exp(-slant)) * 0.6f * Mathf.Exp(-0.25f * opticalDepth);
        }

        /// <summary>Seconds elapsed in the current sol.</summary>
        public double SolClockSeconds => _solClock;

        /// <summary>Restores the clock from a save.</summary>
        public void SetClock(int sol, double solClockSeconds, float ls, float tau)
        {
            Sol = sol;
            _solClock = Mathf.Clamp((float)solClockSeconds, 0f, SolSeconds - 1f);
            solarLongitudeDeg = ls;
            opticalDepth = tau;
            UpdateSun();
        }

        /// <summary>
        /// Near-surface air temperature (deg C). It follows the Perseverance MEDA range at Jezero: about -85 C before
        /// dawn to about -10 C mid-afternoon. Dust narrows the daily swing, and perihelion is warmer.
        /// </summary>
        public float SurfaceTemperatureC
        {
            get
            {
                float mean = -48f + 8f * Mathf.Cos((solarLongitudeDeg - PerihelionLs) * Mathf.Deg2Rad);
                float swing = 38f * Mathf.Exp(-0.25f * opticalDepth);
                return mean + swing * Mathf.Cos((LocalSolarHour - 14.5f) / 24f * 2f * Mathf.PI);
            }
        }

        /// <summary>Formats the local mean solar time as "HH:MM".</summary>
        public string ClockText
        {
            get
            {
                int minutes = Mathf.FloorToInt(LocalSolarHour * 60f);
                return $"{minutes / 60:00}:{minutes % 60:00}";
            }
        }

        /// <summary>Sim delta for systems that may run without a MarsEnvironment in the scene (e.g. test scenes).</summary>
        public static float DeltaSeconds => Instance != null ? Instance.SimDeltaSeconds : Time.deltaTime;
        public static double TimeSeconds => Instance != null ? Instance.SimTimeSeconds : Time.timeAsDouble;
    }
}
