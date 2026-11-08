using System.Collections.Generic;
using EventHorizon.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace EventHorizon.Space
{
    /// <summary>
    /// Procedural star field and distant galaxies, each drawn as a single mesh: one draw call for tens of thousands
    /// of stars, with no GameObject per star.
    /// Star counts follow the real magnitude distribution (about 3.5x more stars per magnitude step) and crowd
    /// toward the galactic band. Colours come from a realistic spectral mix: most stars are cool K/M dwarfs, a few
    /// are hot blue O/B stars. The mesh follows the camera so stars never get closer.
    /// </summary>
    [ExecuteAlways]
    public class Starfield : MonoBehaviour
    {
        public int seed = 1977;
        [Range(1000, 60000)] public int starCount = 24000;
        [Range(0, 200)] public int galaxyCount = 60;
        [Tooltip("Faintest magnitude drawn; 6.5 is the naked-eye limit, higher adds more faint stars.")]
        public float limitingMagnitude = 8.5f;
        [Tooltip("How strongly stars crowd toward the galactic band (0 = uniform).")]
        [Range(0f, 1f)] public float bandConcentration = 0.55f;
        public Vector3 bandNormal = new Vector3(0.25f, 0.9f, 0.35f);
        public Material starMaterial;
        public Material galaxyMaterial;
        [Tooltip("On Mars, fade the stars out in daylight.")]
        public bool fadeWithMarsDaylight;
        public float intensity = 1f;

        Mesh _stars, _galaxies;
        MaterialPropertyBlock _mpb;
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        void OnEnable() => Build();

        void OnDisable()
        {
            if (_stars != null) DestroyImmediate(_stars);
            if (_galaxies != null) DestroyImmediate(_galaxies);
        }

        [ContextMenu("Rebuild")]
        public void Build()
        {
            var rng = new System.Random(seed);
            var band = bandNormal.normalized;
            float R() => (float)rng.NextDouble();

            // ---- stars
            var verts = new List<Vector3>(starCount * 4);
            var colors = new List<Color>(starCount * 4);
            var corners = new List<Vector2>(starCount * 4);
            var sizes = new List<Vector2>(starCount * 4);
            var tris = new List<int>(starCount * 6);
            Vector2[] quad = { new(-1, -1), new(1, -1), new(1, 1), new(-1, 1) };
            for (int i = 0; i < starCount; i++)
            {
                Vector3 d = RandomDirection(rng);
                // Rejection sampling toward the band.
                if (R() < bandConcentration && Mathf.Abs(Vector3.Dot(d, band)) > R() * 0.6f) d = Vector3.ProjectOnPlane(d, band).normalized * 0.85f + d * 0.15f;
                d.Normalize();
                // Magnitude: cumulative counts grow ~10^(0.55 m) -> invert a uniform sample.
                float m = limitingMagnitude + Mathf.Log10(Mathf.Max(R(), 1e-6f)) / 0.55f;
                m = Mathf.Max(m, -1.5f);
                float brightness = Mathf.Clamp01(Mathf.Pow(10f, -0.4f * (m - 1.5f)) * 0.6f + 0.06f);
                float size = Mathf.Lerp(1.1f, 4.2f, Mathf.Pow(brightness, 0.6f));
                Color c = StarColor(R());
                c.a = brightness;
                int b = verts.Count;
                for (int k = 0; k < 4; k++)
                {
                    verts.Add(d);
                    colors.Add(c);
                    corners.Add(quad[k]);
                    sizes.Add(new Vector2(size, 0f));
                }
                tris.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            }
            _stars = Replace(_stars, "Stars", verts, colors, corners, sizes, tris);

            // ---- galaxies: tiny world-space ellipses away from the band (the Milky Way hides those behind it)
            verts.Clear(); colors.Clear(); corners.Clear(); sizes.Clear(); tris.Clear();
            for (int i = 0; i < galaxyCount; i++)
            {
                Vector3 d = RandomDirection(rng);
                if (Mathf.Abs(Vector3.Dot(d, band)) < 0.25f) continue;
                float angSize = Mathf.Lerp(0.0035f, 0.018f, Mathf.Pow(R(), 3f)); // ~0.2 deg to 1 deg
                var t = Vector3.Cross(d, Vector3.up).sqrMagnitude > 1e-4f ? Vector3.Cross(d, Vector3.up).normalized : Vector3.right;
                var bt = Vector3.Cross(d, t);
                float rot = R() * Mathf.PI;
                var ax = (t * Mathf.Cos(rot) + bt * Mathf.Sin(rot)) * angSize;
                var ay = Vector3.Cross(d, ax);
                Color c = Color.Lerp(new Color(1f, 0.88f, 0.72f), new Color(0.78f, 0.86f, 1f), R());
                c.a = Mathf.Lerp(0.2f, 1f, R());
                var shape = new Vector2(Mathf.Lerp(0.25f, 1f, R()), R() * 6.28f);
                int b = verts.Count;
                for (int k = 0; k < 4; k++)
                {
                    verts.Add(d + ax * quad[k].x + ay * quad[k].y);
                    colors.Add(c);
                    corners.Add(quad[k]);
                    sizes.Add(shape);
                }
                tris.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            }
            _galaxies = Replace(_galaxies, "Galaxies", verts, colors, corners, sizes, tris);
        }

        static Mesh Replace(Mesh old, string name, List<Vector3> v, List<Color> c, List<Vector2> uv0, List<Vector2> uv1, List<int> t)
        {
            if (old != null) DestroyImmediate(old);
            var m = new Mesh { name = name, hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
            m.SetVertices(v);
            m.SetColors(c);
            m.SetUVs(0, uv0);
            m.SetUVs(1, uv1);
            m.SetTriangles(t, 0);
            m.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            return m;
        }

        static Vector3 RandomDirection(System.Random rng)
        {
            float z = (float)rng.NextDouble() * 2f - 1f;
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            float r = Mathf.Sqrt(1f - z * z);
            return new Vector3(r * Mathf.Cos(a), z, r * Mathf.Sin(a));
        }

        /// <summary>Colour by spectral class, weighted toward what we actually see in the sky.</summary>
        static Color StarColor(float u)
        {
            if (u < 0.03f) return new Color(0.62f, 0.72f, 1f);     // O/B: blue-white
            if (u < 0.15f) return new Color(0.82f, 0.88f, 1f);     // A: white-blue
            if (u < 0.40f) return new Color(1f, 0.98f, 0.94f);     // F: white
            if (u < 0.68f) return new Color(1f, 0.93f, 0.80f);     // G: yellow-white (Sun-like)
            if (u < 0.90f) return new Color(1f, 0.82f, 0.62f);     // K: orange
            return new Color(1f, 0.70f, 0.52f);                    // M: red-orange
        }

        void Update()
        {
            if (_stars == null) Build();
            float k = intensity;
            if (fadeWithMarsDaylight && MarsEnvironment.Instance != null)
            {
                var env = MarsEnvironment.Instance;
                float night = Mathf.Clamp01(-env.SunDirection.y * 6f + 0.15f);
                k *= night * Mathf.Exp(-0.6f * env.opticalDepth);
                if (k <= 0.001f) return;
            }
            var cam = Camera.main;
#if UNITY_EDITOR
            if (!Application.isPlaying && UnityEditor.SceneView.lastActiveSceneView != null) cam = UnityEditor.SceneView.lastActiveSceneView.camera;
#endif
            if (cam == null) return;
            float radius = cam.farClipPlane * 0.85f;
            var matrix = Matrix4x4.TRS(cam.transform.position, Quaternion.identity, Vector3.one * radius);
            _mpb ??= new MaterialPropertyBlock();
            if (starMaterial != null)
            {
                _mpb.SetFloat(IntensityId, k);
                Graphics.RenderMesh(new RenderParams(starMaterial) { matProps = _mpb, worldBounds = new Bounds(cam.transform.position, Vector3.one * radius * 2.2f), layer = gameObject.layer }, _stars, 0, matrix);
            }
            if (galaxyMaterial != null && _galaxies != null && _galaxies.vertexCount > 0)
            {
                var gp = new MaterialPropertyBlock();
                gp.SetFloat(IntensityId, 0.6f * k);
                Graphics.RenderMesh(new RenderParams(galaxyMaterial) { matProps = gp, worldBounds = new Bounds(cam.transform.position, Vector3.one * radius * 2.2f), layer = gameObject.layer }, _galaxies, 0, matrix);
            }
        }
    }
}
