using EventHorizon.Procedural;
using UnityEngine;

namespace EventHorizon.Space
{
    /// <summary>
    /// Builds a lumpy, cratered ellipsoid for Phobos or Deimos at their real triaxial dimensions.
    /// UVs are equirectangular, so the NASA/JPL texture maps straight on.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class IrregularMoonMesh : MonoBehaviour
    {
        public enum Preset { Phobos, Deimos, Custom }

        public Preset preset = Preset.Phobos;
        [Tooltip("Semi-axes in km (x = toward Mars, z = along the orbit).")]
        public Vector3 semiAxesKm = PlanetCatalog.PhobosAxesKm;
        public float kmPerUnit = 1f;
        public int seed = 7;
        [Range(0f, 0.2f)] public float lumpiness = 0.07f;
        public int craterCount = 40;
        [Tooltip("Phobos' Stickney crater is 9 km across, almost half the moon's width.")]
        public bool largeCrater = true;
        [Range(32, 256)] public int longitudeSegments = 128;

        [SerializeField, HideInInspector] Preset _appliedPreset = Preset.Phobos;
        bool _dirty = true;

        void OnEnable() => _dirty = true;

        // Unity forbids changing the MeshFilter inside OnValidate, so rebuild on the next Update.
        void OnValidate()
        {
            // Apply preset values only when the preset changes, so hand-tuned values survive.
            if (preset != _appliedPreset)
            {
                if (preset == Preset.Phobos) { semiAxesKm = PlanetCatalog.PhobosAxesKm; largeCrater = true; lumpiness = 0.07f; }
                else if (preset == Preset.Deimos) { semiAxesKm = PlanetCatalog.DeimosAxesKm; largeCrater = false; lumpiness = 0.04f; }
                _appliedPreset = preset;
            }
            _dirty = true;
        }

        void Update()
        {
            if (!_dirty) return;
            _dirty = false;
            Generate();
        }

        [ContextMenu("Regenerate")]
        public void Generate()
        {
            var rng = new System.Random(seed);
            var craters = new (Vector3 dir, float radius, float depth)[craterCount + (largeCrater ? 1 : 0)];
            for (int i = 0; i < craterCount; i++)
            {
                var d = new Vector3((float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1).normalized;
                // Crater sizes follow a power law: many small ones, few large.
                float r = Mathf.Lerp(0.04f, 0.25f, Mathf.Pow((float)rng.NextDouble(), 3f));
                craters[i] = (d, r, r * 0.25f);
            }
            if (largeCrater) craters[craterCount] = (new Vector3(-0.6f, -0.1f, -0.8f).normalized, 0.42f, 0.12f);

            float Radius(Vector3 dir)
            {
                float h = 1f + Noise.Fbm(dir * 2.5f, seed, 5) * lumpiness * 2f;
                foreach (var c in craters)
                {
                    float ang = Mathf.Acos(Mathf.Clamp(Vector3.Dot(dir, c.dir), -1f, 1f));
                    float x = ang / c.radius;
                    if (x < 1f) h -= c.depth * (1f - x * x);                     // bowl
                    else if (x < 1.6f) h += c.depth * 0.25f * (1f - (x - 1f) / 0.6f); // raised rim
                }
                return h;
            }

            var mesh = MeshBuilders.UvSphere(longitudeSegments, longitudeSegments / 2, Radius);
            var verts = mesh.vertices;
            var axes = semiAxesKm / Mathf.Max(kmPerUnit, 1e-6f);
            for (int i = 0; i < verts.Length; i++) verts[i] = Vector3.Scale(verts[i], axes);
            mesh.vertices = verts;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            mesh.name = $"{preset} (procedural)";
            mesh.hideFlags = HideFlags.DontSave;

            var mf = GetComponent<MeshFilter>();
            if (mf.sharedMesh != null && mf.sharedMesh.hideFlags == HideFlags.DontSave)
            {
                if (Application.isPlaying) Destroy(mf.sharedMesh);
                else DestroyImmediate(mf.sharedMesh);
            }
            mf.sharedMesh = mesh;
        }
    }
}
