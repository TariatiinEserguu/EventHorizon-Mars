using System.Collections.Generic;
using System.IO;
using EventHorizon.Interaction;
using EventHorizon.Procedural;
using EventHorizon.Space;
using EventHorizon.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace EventHorizon.EditorTools
{
    /// <summary>
    /// Builds the space environments:
    /// - SpaceLab (planetarium): the Sun and all eight planets with moons, an instanced asteroid belt, dwarf planet
    ///   Ceres, notable asteroids and a comet. Sizes and distances are compressed so everything fits in one view.
    /// - MarsOrbit: Mars at true relative scale with Phobos and Deimos, an orbital research station, the MRO
    ///   satellite and the ARES-1 landing site.
    /// - BlackHoleLab: the lensing black hole in a dark region, with the research console.
    /// Also builds the shared planet prefabs (custom Planet shader, three LODs, atmosphere shells, rings).
    /// </summary>
    public static class SpaceSceneBuilder
    {
        const string Out = AssetIntegrationBuilder.Out;
        const string SSS = "Assets/ThirdParty/SolarSystemScope/";
        const string Nasa = "Assets/ThirdParty/NASA/";
        static string MatPath(string n) => $"{Out}/Materials/{n}.mat";
        static string PrefabPath(string n) => $"{Out}/Prefabs/{n}.prefab";
        static Texture2D Tex(string p) => AssetDatabase.LoadAssetAtPath<Texture2D>(p);

        // ------------------------------------------------------------------ materials

        static Material Mat(string name, string shader)
        {
            var s = Shader.Find(shader);
            if (s == null) { Debug.LogWarning($"[Event Horizon] Missing shader {shader}"); return null; }
            var m = AssetDatabase.LoadAssetAtPath<Material>(MatPath(name));
            if (m == null) { m = new Material(s); AssetDatabase.CreateAsset(m, MatPath(name)); }
            else m.shader = s;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Sky presets: each region has its own cosmic backdrop.</summary>
        public static Material SkyPreset(string region)
        {
            var m = Mat("Sky_Deep_" + region, "EventHorizon/DeepSpaceSky");
            if (m == null) return null;
            m.SetTexture("_MilkyWay", Tex(SSS + "8k_stars_milky_way.jpg"));
            switch (region)
            {
                case "MarsOrbit":
                    m.SetFloat("_MilkyWayExposure", 0.32f); m.SetFloat("_Rotation", 40f);
                    m.SetColor("_NebulaA", new Color(0.45f, 0.14f, 0.10f)); m.SetColor("_NebulaB", new Color(0.10f, 0.22f, 0.30f));
                    m.SetFloat("_NebulaIntensity", 0.35f); m.SetFloat("_NebulaSeed", 1.9f); m.SetFloat("_DustDarkness", 0.55f);
                    break;
                case "BlackHole":
                    // A dark, quiet region: faint galaxies, little nebulosity, so the accretion disk dominates.
                    m.SetFloat("_MilkyWayExposure", 0.18f); m.SetFloat("_Rotation", 200f);
                    m.SetColor("_NebulaA", new Color(0.20f, 0.10f, 0.22f)); m.SetColor("_NebulaB", new Color(0.05f, 0.12f, 0.22f));
                    m.SetFloat("_NebulaIntensity", 0.22f); m.SetFloat("_NebulaSeed", 7.3f); m.SetFloat("_DustDarkness", 0.8f);
                    m.SetFloat("_BandWidth", 0.25f);
                    break;
                default:
                    m.SetFloat("_MilkyWayExposure", 0.38f); m.SetFloat("_Rotation", 0f);
                    m.SetColor("_NebulaA", new Color(0.55f, 0.12f, 0.10f)); m.SetColor("_NebulaB", new Color(0.08f, 0.32f, 0.38f));
                    m.SetFloat("_NebulaIntensity", 0.36f); m.SetFloat("_NebulaSeed", 3.7f); m.SetFloat("_DustDarkness", 0.6f);
                    break;
            }
            return m;
        }

        /// <summary>
        /// Normal map from a height map (or, for bodies without elevation data, from albedo brightness, a stand-in).
        /// Equirectangular: wraps around in longitude.
        /// </summary>
        static Texture2D NormalFromHeight(string sourcePath, string name, float strength)
        {
            string path = $"{Out}/Textures/Normal_{name}.png";
            if (!File.Exists(path) && File.Exists(sourcePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var src = new Texture2D(2, 2);
                src.LoadImage(File.ReadAllBytes(sourcePath));
                int w = Mathf.Min(src.width, 2048), h = Mathf.Min(src.height, 1024);
                var hgt = new float[w * h];
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var c = src.GetPixelBilinear((x + 0.5f) / w, (y + 0.5f) / h);
                    hgt[y * w + x] = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                }
                var outTex = new Texture2D(w, h, TextureFormat.RGB24, false);
                var px = new Color[w * h];
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float hl = hgt[y * w + (x + w - 1) % w], hr = hgt[y * w + (x + 1) % w];
                    float hd = hgt[Mathf.Max(0, y - 1) * w + x], hu = hgt[Mathf.Min(h - 1, y + 1) * w + x];
                    var n = new Vector3((hl - hr) * strength * w / 512f, (hd - hu) * strength * h / 256f, 1f).normalized;
                    px[y * w + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f);
                }
                outTex.SetPixels(px);
                outTex.Apply();
                File.WriteAllBytes(path, outTex.EncodeToPNG());
                Object.DestroyImmediate(src);
                Object.DestroyImmediate(outTex);
                AssetDatabase.ImportAsset(path);
                if (AssetImporter.GetAtPath(path) is TextureImporter ti)
                {
                    ti.textureType = TextureImporterType.NormalMap;
                    ti.wrapModeU = TextureWrapMode.Repeat;
                    ti.wrapModeV = TextureWrapMode.Clamp;
                    ti.maxTextureSize = 2048;
                    ti.SaveAndReimport();
                }
            }
            return Tex(path);
        }

        /// <summary>
        /// Mars relief from real laser altimetry: the MGS MOLA MEGDR grid (1440 x 720, big-endian int16 metres,
        /// row 0 = 90 N, column 0 = 0 E), shifted so 0 deg longitude sits at the texture centre like the albedo map.
        /// Slopes are exaggerated 12x; at true scale Mars' relief is invisible from orbit.
        /// </summary>
        static Texture2D MarsNormalFromMola()
        {
            const string src = Nasa + "MOLA/megt90n000cb.img";
            string path = $"{Out}/Textures/Normal_Mars_MOLA.png";
            if (!File.Exists(path) && File.Exists(src))
            {
                const int W = 1440, H = 720;
                var bytes = File.ReadAllBytes(src);
                var h = new float[W * H];
                for (int row = 0; row < H; row++)
                for (int col = 0; col < W; col++)
                {
                    int i = (row * W + col) * 2;
                    short v = (short)((bytes[i] << 8) | bytes[i + 1]);
                    int x = (col + W / 2) % W, y = H - 1 - row;
                    h[y * W + x] = v;
                }
                const float metresPerPixel = 2f * Mathf.PI * 3389500f / W;
                const float exaggeration = 12f;
                var px = new Color[W * H];
                for (int y = 0; y < H; y++)
                {
                    float lat = ((y + 0.5f) / H - 0.5f) * Mathf.PI;
                    float cosLat = Mathf.Max(Mathf.Cos(lat), 0.05f);
                    for (int x = 0; x < W; x++)
                    {
                        float dx = (h[y * W + (x + 1) % W] - h[y * W + (x + W - 1) % W]) / (2f * metresPerPixel * cosLat);
                        float dy = (h[Mathf.Min(H - 1, y + 1) * W + x] - h[Mathf.Max(0, y - 1) * W + x]) / (2f * metresPerPixel);
                        var n = new Vector3(-dx * exaggeration, -dy * exaggeration, 1f).normalized;
                        px[y * W + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f);
                    }
                }
                var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                tex.SetPixels(px);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                if (AssetImporter.GetAtPath(path) is TextureImporter ti)
                {
                    ti.textureType = TextureImporterType.NormalMap;
                    ti.wrapModeU = TextureWrapMode.Repeat;
                    ti.wrapModeV = TextureWrapMode.Clamp;
                    ti.SaveAndReimport();
                }
            }
            return Tex(path);
        }

        struct PlanetLook
        {
            public string albedo, normalSource, night, spec;
            public float normalStrength, nightIntensity, specular, atmo;
            public Color atmoColor, twilight, rimColor;
            public bool shell;
        }

        static PlanetLook Look(string body)
        {
            var l = new PlanetLook { normalStrength = 1f, atmoColor = new Color(0.35f, 0.55f, 1f), twilight = new Color(1f, 0.45f, 0.2f) };
            switch (body)
            {
                case "Mars":
                    l.albedo = SSS + "2k_mars.jpg"; l.normalSource = null; l.normalStrength = 1f; // relief from MOLA, below
                    l.atmo = 0.55f; l.atmoColor = new Color(0.9f, 0.6f, 0.42f); l.twilight = new Color(0.4f, 0.6f, 1f);
                    l.shell = true; l.rimColor = new Color(1.0f, 0.68f, 0.5f);
                    break;
                case "Earth":
                    l.albedo = SSS + "2k_earth_daymap.jpg"; l.night = SSS + "2k_earth_nightmap.jpg"; l.spec = SSS + "2k_earth_specular_map.tif";
                    l.normalSource = null; l.nightIntensity = 1.4f; l.specular = 0.45f;
                    l.atmo = 1.3f; l.atmoColor = new Color(0.32f, 0.58f, 1f); l.shell = true; l.rimColor = new Color(0.45f, 0.7f, 1.3f);
                    break;
                case "Moon":
                    l.albedo = Nasa + "MoonKit/lroc_color_2k.jpg"; l.normalSource = Nasa + "MoonKit/ldem_3_8bit.jpg"; l.normalStrength = 2.2f;
                    break;
                case "Mercury":
                    l.albedo = SSS + "2k_mercury.jpg"; l.normalSource = SSS + "2k_mercury.jpg"; l.normalStrength = 0.45f;
                    break;
                case "Venus":
                    l.albedo = SSS + "2k_venus_atmosphere.jpg"; l.atmo = 0.9f; l.atmoColor = new Color(1f, 0.86f, 0.55f);
                    l.shell = true; l.rimColor = new Color(1.2f, 1.0f, 0.65f);
                    break;
                case "Jupiter": l.albedo = SSS + "2k_jupiter.jpg"; l.atmo = 0.45f; l.atmoColor = new Color(0.95f, 0.85f, 0.7f); break;
                case "Saturn": l.albedo = SSS + "2k_saturn.jpg"; l.atmo = 0.4f; l.atmoColor = new Color(0.95f, 0.88f, 0.7f); break;
                case "Uranus": l.albedo = SSS + "2k_uranus.jpg"; l.atmo = 0.7f; l.atmoColor = new Color(0.6f, 0.9f, 0.95f); break;
                case "Neptune": l.albedo = SSS + "2k_neptune.jpg"; l.atmo = 0.7f; l.atmoColor = new Color(0.4f, 0.6f, 1f); break;
            }
            return l;
        }

        static Material PlanetMaterial(string body, PlanetCatalog.Body data)
        {
            var l = Look(body);
            var m = Mat("SpacePlanet_" + body, "EventHorizon/Planet");
            if (m == null) return null;
            m.SetTexture("_MainTex", Tex(l.albedo));
            var normal = body == "Earth" ? Tex(SSS + "2k_earth_normal_map.tif")
                       : body == "Mars" ? MarsNormalFromMola()
                       : l.normalSource != null ? NormalFromHeight(l.normalSource, body, 6f) : null;
            if (normal != null) { m.SetTexture("_NormalMap", normal); m.SetFloat("_NormalStrength", body == "Earth" ? 0.6f : l.normalStrength); }
            if (l.night != null) { m.SetTexture("_NightTex", Tex(l.night)); m.SetFloat("_NightIntensity", l.nightIntensity); }
            if (l.spec != null) { m.SetTexture("_SpecMask", Tex(l.spec)); m.SetFloat("_Specular", l.specular); }
            m.SetFloat("_AtmoStrength", l.atmo);
            m.SetColor("_AtmoColor", l.atmoColor);
            m.SetColor("_AtmoTwilight", l.twilight);
            if (data.ringOuterKm > 0f)
            {
                m.SetFloat("_RingInner", data.ringInnerKm / data.meanRadiusKm);
                m.SetFloat("_RingOuter", data.ringOuterKm / data.meanRadiusKm);
                m.SetTexture("_RingTex", Tex(SSS + "2k_saturn_ring_alpha.png"));
            }
            return m;
        }

        static Mesh SphereLod(int segments)
        {
            string path = $"{Out}/Meshes/PlanetSphere_{segments}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            var mesh = MeshBuilders.UvSphere(segments, segments / 2);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>Planet prefabs with the custom shader, three LODs, atmosphere shell, clouds (Earth) and rings (Saturn).</summary>
        public static void BuildPlanetPrefabs()
        {
            Directory.CreateDirectory($"{Out}/Textures");
            // Created here, early in the build, because the Mars colony scene uses them too.
            Mat("Starfield", "EventHorizon/Starfield");
            Mat("Galaxies", "EventHorizon/Galaxy");
            var lods = new[] { SphereLod(160), SphereLod(64), SphereLod(24) };
            foreach (var body in PlanetCatalog.All)
            {
                if (body.name == "Phobos" || body.name == "Deimos") continue;
                var root = new GameObject(body.name);
                var cb = root.AddComponent<CelestialBody>();
                cb.Apply(body);
                cb.meshDiameter = 2f;
                cb.kmPerUnit = 100f;

                Material mat;
                if (body.name == "Sun")
                {
                    mat = Mat("SpaceSun", "EventHorizon/SunSurface");
                    mat.SetTexture("_MainTex", Tex(SSS + "2k_sun.jpg"));
                }
                else mat = PlanetMaterial(body.name, body);

                var renderers = new List<Renderer[]>();
                for (int i = 0; i < lods.Length; i++)
                {
                    var go = new GameObject($"LOD{i}");
                    go.transform.SetParent(root.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = lods[i];
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = mat;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    renderers.Add(new Renderer[] { r });
                }
                var lodGroup = root.AddComponent<LODGroup>();
                lodGroup.SetLODs(new[] { new LOD(0.25f, renderers[0]), new LOD(0.04f, renderers[1]), new LOD(0.0015f, renderers[2]) });
                lodGroup.RecalculateBounds();

                var look = Look(body.name);
                if (look.shell)
                {
                    var rim = Mat("SpaceAtmo_" + body.name, "EventHorizon/AtmosphereRim");
                    rim.SetColor("_DayColor", look.rimColor);
                    rim.SetFloat("_Intensity", body.name == "Earth" ? 1.4f : 1.0f);
                    var shell = new GameObject("Atmosphere");
                    shell.transform.SetParent(root.transform, false);
                    shell.transform.localScale = Vector3.one * (body.name == "Earth" ? 1.03f : 1.02f);
                    shell.AddComponent<MeshFilter>().sharedMesh = lods[1];
                    var sr = shell.AddComponent<MeshRenderer>();
                    sr.sharedMaterial = rim;
                    sr.shadowCastingMode = ShadowCastingMode.Off;
                }
                if (body.name == "Earth")
                {
                    var clouds = new GameObject("Clouds");
                    clouds.transform.SetParent(root.transform, false);
                    clouds.transform.localScale = Vector3.one * 1.012f;
                    clouds.AddComponent<MeshFilter>().sharedMesh = lods[1];
                    var cr = clouds.AddComponent<MeshRenderer>();
                    cr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MatPath("Planet_EarthClouds"));
                    cr.shadowCastingMode = ShadowCastingMode.Off;
                    cb.clouds = clouds.transform;
                }
                if (body.ringOuterKm > 0f)
                {
                    var ringMesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{Out}/Meshes/{body.name}Ring.asset");
                    if (ringMesh == null)
                    {
                        ringMesh = MeshBuilders.Ring(body.ringInnerKm / body.meanRadiusKm, body.ringOuterKm / body.meanRadiusKm, 256);
                        AssetDatabase.CreateAsset(ringMesh, $"{Out}/Meshes/{body.name}Ring.asset");
                    }
                    var ringMat = Mat("SpaceRing_" + body.name, "EventHorizon/PlanetRing");
                    ringMat.SetTexture("_MainTex", Tex(SSS + "2k_saturn_ring_alpha.png"));
                    var ring = new GameObject("Ring");
                    ring.transform.SetParent(root.transform, false);
                    ring.AddComponent<MeshFilter>().sharedMesh = ringMesh;
                    var rr = ring.AddComponent<MeshRenderer>();
                    rr.sharedMaterial = ringMat;
                    rr.shadowCastingMode = ShadowCastingMode.Off;
                }
                if (body.name == "Sun")
                {
                    var glowMat = Mat("SpaceSunGlow", "EventHorizon/SunGlow");
                    var glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    glow.name = "Glow";
                    Object.DestroyImmediate(glow.GetComponent<Collider>());
                    glow.transform.SetParent(root.transform, false);
                    glow.transform.localScale = Vector3.one * 7f;
                    glow.GetComponent<MeshRenderer>().sharedMaterial = glowMat;
                    glow.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
                var info = root.AddComponent<ScienceInspectable>();
                info.title = body.name;
                info.facts = Facts(body.name);
                info.eventId = "observe_" + body.name.ToLowerInvariant();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath("Planet_" + body.name));
                Object.DestroyImmediate(root);
            }
        }

        static string Facts(string body) => body switch
        {
            "Sun" => "A G2V main-sequence star, 1.39 million km across, surface ~5,772 K. It turns once every ~25 days at the equator and ~35 days near the poles.",
            "Mercury" => "Smallest planet: 4,879 km across. No real atmosphere, so its surface swings from ~430 C by day to ~-180 C at night. A year lasts 88 days.",
            "Venus" => "Nearly Earth's size, but buried under a 92-bar CO2 atmosphere with sulfuric-acid clouds. The surface is ~465 C, hotter than Mercury. It spins backwards.",
            "Earth" => "The only world known to host life. 71% ocean. Its nitrogen-oxygen atmosphere scatters blue light; at night the cities glow.",
            "Moon" => "3,474 km across, tidally locked to Earth. The dark maria are ancient lava plains; the bright highlands are heavily cratered crust. Relief here comes from LRO laser altimetry.",
            "Mars" => "6,779 km across, with a thin 0.6 kPa CO2 atmosphere. Home of Olympus Mons (~22 km high) and Valles Marineris (~4,000 km long). Polar caps of water ice and CO2 frost.",
            "Jupiter" => "Largest planet: 11 Earths across, mostly hydrogen and helium. The Great Red Spot is a storm wider than Earth that has raged for centuries.",
            "Saturn" => "Its rings are mostly water ice, 280,000 km across but often only ~10 m thick. Saturn is less dense than water.",
            "Uranus" => "An ice giant tipped on its side (98 deg axial tilt). Methane in its atmosphere absorbs red light, giving it a cyan colour.",
            "Neptune" => "The windiest planet: supersonic winds up to ~2,100 km/h. Methane makes it blue. One orbit takes 165 years.",
            _ => "",
        };

        // ------------------------------------------------------------------ shared scene pieces

        static void SpaceBasics(string skyRegion, out Light sunLight)
        {
            RenderSettings.skybox = SkyPreset(skyRegion);
            RenderSettings.fog = false; // space is a vacuum
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.012f, 0.012f, 0.016f);
            sunLight = new GameObject("Sunlight").AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.color = new Color(1f, 0.97f, 0.92f);
            sunLight.intensity = 1.6f;
            sunLight.shadows = LightShadows.Soft;

            var stars = new GameObject("Starfield").AddComponent<Starfield>();
            stars.starMaterial = Mat("Starfield", "EventHorizon/Starfield");
            stars.galaxyMaterial = Mat("Galaxies", "EventHorizon/Galaxy");
            if (skyRegion == "BlackHole") { stars.starCount = 30000; stars.limitingMagnitude = 9f; stars.bandConcentration = 0.3f; }

            VisualSetup.AddGlobalVolume();
            new GameObject("Session").AddComponent<Game.SessionBootstrap>();
        }

        static GameObject Place(string prefab, Vector3 pos)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(prefab));
            if (asset == null) { Debug.LogWarning($"[Event Horizon] Missing prefab {prefab}"); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.transform.position = pos;
            return go;
        }

        static Camera MakeCamera(out OrbitCamera orbit, float far = 9000f)
        {
            var cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = far;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.gameObject.AddComponent<AudioListener>();
            orbit = cam.gameObject.AddComponent<OrbitCamera>();
            return cam;
        }

        static Material[] AsteroidMaterials()
        {
            var rock = "Assets/ThirdParty/PolyHaven/Rock/rock_boulder_dry/rock_boulder_dry_";
            Material Make(string name, Color c, float metal, float smooth)
            {
                var m = Mat(name, "Universal Render Pipeline/Lit");
                m.SetTexture("_BaseMap", Tex(rock + "diff_1k.jpg"));
                m.SetTexture("_BumpMap", Tex(rock + "nor_gl_1k.jpg"));
                m.EnableKeyword("_NORMALMAP");
                m.SetColor("_BaseColor", c);
                m.SetFloat("_Metallic", metal);
                m.SetFloat("_Smoothness", smooth);
                m.enableInstancing = true;
                return m;
            }
            return new[]
            {
                Make("Asteroid_Carbonaceous", new Color(0.32f, 0.3f, 0.29f), 0f, 0.08f),
                Make("Asteroid_Silicate", new Color(0.62f, 0.55f, 0.47f), 0f, 0.15f),
                Make("Asteroid_Metallic", new Color(0.7f, 0.68f, 0.64f), 0.75f, 0.4f),
            };
        }

        static GameObject NamedRock(string name, Vector3 pos, float size, int seed, Material mat, string facts)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var mesh = RockMeshFactory.Asteroid(seed, 5, 0.3f);
            string path = $"{Out}/Meshes/Asteroid_{name.Replace(' ', '_')}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.transform.localScale = Vector3.one * size;
            var info = go.AddComponent<ScienceInspectable>();
            info.title = name;
            info.facts = facts;
            return go;
        }

        // ------------------------------------------------------------------ planetarium

        [MenuItem("Event Horizon/Assets/5. Build Space Scenes", priority = 5)]
        public static void BuildAll()
        {
            BuildPlanetarium();
            BuildMarsOrbit();
            BuildBlackHoleLab();
        }

        public static void BuildPlanetarium()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SpaceBasics("Planetarium", out var light);
            new GameObject("Clock").AddComponent<Core.MarsEnvironment>().timeScale = 900f;

            var sun = Place("Planet_Sun", Vector3.zero);
            SetSize(sun, 26f);
            var sunLight = sun.AddComponent<SunLight>();
            sunLight.directional = light;
            sunLight.sunRadius = 26f;
            var glowT = sun.transform.Find("Glow");
            if (glowT != null) sunLight.glow = glowT.GetComponent<Renderer>();

            // Not to scale: radius ~ R^0.45 and distances compressed, so all planets fit and stay recognisable.
            var plan = new (string name, float dist, float hours, float angle)[]
            {
                ("Mercury", 70f, 2111f, 30f), ("Venus", 100f, 5393f, 140f), ("Earth", 135f, 8766f, 250f), ("Mars", 175f, 16488f, 330f),
                ("Jupiter", 380f, 103982f, 60f), ("Saturn", 520f, 258221f, 175f), ("Uranus", 650f, 736455f, 280f), ("Neptune", 760f, 1444554f, 20f),
            };
            var targets = new List<Transform> { sun.transform };
            var distances = new List<float> { 130f };
            var occluders = new List<Transform>();
            GameObject earth = null, mars = null;
            foreach (var (name, dist, hours, angle) in plan)
            {
                var p = Place("Planet_" + name, Vector3.zero);
                if (p == null) continue;
                var data = System.Array.Find(PlanetCatalog.All, b => b.name == name);
                float radius = 0.06f * Mathf.Pow(data.meanRadiusKm, 0.45f);
                SetSize(p, radius);
                var orbit = p.AddComponent<OrbitMotion>();
                orbit.center = sun.transform;
                orbit.radius = dist;
                orbit.periodHours = hours;
                orbit.timeScale = 260f;
                orbit.startAngle = angle;
                p.transform.position = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * dist;
                targets.Add(p.transform);
                distances.Add(radius * (name == "Saturn" ? 9f : 5f));
                occluders.Add(p.transform);
                if (name == "Earth") earth = p;
                if (name == "Mars") mars = p;
            }
            sunLight.occluders = occluders;

            if (earth != null)
            {
                var moon = Place("Planet_Moon", earth.transform.position + Vector3.right * 9f);
                SetSize(moon, 0.06f * Mathf.Pow(1737.4f, 0.45f));
                var mo = moon.AddComponent<OrbitMotion>();
                mo.center = earth.transform; mo.radius = 9f; mo.periodHours = 655.7f; mo.timeScale = 260f; mo.tidallyLocked = true;
                targets.Insert(4, moon.transform);
                distances.Insert(4, 6f);
                occluders.Add(moon.transform);
            }
            if (mars != null)
            {
                foreach (var (preset, r, h) in new[] { ("Moon_Phobos", 4.5f, 7.65f), ("Moon_Deimos", 7f, 30.3f) })
                {
                    var m = Place(preset, mars.transform.position + Vector3.right * r);
                    if (m == null) continue;
                    m.transform.localScale = Vector3.one * 0.03f;
                    var mo = m.AddComponent<OrbitMotion>();
                    mo.center = mars.transform; mo.radius = r; mo.periodHours = h; mo.timeScale = 260f; mo.tidallyLocked = true;
                }
            }

            // Asteroid belt between Mars and Jupiter, plus notable bodies to discover.
            var belt = new GameObject("Asteroid Belt").AddComponent<AsteroidField>();
            belt.radiusRange = new Vector2(235f, 300f);
            belt.thickness = 9f;
            belt.count = 5000;
            belt.sizeRange = new Vector2(0.05f, 1.1f);
            belt.orbitSpeed = 0.35f;
            belt.lodDistance = 60f;
            belt.drawDistance = 900f;
            belt.materials = AsteroidMaterials();
            var mats = belt.materials;
            var ceres = Place("Planet_Moon", Quaternion.Euler(0f, 205f, 0f) * Vector3.forward * 262f);
            if (ceres != null)
            {
                ceres.name = "Ceres";
                SetSize(ceres, 1.6f);
                var info = ceres.GetComponent<ScienceInspectable>();
                info.title = "Ceres (dwarf planet)";
                info.facts = "The largest object in the asteroid belt, 940 km across, holding about a third of the belt's mass. NASA's Dawn found bright salt deposits in Occator crater from briny water below. (Shown with lunar textures as a stand-in.)";
                foreach (var r in ceres.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mats[0];
                targets.Add(ceres.transform);
                distances.Add(8f);
            }
            var psyche = NamedRock("16 Psyche", Quaternion.Euler(0f, 228f, 2f) * Vector3.forward * 270f, 1.4f, 16, mats[2],
                "A metal-rich asteroid about 220 km across, perhaps the exposed core of a shattered protoplanet. NASA's Psyche spacecraft arrives in 2029.");
            var bennu = NamedRock("101955 Bennu", Quaternion.Euler(0f, 190f, -1f) * Vector3.forward * 248f, 0.6f, 101, mats[0],
                "A 490 m carbon-rich rubble pile, loosely held together by its own weak gravity. OSIRIS-REx brought 121.6 g of it to Earth in 2023.");
            targets.Add(psyche.transform); distances.Add(7f);
            targets.Add(bennu.transform); distances.Add(4f);

            // Comet on an inclined orbit with ion and dust tails.
            var comet = BuildComet(sun.transform);
            targets.Add(comet.transform); distances.Add(45f);

            var cam = MakeCamera(out var orbitCam);
            orbitCam.sun = sun.transform;
            orbitCam.targets = targets.ToArray();
            orbitCam.targetDistances = distances.ToArray();
            orbitCam.targetIndex = 3; // Earth
            orbitCam.distance = distances[3];
            orbitCam.pitch = 12f;
            orbitCam.yaw = -30f;
            EditorSceneManager.SaveScene(scene, "Assets/EventHorizon/Scenes/SpaceLab.unity");
            Debug.Log("[Event Horizon] Saved Scenes/SpaceLab.unity (planetarium)");
        }

        static void SetSize(GameObject go, float radiusUnits)
        {
            if (go == null) return;
            var cb = go.GetComponent<CelestialBody>();
            if (cb != null) cb.kmPerUnit = cb.meanRadiusKm / radiusUnits;
            else go.transform.localScale = Vector3.one * radiusUnits * 2f;
        }

        static GameObject BuildComet(Transform sun)
        {
            var comet = new GameObject("Comet");
            var nucleus = new GameObject("Nucleus");
            nucleus.transform.SetParent(comet.transform, false);
            var mesh = RockMeshFactory.Asteroid(67, 4, 0.35f);
            AssetDatabase.DeleteAsset($"{Out}/Meshes/CometNucleus.asset");
            AssetDatabase.CreateAsset(mesh, $"{Out}/Meshes/CometNucleus.asset");
            nucleus.AddComponent<MeshFilter>().sharedMesh = mesh;
            nucleus.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MatPath("Asteroid_Carbonaceous"));
            nucleus.transform.localScale = Vector3.one * 0.5f;
            var c = comet.AddComponent<Comet>();
            c.sun = sun;
            c.ionTail = Tail(comet.transform, "Ion Tail", new Color(0.35f, 0.6f, 1f, 0.18f), 22f, 1.1f, 3f);
            c.dustTail = Tail(comet.transform, "Dust Tail", new Color(1f, 0.9f, 0.72f, 0.14f), 12f, 2.2f, 14f);
            var orbit = comet.AddComponent<OrbitMotion>();
            orbit.center = sun;
            orbit.radius = 200f;
            orbit.inclination = 24f;
            orbit.periodHours = 30000f;
            orbit.timeScale = 260f;
            orbit.startAngle = 110f;
            comet.transform.position = Quaternion.Euler(24f, 110f, 0f) * Vector3.forward * 200f;
            var info = comet.AddComponent<ScienceInspectable>();
            info.title = "Comet";
            info.facts = "A dirty snowball a few km across. Near the Sun its ices sublimate: the blue ion tail is pushed straight away from the Sun by the solar wind, the curved dust tail trails along the orbit.";
            return comet;
        }

        static ParticleSystem Tail(Transform parent, string name, Color color, float speed, float size, float spread)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = 1.6f;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 600;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = spread;
            shape.radius = 0.2f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                         new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.4f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            var m = AssetDatabase.LoadAssetAtPath<Material>(MatPath("CometTail"));
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(m, MatPath("CometTail"));
            }
            var dust = AssetDatabase.LoadAssetAtPath<Material>(MatPath("DustMotes"));
            if (dust != null) m.CopyPropertiesFromMaterial(dust);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Blend", 2f); // additive, weighted by the soft sprite's alpha (One/One would draw squares)
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.One);
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        // ------------------------------------------------------------------ Mars orbit

        public static void BuildMarsOrbit()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SpaceBasics("MarsOrbit", out var light);
            var clock = new GameObject("Clock").AddComponent<Core.MarsEnvironment>();
            clock.timeScale = 120f;
            clock.driveAmbient = false;

            // True relative scale: Mars radius 60 units (56.5 km per unit). Moons and spacecraft are enlarged
            // (noted on their info cards), otherwise they would be sub-pixel specks.
            var mars = Place("Planet_Mars", Vector3.zero);
            SetSize(mars, 60f);
            // Distant Sun: lighting and the visible disc (apparent size from Mars is ~0.35 deg).
            var sun = Place("Planet_Sun", new Vector3(-5200f, 900f, 2400f));
            SetSize(sun, 18f);
            var sl = sun.AddComponent<SunLight>();
            sl.directional = light;
            sl.sunRadius = 18f;
            var g = sun.transform.Find("Glow");
            if (g != null) sl.glow = g.GetComponent<Renderer>();

            var targets = new List<Transform> { mars.transform };
            var distances = new List<float> { 190f };

            // ARES-1 landing site beacon in Jezero crater (18.38 N, 77.58 E).
            float lat = 18.38f * Mathf.Deg2Rad, lon = 77.58f * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Cos(lat) * Mathf.Sin(lon), Mathf.Sin(lat), -Mathf.Cos(lat) * Mathf.Cos(lon));
            var site = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            site.name = "ARES-1 Landing Site";
            Object.DestroyImmediate(site.GetComponent<Collider>());
            site.transform.SetParent(mars.transform, false);
            site.transform.localPosition = dir * 1.004f;
            site.transform.localScale = Vector3.one * 0.012f;
            site.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MatPath("Beacon"));
            site.AddComponent<BeaconPulse>();
            var siteInfo = site.AddComponent<ScienceInspectable>();
            siteInfo.title = "ARES-1 // Jezero crater";
            siteInfo.facts = "A 45 km crater that once held a lake. Its river delta is where Perseverance has searched for signs of ancient life since 2021, and where the ARES-1 colony stands in 2189.";
            targets.Add(site.transform);
            distances.Add(45f);

            foreach (var (prefab, r, hours, label, facts) in new[]
            {
                ("Moon_Phobos", 166f, 7.65f, "Phobos", "26.8 x 22.4 x 18.4 km, orbiting just 6,000 km above Mars, faster than Mars spins, so it rises in the west. Stickney crater is 9 km wide. (Shown 10x larger.)"),
                ("Moon_Deimos", 415f, 30.3f, "Deimos", "The smaller moon, 15 x 12 x 10 km, smoothed by a thick layer of regolith. From Mars it looks like a bright star. (Shown 10x larger.)"),
            })
            {
                var m = Place(prefab, Vector3.right * r);
                if (m == null) continue;
                m.transform.localScale = Vector3.one * (10f * 1f / 56.5f) * 1f;
                var mo = m.AddComponent<OrbitMotion>();
                mo.center = mars.transform; mo.radius = r; mo.periodHours = hours; mo.timeScale = 1f; mo.tidallyLocked = true;
                mo.inclination = prefab.Contains("Phobos") ? 1.1f : 1.8f;
                mo.startAngle = prefab.Contains("Phobos") ? -30f : 50f;
                m.transform.position = Quaternion.Euler(mo.inclination, mo.startAngle, 0f) * Vector3.forward * r;
                var info = m.AddComponent<ScienceInspectable>();
                info.title = label; info.facts = facts;
                targets.Add(m.transform);
                distances.Add(prefab.Contains("Phobos") ? 9f : 6f);
                sl.occluders.Add(m.transform);
            }
            sl.occluders.Add(mars.transform);

            var station = Place("Station_Gateway", Vector3.zero);
            if (station != null)
            {
                var so = station.AddComponent<OrbitMotion>();
                so.center = mars.transform; so.radius = 84f; so.periodHours = 3.2f; so.timeScale = 0.25f; so.inclination = 25f; so.startAngle = -15f;
                station.transform.position = Quaternion.Euler(25f, -15f, 0f) * Vector3.forward * 84f;
                var info = station.GetComponent<ScienceInspectable>();
                if (info == null) info = station.AddComponent<ScienceInspectable>();
                info.title = "ARES Orbital Research Station";
                info.facts = "A Gateway-class station in a 1,400 km orbit: power and propulsion element, habitation modules and a docking hub for landers to and from Jezero. (Model: NASA Gateway; shown enlarged.)";
                targets.Add(station.transform);
                distances.Add(7f);
            }
            var mro = Place("Orbiter_MRO", Vector3.zero);
            if (mro != null)
            {
                mro.transform.localScale = Vector3.one * 0.12f;
                var mo = mro.AddComponent<OrbitMotion>();
                mo.center = mars.transform; mo.radius = 66f; mo.periodHours = 1.87f; mo.timeScale = 0.25f; mo.inclination = 93f; mo.startAngle = 20f;
                mro.transform.position = Quaternion.Euler(93f, 20f, 0f) * Vector3.forward * 66f;
                var info = mro.AddComponent<ScienceInspectable>();
                info.title = "Mars Reconnaissance Orbiter";
                info.facts = "In orbit since 2006 in a near-polar 255 x 320 km orbit. Its HiRISE camera resolves objects ~1 m across; it also relays rover data to Earth. (Shown enlarged.)";
                targets.Add(mro.transform);
                distances.Add(3f);
            }

            var cam = MakeCamera(out var orbitCam);
            orbitCam.sun = sun.transform;
            orbitCam.targets = targets.ToArray();
            orbitCam.targetDistances = distances.ToArray();
            orbitCam.distance = 190f;
            orbitCam.pitch = 10f;
            orbitCam.yaw = 200f;
            EditorSceneManager.SaveScene(scene, "Assets/EventHorizon/Scenes/MarsOrbit.unity");
            Debug.Log("[Event Horizon] Saved Scenes/MarsOrbit.unity");
        }

        // ------------------------------------------------------------------ black hole

        public static void BuildBlackHoleLab()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SpaceBasics("BlackHole", out var light);
            light.intensity = 0.15f;
            new GameObject("Clock").AddComponent<Core.MarsEnvironment>().driveAmbient = false;
            var bh = Place("BlackHole", Vector3.zero);
            var cam = MakeCamera(out var orbitCam, 6000f);
            if (bh != null)
            {
                orbitCam.targets = new[] { bh.transform };
                var info = bh.AddComponent<ScienceInspectable>();
                info.title = "Black hole";
                info.facts = "Light bends around it: the far side of the accretion disk appears over the top, an Einstein ring wraps beneath, and the approaching side of the disk shines brighter (Doppler beaming).";
            }
            orbitCam.distance = 58f;
            orbitCam.distanceRange = new Vector2(20f, 600f);
            orbitCam.pitch = 4f;
            orbitCam.yaw = 0f;
            new GameObject("Research Console").AddComponent<BlackHoleLabUI>();
            EditorSceneManager.SaveScene(scene, "Assets/EventHorizon/Scenes/BlackHoleLab.unity");
            Debug.Log("[Event Horizon] Saved Scenes/BlackHoleLab.unity");
        }
    }
}
