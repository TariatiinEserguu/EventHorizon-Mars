using System;
using UnityEngine;

namespace EventHorizon.Space
{
    /// <summary>
    /// The physics of a black hole of mass M and spin a = cJ / (G M^2), and the bridge to the BlackHoleLensing shader.
    ///
    /// Exact (Kerr metric, Boyer-Lindquist coordinates, equatorial plane where it matters): outer and inner horizons,
    /// the ergosphere r_E(theta) = M + sqrt(M^2 - a^2 cos^2 theta), prograde and retrograde photon orbits and ISCOs
    /// (Bardeen, Press &amp; Teukolsky 1972), orbital angular velocity Omega = 1 / (M (r^3/2 + a)), the frame-dragging rate
    /// omega of zero-angular-momentum observers, clock rates of static and orbiting observers, and Newtonian tidal
    /// stretching (accurate far from the horizon). The shader's light paths are Schwarzschild plus an approximate
    /// frame-dragging term; see BlackHoleLensing.shader.
    ///
    /// Units in this class: geometric units with M = 1 unless the name says otherwise ("Rs" = Schwarzschild radii,
    /// "M" suffix = metres, "Hz" = per second).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshRenderer))]
    public class BlackHole : MonoBehaviour
    {
        public const double G = 6.67430e-11;
        public const double C = 299792458.0;
        public const double SolarMass = 1.98847e30;
        public const double EarthMass = 5.9722e24;

        [Tooltip("Mass in solar masses. Sagittarius A* is about 4.3 million; M87* about 6.5 billion.")]
        public double massSolar = 4.3e6;
        [Tooltip("Dimensionless spin a = J c / (G M^2), 0 to 0.998 (the Thorne limit for accreting black holes).")]
        [Range(0f, 0.998f)] public float spin;
        [Range(0.2f, 8f)] public float diskIntensity = 3f;
        [Tooltip("Visual peak temperature of the disk (K). See the shader notes on visual versus physical temperature.")]
        public float diskTemperature = 4300f;
        [Range(0f, 1f)] public float diskDensity = 0.8f;
        [Tooltip("Hot gas clump orbiting at the ISCO (its period is the signal in chapter 9). 0 = off.")]
        public float hotSpot;
        [Tooltip("Frame dragging of light in the renderer (0 = pure Schwarzschild light paths).")]
        [Range(0f, 1f)] public float frameDragging = 1f;

        [Header("Background star")]
        public Vector3 sourceDirection = new Vector3(0f, 0f, 1f);
        public float sourceBrightness;
        public float sourceSizeDeg = 0.6f;

        [Header("Simulation")]
        public float simulationSpeed = 1f;
        public bool paused;

        [Header("Rendering (in Schwarzschild radii)")]
        [Tooltip("Radius of this sphere in r_s. Bigger includes more weak lensing but costs more steps.")]
        public float boundRadius = 40f;
        public float diskOuter = 14f;
        [Range(32, 600)] public int steps = 300;
        [Range(0.5f, 2.5f)] public float stepScale = 1f;
        [Range(1, 3)] public int detail = 3;

        static readonly int BoundId = Shader.PropertyToID("_Bound"), HorizonId = Shader.PropertyToID("_HorizonR"),
            InnerId = Shader.PropertyToID("_DiskInner"), OuterId = Shader.PropertyToID("_DiskOuter"), StepsId = Shader.PropertyToID("_Steps"),
            SpinId = Shader.PropertyToID("_Spin"), BrightId = Shader.PropertyToID("_DiskBrightness"), TempId = Shader.PropertyToID("_DiskTemperature"),
            DensityId = Shader.PropertyToID("_DiskDensity"), TimeId = Shader.PropertyToID("_SimTime"), StepScaleId = Shader.PropertyToID("_StepScale"),
            DetailId = Shader.PropertyToID("_Detail"), DragId = Shader.PropertyToID("_FrameDrag"), SourceDirId = Shader.PropertyToID("_SourceDir"),
            SourceSizeId = Shader.PropertyToID("_SourceSize"), SourceBrightId = Shader.PropertyToID("_SourceBrightness"),
            HotRId = Shader.PropertyToID("_HotSpotR"), HotId = Shader.PropertyToID("_HotSpotStrength"), HotOmegaId = Shader.PropertyToID("_HotSpotOmega");

        MaterialPropertyBlock _mpb;
        float _simTime;

        public float SimTime => _simTime;

        // ------------------------------------------------------------------ Kerr formulas (static, M = 1)

        /// <summary>Outer horizon r+ = 1 + sqrt(1 - a^2), inner r- = 1 - sqrt(1 - a^2).</summary>
        public static double HorizonOuter(double a) => 1.0 + Math.Sqrt(Math.Max(0.0, 1.0 - a * a));
        public static double HorizonInner(double a) => 1.0 - Math.Sqrt(Math.Max(0.0, 1.0 - a * a));

        /// <summary>Ergosphere boundary at polar angle theta (0 = along the spin axis): 1 + sqrt(1 - a^2 cos^2 theta).</summary>
        public static double Ergosphere(double a, double theta) => 1.0 + Math.Sqrt(Math.Max(0.0, 1.0 - a * a * Math.Cos(theta) * Math.Cos(theta)));

        /// <summary>Circular photon orbit: 2 {1 + cos[(2/3) acos(-a)]} prograde, with +a for retrograde.</summary>
        public static double PhotonOrbit(double a, bool prograde) => 2.0 * (1.0 + Math.Cos(2.0 / 3.0 * Math.Acos(prograde ? -a : a)));

        /// <summary>Innermost stable circular orbit (Bardeen, Press &amp; Teukolsky 1972). 6 for a = 0; 1 (prograde) to 9 (retrograde) for a = 1.</summary>
        public static double Isco(double a, bool prograde = true)
        {
            double z1 = 1.0 + Math.Cbrt(1.0 - a * a) * (Math.Cbrt(1.0 + a) + Math.Cbrt(1.0 - a));
            double z2 = Math.Sqrt(3.0 * a * a + z1 * z1);
            double root = Math.Sqrt(Math.Max(0.0, (3.0 - z1) * (3.0 + z1 + 2.0 * z2)));
            return prograde ? 3.0 + z2 - root : 3.0 + z2 + root;
        }

        /// <summary>Orbital angular velocity seen from far away, prograde circular orbit: 1 / (r^3/2 + a).</summary>
        public static double OrbitalOmega(double r, double a) => 1.0 / (Math.Pow(r, 1.5) + a);

        /// <summary>u^t (dt/dtau) of a prograde circular equatorial orbit; NaN where no circular orbit exists.</summary>
        public static double OrbitUt(double r, double a)
        {
            double d = Math.Pow(r, 1.5) - 3.0 * Math.Sqrt(r) + 2.0 * a;
            return d > 0 ? (Math.Pow(r, 1.5) + a) / (Math.Pow(r, 0.75) * Math.Sqrt(d)) : double.NaN;
        }

        /// <summary>Frame-dragging angular velocity of zero-angular-momentum observers in the equatorial plane.</summary>
        public static double FrameDragOmega(double r, double a)
        {
            double delta = r * r - 2.0 * r + a * a;
            double A = (r * r + a * a) * (r * r + a * a) - a * a * delta;
            return 2.0 * a * r / A;
        }

        /// <summary>GM/c^3 in seconds for a mass in solar masses: the time unit of M = 1.</summary>
        public static double TimeUnitS(double massSolar) => G * massSolar * SolarMass / (C * C * C);

        /// <summary>Frequency (Hz) of a prograde orbit at the ISCO: what a hot spot there would modulate light at.</summary>
        public static double IscoFrequencyHz(double massSolar, double a) =>
            OrbitalOmega(Isco(a), a) / (2.0 * Math.PI * TimeUnitS(massSolar));

        // ------------------------------------------------------------------ this black hole

        /// <summary>r_s = 2GM / c^2, in metres.</summary>
        public double SchwarzschildRadiusM => 2.0 * G * massSolar * SolarMass / (C * C);
        public double GravitationalRadiusM => SchwarzschildRadiusM * 0.5;

        /// <summary>Kerr outer horizon in units of r_s.</summary>
        public double HorizonRs => HorizonOuter(spin) * 0.5;
        public double InnerHorizonRs => HorizonInner(spin) * 0.5;
        public double IscoRs => Isco(spin) * 0.5;
        public double IscoRetroRs => Isco(spin, false) * 0.5;
        public double PhotonOrbitRs => PhotonOrbit(spin, true) * 0.5;
        public double PhotonOrbitRetroRs => PhotonOrbit(spin, false) * 0.5;
        /// <summary>Ergosphere radius at the equator (always 2M = r_s) and at the poles (= r+).</summary>
        public double ErgoEquatorRs => Ergosphere(spin, Math.PI / 2) * 0.5;
        /// <summary>Apparent shadow radius for a = 0, (3 sqrt 3 / 2) r_s.</summary>
        public double ShadowRadiusM => 1.5 * Math.Sqrt(3.0) * SchwarzschildRadiusM;
        /// <summary>Angular momentum J = a G M^2 / c (kg m^2 / s).</summary>
        public double AngularMomentum => spin * G * Math.Pow(massSolar * SolarMass, 2) / C;
        public double IscoFrequency => IscoFrequencyHz(massSolar, spin);
        public double IscoPeriodS => 1.0 / IscoFrequency;

        /// <summary>Static (hovering) observer's clock rate at r metres in the equatorial plane: sqrt(1 - 2M/r). 0 inside the ergosphere, where no observer can stay still.</summary>
        public double TimeDilationFactor(double rMetres)
        {
            double x = 1.0 - SchwarzschildRadiusM / rMetres;
            return x > 0 ? Math.Sqrt(x) : 0.0;
        }

        /// <summary>Clock rate of an observer on a prograde circular orbit at r metres (1 / u^t); NaN inside the photon orbit.</summary>
        public double OrbitingClockRate(double rMetres) => 1.0 / OrbitUt(rMetres / GravitationalRadiusM, spin);

        /// <summary>Head-to-foot tidal acceleration (m/s^2) on a body of length L at r: 2GM L / r^3. Sideways squeeze is half of it.</summary>
        public double TidalAcceleration(double rMetres, double lengthM = 2.0) =>
            2.0 * G * massSolar * SolarMass * lengthM / (rMetres * rMetres * rMetres);

        /// <summary>Radius where the tidal stretch on a body of length L reaches <paramref name="limit"/> m/s^2.</summary>
        public double TidalRadiusM(double lengthM, double limit) => Math.Cbrt(2.0 * G * massSolar * SolarMass * lengthM / limit);

        /// <summary>World units per r_s for this sphere (default Unity sphere: radius 0.5 = boundRadius r_s).</summary>
        public float WorldUnitsPerRs => transform.lossyScale.x * 0.5f / boundRadius;

        /// <summary>Local position (r_s units, disk plane = XZ) to world.</summary>
        public Vector3 RsToWorld(Vector3 rs) => transform.position + transform.rotation * (rs * WorldUnitsPerRs);

        void Update()
        {
            var s = transform.localScale;
            float u = (s.x + s.y + s.z) / 3f;
            if (Mathf.Abs(s.x - u) > 1e-4f || Mathf.Abs(s.y - u) > 1e-4f) transform.localScale = Vector3.one * u;
            if (Application.isPlaying && !paused) _simTime += Time.deltaTime * simulationSpeed;

            // Capture at the Kerr horizon; keep the drawn inner edge just outside it.
            float horizon = (float)HorizonRs;
            float inner = Mathf.Max((float)IscoRs, horizon + 0.12f);
            _mpb ??= new MaterialPropertyBlock();
            var r = GetComponent<MeshRenderer>();
            r.GetPropertyBlock(_mpb);
            _mpb.SetFloat(BoundId, boundRadius);
            _mpb.SetFloat(HorizonId, horizon);
            _mpb.SetFloat(InnerId, inner);
            _mpb.SetFloat(OuterId, Mathf.Max(diskOuter, inner + 0.5f));
            _mpb.SetFloat(StepsId, steps);
            _mpb.SetFloat(StepScaleId, stepScale);
            _mpb.SetFloat(DetailId, detail);
            _mpb.SetFloat(SpinId, spin);
            _mpb.SetFloat(DragId, frameDragging);
            _mpb.SetFloat(BrightId, diskIntensity);
            _mpb.SetFloat(TempId, diskTemperature);
            _mpb.SetFloat(DensityId, diskDensity);
            _mpb.SetFloat(TimeId, _simTime);
            _mpb.SetVector(SourceDirId, sourceDirection.sqrMagnitude > 0f ? sourceDirection.normalized : Vector3.forward);
            _mpb.SetFloat(SourceSizeId, sourceSizeDeg * Mathf.Deg2Rad);
            _mpb.SetFloat(SourceBrightId, sourceBrightness);
            // Hot spot at the ISCO with the true orbital rate, in the shader's r_s, c = 1 units (M = 0.5).
            float rHot = inner * 1.05f;
            _mpb.SetFloat(HotRId, rHot);
            _mpb.SetFloat(HotId, hotSpot);
            _mpb.SetFloat(HotOmegaId, (float)(OrbitalOmega(rHot * 2.0, spin) * 2.0) * 3f);
            r.SetPropertyBlock(_mpb);
        }
    }
}
