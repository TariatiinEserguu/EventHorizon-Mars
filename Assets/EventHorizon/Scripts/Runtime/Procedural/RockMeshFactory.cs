using System.Collections.Generic;
using UnityEngine;

namespace EventHorizon.Procedural
{
    /// <summary>
    /// Low-poly, flat-shaded rocks and ice crystals, so the terrain shares the faceted style of the Kenney
    /// and Quaternius models. A boulder is 80-320 triangles.
    /// </summary>
    public static class RockMeshFactory
    {
        /// <param name="irregularity">0 = sphere, 0.5 = very lumpy.</param>
        /// <param name="flatten">Vertical squash; Martian rocks weathered by wind tend to be flat-topped.</param>
        public static Mesh Boulder(int seed, int subdivisions = 1, float irregularity = 0.35f, float flatten = 0.65f)
        {
            MeshBuilders.Icosphere(subdivisions, out var verts, out var tris);
            var rng = new System.Random(seed);
            var stretch = new Vector3(1f + (float)rng.NextDouble() * 0.5f, flatten, 1f + (float)rng.NextDouble() * 0.3f);
            var offset = new Vector3(seed * 17.3f, seed * 5.1f, seed * 9.7f);
            var p = new Vector3[verts.Count];
            for (int i = 0; i < verts.Count; i++)
            {
                var v = verts[i];
                float n = Noise.Fbm(v * 1.7f + offset, seed, 3);
                v *= 1f + n * irregularity * 2f;
                v = Vector3.Scale(v, stretch);
                // Wind-cut flat base so rocks sit on the ground.
                if (v.y < -0.35f * flatten) v.y = -0.35f * flatten;
                p[i] = v * 0.5f; // about 1 m across before scaling
            }
            return MeshBuilders.FlatShaded(p, tris.ToArray(), $"Boulder_{seed}");
        }

        /// <summary>Cluster of hexagonal ice prisms with pointed tips, for subsurface ice caves and deposits.</summary>
        public static Mesh IceCluster(int seed, int crystals = 7)
        {
            var rng = new System.Random(seed);
            var pos = new List<Vector3>();
            var tri = new List<int>();
            for (int c = 0; c < crystals; c++)
            {
                float height = Mathf.Lerp(0.4f, 1.4f, (float)rng.NextDouble());
                float radius = Mathf.Lerp(0.06f, 0.16f, (float)rng.NextDouble());
                var tilt = Quaternion.Euler(Mathf.Lerp(-35f, 35f, (float)rng.NextDouble()), (float)rng.NextDouble() * 360f,
                                            Mathf.Lerp(-35f, 35f, (float)rng.NextDouble()));
                var basePos = new Vector3(Mathf.Lerp(-0.3f, 0.3f, (float)rng.NextDouble()), 0f, Mathf.Lerp(-0.3f, 0.3f, (float)rng.NextDouble()));
                int start = pos.Count;
                for (int k = 0; k < 6; k++)
                {
                    float a = k / 6f * Mathf.PI * 2f;
                    var ring = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                    pos.Add(basePos + tilt * ring);
                    pos.Add(basePos + tilt * (ring + Vector3.up * height));
                }
                pos.Add(basePos + tilt * (Vector3.up * (height + radius * 2.5f)));
                int tip = pos.Count - 1;
                for (int k = 0; k < 6; k++)
                {
                    int b0 = start + k * 2, t0 = b0 + 1;
                    int b1 = start + (k + 1) % 6 * 2, t1 = b1 + 1;
                    tri.AddRange(new[] { b0, t0, b1, b1, t0, t1, t0, tip, t1 });
                }
            }
            return MeshBuilders.FlatShaded(pos.ToArray(), tri.ToArray(), $"IceCluster_{seed}");
        }
    }
}
