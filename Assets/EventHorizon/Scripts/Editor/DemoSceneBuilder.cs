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

            var sky = AssetDatabase.LoadAssetAtPath<Material>(MatPath("Sky_Mars"));
            if (sky != null) RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Skybox;
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
            colony.AddComponent<ColonyStatusHUD>();
            var ct = colony.transform;
            Place("Habitat", new Vector3(0f, 0f, 18f), 180f, ct);
            Place("Greenhouse", new Vector3(-22f, 0f, 10f), 90f, ct);
            Place("OxygenPlant", new Vector3(12f, 0f, 4f), 0f, ct);
            Place("WaterTank", new Vector3(16f, 0f, 9f), 0f, ct);
            Place("OxygenTank", new Vector3(8f, 0f, 9f), 0f, ct);
            Place("FoodStore", new Vector3(5f, 0f, 12f), 0f, ct);
            Place("BatteryBank", new Vector3(12f, 0f, -2f), 0f, ct);
            Place("CommsDish", new Vector3(-12f, 0f, 30f), 200f, ct);
            for (int x = 0; x < 6; x++)
            for (int z = 0; z < 4; z++)
                Place("SolarPanel", new Vector3(28f + x * 5f, 0f, -10f + z * 6f), 180f, ct); // 24 x 10 m2 facing south
            Place("Lander", new Vector3(-40f, 0f, -40f), 30f, ct);
            Place("Rover", new Vector3(-6f, 0f, -8f), 60f, ct);

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
            var astro = Place("Astronaut", new Vector3(0f, 0.1f, -4f));
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
            Place("Planet_Mars", new Vector3(-120f, 0f, 60f));
            var phobos = Place("Moon_Phobos", new Vector3(-80f, 2f, 60f));
            if (phobos != null) phobos.transform.localScale = Vector3.one * 0.3f;
            var deimos = Place("Moon_Deimos", new Vector3(-66f, -3f, 60f));
            if (deimos != null) deimos.transform.localScale = Vector3.one * 0.3f;
            SetKmPerUnit(Place("Planet_Earth", new Vector3(140f, 10f, 90f)), 200f);
            SetKmPerUnit(Place("Planet_Saturn", new Vector3(60f, 40f, 500f)), 2000f);

            var cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(new Vector3(0f, 6f, -70f), Quaternion.Euler(4f, 0f, 0f));
            cam.farClipPlane = 5000f;

            EditorSceneManager.SaveScene(scene, $"{SceneDir}/SpaceLab.unity");
            Debug.Log("[Event Horizon] Saved Scenes/SpaceLab.unity.");
        }

        static void SetKmPerUnit(GameObject go, float km)
        {
            var body = go != null ? go.GetComponent<Space.CelestialBody>() : null;
            if (body != null) body.kmPerUnit = km;
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
