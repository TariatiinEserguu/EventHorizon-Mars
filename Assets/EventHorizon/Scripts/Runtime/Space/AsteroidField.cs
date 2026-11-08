using System.Collections.Generic;
using EventHorizon.Core;
using EventHorizon.Procedural;
using UnityEngine;
using UnityEngine.Rendering;

namespace EventHorizon.Space
{
    /// <summary>
    /// An asteroid belt or cluster drawn with GPU instancing (Graphics.RenderMeshInstanced), not one GameObject per
    /// rock. Every asteroid has its own shape (one of several procedural meshes), size, spin axis, spin rate,
    /// material class and orbit. Orbits follow Kepler (inner rocks move faster: omega ~ r^-1.5).
    /// Two LODs: detailed meshes near the camera, simpler meshes far away. Distant chunks are culled.
    /// </summary>
    [ExecuteAlways]
    public class AsteroidField : MonoBehaviour
    {
        public enum Shape { Belt, Cluster }

        public Shape shape = Shape.Belt;
        public int count = 3000;
        public int seed = 433;
        [Tooltip("Belt: inner/outer radius. Cluster: x = radius.")]
        public Vector2 radiusRange = new Vector2(300f, 380f);
        public float thickness = 14f;
        public Vector2 sizeRange = new Vector2(0.2f, 5f);
        [Range(1f, 6f)] public float sizeExponent = 3.5f;
        [Tooltip("Orbital angular speed at the inner edge, degrees per real second.")]
        public float orbitSpeed = 0.6f;
        public float lodDistance = 160f;
        public float drawDistance = 1400f;
        [Tooltip("Rock classes: carbonaceous (dark), silicate (grey-brown), metallic.")]
        public Material[] materials = new Material[0];

        const int Variants = 6;
        const int MaxBatch = 1023;

        struct Rock
        {
            public float r, angle, height, scale, spinRate;
            public Vector3 spinAxis;
            public int mesh, mat;
        }

        Rock[] _rocks;
        Mesh[] _near, _far;
        readonly List<Matrix4x4>[,] _buckets = new List<Matrix4x4>[Variants * 2, 3];
        readonly Matrix4x4[] _batch = new Matrix4x4[MaxBatch];
        float _time;

        void OnEnable() => Build();

        [ContextMenu("Rebuild")]
        public void Build()
        {
            _near = new Mesh[Variants];
            _far = new Mesh[Variants];
            for (int i = 0; i < Variants; i++)
            {
                // Lumpier and more elongated than ground boulders: asteroids are rubble piles and collision fragments.
                _near[i] = RockMeshFactory.Asteroid(seed * 7 + i, 4, 0.25f + 0.04f * (i % 3));
                _far[i] = RockMeshFactory.Asteroid(seed * 7 + i, 2, 0.25f + 0.04f * (i % 3));
                _near[i].hideFlags = _far[i].hideFlags = HideFlags.DontSave;
            }
            var rng = new System.Random(seed);
            float R() => (float)rng.NextDouble();
            _rocks = new Rock[count];
            for (int i = 0; i < count; i++)
            {
                float r = shape == Shape.Belt ? Mathf.Lerp(radiusRange.x, radiusRange.y, Mathf.Sqrt(R())) : radiusRange.x * Mathf.Pow(R(), 0.5f);
                float h = (R() + R() + R() - 1.5f) / 1.5f * thickness; // roughly Gaussian vertical spread
                if (shape == Shape.Cluster) h *= 2f;
                _rocks[i] = new Rock
                {
                    r = r,
                    angle = R() * 360f,
                    height = h,
                    scale = Mathf.Lerp(sizeRange.x, sizeRange.y, Mathf.Pow(R(), sizeExponent)),
                    spinRate = Mathf.Lerp(-40f, 40f, R()),
                    spinAxis = new Vector3(R() - 0.5f, R() - 0.5f, R() - 0.5f).normalized,
                    mesh = rng.Next(Variants),
                    mat = R() < 0.75f ? 0 : R() < 0.85f ? 1 : 2, // C-types dominate, then S-types, a few metallic M-types
                };
            }
            for (int m = 0; m < Variants * 2; m++)
                for (int k = 0; k < 3; k++) _buckets[m, k] = new List<Matrix4x4>(256);
        }

        void Update()
        {
            if (_rocks == null || materials == null || materials.Length == 0) return;
            _time += Application.isPlaying ? Time.deltaTime : 0f;
            var cam = Camera.main;
#if UNITY_EDITOR
            if (!Application.isPlaying && UnityEditor.SceneView.lastActiveSceneView != null) cam = UnityEditor.SceneView.lastActiveSceneView.camera;
#endif
            Vector3 eye = cam != null ? cam.transform.position : transform.position;
            float lod2 = lodDistance * lodDistance, draw2 = drawDistance * drawDistance;
            foreach (var b in _buckets) b.Clear();

            var origin = transform.position;
            var basis = transform.rotation;
            for (int i = 0; i < _rocks.Length; i++)
            {
                ref var k = ref _rocks[i];
                float omega = shape == Shape.Belt ? orbitSpeed * Mathf.Pow(radiusRange.x / k.r, 1.5f) : orbitSpeed * 0.2f;
                float a = (k.angle + omega * _time) * Mathf.Deg2Rad;
                var pos = origin + basis * new Vector3(Mathf.Cos(a) * k.r, k.height, Mathf.Sin(a) * k.r);
                float d2 = (pos - eye).sqrMagnitude;
                if (d2 > draw2 * Mathf.Max(0.15f, k.scale / sizeRange.y)) continue; // small rocks vanish sooner
                var rot = Quaternion.AngleAxis(k.spinRate * _time + i * 13f, k.spinAxis);
                int lod = d2 < lod2 ? 0 : 1;
                _buckets[k.mesh + lod * Variants, Mathf.Min(k.mat, materials.Length - 1)].Add(Matrix4x4.TRS(pos, rot, Vector3.one * k.scale));
            }

            var bounds = new Bounds(origin, Vector3.one * (radiusRange.y * 2f + thickness * 4f));
            for (int m = 0; m < Variants * 2; m++)
            {
                var mesh = m < Variants ? _near[m] : _far[m - Variants];
                for (int c = 0; c < 3 && c < materials.Length; c++)
                {
                    var list = _buckets[m, c];
                    if (list.Count == 0 || materials[c] == null) continue;
                    var rp = new RenderParams(materials[c]) { worldBounds = bounds, shadowCastingMode = ShadowCastingMode.On, receiveShadows = true, layer = gameObject.layer };
                    for (int s = 0; s < list.Count; s += MaxBatch)
                    {
                        int n = Mathf.Min(MaxBatch, list.Count - s);
                        list.CopyTo(s, _batch, 0, n);
                        Graphics.RenderMeshInstanced(rp, mesh, 0, _batch, n);
                    }
                }
            }
        }
    }
}
