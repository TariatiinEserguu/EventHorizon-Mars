using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using EventHorizon.Agriculture;
using EventHorizon.Astronaut;
using EventHorizon.Colony;
using EventHorizon.Interaction;
using EventHorizon.Space;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace EventHorizon.EditorTools
{
    /// <summary>
    /// Turns downloaded models into gameplay prefabs: real-world scale, base on the ground, colliders, URP materials,
    /// and the simulation component that makes each one work (a Kenney generator becomes the oxygen plant, the glass
    /// hangar becomes a greenhouse with hydroponic beds, and so on). Anything missing falls back to primitives or
    /// procedural meshes, so the build never stops on a missing pack.
    /// </summary>
    public static class AssetIntegrationBuilder
    {
        const string TP = "Assets/ThirdParty";
        const string Kenney = TP + "/Kenney/SpaceKit/Models/FBX format/";
        const string SSS = TP + "/SolarSystemScope/";
        const string PolyHaven = TP + "/PolyHaven/";
        const string Nasa = TP + "/NASA/";
        const string QuaterniusSpace = TP + "/Quaternius/UltimateSpaceKit";
        const string QuaterniusCrops = TP + "/Quaternius/UltimateCrops";

        public const string Out = "Assets/EventHorizon/Generated";
        const string MatDir = Out + "/Materials";
        const string MeshDir = Out + "/Meshes";
        const string PrefabDir = Out + "/Prefabs";
        const string CropDir = Out + "/Crops";
        const string TerrainDir = Out + "/Terrain";
        const string AnimDir = Out + "/Animation";

        static readonly List<string> s_report = new();

        // Real-world sizes (metres) the models are scaled to.
        const float AstronautHeight = 1.85f;

        [MenuItem("Event Horizon/Assets/1. Build Integrated Prefabs", priority = 1)]
        public static void BuildAll()
        {
            s_report.Clear();
            VisualSetup.SetupAll();
            foreach (var d in new[] { MatDir, MeshDir, PrefabDir, CropDir, TerrainDir, AnimDir }) Directory.CreateDirectory(d);
            AssetDatabase.Refresh();

            BuildMaterials();
            var crops = BuildCrops();
            BuildColonyPrefabs(crops);
            BuildTerminal();
            BuildOrbiter();
            BuildStation();
            BuildEnvironmentPrefabs();
            BuildSpacePrefabs();
            BuildAstronaut();
            BuildTerrainLayers();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Event Horizon] Prefabs built in " + Out + ":\n- " + string.Join("\n- ", s_report));
        }

        // ------------------------------------------------------------------ materials

        static Material Lit(string name, Color color, Texture2D baseMap = null, Texture2D normal = null,
                            float smoothness = 0.3f, float metallic = 0f, Vector2? tiling = null)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            if (baseMap != null) m.SetTexture("_BaseMap", baseMap);
            if (tiling.HasValue) m.SetTextureScale("_BaseMap", tiling.Value);
            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                m.EnableKeyword("_NORMALMAP");
            }
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Unlit(string name, Texture2D tex, Color color, bool transparent)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", color);
            if (transparent) MakeTransparent(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", (float)CullMode.Off);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        static Texture2D Tex(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{name}.mat");

        static void BuildMaterials()
        {
            var rock = PolyHaven + "Rock/rock_boulder_dry/rock_boulder_dry_";
            // Earth rock tinted toward Martian basalt with its oxidised dust coating.
            Lit("Rock_Mars", new Color(0.78f, 0.52f, 0.38f), Tex(rock + "diff_1k.jpg"), Tex(rock + "nor_gl_1k.jpg"), 0.12f);
            Lit("Ice_Water", new Color(0.82f, 0.92f, 1f), smoothness: 0.92f);
            Lit("SolarCell", new Color(0.05f, 0.08f, 0.2f), smoothness: 0.85f, metallic: 0.3f);
            Lit("Aluminium", new Color(0.82f, 0.83f, 0.85f), smoothness: 0.55f, metallic: 0.9f);
            Lit("PlanterWhite", new Color(0.92f, 0.92f, 0.9f), smoothness: 0.35f);

            var sky = Mat("Sky_Mars");
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, $"{MatDir}/Sky_Mars.mat");
            }
            // Daytime Mars: dust makes the sky butterscotch rather than blue.
            sky.SetColor("_SkyTint", new Color(0.85f, 0.58f, 0.38f));
            sky.SetColor("_GroundColor", new Color(0.45f, 0.28f, 0.18f));
            sky.SetFloat("_AtmosphereThickness", 0.55f);
            sky.SetFloat("_SunSize", 0.025f); // the sun looks ~2/3 as wide from Mars
            sky.SetFloat("_Exposure", 1.1f);

            var stars = Tex(SSS + "8k_stars_milky_way.jpg");
            var space = Mat("Sky_Stars");
            if (space == null)
            {
                space = new Material(Shader.Find("Skybox/Panoramic"));
                AssetDatabase.CreateAsset(space, $"{MatDir}/Sky_Stars.mat");
            }
            if (stars != null) space.SetTexture("_MainTex", stars);

            var bhShader = Shader.Find("EventHorizon/BlackHoleLensing");
            if (bhShader != null)
            {
                var bh = Mat("BlackHole");
                if (bh == null)
                {
                    bh = new Material(bhShader);
                    AssetDatabase.CreateAsset(bh, $"{MatDir}/BlackHole.mat");
                }
                if (stars != null) bh.SetTexture("_StarTex", stars);
                // Set explicitly: an existing material keeps old values even when the shader defaults change.
                bh.SetFloat("_DiskTemperature", 4300f);
                bh.SetFloat("_DiskBrightness", 3f);
                bh.SetFloat("_DiskInner", 3f);
                bh.SetFloat("_DiskOuter", 14f);
                bh.SetFloat("_RotationSpeed", 0.6f);
                bh.SetFloat("_StarExposure", 1f);
                EditorUtility.SetDirty(bh);
            }
            s_report.Add("materials: rock, ice, solar cell, Mars sky, star sky, black hole");
        }

        // ------------------------------------------------------------------ prefabs: helpers

        enum Fit { Height, Footprint }

        static GameObject LoadModel(string path)
        {
            if (path.EndsWith(".glb")) GlbTextureOptimizer.EnsureReadable(path);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        /// <summary>Compressed, size-capped copies of a .glb model's embedded textures (see GlbTextureOptimizer).</summary>
        static void OptimizeGlb(GameObject root, GameObject model, string key)
        {
            if (model != null && AssetDatabase.GetAssetPath(model).EndsWith(".glb")) GlbTextureOptimizer.Optimize(root, key);
        }

        static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Root object holding the model as a child, scaled to <paramref name="size"/> and set on the ground at the origin.</summary>
        static GameObject Wrap(string name, GameObject model, float size, Fit fit)
        {
            var root = new GameObject(name);
            if (model == null) return root;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.name = "Model";
            inst.transform.SetParent(root.transform, false);
            var b = WorldBounds(inst);
            float current = fit == Fit.Height ? b.size.y : Mathf.Max(b.size.x, b.size.z);
            if (current > 1e-4f) inst.transform.localScale *= size / current;
            b = WorldBounds(inst);
            inst.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);
            return root;
        }

        static void AddBoxCollider(GameObject root)
        {
            var b = WorldBounds(root);
            var bc = root.AddComponent<BoxCollider>();
            bc.center = root.transform.InverseTransformPoint(b.center);
            bc.size = b.size;
        }

        /// <summary>Walk-in buildings need their real shape, not a solid box.</summary>
        static void AddMeshColliders(GameObject root)
        {
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        }

        static GameObject Save(GameObject root, string name)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static string Source(GameObject model, string fallback) => model != null ? AssetDatabase.GetAssetPath(model) : fallback;

        // ------------------------------------------------------------------ colony

        static void BuildColonyPrefabs(CropDefinition[] crops)
        {
            // Realistic procedural structures (ColonyKit) replace the low-poly placeholders.
            ColonyKit.BuildMaterials();
            ColonyKit.SolarTracker();
            s_report.Add("SolarPanel: tracking table, 12 PV modules (29 m2) + SolarPanel");
            ColonyKit.PowerStation();
            s_report.Add("BatteryBank: power station, 2 battery containers (400 kWh), inverter, transformer + BatteryBank");
            ColonyKit.Habitat();
            s_report.Add("Habitat: two pressure modules, node, airlock, radiators + CrewLifeSupport");
            ColonyKit.OxygenPlant();
            s_report.Add("OxygenPlant: MOXIE-style skid (intake, compressor, SOXE stack) + OxygenGenerator");
            ColonyKit.Tanks("WaterTank", ResourceType.Water, 10000f, 4000f, 2, 4.2f, "HullWhite");
            ColonyKit.Tanks("OxygenTank", ResourceType.Oxygen, 500f, 150f, 1, 3.2f, "MLI");
            ColonyKit.FoodStore();
            ColonyKit.PathSlab();
            s_report.Add("WaterTank, OxygenTank, FoodStore, PathSlab");

            Machine("CommsDish", System.IO.File.Exists(Nasa + "DSN34/DSN34.glb") ? Nasa + "DSN34/DSN34.glb" : Kenney + "satelliteDish_large.fbx", 9f, go =>
            {
                var s = go.AddComponent<ScienceInspectable>();
                s.eventId = "inspect_relay";
                s.title = "Relay dish";
                s.facts = "Radio signals take 3 to 22 minutes to cross between Mars and Earth, depending on where the planets are. " +
                          "Every 26 months the Sun sits between them and blocks contact for about two weeks.";
            }, "ScienceInspectable (light-time facts)");

            // Hydroponic bed: a white trough with grow medium; the greenhouse lays these out.
            var root = new GameObject("HydroponicBed");
            Primitive(PrimitiveType.Cube, "Trough", root.transform, new Vector3(0f, 0.4f, 0f), new Vector3(2f, 0.8f, 0.8f), Mat("PlanterWhite"));
            Primitive(PrimitiveType.Cube, "Rail", root.transform, new Vector3(0f, 0.81f, 0f), new Vector3(2.04f, 0.04f, 0.84f), Mat("Aluminium"));
            var bed = root.AddComponent<HydroponicBed>();
            bed.bedSize = new Vector2(2f, 0.8f);
            if (crops.Length > 0) bed.crop = crops[0];
            AddBoxCollider(root);
            var bedPrefab = Save(root, "HydroponicBed");
            ColonyKit.Greenhouse(bedPrefab, crops);
            s_report.Add($"Greenhouse: glass dome, frame, LED bars + Greenhouse (6 beds, {crops.Length} crops)");
        }

        /// <summary>A free-standing control console with a glowing screen. The scene builder sets its type.</summary>
        static void BuildTerminal()
        {
            var root = new GameObject("Terminal");
            Primitive(PrimitiveType.Cube, "Pedestal", root.transform, new Vector3(0f, 0.5f, 0f), new Vector3(0.7f, 1f, 0.45f), Mat("Aluminium"));
            var screen = Primitive(PrimitiveType.Cube, "Screen", root.transform, new Vector3(0f, 1.15f, -0.08f), new Vector3(0.66f, 0.42f, 0.04f), Mat("TerminalScreen"));
            screen.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            AddBoxCollider(root);
            root.AddComponent<ColonyTerminal>();
            Save(root, "Terminal");
            s_report.Add("Terminal: console primitives + ColonyTerminal (type set per placement)");
        }

        /// <summary>NASA's Gateway, used as the orbital research station above Mars.</summary>
        static void BuildStation()
        {
            var model = LoadModel(Nasa + "Gateway/Gateway.glb");
            if (model == null) return;
            var root = Wrap("Station_Gateway", model, 4f, Fit.Footprint);
            OptimizeGlb(root, model, "Gateway");
            Save(root, "Station_Gateway");
            s_report.Add("Station_Gateway: NASA Gateway (orbital research station; protected wordmark painted over)");
        }

        /// <summary>The Mars Reconnaissance Orbiter, used in the main-menu sky.</summary>
        static void BuildOrbiter()
        {
            var model = LoadModel(Nasa + "MRO/MRO.glb");
            if (model == null) return;
            var root = Wrap("Orbiter_MRO", model, 9f, Fit.Footprint);
            OptimizeGlb(root, model, "MRO");
            Save(root, "Orbiter_MRO");
            s_report.Add("Orbiter_MRO: NASA Mars Reconnaissance Orbiter (menu sky)");
        }

        static void Machine(string name, string modelPath, float height, System.Action<GameObject> addComponents, string what)
        {
            var model = LoadModel(modelPath);
            var root = Wrap(name, model, height, Fit.Height);
            OptimizeGlb(root, model, name);
            if (model == null)
                Primitive(PrimitiveType.Cylinder, "Body", root.transform, new Vector3(0, height * 0.5f, 0), new Vector3(1.5f, height * 0.5f, 1.5f), Mat("Aluminium"));
            AddBoxCollider(root);
            addComponents(root);
            Save(root, name);
            s_report.Add($"{name}: {Source(model, "primitive")} + {what}");
        }

        // ------------------------------------------------------------------ environment

        static void BuildEnvironmentPrefabs()
        {
            // Ice deposit: procedural crystal mesh saved as an asset so the prefab can reference it.
            var iceMesh = SaveMesh(Procedural.RockMeshFactory.IceCluster(11), "IceCluster");
            var ice = new GameObject("IceDeposit");
            var vis = new GameObject("Crystals");
            vis.transform.SetParent(ice.transform, false);
            vis.AddComponent<MeshFilter>().sharedMesh = iceMesh;
            vis.AddComponent<MeshRenderer>().sharedMaterial = Mat("Ice_Water");
            vis.transform.localScale = Vector3.one * 1.5f;
            var dep = ice.AddComponent<IceDeposit>();
            dep.visual = vis.transform;
            AddBoxCollider(ice);
            Save(ice, "IceDeposit");

            for (int i = 0; i < 4; i++) SaveMesh(Procedural.RockMeshFactory.Boulder(100 + i, i == 0 ? 2 : 1), $"Boulder_{i}");

            ColonyKit.Lander();

            bool sev = System.IO.File.Exists(Nasa + "SEV/SEV.glb");
            Landmark("Rover", sev ? Nasa + "SEV/SEV.glb" : Kenney + "rover.fbx", sev ? 6f : 4f, "Space Exploration Vehicle",
                     "NASA's SEV concept: a pressurised rover that two astronauts can live in for up to two weeks, " +
                     "with suit ports on the back so they can step out without an airlock.");
            Landmark("Perseverance", Nasa + "Perseverance/Perseverance.glb", 3f, "Mars 2020 Perseverance Rover", null);
            // (eventId defaults to "inspect"; objectives can count landmark visits)
            Landmark("Ingenuity", Nasa + "Ingenuity/Ingenuity.glb", 1.2f, "Ingenuity Mars Helicopter",
                     "First powered flight on another planet (19 Apr 2021). The air is ~1% as dense as Earth's, so its " +
                     "1.2 m rotors spun at about 2,400 rpm. It flew 72 times.");
            s_report.Add("IceDeposit, 4 boulder meshes, Lander (Kenney rocket parts), Rover, Perseverance + Ingenuity (NASA)");
        }

        static void Landmark(string name, string path, float size, string title, string facts)
        {
            var model = LoadModel(path);
            if (model == null)
            {
                s_report.Add($"{name}: SKIPPED, {path} not imported (glTFast package missing or not fetched)");
                return;
            }
            var root = Wrap(name, model, size, Fit.Footprint);
            OptimizeGlb(root, model, name);
            AddBoxCollider(root);
            var s = root.AddComponent<ScienceInspectable>();
            s.title = title;
            if (facts != null) s.facts = facts;
            // NASA models are detailed (Perseverance ~200k triangles): cull them when tiny on screen.
            var renderers = root.GetComponentsInChildren<Renderer>();
            var lod = root.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(0.02f, renderers) });
            lod.RecalculateBounds();
            Save(root, name);
        }

        static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = $"{MeshDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            mesh.hideFlags = HideFlags.None;
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static void BuildTerrainLayers()
        {
            void Layer(string name, string slug, Color tint, float tile)
            {
                string path = $"{TerrainDir}/{name}.terrainlayer";
                var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                if (layer == null)
                {
                    layer = new TerrainLayer();
                    AssetDatabase.CreateAsset(layer, path);
                }
                string b = $"{PolyHaven}{(slug == "rock_boulder_dry" ? "Rock" : "MarsGround")}/{slug}/{slug}_";
                layer.diffuseTexture = Tex(b + "diff_1k.jpg");
                layer.normalMapTexture = Tex(b + "nor_gl_1k.jpg");
                layer.tileSize = new Vector2(tile, tile);
                layer.diffuseRemapMax = tint; // shift Earth soil toward Martian regolith colour
                layer.smoothness = 0.05f;      // fine dust is matte
                layer.metallic = 0f;
                EditorUtility.SetDirty(layer);
            }
            Layer("Mars_Regolith", "red_sand", new Color(1f, 0.82f, 0.68f, 1f), 4f);
            Layer("Mars_Cracked", "cracked_red_ground", new Color(0.95f, 0.72f, 0.55f, 1f), 6f);
            Layer("Mars_Gravel", "red_laterite_soil_stones", new Color(0.9f, 0.7f, 0.55f, 1f), 3f);
            Layer("Mars_Rock", "rock_boulder_dry", new Color(0.85f, 0.58f, 0.42f, 1f), 8f);
            s_report.Add("Terrain layers: regolith, cracked ground, gravel, rock (Poly Haven, Mars-tinted)");
        }

        // ------------------------------------------------------------------ crops

        struct CropData
        {
            public string name; public string[] keywords; public float days, yield, transpiration, o2, light, height;
            public Color color;
        }

        // Approximate controlled-environment values (NASA BVAD, NASA/TP-2015-218570, plus vertical-farming data).
        static readonly CropData[] s_crops =
        {
            new() { name = "Lettuce", keywords = new[] { "lettuce", "cabbage" }, days = 42, yield = 3.5f, transpiration = 4f, o2 = 0.02f, light = 65, height = 0.25f, color = new Color(0.45f, 0.8f, 0.3f) },
            new() { name = "Spinach", keywords = new[] { "spinach", "chard" }, days = 45, yield = 2f, transpiration = 3.5f, o2 = 0.02f, light = 60, height = 0.25f, color = new Color(0.2f, 0.55f, 0.2f) },
            new() { name = "Potato", keywords = new[] { "potato" }, days = 105, yield = 10f, transpiration = 4.5f, o2 = 0.03f, light = 110, height = 0.6f, color = new Color(0.3f, 0.6f, 0.25f) },
            new() { name = "Tomato", keywords = new[] { "tomato" }, days = 85, yield = 8f, transpiration = 5f, o2 = 0.03f, light = 120, height = 1.2f, color = new Color(0.3f, 0.6f, 0.2f) },
            new() { name = "Wheat", keywords = new[] { "wheat" }, days = 70, yield = 1.2f, transpiration = 5f, o2 = 0.04f, light = 200, height = 0.5f, color = new Color(0.85f, 0.75f, 0.35f) },
            new() { name = "Beans", keywords = new[] { "bean", "pea" }, days = 60, yield = 1.5f, transpiration = 4f, o2 = 0.025f, light = 90, height = 0.5f, color = new Color(0.35f, 0.65f, 0.25f) },
        };

        static CropDefinition[] BuildCrops()
        {
            var models = AssetDatabase.FindAssets("t:GameObject", new[] { QuaterniusCrops })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".fbx")).ToList();
            var result = new List<CropDefinition>();
            foreach (var c in s_crops)
            {
                string path = $"{CropDir}/{c.name}.asset";
                var def = AssetDatabase.LoadAssetAtPath<CropDefinition>(path);
                if (def == null)
                {
                    def = ScriptableObject.CreateInstance<CropDefinition>();
                    AssetDatabase.CreateAsset(def, path);
                }
                def.displayName = c.name;
                def.daysToHarvest = c.days;
                def.yieldKgPerM2 = c.yield;
                def.transpirationKgPerM2PerSol = c.transpiration;
                def.oxygenKgPerM2PerSol = c.o2;
                def.lightWattsPerM2 = c.light;
                def.matureHeight = c.height;
                def.fallbackColor = c.color;

                // Stage models: files whose name contains the keyword, ordered by the last number in the name.
                var matches = c.keywords
                    .Select(k => models.Where(p => Path.GetFileNameWithoutExtension(p).ToLowerInvariant().Contains(k)).ToList())
                    .FirstOrDefault(l => l.Count > 0) ?? new List<string>();
                matches = matches.OrderBy(StageNumber).ThenBy(p => p).ToList();
                var prefabs = matches.Select(LoadModel).Where(g => g != null).ToArray();
                def.stagePrefabs = prefabs.Length > 1 ? prefabs : new GameObject[0];
                def.maturePrefab = prefabs.Length > 0 ? prefabs[^1] : null;
                EditorUtility.SetDirty(def);
                result.Add(def);
                s_report.Add($"Crop {c.name}: {(prefabs.Length > 1 ? prefabs.Length + " stage models" : prefabs.Length == 1 ? "1 model, scaled by growth" : "primitive fallback")}");
            }
            return result.ToArray();
        }

        static int StageNumber(string path)
        {
            var m = Regex.Matches(Path.GetFileNameWithoutExtension(path), @"\d+");
            return m.Count > 0 ? int.Parse(m[m.Count - 1].Value) : 0;
        }

        // ------------------------------------------------------------------ space

        static void BuildSpacePrefabs()
        {
            SaveMesh(Procedural.MeshBuilders.UvSphere(128, 64), "PlanetSphere");
            // Lit (not unlit) so clouds darken on the night side; brightness becomes opacity via the importer.
            var clouds = Lit("Planet_EarthClouds", Color.white, Tex(SSS + "2k_earth_clouds.jpg"), null, 0.1f);
            clouds.shader = Shader.Find("Universal Render Pipeline/Lit");
            MakeTransparent(clouds);
            clouds.SetFloat("_Cull", (float)CullMode.Back);
            SpaceSceneBuilder.BuildPlanetPrefabs();

            foreach (var (preset, tex) in new[] { (IrregularMoonMesh.Preset.Phobos, "Phobos.jpg"), (IrregularMoonMesh.Preset.Deimos, "Deimos.jpg") })
            {
                var root = new GameObject(preset.ToString());
                root.AddComponent<MeshFilter>();
                root.AddComponent<MeshRenderer>().sharedMaterial = Lit("Moon_" + preset, Color.white, Tex(Nasa + "MarsMoons/" + tex), null, 0.05f);
                var moon = root.AddComponent<IrregularMoonMesh>();
                moon.preset = preset;
                moon.semiAxesKm = preset == IrregularMoonMesh.Preset.Phobos ? PlanetCatalog.PhobosAxesKm : PlanetCatalog.DeimosAxesKm;
                Save(root, "Moon_" + preset);
            }

            var bhMat = Mat("BlackHole");
            if (bhMat != null)
            {
                var bh = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bh.name = "BlackHole";
                Object.DestroyImmediate(bh.GetComponent<Collider>());
                bh.GetComponent<MeshRenderer>().sharedMaterial = bhMat;
                bh.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                bh.AddComponent<BlackHole>();
                bh.transform.localScale = Vector3.one * 100f; // r_s = 1.25 units; disk spans ~35 units
                Save(bh, "BlackHole");
            }
            s_report.Add("Planets (Sun..Neptune, Earth clouds, Saturn ring), Phobos + Deimos (procedural), BlackHole");
        }

        // ------------------------------------------------------------------ astronaut

        static void BuildAstronaut()
        {
            string rigged = AssetDatabase.FindAssets("Astronaut_ t:GameObject", new[] { QuaterniusSpace })
                .Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => p.EndsWith(".fbx"));
            var model = rigged != null ? LoadModel(rigged) : LoadModel(Kenney + "astronautA.fbx");
            var root = Wrap("Astronaut", model, AstronautHeight, Fit.Height);

            var cc = root.AddComponent<CharacterController>();
            cc.height = AstronautHeight;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, AstronautHeight * 0.5f, 0f);
            cc.slopeLimit = 40f;
            cc.stepOffset = 0.35f;
            root.AddComponent<Interactor>();
            root.AddComponent<SuitLifeSupport>();
            if (rigged != null)
            {
                root.AddComponent<AstronautSuit>();
                var anim = root.GetComponentInChildren<Animator>();
                if (anim == null) anim = root.transform.Find("Model").gameObject.AddComponent<Animator>();
                anim.applyRootMotion = false;
                anim.runtimeAnimatorController = BuildAnimator(rigged);
            }
            root.AddComponent<AstronautController>();
            Save(root, "Astronaut");
            s_report.Add(rigged != null
                ? $"Astronaut: {rigged} (animated) + visor/backpack, controller, suit O2"
                : "Astronaut: Kenney astronautA (static: fetch Quaternius Ultimate Space Kit for animations)");
        }

        static RuntimeAnimatorController BuildAnimator(string fbxPath)
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToList();
            AnimationClip Find(params string[] names) =>
                names.Select(n => clips.FirstOrDefault(c => c.name.Equals(n, System.StringComparison.OrdinalIgnoreCase))
                                  ?? clips.FirstOrDefault(c => c.name.ToLowerInvariant().EndsWith(n.ToLowerInvariant())))
                     .FirstOrDefault(c => c != null);

            string path = $"{AnimDir}/Astronaut.controller";
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = ctrl.layers[0].stateMachine;
            var map = new (string state, string[] clipNames)[]
            {
                ("Idle", new[] { "Idle" }),
                ("Walk", new[] { "Walk" }),
                ("Run", new[] { "Run" }),
                ("Jump", new[] { "Jump", "Jump_Idle" }),
                ("Interact", new[] { "Interact", "PickUp", "Yes", "Wave" }),
            };
            foreach (var (state, names) in map)
            {
                var clip = Find(names);
                if (clip == null) continue;
                var s = sm.AddState(state);
                s.motion = clip;
                if (state == "Idle") sm.defaultState = s;
            }
            return ctrl;
        }
    }
}
