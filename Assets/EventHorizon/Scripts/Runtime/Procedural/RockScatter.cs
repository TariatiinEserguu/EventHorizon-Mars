using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EventHorizon.Procedural
{
    /// <summary>
    /// Scatters thousands of rocks on a terrain and draws them with GPU instancing
    /// (Graphics.RenderMeshInstanced), not one GameObject per rock. Rocks are grouped into grid cells that are
    /// distance- and frustum-culled. Only large rocks get colliders.
    /// </summary>
    [ExecuteAlways]
    public class RockScatter : MonoBehaviour
    {
        public Terrain terrain;
        [Tooltip("Rock meshes. Leave empty to generate procedural boulders.")]
        public Mesh[] meshes = new Mesh[0];
        [Tooltip("Must have 'Enable GPU Instancing' ticked.")]
        public Material material;
        public int count = 4000;
        public int seed = 1;
        [Tooltip("Rock size distribution: most rocks are small.")]
        public Vector2 sizeRange = new Vector2(0.15f, 2.5f);
        [Range(1f, 6f)] public float sizeExponent = 3f;
        [Tooltip("No rocks on slopes steeper than this (degrees).")]
        public float maxSlope = 35f;
        public float drawDistance = 400f;
        public float cellSize = 64f;
        [Tooltip("Rocks larger than this get a static collider.")]
        public float colliderAboveSize = 1.2f;
        public ShadowCastingMode shadows = ShadowCastingMode.On;
        [Tooltip("No rocks within keepClearRadius of this point (e.g. the colony pad).")]
        public Transform keepClear;
        public float keepClearRadius = 90f;

        const int MaxPerBatch = 1023;

        class Cell
        {
            public Bounds bounds;
            public readonly List<Matrix4x4>[] perMesh;
            public Cell(int meshes) { perMesh = new List<Matrix4x4>[meshes]; for (int i = 0; i < meshes; i++) perMesh[i] = new List<Matrix4x4>(); }
        }

        readonly List<Cell> _cells = new();
        Mesh[] _meshes;
        Transform _colliderRoot;
        readonly Matrix4x4[] _batch = new Matrix4x4[MaxPerBatch];
        bool _warned;

        void OnEnable() => Build();

        void OnDisable()
        {
            _cells.Clear();
            if (_colliderRoot != null)
            {
                if (Application.isPlaying) Destroy(_colliderRoot.gameObject);
                else DestroyImmediate(_colliderRoot.gameObject);
            }
        }

        [ContextMenu("Rebuild")]
        public void Build()
        {
            _cells.Clear();
            // Explicit checks: '??' skips Unity's overloaded null test on destroyed or missing objects.
            if (terrain == null) terrain = GetComponent<Terrain>();
            if (terrain == null) terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null) return;

            _meshes = meshes != null && meshes.Length > 0 ? meshes : GenerateMeshes();
            var data = terrain.terrainData;
            var origin = terrain.transform.position;
            int cellsX = Mathf.Max(1, Mathf.CeilToInt(data.size.x / cellSize));
            int cellsZ = Mathf.Max(1, Mathf.CeilToInt(data.size.z / cellSize));
            var grid = new Cell[cellsX * cellsZ];

            if (_colliderRoot != null) DestroyImmediate(_colliderRoot.gameObject);
            _colliderRoot = new GameObject("Rock Colliders") { hideFlags = HideFlags.DontSave }.transform;
            _colliderRoot.SetParent(transform, false);

            var rng = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                float nx = (float)rng.NextDouble(), nz = (float)rng.NextDouble();
                if (data.GetSteepness(nx, nz) > maxSlope) continue;
                float size = Mathf.Lerp(sizeRange.x, sizeRange.y, Mathf.Pow((float)rng.NextDouble(), sizeExponent));
                var pos = origin + new Vector3(nx * data.size.x, 0f, nz * data.size.z);
                if (keepClear != null)
                {
                    var flat = pos - keepClear.position;
                    flat.y = 0f;
                    if (flat.sqrMagnitude < keepClearRadius * keepClearRadius) continue;
                }
                pos.y = origin.y + data.GetInterpolatedHeight(nx, nz) - size * 0.15f; // bury slightly
                var normal = data.GetInterpolatedNormal(nx, nz);
                var rot = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, normal), 0.7f)
                          * Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                var scale = Vector3.one * size;
                int meshIndex = rng.Next(_meshes.Length);

                int cx = Mathf.Min(cellsX - 1, (int)(nx * cellsX)), cz = Mathf.Min(cellsZ - 1, (int)(nz * cellsZ));
                ref var cell = ref grid[cz * cellsX + cx];
                if (cell == null)
                {
                    cell = new Cell(_meshes.Length);
                    cell.bounds = new Bounds(pos, Vector3.one);
                }
                cell.perMesh[meshIndex].Add(Matrix4x4.TRS(pos, rot, scale));
                cell.bounds.Encapsulate(new Bounds(pos, Vector3.one * size * 2f));

                if (size >= colliderAboveSize)
                {
                    // A sphere works for any mesh. Convex mesh colliders would need CPU-readable meshes at runtime.
                    var col = new GameObject("Rock") { hideFlags = HideFlags.DontSave };
                    col.transform.SetParent(_colliderRoot, false);
                    col.transform.SetPositionAndRotation(pos, rot);
                    col.transform.localScale = scale;
                    var b = _meshes[meshIndex].bounds;
                    var sc = col.AddComponent<SphereCollider>();
                    sc.center = b.center;
                    sc.radius = Mathf.Min(b.extents.x, Mathf.Min(b.extents.y * 1.6f, b.extents.z));
                    col.isStatic = true;
                }
            }
            foreach (var c in grid) if (c != null) _cells.Add(c);
        }

        Mesh[] GenerateMeshes()
        {
            var result = new Mesh[6];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = RockMeshFactory.Boulder(seed * 31 + i, i % 3 == 0 ? 2 : 1, 0.25f + 0.05f * i, 0.5f + 0.08f * i);
                result[i].hideFlags = HideFlags.DontSave;
            }
            return result;
        }

        void Update()
        {
            if (material == null || _cells.Count == 0) return;
            if (!material.enableInstancing)
            {
                if (!_warned) Debug.LogWarning($"{name}: enable GPU instancing on {material.name} to draw rocks.", this);
                _warned = true;
                return;
            }
            var cam = Camera.main;
#if UNITY_EDITOR
            if (!Application.isPlaying && UnityEditor.SceneView.lastActiveSceneView != null)
                cam = UnityEditor.SceneView.lastActiveSceneView.camera;
#endif
            Vector3 eye = cam != null ? cam.transform.position : transform.position;
            float maxSqr = drawDistance * drawDistance;

            for (int c = 0; c < _cells.Count; c++)
            {
                var cell = _cells[c];
                if (cell.bounds.SqrDistance(eye) > maxSqr) continue;
                var rp = new RenderParams(material)
                {
                    worldBounds = cell.bounds, shadowCastingMode = shadows, receiveShadows = true, layer = gameObject.layer,
                };
                for (int m = 0; m < _meshes.Length; m++)
                {
                    var list = cell.perMesh[m];
                    for (int start = 0; start < list.Count; start += MaxPerBatch)
                    {
                        int n = Mathf.Min(MaxPerBatch, list.Count - start);
                        list.CopyTo(start, _batch, 0, n);
                        Graphics.RenderMeshInstanced(rp, _meshes[m], 0, _batch, n);
                    }
                }
            }
        }
    }
}
