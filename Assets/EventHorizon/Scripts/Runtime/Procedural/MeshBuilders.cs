using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EventHorizon.Procedural
{
    /// <summary>Small procedural meshes used where no free model fits.</summary>
    public static class MeshBuilders
    {
        /// <summary>
        /// Latitude/longitude sphere of radius 1. UVs are equirectangular, which matches the planet textures
        /// from Solar System Scope and NASA. The seam column is duplicated so the texture doesn't smear.
        /// </summary>
        public static Mesh UvSphere(int longitudeSegments = 96, int latitudeSegments = 48, System.Func<Vector3, float> radius = null)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int y = 0; y <= latitudeSegments; y++)
            {
                float v = (float)y / latitudeSegments;
                float lat = Mathf.Lerp(-90f, 90f, v) * Mathf.Deg2Rad;
                for (int x = 0; x <= longitudeSegments; x++)
                {
                    float u = (float)x / longitudeSegments;
                    float lon = (u * 360f - 180f) * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Cos(lat) * Mathf.Sin(lon), Mathf.Sin(lat), -Mathf.Cos(lat) * Mathf.Cos(lon));
                    verts.Add(dir * (radius?.Invoke(dir) ?? 1f));
                    uvs.Add(new Vector2(u, v));
                }
            }
            int row = longitudeSegments + 1;
            for (int y = 0; y < latitudeSegments; y++)
            for (int x = 0; x < longitudeSegments; x++)
            {
                int i = y * row + x;
                tris.Add(i); tris.Add(i + row); tris.Add(i + 1);
                tris.Add(i + 1); tris.Add(i + row); tris.Add(i + row + 1);
            }
            var mesh = new Mesh { name = "UvSphere", indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Flat ring in the XZ plane. U runs from the inner edge (0) to the outer edge (1),
        /// matching the radial strip in Solar System Scope's Saturn ring texture. Double-sided.
        /// </summary>
        public static Mesh Ring(float inner, float outer, int segments = 128)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            for (int side = 0; side < 2; side++)
            {
                int start = verts.Count;
                for (int i = 0; i <= segments; i++)
                {
                    float a = (float)i / segments * Mathf.PI * 2f;
                    var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    verts.Add(d * inner); uvs.Add(new Vector2(0f, (float)i / segments));
                    verts.Add(d * outer); uvs.Add(new Vector2(1f, (float)i / segments));
                    // Unity front faces wind clockwise: (inner_i, outer_i, inner_i+1) faces -Y.
                    normals.Add(side == 0 ? Vector3.down : Vector3.up);
                    normals.Add(side == 0 ? Vector3.down : Vector3.up);
                }
                for (int i = 0; i < segments; i++)
                {
                    int a = start + i * 2;
                    if (side == 0) { tris.Add(a); tris.Add(a + 1); tris.Add(a + 2); tris.Add(a + 2); tris.Add(a + 1); tris.Add(a + 3); }
                    else { tris.Add(a); tris.Add(a + 2); tris.Add(a + 1); tris.Add(a + 2); tris.Add(a + 3); tris.Add(a + 1); }
                }
            }
            var mesh = new Mesh { name = "Ring" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Converts an indexed mesh to flat shading: every triangle gets its own vertices and face normal.</summary>
        public static Mesh FlatShaded(Vector3[] positions, int[] triangles, string name)
        {
            var v = new Vector3[triangles.Length];
            var n = new Vector3[triangles.Length];
            var uv = new Vector2[triangles.Length];
            var t = new int[triangles.Length];
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = positions[triangles[i]], b = positions[triangles[i + 1]], c = positions[triangles[i + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                // Box projection along the dominant axis gives tileable UVs for rock textures.
                Vector3 an = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
                for (int k = 0; k < 3; k++)
                {
                    var p = positions[triangles[i + k]];
                    v[i + k] = p;
                    n[i + k] = normal;
                    uv[i + k] = an.x >= an.y && an.x >= an.z ? new Vector2(p.z, p.y)
                              : an.y >= an.z ? new Vector2(p.x, p.z) : new Vector2(p.x, p.y);
                    t[i + k] = i + k;
                }
            }
            var mesh = new Mesh { name = name, indexFormat = v.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = v;
            mesh.normals = n;
            mesh.uv = uv;
            mesh.triangles = t;
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Icosphere of radius 1 as shared vertices and triangles (subdivisions 0-4).</summary>
        public static void Icosphere(int subdivisions, out List<Vector3> verts, out List<int> tris)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            verts = new List<Vector3>
            {
                new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0),
                new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t),
                new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
            };
            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
            tris = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            var cache = new Dictionary<long, int>();
            var vlist = verts;
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (cache.TryGetValue(key, out int idx)) return idx;
                vlist.Add(((vlist[a] + vlist[b]) * 0.5f).normalized);
                cache[key] = vlist.Count - 1;
                return vlist.Count - 1;
            }
            for (int s = 0; s < Mathf.Clamp(subdivisions, 0, 4); s++)
            {
                var next = new List<int>(tris.Count * 4);
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                tris = next;
            }
        }
    }
}
