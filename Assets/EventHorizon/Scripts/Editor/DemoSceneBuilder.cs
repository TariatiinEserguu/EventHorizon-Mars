using EventHorizon.Astronaut;
using EventHorizon.Colony;
using EventHorizon.Core;
using EventHorizon.Interaction;
using EventHorizon.Procedural;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace EventHorizon.EditorTools
{
    /// <summary>
    /// Builds two playable test scenes from the generated prefabs, to check the integration end to end:
    /// - MarsColony: terrain, rocks, a small working colony (power, oxygen, water, food, greenhouse) and the astronaut.
    /// - SpaceLab: star skybox, black hole, Mars with Phobos and Deimos, Earth, Saturn.
    /// </summary>
    public static class DemoSceneBuilder
    {
        const string SceneDir = "Assets/EventHorizon/Scenes";
        static string Prefab(string name) => $"{AssetIntegrationBuilder.Out}/Prefabs/{name}.prefab";
        static string MatPath(string name) => $"{AssetIntegrationBuilder.Out}/Materials/{name}.mat";

        static GameObject Place(string prefab, Vector3 pos, float yaw = 0f, Transform parent = null)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab(prefab));
            if (asset == null)
            {
                Debug.LogWarning($"[Event Horizon] {prefab} prefab missing: run 'Build Integrated Prefabs' first.");
                return null;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            return go;
        }

        [MenuItem("Event Horizon/Assets/2. Build Mars Colony Scene", priority = 2)]
        public static void BuildMarsColony()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            System.IO.Directory.CreateDirectory(SceneDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Sun + Mars clock
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.color = new Color(1f, 0.95f, 0.88f);
            var env = new GameObject("Mars Environment").AddComponent<MarsEnvironment>();
            env.sun = sun;

            var sky = AssetDatabase.LoadAssetAtPath<Material>(MatPath("Sky_MarsDynamic"))
                      ?? AssetDatabase.LoadAssetAtPath<Material>(MatPath("Sky_Mars"));
            if (sky != null) RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight; // driven by MarsEnvironment through the day
            VisualSetup.AddGlobalVolume();
            new GameObject("Session").AddComponent<Game.SessionBootstrap>();
            RenderSettings.fog = true; // suspended dust
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.78f, 0.58f, 0.42f);
            RenderSettings.fogDensity = 0.0012f;

            // Terrain: 2 km square, centred on the origin.
            var data = new TerrainData();
            AssetDatabase.CreateAsset(data, $"{AssetIntegrationBuilder.Out}/Terrain/MarsColony_TerrainData.asset");
            var terrainGo = Terrain.CreateTerrainGameObject(data);
            terrainGo.name = "Mars Terrain";
            var terrain = terrainGo.GetComponent<Terrain>();
            var gen = terrainGo.AddComponent<MarsTerrainGenerator>();
            gen.layers = new[]
            {
                Layer("Mars_Regolith"), Layer("Mars_Cracked"), Layer("Mars_Gravel"), Layer("Mars_Rock"),
            };
            if (gen.layers[0] == null) gen.layers = new TerrainLayer[0];
            gen.Generate();
            // Centre the terrain on the origin with the colony pad at y = 0.
            terrainGo.transform.position = new Vector3(-gen.size.x * 0.5f, -data.GetInterpolatedHeight(0.5f, 0.5f), -gen.size.z * 0.5f);
            terrain.basemapDistance = 300f;
            terrain.heightmapPixelError = 4f;
            EditorUtility.SetDirty(data);

            // Colony on the flattened pad.
            var colony = new GameObject("Colony");
            colony.AddComponent<ColonyGrid>();
            var ct = colony.transform;
            // Life-support district around the habitat (airlock faces the landing pad to the south).
            Place("Habitat", new Vector3(0f, 0f, 24f), 0f, ct);
            Place("Greenhouse", new Vector3(-30f, 0f, 6f), 90f, ct);
            Place("OxygenPlant", new Vector3(16f, 0f, 6f), 0f, ct);
            Place("OxygenTank", new Vector3(24f, 0f, 12f), 0f, ct);
            Place("WaterTank", new Vector3(23f, 0f, -1f), 0f, ct);
            Place("FoodStore", new Vector3(-12f, 0f, 16f), 90f, ct);
            Place("CommsDish", new Vector3(-24f, 0f, 34f), 200f, ct);

            // Power plant: a solar field of 6 x 6 tracking tables (~1,040 m2 of PV) east of the colony,
            // with the battery/inverter station between the field and the colony.
            Place("BatteryBank", new Vector3(40f, 0f, -22f), 90f, ct);
            for (int x = 0; x < 6; x++)
            for (int z = 0; z < 6; z++)
                Place("SolarPanel", new Vector3(52f + x * 9f, 0f, -26f + z * 7f), 0f, ct);

            Place("Lander", new Vector3(-42f, 0f, -42f), 30f, ct);
            Place("Rover", new Vector3(-8f, 0f, -12f), 60f, ct);

            // Sintered-regolith walkways from the pad to each facility.
            void Walk(Vector3 a, Vector3 b)
            {
                var d = b - a;
                int n = Mathf.Max(1, Mathf.RoundToInt(d.magnitude / 3.2f));
                float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg + 90f;
                for (int i = 0; i <= n; i++) Place("PathSlab", Vector3.Lerp(a, b, i / (float)n), yaw, ct);
            }
            Walk(new Vector3(0f, 0f, -4f), new Vector3(0f, 0f, 17f));
            Walk(new Vector3(0f, 0f, 2f), new Vector3(10f, 0f, 2f));
            Walk(new Vector3(0f, 0f, 2f), new Vector3(-19f, 0f, 3f));
            Walk(new Vector3(12f, 0f, 2f), new Vector3(34f, 0f, -18f));

            // Facility terminals, each beside its facility, facing the walkway.
            Terminal(TerminalType.Command, new Vector3(-3.5f, 0f, 16f), 180f, ct);
            Terminal(TerminalType.Oxygen, new Vector3(10f, 0f, 3.5f), 270f, ct);
            Terminal(TerminalType.Water, new Vector3(19f, 0f, -3.5f), 180f, ct);
            Terminal(TerminalType.Solar, new Vector3(36f, 0f, -16f), 225f, ct);
            Terminal(TerminalType.Greenhouse, new Vector3(-18f, 0f, 0.5f), 90f, ct);

            // The story's places, colonists, extra terminals and the cinematic director.
            StoryKit.Build(terrain, ct);

            // Weather: dust motes around the camera, fog and occasional storms.
            var weather = new GameObject("Weather").AddComponent<DustStormSystem>();
            weather.dust = DustParticles(weather.transform);

            // Sky reflections for the whole colony area.
            var probeGo = new GameObject("Sky Reflections");
            probeGo.transform.position = new Vector3(10f, 20f, 0f);
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.size = new Vector3(600f, 200f, 600f);
            probe.resolution = 128;
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            probe.cullingMask = 0; // sky only: cheap, and no interior self-reflections
            probeGo.AddComponent<SkyReflections>();

            // Night sky: the procedural star field, fading out in daylight and dust.
            var stars = new GameObject("Starfield").AddComponent<Space.Starfield>();
            stars.starMaterial = AssetDatabase.LoadAssetAtPath<Material>(MatPath("Starfield"));
            stars.galaxyMaterial = AssetDatabase.LoadAssetAtPath<Material>(MatPath("Galaxies"));
            stars.fadeWithMarsDaylight = true;
            stars.intensity = 0.8f;

            // Exploration targets beyond the pad.
            Place("Perseverance", new Vector3(140f, 0f, 90f), 220f);
            Place("Ingenuity", new Vector3(146f, 0f, 96f), 0f);
            var ice = Place("IceDeposit", new Vector3(-110f, 0f, 70f));
            if (ice != null) SnapToGround(ice.transform, terrain);

            // Rocks, drawn instanced.
            var scatter = terrainGo.AddComponent<RockScatter>();
            scatter.terrain = terrain;
            scatter.material = AssetDatabase.LoadAssetAtPath<Material>(MatPath("Rock_Mars"));
            scatter.keepClear = colony.transform;
            scatter.Build();

            // Astronaut + camera.
            var cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 3000f;
            cam.gameObject.AddComponent<AudioListener>();
            var astro = Place("Astronaut", new Vector3(0f, 0.1f, -8f));
            if (astro != null)
            {
                astro.GetComponent<AstronautController>().viewCamera = cam;
                astro.GetComponent<Interactor>().viewCamera = cam;
            }

            foreach (Transform t in ct) SnapToGround(t, terrain);
            foreach (var name in new[] { "Perseverance", "Ingenuity" })
            {
                var go = GameObject.Find(name);
                if (go != null) SnapToGround(go.transform, terrain);
            }

            EditorSceneManager.SaveScene(scene, $"{SceneDir}/MarsColony.unity");
            Debug.Log("[Event Horizon] Saved Scenes/MarsColony.unity. Press Play: WASD, mouse, Shift, Space, E to interact, V to switch view.");
        }

        [MenuItem("Event Horizon/Assets/3. Build Space Lab Scene", priority = 3)]
        public static void BuildSpaceLab()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            System.IO.Directory.CreateDirectory(SceneDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var stars = AssetDatabase.LoadAssetAtPath<Material>(MatPath("Sky_Stars"));
            if (stars != null) RenderSettings.skybox = stars;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.03f, 0.03f, 0.04f);

            var light = new GameObject("Sunlight").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(10f, 60f, 0f);
            new GameObject("Clock").AddComponent<MarsEnvironment>().timeScale = 600f;

            Place("BlackHole", Vector3.zero);
            // Showcase, not to scale: at Mars' 100 km/unit, Phobos would be a 0.3-unit speck, so the moons are
            // drawn ~25x larger, and Earth and Saturn are shrunk so they fit in one view.
            Place("Planet_Mars", new Vector3(-190f, -20f, 260f));
            var phobos = Place("Moon_Phobos", new Vector3(-140f, -12f, 250f));
            if (phobos != null) phobos.transform.localScale = Vector3.one * 0.3f;
            var deimos = Place("Moon_Deimos", new Vector3(-122f, -24f, 240f));
            if (deimos != null) deimos.transform.localScale = Vector3.one * 0.3f;
            SetKmPerUnit(Place("Planet_Earth", new Vector3(230f, 30f, 330f)), 200f);
            SetKmPerUnit(Place("Planet_Saturn", new Vector3(80f, 90f, 700f)), 2000f);

            var cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(new Vector3(0f, 6f, -70f), Quaternion.Euler(4f, 0f, 0f));
            cam.farClipPlane = 5000f;
            var orbit = cam.gameObject.AddComponent<Space.OrbitCamera>();
            var targets = new System.Collections.Generic.List<Transform>();
            foreach (var n in new[] { "BlackHole", "Planet_Mars", "Planet_Earth", "Planet_Saturn" })
            {
                var go = GameObject.Find(n);
                if (go != null) targets.Add(go.transform);
            }
            orbit.targets = targets.ToArray();
            orbit.distance = 70f;
            VisualSetup.AddGlobalVolume();
            new GameObject("Session").AddComponent<Game.SessionBootstrap>();

            EditorSceneManager.SaveScene(scene, $"{SceneDir}/SpaceLab.unity");
            Debug.Log("[Event Horizon] Saved Scenes/SpaceLab.unity.");
        }

        static void SetKmPerUnit(GameObject go, float km)
        {
            var body = go != null ? go.GetComponent<Space.CelestialBody>() : null;
            if (body != null) body.kmPerUnit = km;
        }

        static void Terminal(TerminalType type, Vector3 pos, float yaw, Transform parent)
        {
            var t = Place("Terminal", pos, yaw, parent);
            if (t != null) t.GetComponent<ColonyTerminal>().type = type;
        }

        /// <summary>Small soft particles drifting with the wind: airborne dust near the viewer.</summary>
        static ParticleSystem DustParticles(Transform parent)
        {
            var go = new GameObject("Dust Motes");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 6f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
            main.startColor = new Color(0.85f, 0.65f, 0.48f, 0.35f);
            main.maxParticles = 4000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(40f, 8f, 40f);
            var emission = ps.emission;
            emission.rateOverTime = 40f;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.3f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MatPath("DustMotes"));
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        static TerrainLayer Layer(string name) =>
            AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{AssetIntegrationBuilder.Out}/Terrain/{name}.terrainlayer");

        static void SnapToGround(Transform t, Terrain terrain)
        {
            var p = t.position;
            p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
            t.position = p;
        }
    }
}
