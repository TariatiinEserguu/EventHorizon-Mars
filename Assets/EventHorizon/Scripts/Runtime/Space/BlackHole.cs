using UnityEngine;

namespace EventHorizon.Space
{
    /// <summary>
    /// Drives the BlackHoleLensing material and exposes the physics for the research lab UI.
    /// Exact (Kerr metric, equatorial prograde orbits): event horizon, ISCO (Bardeen, Press &amp; Teukolsky 1972),
    /// photon orbit, plus Schwarzschild time dilation and tidal acceleration.
    /// Rendering approximation: light paths are traced in the non-spinning (Schwarzschild) metric. Spin only moves
    /// the disk's inner edge to the Kerr ISCO and strengthens the Doppler asymmetry.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshRenderer))]
    public class BlackHole : MonoBehaviour
    {
        public const double G = 6.67430e-11;
        public const double C = 299792458.0;
        public const double SolarMass = 1.98847e30;

        [Tooltip("Mass in solar masses. Sagittarius A* is about 4.3 million; M87* about 6.5 billion.")]
        public double massSolar = 4.3e6;
        [Tooltip("Dimensionless spin a = J c / (G M^2), 0 to 0.998 (the Thorne limit for accreting black holes).")]
        [Range(0f, 0.998f)] public float spin;
        [Range(0.2f, 8f)] public float diskIntensity = 3f;

        [Header("Rendering (in Schwarzschild radii)")]
        [Tooltip("Radius of this sphere in r_s. Bigger includes more weak lensing but costs more steps.")]
        public float boundRadius = 40f;
        public float diskOuter = 14f;
        [Range(32, 600)] public int steps = 300;

        static readonly int BoundId = Shader.PropertyToID("_Bound");
        static readonly int InnerId = Shader.PropertyToID("_DiskInner");
        static readonly int OuterId = Shader.PropertyToID("_DiskOuter");
        static readonly int StepsId = Shader.PropertyToID("_Steps");
        static readonly int SpinId = Shader.PropertyToID("_Spin");
        static readonly int BrightId = Shader.PropertyToID("_DiskBrightness");

        MaterialPropertyBlock _mpb;

        /// <summary>r_s = 2GM / c^2, in metres.</summary>
        public double SchwarzschildRadiusM => 2.0 * G * massSolar * SolarMass / (C * C);
        /// <summary>Gravitational radius GM/c^2 = r_s / 2.</summary>
        public double GravitationalRadiusM => SchwarzschildRadiusM * 0.5;

        /// <summary>Kerr outer horizon r+ = M (1 + sqrt(1 - a^2)), in units of r_s.</summary>
        public double HorizonRs => (1.0 + System.Math.Sqrt(1.0 - spin * spin)) * 0.5;

        /// <summary>Prograde ISCO in units of r_s (3 for a = 0, 0.5 for a = 1).</summary>
        public double IscoRs
        {
            get
            {
                double a = spin;
                double z1 = 1.0 + System.Math.Cbrt(1.0 - a * a) * (System.Math.Cbrt(1.0 + a) + System.Math.Cbrt(1.0 - a));
                double z2 = System.Math.Sqrt(3.0 * a * a + z1 * z1);
                return (3.0 + z2 - System.Math.Sqrt((3.0 - z1) * (3.0 + z1 + 2.0 * z2))) * 0.5;
            }
        }

        /// <summary>Prograde circular photon orbit, units of r_s (1.5 for a = 0).</summary>
        public double PhotonOrbitRs => (1.0 + System.Math.Cos(2.0 / 3.0 * System.Math.Acos(-spin)));

        /// <summary>Apparent shadow radius for a = 0, (3 sqrt 3 / 2) r_s.</summary>
        public double ShadowRadiusM => 1.5 * System.Math.Sqrt(3.0) * SchwarzschildRadiusM;

        /// <summary>Static-observer clock rate sqrt(1 - r_s / r) at r metres (Schwarzschild). 0 inside the horizon.</summary>
        public double TimeDilationFactor(double rMetres)
        {
            double x = 1.0 - SchwarzschildRadiusM / rMetres;
            return x > 0 ? System.Math.Sqrt(x) : 0.0;
        }

        /// <summary>Head-to-foot tidal acceleration (m/s^2) on a body of length L at r: 2GM L / r^3.</summary>
        public double TidalAcceleration(double rMetres, double lengthM = 2.0) =>
            2.0 * G * massSolar * SolarMass * lengthM / (rMetres * rMetres * rMetres);

        /// <summary>World units per r_s for this sphere (default Unity sphere: radius 0.5 = boundRadius r_s).</summary>
        public float WorldUnitsPerRs => transform.lossyScale.x * 0.5f / boundRadius;

        void Update()
        {
            var s = transform.localScale;
            float u = (s.x + s.y + s.z) / 3f;
            if (Mathf.Abs(s.x - u) > 1e-4f || Mathf.Abs(s.y - u) > 1e-4f) transform.localScale = Vector3.one * u;

            // The light paths are Schwarzschild, so keep the drawn inner edge outside its photon sphere (1.5 r_s).
            float inner = Mathf.Max((float)IscoRs, 1.6f);
            _mpb ??= new MaterialPropertyBlock();
            var r = GetComponent<MeshRenderer>();
            r.GetPropertyBlock(_mpb);
            _mpb.SetFloat(BoundId, boundRadius);
            _mpb.SetFloat(InnerId, inner);
            _mpb.SetFloat(OuterId, Mathf.Max(diskOuter, inner + 0.5f));
            _mpb.SetFloat(StepsId, steps);
            _mpb.SetFloat(SpinId, spin);
            _mpb.SetFloat(BrightId, diskIntensity);
            r.SetPropertyBlock(_mpb);
        }
    }
}
