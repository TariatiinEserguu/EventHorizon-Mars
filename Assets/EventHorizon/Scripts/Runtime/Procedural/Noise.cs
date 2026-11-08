using UnityEngine;

namespace EventHorizon.Procedural
{
    /// <summary>Deterministic 3D value noise and fBm. Same seed, same rocks, on every machine.</summary>
    public static class Noise
    {
        static float Hash(int x, int y, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u;
                h ^= (uint)x * 0x85EBCA77u; h = (h << 13) | (h >> 19);
                h ^= (uint)y * 0xC2B2AE3Du; h = (h << 13) | (h >> 19);
                h ^= (uint)z * 0x27D4EB2Fu; h = (h << 13) | (h >> 19);
                h *= 0x165667B1u;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777215f * 2f - 1f;
            }
        }

        public static float Value(Vector3 p, int seed)
        {
            int xi = Mathf.FloorToInt(p.x), yi = Mathf.FloorToInt(p.y), zi = Mathf.FloorToInt(p.z);
            float xf = p.x - xi, yf = p.y - yi, zf = p.z - zi;
            float u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf), w = zf * zf * (3 - 2 * zf);
            float Lerp3(int dx, int dy) =>
                Mathf.Lerp(Hash(xi + dx, yi + dy, zi, seed), Hash(xi + dx, yi + dy, zi + 1, seed), w);
            float x00 = Mathf.Lerp(Lerp3(0, 0), Lerp3(1, 0), u);
            float x10 = Mathf.Lerp(Lerp3(0, 1), Lerp3(1, 1), u);
            return Mathf.Lerp(x00, x10, v);
        }

        /// <summary>Fractal sum in roughly [-1, 1].</summary>
        public static float Fbm(Vector3 p, int seed, int octaves = 4, float lacunarity = 2f, float gain = 0.5f)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Value(p, seed + i * 131) * amp;
                norm += amp;
                p *= lacunarity;
                amp *= gain;
            }
            return sum / norm;
        }
    }
}
