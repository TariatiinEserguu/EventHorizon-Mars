using System.IO;
using EventHorizon.Agriculture;
using EventHorizon.Colony;
using EventHorizon.Core;
using EventHorizon.Game;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace EventHorizon.EditorTools
{
    /// <summary>
    /// Procedural, realistic colony structures built from detailed parts with full PBR materials (Poly Haven CC0
    /// albedo, normal, AO, roughness and metalness). They replace the low-poly placeholder models under the same
    /// prefab names, so scenes and gameplay components keep working.
    /// Proportions follow real hardware: 2.2 m PV modules on single-axis trackers, 12 m battery containers,
    /// 4.5 m-diameter pressure-vessel habitat modules, a MOXIE-style oxygen skid.
    /// </summary>
    public static class ColonyKit
    {
        const string Out = AssetIntegrationBuilder.Out;
        const string PH = "Assets/ThirdParty/PolyHaven/Industrial/";
        static string MatPath(string n) => $"{Out}/Materials/{n}.mat";

        // ------------------------------------------------------------------ materials

        static Material Lit(string name)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(MatPath(name));
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, MatPath(name)); }
            else m.shader = shader;
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>URP mask: R = metallic, G = ambient occlusion, A = smoothness, built from Poly Haven's ARM map.</summary>
        static Texture2D UrpMask(string slug)
        {
            string src = $"{PH}{slug}/{slug}_arm_1k.jpg";
            string path = $"{Out}/Textures/Mask_{slug}.png";
            if (!File.Exists(path) && File.Exists(src))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var arm = new Texture2D(2, 2);
                arm.LoadImage(File.ReadAllBytes(src));
                var px = arm.GetPixels();
                for (int i = 0; i < px.Length; i++) px[i] = new Color(px[i].b, px[i].r, 0f, 1f - px[i].g);
                var outTex = new Texture2D(arm.width, arm.height, TextureFormat.RGBA32, true);
                outTex.SetPixels(px);
                outTex.Apply();
                File.WriteAllBytes(path, outTex.EncodeToPNG());
                Object.DestroyImmediate(arm);
                Object.DestroyImmediate(outTex);
                AssetDatabase.ImportAsset(path);
                if (AssetImporter.GetAtPath(path) is TextureImporter ti) { ti.sRGBTexture = false; ti.maxTextureSize = 1024; ti.SaveAndReimport(); }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material Pbr(string name, string slug, Color tint, float tiling, float smoothness = 1f, float metalOverride = -1f, bool albedo = true)
        {
            var m = Lit(name);
            // Painted surfaces keep only the texture's relief (normal + AO); the colour is the paint.
            m.SetTexture("_BaseMap", albedo ? AssetDatabase.LoadAssetAtPath<Texture2D>($"{PH}{slug}/{slug}_diff_1k.jpg") : null);
            m.SetTextureScale("_BaseMap", Vector2.one * tiling);
            m.SetColor("_BaseColor", tint);
            var nor = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PH}{slug}/{slug}_nor_gl_1k.jpg");
            if (nor != null) { m.SetTexture("_BumpMap", nor); m.EnableKeyword("_NORMALMAP"); }
            var mask = UrpMask(slug);
            if (mask != null)
            {
                m.SetTexture("_OcclusionMap", mask);
                m.EnableKeyword("_OCCLUSIONMAP");
                if (metalOverride < 0f)
                {
                    m.SetTexture("_MetallicGlossMap", mask);
                    m.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
            }
            m.SetFloat("_Smoothness", smoothness);
            if (metalOverride >= 0f) m.SetFloat("_Metallic", metalOverride);
            return m;
        }

        static Material Plain(string name, Color c, float metal, float smooth, Color? emission = null)
        {
            var m = Lit(name);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Metallic", metal);
            m.SetFloat("_Smoothness", smooth);
            if (emission.HasValue)
            {
                m.SetColor("_EmissionColor", emission.Value);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return m;
        }

        /// <summary>Photovoltaic module texture: 6 x 12 dark-blue cells with silver busbars and white gaps.</summary>
        static Texture2D PvTexture()
        {
            string path = $"{Out}/Textures/PV_Module.png";
            if (!File.Exists(path))
            {
                const int W = 384, H = 768, cx = 6, cy = 12;
                var tex = new Texture2D(W, H, TextureFormat.RGB24, true);
                var rng = new System.Random(5);
                var cellTint = new float[cx * cy];
                for (int i = 0; i < cellTint.Length; i++) cellTint[i] = (float)rng.NextDouble() * 0.03f;
                for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W * cx, v = y / (float)H * cy;
                    float fu = u - Mathf.Floor(u), fv = v - Mathf.Floor(v);
                    Color c;
                    if (fu < 0.03f || fu > 0.97f || fv < 0.03f || fv > 0.97f) c = new Color(0.82f, 0.84f, 0.86f);      // gaps
                    else if (Mathf.Abs(fu - 0.25f) < 0.008f || Mathf.Abs(fu - 0.5f) < 0.008f || Mathf.Abs(fu - 0.75f) < 0.008f)
                        c = new Color(0.62f, 0.64f, 0.68f);                                                           // busbars
                    else if (((int)(fv * 64f)) % 2 == 0 && fv > 0.05f) c = new Color(0.05f, 0.09f, 0.2f);              // finger lines
                    else
                    {
                        float t = cellTint[Mathf.Min((int)u, cx - 1) + Mathf.Min((int)v, cy - 1) * cx];
                        c = new Color(0.035f + t, 0.06f + t, 0.15f + t * 2f);
                    }
                    tex.SetPixel(x, y, c);
                }
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Diagonal hazard stripes (black and yellow) for edges, steps and equipment guards.</summary>
        static Texture2D StripeTexture()
        {
            string path = $"{Out}/Textures/HazardStripes.png";
            if (!File.Exists(path))
            {
                var tex = new Texture2D(128, 128, TextureFormat.RGB24, true);
                for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                    tex.SetPixel(x, y, ((x + y) / 32) % 2 == 0 ? new Color(0.9f, 0.7f, 0.08f) : new Color(0.05f, 0.05f, 0.05f));
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        internal static Material M(string key) => AssetDatabase.LoadAssetAtPath<Material>(MatPath("Kit_" + key));

        public static void BuildMaterials()
        {
            Directory.CreateDirectory($"{Out}/Textures");
            Pbr("Kit_HullWhite", "metal_plate_02", new Color(0.84f, 0.84f, 0.82f), 1.5f, 0.4f, 0.05f, albedo: false);
            Pbr("Kit_Steel", "metal_plate", new Color(0.78f, 0.78f, 0.8f), 2f, 0.8f);
            Pbr("Kit_Container", "corrugated_iron_02", new Color(0.8f, 0.81f, 0.82f), 2f, 0.35f, 0.15f, albedo: false);
            Pbr("Kit_Cladding", "box_profile_metal_sheet", new Color(0.62f, 0.64f, 0.66f), 1.5f, 0.45f, 0.3f, albedo: false);
            Pbr("Kit_Pad", "concrete_panels", new Color(0.78f, 0.6f, 0.48f), 0.25f, 0.4f, 0f); // sintered-regolith pads
            var pv = Lit("Kit_PV");
            pv.SetTexture("_BaseMap", PvTexture());
            pv.SetColor("_BaseColor", Color.white);
            pv.SetFloat("_Smoothness", 0.88f);
            pv.SetFloat("_Metallic", 0.15f);
            Plain("Kit_Aluminium", new Color(0.8f, 0.81f, 0.83f), 0.9f, 0.55f);
            Plain("Kit_DarkComposite", new Color(0.12f, 0.12f, 0.13f), 0.1f, 0.35f);
            Plain("Kit_MLI", new Color(0.78f, 0.6f, 0.25f), 0.9f, 0.62f);                          // gold multilayer insulation
            Plain("Kit_Radiator", new Color(0.92f, 0.93f, 0.93f), 0.05f, 0.45f);
            Plain("Kit_OrangeStripe", new Color(0.92f, 0.42f, 0.12f), 0.05f, 0.4f);
            Plain("Kit_LampWarm", new Color(1f, 0.9f, 0.7f), 0f, 0.5f, new Color(1f, 0.85f, 0.6f) * 4f);
            Plain("Kit_StatusGreen", new Color(0.2f, 1f, 0.4f), 0f, 0.5f, new Color(0.2f, 1f, 0.4f) * 3f);
            Plain("Kit_StatusAmber", new Color(1f, 0.6f, 0.1f), 0f, 0.5f, new Color(1f, 0.55f, 0.1f) * 3f);
            Plain("Kit_LED", new Color(0.9f, 0.4f, 0.9f), 0f, 0.5f, new Color(0.85f, 0.35f, 0.95f) * 2.5f); // red+blue grow light
            var stripes = Lit("Kit_Hazard");
            stripes.SetTexture("_BaseMap", StripeTexture());
            stripes.SetColor("_BaseColor", Color.white);
            stripes.SetFloat("_Smoothness", 0.3f);
            var glass = Lit("Kit_Glass");
            glass.SetColor("_BaseColor", new Color(0.78f, 0.86f, 0.9f, 0.3f));
            glass.SetFloat("_Smoothness", 0.96f);
            glass.SetFloat("_Metallic", 0f);
            VisualSetup.MakeTransparent(glass);
            Plain("Kit_Window", new Color(0.05f, 0.08f, 0.1f), 0.2f, 0.95f, new Color(1f, 0.85f, 0.6f) * 0.6f);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ geometry helpers

        internal static GameObject Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Vector3 euler, string mat, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = type + "_" + mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            var m = M(mat);
            if (m != null) go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        /// <summary>A straight pipe between two local points.</summary>
        internal static void Pipe(Transform parent, Vector3 a, Vector3 b, float radius, string mat = "Steel")
        {
            var mid = (a + b) * 0.5f;
            var dir = b - a;
            var go = Part(PrimitiveType.Cylinder, parent, mid, new Vector3(radius * 2f, dir.magnitude * 0.5f, radius * 2f), Vector3.zero, mat);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
        }

        static GameObject Root(string name) => new GameObject(name);

        static GameObject Save(GameObject root, string name)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Out}/Prefabs/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        internal static void Floodlight(Transform parent, Vector3 pos, Vector3 euler, float range = 30f, float intensity = 25f)
        {
            var lamp = Part(PrimitiveType.Cube, parent, pos, new Vector3(0.5f, 0.15f, 0.25f), euler, "LampWarm");
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(lamp.transform, false);
            light.transform.localRotation = Quaternion.Euler(60f, 0f, 0f);
            light.type = LightType.Spot;
            light.spotAngle = 110f;
            light.range = range;
            light.intensity = intensity;
            light.color = new Color(1f, 0.86f, 0.66f);
            light.shadows = LightShadows.None;
            light.gameObject.AddComponent<NightLight>().lampRenderer = lamp.GetComponent<Renderer>();
        }

        // ------------------------------------------------------------------ structures

        /// <summary>
        /// Single-axis tracking table: 2 x 6 PV modules (1.1 x 2.2 m each, ~29 m^2) on a torque tube, central post,
        /// slew drive and footing. The tracking pivot rotates the whole table toward the sun.
        /// </summary>
        public static void SolarTracker()
        {
            var root = Root("SolarPanel");
            Part(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.15f, 0f), new Vector3(0.9f, 0.3f, 0.9f), Vector3.zero, "Pad", true);
            Part(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 1.1f, 0f), new Vector3(0.22f, 0.8f, 0.22f), Vector3.zero, "Steel", true);
            Part(PrimitiveType.Cube, root.transform, new Vector3(0f, 1.95f, 0f), new Vector3(0.5f, 0.35f, 0.45f), Vector3.zero, "DarkComposite");
            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(0f, 2.2f, 0f);
            pivot.localRotation = Quaternion.Euler(18.4f, 0f, 0f);
            var surface = new GameObject("Cells").transform;
            surface.SetParent(pivot, false);
            Part(PrimitiveType.Cylinder, pivot, Vector3.zero, new Vector3(0.12f, 3.4f, 0.12f), new Vector3(0f, 0f, 90f), "Steel");
            var cells = new System.Collections.Generic.List<Renderer>();
            for (int i = 0; i < 6; i++)
            for (int j = 0; j < 2; j++)
            {
                float x = (i - 2.5f) * 1.13f, z = (j - 0.5f) * 2.24f;
                var module = Part(PrimitiveType.Cube, surface, new Vector3(x, 0.09f, z), new Vector3(1.1f, 0.035f, 2.2f), Vector3.zero, "PV");
                cells.Add(module.GetComponent<Renderer>());
                Part(PrimitiveType.Cube, surface, new Vector3(x, 0.07f, z), new Vector3(1.12f, 0.045f, 2.22f), Vector3.zero, "Aluminium");
            }
            for (int i = -1; i <= 1; i += 2)
                Part(PrimitiveType.Cube, pivot, new Vector3(i * 3.2f, 0.04f, 0f), new Vector3(0.06f, 0.06f, 4.4f), Vector3.zero, "Steel");
            var panel = root.AddComponent<SolarPanel>();
            panel.surface = surface;
            panel.trackingPivot = pivot;
            panel.areaM2 = 12 * 1.1f * 2.2f;
            panel.sunTracking = true;
            panel.cellRenderers = cells.ToArray();
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 2.2f, 0f);
            col.size = new Vector3(7f, 0.6f, 4.6f);
            Save(root, "SolarPanel");
        }

        /// <summary>Power station: two 12 m battery containers with HVAC units, an inverter skid, a transformer and light masts.</summary>
        public static void PowerStation()
        {
            var root = Root("BatteryBank");
            Part(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.1f, 0f), new Vector3(16f, 0.2f, 10f), Vector3.zero, "Pad", true);
            for (int k = 0; k < 2; k++)
            {
                float z = (k - 0.5f) * 3.6f;
                Part(PrimitiveType.Cube, root.transform, new Vector3(-1.5f, 1.5f, z), new Vector3(12.2f, 2.6f, 2.45f), Vector3.zero, "Container", true);
                Part(PrimitiveType.Cube, root.transform, new Vector3(-1.5f, 0.26f, z), new Vector3(12.3f, 0.12f, 2.5f), Vector3.zero, "Steel");
                for (int u = 0; u < 3; u++)
                    Part(PrimitiveType.Cube, root.transform, new Vector3(-6f + u * 4.5f, 3.05f, z), new Vector3(1.4f, 0.5f, 1.6f), Vector3.zero, "Cladding");
                Part(PrimitiveType.Cube, root.transform, new Vector3(-1.5f, 2.6f, z + (k == 0 ? -1.24f : 1.24f)), new Vector3(12.2f, 0.12f, 0.02f), Vector3.zero, "OrangeStripe");
                Part(PrimitiveType.Cube, root.transform, new Vector3(4.65f, 1.4f, z), new Vector3(0.05f, 2.2f, 2.1f), Vector3.zero, "Steel");
                Part(PrimitiveType.Cube, root.transform, new Vector3(4.7f, 2.2f, z + 0.6f), new Vector3(0.05f, 0.12f, 0.12f), Vector3.zero, "StatusGreen");
            }
            // Inverter skid and transformer with cooling fins.
            Part(PrimitiveType.Cube, root.transform, new Vector3(6.4f, 1.1f, -2.2f), new Vector3(2.2f, 2f, 1.6f), Vector3.zero, "Cladding", true);
            Part(PrimitiveType.Cube, root.transform, new Vector3(6.4f, 1.1f, 2.4f), new Vector3(1.8f, 1.8f, 1.8f), Vector3.zero, "DarkComposite", true);
            for (int f = 0; f < 6; f++)
                Part(PrimitiveType.Cube, root.transform, new Vector3(5.45f, 1.1f, 1.7f + f * 0.28f), new Vector3(0.1f, 1.5f, 0.04f), Vector3.zero, "Steel");
            Part(PrimitiveType.Cube, root.transform, new Vector3(6.4f, 0.3f, 0f), new Vector3(2.6f, 0.4f, 7.2f), Vector3.zero, "Hazard");
            Pipe(root.transform, new Vector3(4.7f, 0.5f, -2.2f), new Vector3(5.3f, 0.5f, -2.2f), 0.1f, "DarkComposite");
            // Cable tray toward the solar field.
            for (int s = 0; s < 4; s++)
            {
                Part(PrimitiveType.Cube, root.transform, new Vector3(-9f - s * 3f, 0.55f, 4f), new Vector3(3f, 0.12f, 0.5f), Vector3.zero, "Steel");
                Part(PrimitiveType.Cylinder, root.transform, new Vector3(-9f - s * 3f, 0.25f, 4f), new Vector3(0.08f, 0.25f, 0.08f), Vector3.zero, "Steel");
            }
            foreach (var x in new[] { -8f, 8f })
            {
                Part(PrimitiveType.Cylinder, root.transform, new Vector3(x, 3f, -4.6f), new Vector3(0.18f, 3f, 0.18f), Vector3.zero, "Steel");
                Floodlight(root.transform, new Vector3(x, 6.1f, -4.3f), new Vector3(0f, 180f, 0f), 35f, 30f);
            }
            var bank = root.AddComponent<BatteryBank>();
            bank.capacityWh = 400000f; // 2 x 200 kWh containers
            Save(root, "BatteryBank");
        }

        /// <summary>
        /// Habitat complex: two 4.5 m pressure-vessel modules joined by a node, an airlock with a hazard-marked
        /// hatch, radiator wings, windows, a comms mast and floodlights. Each module rests on saddle supports.
        /// </summary>
        public static void Habitat()
        {
            var root = Root("Habitat");
            Part(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.1f, 0f), new Vector3(34f, 0.2f, 12f), Vector3.zero, "Pad", true);
            foreach (var x in new[] { -8.5f, 8.5f })
            {
                // Capsule along X: height 12 m -> scale.y = 6 (capsule height = 2 * scale.y), diameter 4.5 m.
                Part(PrimitiveType.Capsule, root.transform, new Vector3(x, 3.2f, 0f), new Vector3(4.5f, 6.5f, 4.5f), new Vector3(0f, 0f, 90f), "HullWhite", true);
                for (int r = -2; r <= 2; r++)
                    Part(PrimitiveType.Cylinder, root.transform, new Vector3(x + r * 2.4f, 3.2f, 0f), new Vector3(4.62f, 0.08f, 4.62f), new Vector3(0f, 0f, 90f), "Steel");
                for (int s = -1; s <= 1; s += 2)
                {
                    Part(PrimitiveType.Cube, root.transform, new Vector3(x + s * 3.5f, 0.75f, 0f), new Vector3(0.6f, 1.1f, 3.6f), Vector3.zero, "Steel", true);
                }
                for (int w = -1; w <= 1; w++)
                    Part(PrimitiveType.Cylinder, root.transform, new Vector3(x + w * 2.8f, 3.8f, -2.2f), new Vector3(0.55f, 0.04f, 0.55f), new Vector3(90f, 0f, 0f), "Window");
                Part(PrimitiveType.Cube, root.transform, new Vector3(x, 3.2f, 2.3f), new Vector3(10f, 0.12f, 0.03f), Vector3.zero, "OrangeStripe");
                // Radiator wing on the north side.
                Part(PrimitiveType.Cube, root.transform, new Vector3(x, 5.6f, 3.6f), new Vector3(7f, 0.06f, 2.6f), new Vector3(-15f, 0f, 0f), "Radiator");
                Pipe(root.transform, new Vector3(x, 4.9f, 2.0f), new Vector3(x, 5.4f, 2.6f), 0.08f);
            }
            // Central node with the airlock facing south (-Z).
            Part(PrimitiveType.Sphere, root.transform, new Vector3(0f, 3.2f, 0f), new Vector3(5.2f, 5.2f, 5.2f), Vector3.zero, "HullWhite", true);
            Part(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 2.4f, -3.6f), new Vector3(2.6f, 1.4f, 2.6f), new Vector3(90f, 0f, 0f), "HullWhite", true);
            Part(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 2.4f, -5.02f), new Vector3(2.75f, 0.06f, 2.75f), new Vector3(90f, 0f, 0f), "Hazard");
            Part(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 2.4f, -5.07f), new Vector3(1.8f, 0.05f, 1.8f), new Vector3(90f, 0f, 0f), "Steel");
            Part(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.9f, -5.6f), new Vector3(2.4f, 0.2f, 1.6f), Vector3.zero, "Steel", true); // step
            Part(PrimitiveType.Cube, root.transform, new Vector3(0.95f, 3.5f, -5.08f), new Vector3(0.16f, 0.16f, 0.04f), Vector3.zero, "StatusGreen");
            Part(PrimitiveType.Cylinder, root.transform, new Vector3(-1.5f, 7.5f, 1f), new Vector3(0.12f, 2.2f, 0.12f), Vector3.zero, "Aluminium");
            Part(PrimitiveType.Sphere, root.transform, new Vector3(-1.5f, 9.8f, 1f), new Vector3(0.5f, 0.2f, 0.5f), Vector3.zero, "Radiator");
            Floodlight(root.transform, new Vector3(-2.2f, 5.6f, -2.7f), new Vector3(0f, 180f, 0f));
            Floodlight(root.transform, new Vector3(2.2f, 5.6f, -2.7f), new Vector3(0f, 180f, 0f));
            root.AddComponent<CrewLifeSupport>();
            var trigger = root.AddComponent<ProximityTrigger>();
            trigger.eventId = "reached_habitat";
            trigger.radius = 16f;
            trigger.message = "TOAST_HABITAT_REACHED";
            Save(root, "Habitat");
        }

        /// <summary>
        /// MOXIE-style oxygen plant: CO2 intake tower with dust filter, compressor, insulated solid-oxide stack
        /// housing (gold MLI), cooling radiator, pipework and status lights, all on a steel skid.
        /// </summary>
        public static void OxygenPlant()
        {
            var root = Root("OxygenPlant");
            Part(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.1f, 0f), new Vector3(11f, 0.2f, 7f), Vector3.zero, "Pad", true);
            Part(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.35f, 0f), new Vector3(9f, 0.3f, 5f), Vector3.zero, "Steel", true);
            Part(PrimitiveType.Cylinder, root.transform, new Vector3(-3.4f, 2.9f, -1.2f), new Vector3(0.9f, 2.4f, 0.9f), Vector3.zero, "Cladding", true);
            Part(PrimitiveType.Cube, root.transform, new Vector3(-3.4f, 5.6f, -1.2f), new Vector3(1.6f, 0.9f, 1.6f), Vector3.zero, "DarkComposite");
            Part(PrimitiveType.Cube, root.transform, new Vector3(-1.2f, 1.4f, -1.2f), new Vector3(2f, 1.8f, 1.6f), Vector3.zero, "Container", true);
            Part(PrimitiveType.Cube, root.transform, new Vector3(1.8f, 1.6f, 0f), new Vector3(3f, 2.2f, 2.6f), Vector3.zero, "MLI", true);
            Part(PrimitiveType.Cube, root.transform, new Vector3(1.8f, 2.8f, 0f), new Vector3(3.2f, 0.15f, 2.8f), Vector3.zero, "Steel");
            Part(PrimitiveType.Cube, root.transform, new Vector3(1.8f, 4.2f, 2.4f), new Vector3(4f, 0.05f, 2f), new Vector3(-60f, 0f, 0f), "Radiator");
            Pipe(root.transform, new Vector3(-3.4f, 1.0f, -1.2f), new Vector3(-2.2f, 1.0f, -1.2f), 0.14f);
            Pipe(root.transform, new Vector3(-0.2f, 1.2f, -1.2f), new Vector3(0.3f, 1.2f, -0.6f), 0.12f);
            Pipe(root.transform, new Vector3(3.3f, 1.0f, 0.6f), new Vector3(4.4f, 1.0f, 0.6f), 0.1f, "Aluminium");
            Pipe(root.transform, new Vector3(4.4f, 1.0f, 0.6f), new Vector3(4.4f, 0.5f, 3.2f), 0.1f, "Aluminium");
            Pipe(root.transform, new Vector3(3.3f, 1.6f, -0.6f), new Vector3(4.2f, 2.4f, -0.6f), 0.08f);
            Part(PrimitiveType.Cube, root.transform, new Vector3(0.2f, 2.65f, -1.32f), new Vector3(0.14f, 0.14f, 0.03f), Vector3.zero, "StatusGreen");
            Part(PrimitiveType.Cube, root.transform, new Vector3(0.5f, 2.65f, -1.32f), new Vector3(0.14f, 0.14f, 0.03f), Vector3.zero, "StatusAmber");
            Part(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.52f, -2.55f), new Vector3(9f, 0.05f, 0.1f), Vector3.zero, "Hazard");
            root.AddComponent<OxygenGenerator>();
            Save(root, "OxygenPlant");
        }

        /// <summary>Vertical insulated storage tanks on a skid: water (two tanks with pump) or oxygen.</summary>
        public static void Tanks(string name, ResourceType resource, float capacity, float initial, int count, float height, string shell)
        {
            var root = Root(name);
            float spacing = 3.1f;
            Part(PrimitiveType.Cube, root.transform, new Vector3((count - 1) * spacing * 0.5f, 0.1f, 0f), new Vector3(count * spacing + 1.5f, 0.2f, 4.2f), Vector3.zero, "Pad", true);
            for (int i = 0; i < count; i++)
            {
                float x = i * spacing;
                Part(PrimitiveType.Capsule, root.transform, new Vector3(x, height * 0.5f + 0.4f, 0f), new Vector3(2.6f, height * 0.5f, 2.6f), Vector3.zero, shell, true);
                for (int b = 0; b < 3; b++)
                    Part(PrimitiveType.Cylinder, root.transform, new Vector3(x, 1.2f + b * (height - 1.6f) / 2f, 0f), new Vector3(2.66f, 0.05f, 2.66f), Vector3.zero, "Steel");
                for (int l = 0; l < 4; l++)
                {
                    float a = l * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                    Part(PrimitiveType.Cylinder, root.transform, new Vector3(x + Mathf.Cos(a) * 1.05f, 0.5f, Mathf.Sin(a) * 1.05f), new Vector3(0.14f, 0.35f, 0.14f), Vector3.zero, "Steel");
                }
                Pipe(root.transform, new Vector3(x, 0.8f, -1.3f), new Vector3(x, 0.8f, -2f), 0.09f, "Aluminium");
            }
            if (count > 1) Pipe(root.transform, new Vector3(0f, 0.8f, -2f), new Vector3((count - 1) * spacing, 0.8f, -2f), 0.09f, "Aluminium");
            Part(PrimitiveType.Cube, root.transform, new Vector3(-1.8f, 0.7f, -1.4f), new Vector3(0.9f, 0.8f, 0.7f), Vector3.zero, "Cladding", true);
            var tank = root.AddComponent<ResourceTank>();
            tank.resource = resource;
            tank.capacityKg = capacity;
            tank.initialKg = initial;
            Save(root, name);
        }

        /// <summary>Food store: an insulated 6 m container with an HVAC unit.</summary>
        public static void FoodStore()
        {
            var root = Root("FoodStore");
            Part(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.1f, 0f), new Vector3(7.5f, 0.2f, 3.8f), Vector3.zero, "Pad", true);
            Part(PrimitiveType.Cube, root.transform, new Vector3(0f, 1.5f, 0f), new Vector3(6.1f, 2.6f, 2.45f), Vector3.zero, "Container", true);
            Part(PrimitiveType.Cube, root.transform, new Vector3(0f, 3.05f, 0f), new Vector3(1.4f, 0.5f, 1.4f), Vector3.zero, "Cladding");
            Part(PrimitiveType.Cube, root.transform, new Vector3(0f, 2.6f, -1.24f), new Vector3(6.1f, 0.12f, 0.02f), Vector3.zero, "OrangeStripe");
            var tank = root.AddComponent<ResourceTank>();
            tank.resource = ResourceType.Food;
            tank.capacityKg = 2000f;
            tank.initialKg = 300f;
            Save(root, "FoodStore");
        }

        /// <summary>
        /// Greenhouse: a 9 m transparent dome with a steel frame (meridians and rings), an entrance tunnel and
        /// LED grow-light bars above the hydroponic beds, which the Greenhouse component lays out on start.
        /// </summary>
        public static void Greenhouse(GameObject bedPrefab, CropDefinition[] crops)
        {
            var root = Root("Greenhouse");
            const float r = 9f;
            Part(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.1f, 0f), new Vector3(r * 2f + 1f, 0.1f, r * 2f + 1f), Vector3.zero, "Pad", true);
            Part(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.45f, 0f), new Vector3(r * 2f, 0.35f, r * 2f), Vector3.zero, "Steel");
            Part(PrimitiveType.Sphere, root.transform, new Vector3(0f, 0.6f, 0f), new Vector3(r * 2f, r * 1.6f, r * 2f), Vector3.zero, "Glass");
            for (int m = 0; m < 8; m++)
            {
                var ring = new GameObject("Meridian");
                ring.transform.SetParent(root.transform, false);
                ring.transform.localPosition = new Vector3(0f, 0.6f, 0f);
                ring.transform.localRotation = Quaternion.Euler(0f, m * 22.5f, 0f);
                for (int s = 0; s < 9; s++)
                {
                    float a0 = s / 9f * Mathf.PI * 0.5f, a1 = (s + 1) / 9f * Mathf.PI * 0.5f;
                    Pipe(ring.transform, new Vector3(Mathf.Cos(a0) * r, Mathf.Sin(a0) * r * 0.8f, 0f), new Vector3(Mathf.Cos(a1) * r, Mathf.Sin(a1) * r * 0.8f, 0f), 0.07f, "Aluminium");
                    Pipe(ring.transform, new Vector3(-Mathf.Cos(a0) * r, Mathf.Sin(a0) * r * 0.8f, 0f), new Vector3(-Mathf.Cos(a1) * r, Mathf.Sin(a1) * r * 0.8f, 0f), 0.07f, "Aluminium");
                }
            }
            // Horizontal hoops, built from short pipe segments (a flattened cylinder would be a solid disc).
            foreach (var h in new[] { 0.35f, 0.7f })
            {
                float rr = Mathf.Cos(Mathf.Asin(h)) * r, y = 0.6f + h * r * 0.8f;
                for (int k = 0; k < 24; k++)
                {
                    float a0 = k / 24f * Mathf.PI * 2f, a1 = (k + 1) / 24f * Mathf.PI * 2f;
                    Pipe(root.transform, new Vector3(Mathf.Cos(a0) * rr, y, Mathf.Sin(a0) * rr), new Vector3(Mathf.Cos(a1) * rr, y, Mathf.Sin(a1) * rr), 0.06f, "Aluminium");
                }
            }
            Part(PrimitiveType.Capsule, root.transform, new Vector3(0f, 1.7f, -r - 1.2f), new Vector3(3f, 2f, 3f), new Vector3(90f, 0f, 0f), "HullWhite");
            for (int b = -1; b <= 1; b++)
                Part(PrimitiveType.Cube, root.transform, new Vector3(b * 3.2f, 2.6f, 0f), new Vector3(0.12f, 0.06f, 7f), Vector3.zero, "LED");
            var gh = root.AddComponent<Agriculture.Greenhouse>();
            gh.bedPrefab = bedPrefab;
            gh.bedSize = new Vector2(2f, 0.8f);
            gh.crops = crops;
            gh.rows = 3;
            gh.columns = 2;
            gh.aisle = 1.4f;
            Save(root, "Greenhouse");
        }

        /// <summary>Crewed lander / Mars ascent vehicle: body, nose, four landing legs with pads, engine bells.</summary>
        public static void Lander()
        {
            var root = Root("Lander");
            Part(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.08f, 0f), new Vector3(14f, 0.08f, 14f), Vector3.zero, "Pad", true);
            Part(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 7.5f, 0f), new Vector3(4.2f, 5f, 4.2f), Vector3.zero, "HullWhite", true);
            Part(PrimitiveType.Capsule, root.transform, new Vector3(0f, 12.6f, 0f), new Vector3(4.2f, 2.6f, 4.2f), Vector3.zero, "HullWhite");
            Part(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 3.0f, 0f), new Vector3(4.6f, 0.5f, 4.6f), Vector3.zero, "DarkComposite", true);
            for (int w = 0; w < 4; w++)
                Part(PrimitiveType.Cylinder, root.transform, new Vector3(Mathf.Cos(w * 1.5708f) * 2.1f, 11f, Mathf.Sin(w * 1.5708f) * 2.1f), new Vector3(0.4f, 0.03f, 0.4f), new Vector3(0f, -w * 90f, 90f), "Window");
            for (int l = 0; l < 4; l++)
            {
                float a = l * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                var top = new Vector3(Mathf.Cos(a) * 2f, 4f, Mathf.Sin(a) * 2f);
                var foot = new Vector3(Mathf.Cos(a) * 4.6f, 0.3f, Mathf.Sin(a) * 4.6f);
                Pipe(root.transform, top, foot, 0.18f, "Steel");
                Pipe(root.transform, top + Vector3.up * 1.5f, foot + (top - foot) * 0.45f, 0.1f, "Aluminium");
                Part(PrimitiveType.Cylinder, root.transform, foot, new Vector3(1.1f, 0.12f, 1.1f), Vector3.zero, "Steel");
            }
            for (int e = 0; e < 3; e++)
            {
                float a = e * Mathf.PI * 2f / 3f;
                Part(PrimitiveType.Cylinder, root.transform, new Vector3(Mathf.Cos(a) * 1f, 2.0f, Mathf.Sin(a) * 1f), new Vector3(1f, 0.6f, 1f), Vector3.zero, "DarkComposite");
            }
            Part(PrimitiveType.Cube, root.transform, new Vector3(0f, 9.5f, 2.12f), new Vector3(1.4f, 2.2f, 0.06f), Vector3.zero, "Steel");
            Floodlight(root.transform, new Vector3(0f, 6f, -2.2f), new Vector3(0f, 180f, 0f));
            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 8f, 0f);
            col.height = 16f;
            col.radius = 2.4f;
            var info = root.AddComponent<Interaction.ScienceInspectable>();
            info.key = "lander";
            info.title = "Ascent vehicle";
            info.facts = "Leaving Mars takes ~4.1 km/s to reach low orbit, about half of what leaving Earth takes. Its methane and oxygen propellant can be made on Mars from CO2 and water ice.";
            Save(root, "Lander");
        }

        /// <summary>Sintered-regolith walkway slab, used to connect facilities.</summary>
        public static void PathSlab()
        {
            var root = Root("PathSlab");
            Part(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.06f, 0f), new Vector3(3f, 0.12f, 1f), Vector3.zero, "Pad", true);
            Save(root, "PathSlab");
        }
    }
}
