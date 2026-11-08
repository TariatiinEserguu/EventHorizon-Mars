using UnityEngine;

namespace EventHorizon.Space
{
    /// <summary>
    /// Drives the BlackHoleLensing material and exposes the physics for the research lab UI:
    /// Schwarzschild radius, photon sphere, ISCO, shadow size and gravitational time dilation.
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

        [Header("Rendering (in Schwarzschild radii)")]
        [Tooltip("Radius of this sphere in r_s. Bigger includes more weak lensing but costs more steps.")]
        public float boundRadius = 40f;
        public float diskInner = 3f;
        public float diskOuter = 14f;
        [Range(32, 600)] public int steps = 300;

        static readonly int BoundId = Shader.PropertyToID("_Bound");
        static readonly int InnerId = Shader.PropertyToID("_DiskInner");
        static readonly int OuterId = Shader.PropertyToID("_DiskOuter");
        static readonly int StepsId = Shader.PropertyToID("_Steps");

        MaterialPropertyBlock _mpb;

        /// <summary>r_s = 2GM / c^2, in metres.</summary>
        public double SchwarzschildRadiusM => 2.0 * G * massSolar * SolarMass / (C * C);
        public double PhotonSphereM => 1.5 * SchwarzschildRadiusM;
        /// <summary>Innermost stable circular orbit, 3 r_s.</summary>
        public double IscoM => 3.0 * SchwarzschildRadiusM;
        /// <summary>Apparent radius of the shadow, (3 sqrt 3 / 2) r_s.</summary>
        public double ShadowRadiusM => 1.5 * System.Math.Sqrt(3.0) * SchwarzschildRadiusM;

        /// <summary>
        /// How fast a static clock at distance r (metres from the centre) ticks relative to one far away:
        /// sqrt(1 - r_s / r). Returns 0 at or inside the horizon.
        /// </summary>
        public double TimeDilationFactor(double rMetres)
        {
            double x = 1.0 - SchwarzschildRadiusM / rMetres;
            return x > 0 ? System.Math.Sqrt(x) : 0.0;
        }

        /// <summary>Uniform world scale that makes 1 Unity unit equal <paramref name="metresPerUnit"/>.</summary>
        public float WorldScaleFor(double metresPerUnit) => (float)(2.0 * boundRadius * SchwarzschildRadiusM / metresPerUnit);

        void Update()
        {
            // The shader assumes uniform scale.
            var s = transform.localScale;
            float u = (s.x + s.y + s.z) / 3f;
            if (Mathf.Abs(s.x - u) > 1e-4f || Mathf.Abs(s.y - u) > 1e-4f) transform.localScale = Vector3.one * u;

            _mpb ??= new MaterialPropertyBlock();
            var r = GetComponent<MeshRenderer>();
            r.GetPropertyBlock(_mpb);
            _mpb.SetFloat(BoundId, boundRadius);
            _mpb.SetFloat(InnerId, Mathf.Max(diskInner, 1.01f));
            _mpb.SetFloat(OuterId, Mathf.Max(diskOuter, diskInner + 0.1f));
            _mpb.SetFloat(StepsId, steps);
            r.SetPropertyBlock(_mpb);
        }
    }
}
