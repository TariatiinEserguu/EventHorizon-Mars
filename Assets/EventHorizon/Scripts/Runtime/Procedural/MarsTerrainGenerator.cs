using UnityEngine;

namespace EventHorizon.Procedural
{
    /// <summary>
    /// Writes a Martian heightmap and splatmap into a Unity Terrain: rolling plains, impact craters with raised rims
    /// and ejecta, and an asymmetric dune field. Crater shapes follow measured Mars ratios:
    /// depth about 0.2 x diameter for simple craters, rim height about 0.04 x diameter, ejecta thinning as r^-3.
    /// </summary>
    [RequireComponent(typeof(Terrain))]
    public class MarsTerrainGenerator : MonoBehaviour
    {
        public int seed = 2026;
        [Tooltip("Width/length in metres and the maximum height range.")]
        public Vector3 size = new Vector3(2048f, 160f, 2048f);
        public int heightmapResolution = 1025;
        public int alphamapResolution = 512;
        public bool generateOnStart;

        [Header("Plains")]
        public float plainsAmplitude = 18f;
        public float plainsWavelength = 600f;

        [Header("Craters")]
        public int craterCount = 60;
        public Vector2 craterDiameterRange = new Vector2(8f, 260f);
        [Range(0.05f, 0.3f)] public float depthToDiameter = 0.2f;
        [Range(0f, 0.1f)] public float rimToDiameter = 0.04f;

        [Header("Dunes")]
        public Vector2 duneFieldCentre = new Vector2(0.72f, 0.3f);
        public float duneFieldRadius = 0.22f;
        public float duneWavelength = 40f;
        public float duneHeight = 4f;
        [Tooltip("Direction the wind blows toward. Dunes have gentle windward slopes and steep lee faces.")]
        public float windAzimuthDeg = 60f;

        [Header("Colony pad")]
        [Tooltip("Flattens a landing/building pad of this radius (m) at the terrain centre. 0 = none.")]
        public float padRadius = 80f;

        [Header("Terrain layers (regolith, cracked ground, gravel, rock)")]
        public TerrainLayer[] layers = new TerrainLayer[0];

        void Start()
        {
            if (generateOnStart) Generate();
        }

        [ContextMenu("Generate")]
        public void Generate()
        {
            var terrain = GetComponent<Terrain>();
            var data = terrain.terrainData;
            if (data == null)
            {
                data = new TerrainData();
                terrain.terrainData = data;
                var col = GetComponent<TerrainCollider>();
                if (col != null) col.terrainData = data;
            }
            data.heightmapResolution = heightmapResolution;
            data.size = size;

            int res = data.heightmapResolution;
            var heights = new float[res, res];
            var rng = new System.Random(seed);
            float baseLevel = size.y * 0.4f; // leaves room for crater bowls

            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                var p = new Vector3(x / (res - 1f) * size.x, 0f, z / (res - 1f) * size.z);
                heights[z, x] = baseLevel + Noise.Fbm(p / plainsWavelength, seed, 5) * plainsAmplitude;
            }

            for (int i = 0; i < craterCount; i++)
            {
                // Power-law size distribution: many small craters, few large ones.
                float d = Mathf.Lerp(craterDiameterRange.x, craterDiameterRange.y, Mathf.Pow((float)rng.NextDouble(), 3f));
                var c = new Vector2((float)rng.NextDouble() * size.x, (float)rng.NextDouble() * size.z);
                StampCrater(heights, res, c, d);
            }

            StampDunes(heights, res);
            FlattenPad(heights, res);

            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
                heights[z, x] = Mathf.Clamp01(heights[z, x] / size.y);
            data.SetHeights(0, 0, heights);

            if (layers != null && layers.Length > 0) Paint(data);
        }

        void StampCrater(float[,] h, int res, Vector2 centre, float diameter)
        {
            float radius = diameter * 0.5f;
            float depth = diameter * depthToDiameter;
            float rim = diameter * rimToDiameter;
            float reach = radius * 3f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((centre.x - reach) / size.x * (res - 1)));
            int x1 = Mathf.Min(res - 1, Mathf.CeilToInt((centre.x + reach) / size.x * (res - 1)));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((centre.y - reach) / size.z * (res - 1)));
            int z1 = Mathf.Min(res - 1, Mathf.CeilToInt((centre.y + reach) / size.z * (res - 1)));
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                var p = new Vector2(x / (res - 1f) * size.x, z / (res - 1f) * size.z);
                float r = Vector2.Distance(p, centre) / radius;
                float delta;
                if (r < 1f) delta = rim + depth * (r * r - 1f);   // parabolic bowl, floor at rim - depth
                else if (r < 3f) delta = rim * Mathf.Pow(r, -3f) * (1f - (r - 1f) / 2f); // ejecta blanket
                else continue;
                h[z, x] += delta;
            }
        }

        void FlattenPad(float[,] h, int res)
        {
            if (padRadius <= 0f) return;
            int c = res / 2;
            float target = h[c, c];
            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                float d = new Vector2((x - c) / (res - 1f) * size.x, (z - c) / (res - 1f) * size.z).magnitude;
                float t = 1f - Mathf.SmoothStep(0f, 1f, (d - padRadius) / (padRadius * 0.6f));
                if (t > 0f) h[z, x] = Mathf.Lerp(h[z, x], target, t);
            }
        }

        void StampDunes(float[,] h, int res)
        {
            float az = windAzimuthDeg * Mathf.Deg2Rad;
            var wind = new Vector2(Mathf.Sin(az), Mathf.Cos(az));
            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                var uv = new Vector2(x / (res - 1f), z / (res - 1f));
                float mask = 1f - Mathf.SmoothStep(0f, 1f, Vector2.Distance(uv, duneFieldCentre) / duneFieldRadius);
                if (mask <= 0f) continue;
                var p = new Vector2(uv.x * size.x, uv.y * size.z);
                float warp = Noise.Fbm(new Vector3(p.x, 0f, p.y) / 120f, seed + 9, 3) * 25f;
                float phase = Mathf.Repeat((Vector2.Dot(p, wind) + warp) / duneWavelength, 1f);
                // Sawtooth: 80% of the wavelength is the gentle windward rise, the last 20% the steep slip face.
                float profile = phase < 0.8f ? phase / 0.8f : (1f - phase) / 0.2f;
                h[z, x] += Mathf.SmoothStep(0f, 1f, profile) * duneHeight * mask;
            }
        }

        void Paint(TerrainData data)
        {
            data.terrainLayers = layers;
            data.alphamapResolution = alphamapResolution;
            int res = data.alphamapResolution;
            int n = layers.Length;
            var maps = new float[res, res, n];
            var w = new float[4];
            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                float u = (x + 0.5f) / res, v = (z + 0.5f) / res;
                float slope = data.GetSteepness(u, v);
                float hNorm = data.GetInterpolatedHeight(u, v) / size.y;
                float noise = Noise.Fbm(new Vector3(u, 0f, v) * 40f, seed + 3, 3) * 0.5f + 0.5f;

                w[0] = 1f;                                                        // fine regolith everywhere
                w[1] = Mathf.Clamp01((0.42f - hNorm) * 12f) * noise;              // cracked ground in low basins
                w[2] = Mathf.Clamp01((slope - 12f) / 10f) * (1f - Mathf.Clamp01((slope - 30f) / 6f)); // gravel on rims
                w[3] = Mathf.Clamp01((slope - 26f) / 8f);                         // bare rock on steep walls
                float sum = 0f;
                for (int l = 0; l < n; l++) sum += l < 4 ? w[l] : 0f;
                for (int l = 0; l < n; l++) maps[z, x, l] = l < 4 ? w[l] / sum : 0f;
            }
            data.SetAlphamaps(0, 0, maps);
        }
    }
}
