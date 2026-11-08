using System.Collections.Generic;
using EventHorizon.Core;
using EventHorizon.Space;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace EventHorizon.EditorTools
{
    /// <summary>
    /// The MainMenu scene: a lightweight real-time orbital view. Mars turns slowly under a thin atmosphere rim and
    /// drifting dust clouds, a beacon marks the ARES-1 colony in Jezero crater, Phobos and Deimos hang nearby,
    /// and a transfer vehicle crosses the star field. The camera drifts gently. No gameplay systems are loaded.
    /// </summary>
    public static class MenuSceneBuilder
    {
        const string ScenePath = "Assets/EventHorizon/Scenes/MainMenu.unity";
        static string Prefab(string n) => $"{AssetIntegrationBuilder.Out}/Prefabs/{n}.prefab";
        static Material Mat(string n) => AssetDatabase.LoadAssetAtPath<Material>($"{AssetIntegrationBuilder.Out}/Materials/{n}.mat");

        [MenuItem("Event Horizon/Assets/4. Build Main Menu Scene", priority = 4)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            System.IO.Directory.CreateDirectory("Assets/EventHorizon/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var stars = Mat("Sky_Stars");
            if (stars != null) RenderSettings.skybox = stars;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.025f, 0.025f, 0.035f);
            RenderSettings.fog = false;

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.6f;
            sun.color = new Color(1f, 0.97f, 0.92f);
            sun.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(8f, 62f, 0f); // light from the left: a crescent terminator on the right

            var clock = new GameObject("Clock").AddComponent<MarsEnvironment>();
            clock.timeScale = 900f;          // one Mars rotation every ~98 s
            clock.advanceSeasons = false;
            clock.driveAmbient = false;

            // Mars
            var marsAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab("Planet_Mars"));
            GameObject mars = null;
            if (marsAsset != null)
            {
                mars = (GameObject)PrefabUtility.InstantiatePrefab(marsAsset);
                mars.transform.position = new Vector3(34f, -20f, 118f);
                var body = mars.GetComponent<CelestialBody>();
                body.kmPerUnit = 100f;

                var clouds = Sphere("Dust Clouds", mars.transform, 1.006f, Mat("Mars_DustClouds"));
                body.clouds = clouds.transform;
                body.cloudSpeedFactor = 1.35f;

                // Beacon at Jezero crater (18.38 N, 77.58 E) on the unit sphere, same mapping as MeshBuilders.UvSphere.
                float lat = 18.38f * Mathf.Deg2Rad, lon = 77.58f * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(lat) * Mathf.Sin(lon), Mathf.Sin(lat), -Mathf.Cos(lat) * Mathf.Cos(lon));
                var beacon = Sphere("ARES-1 Beacon", mars.transform, 0.012f, Mat("Beacon"));
                beacon.transform.localPosition = dir * 1.004f;
                beacon.AddComponent<BeaconPulse>();
            }

            // Moons (shown enlarged; to scale Phobos would be a speck at this distance)
            var phobos = Place("Moon_Phobos", new Vector3(-30f, 14f, 150f), 0.16f);
            var deimos = Place("Moon_Deimos", new Vector3(70f, 26f, 260f), 0.16f);
            if (phobos != null) phobos.transform.rotation = Quaternion.Euler(10f, 40f, 0f);
            if (deimos != null) deimos.transform.rotation = Quaternion.Euler(-20f, 120f, 0f);

            // Mars Reconnaissance Orbiter crossing the sky (NASA model), or a primitive transfer vehicle as fallback.
            var mroAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab("Orbiter_MRO"));
            GameObject ship;
            if (mroAsset != null)
            {
                ship = new GameObject("Mars Reconnaissance Orbiter");
                var mro = (GameObject)PrefabUtility.InstantiatePrefab(mroAsset);
                mro.transform.SetParent(ship.transform, false);
                mro.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                var mover2 = ship.AddComponent<SlowMover>();
                mover2.from = new Vector3(-120f, 38f, 210f);
                mover2.to = new Vector3(160f, 52f, 240f);
                mover2.seconds = 150f;
            }
            else
            {
            ship = new GameObject("Transfer Vehicle");
            var hull = Mat("SpacecraftHull");
            var solar = AssetDatabase.LoadAssetAtPath<Material>($"{AssetIntegrationBuilder.Out}/Materials/SolarCell.mat");
            Part(PrimitiveType.Cylinder, ship.transform, new Vector3(0, 0, 0), new Vector3(0.6f, 2.2f, 0.6f), Quaternion.Euler(90, 0, 0), hull);
            Part(PrimitiveType.Sphere, ship.transform, new Vector3(0, 0, 2.3f), new Vector3(0.62f, 0.62f, 0.9f), Quaternion.identity, hull);
            Part(PrimitiveType.Cylinder, ship.transform, new Vector3(0.75f, 0, -0.6f), new Vector3(0.35f, 1.1f, 0.35f), Quaternion.Euler(90, 0, 0), hull);
            Part(PrimitiveType.Cylinder, ship.transform, new Vector3(-0.75f, 0, -0.6f), new Vector3(0.35f, 1.1f, 0.35f), Quaternion.Euler(90, 0, 0), hull);
            Part(PrimitiveType.Cube, ship.transform, new Vector3(3.2f, 0, 0.2f), new Vector3(4.5f, 0.04f, 1.4f), Quaternion.identity, solar);
            Part(PrimitiveType.Cube, ship.transform, new Vector3(-3.2f, 0, 0.2f), new Vector3(4.5f, 0.04f, 1.4f), Quaternion.identity, solar);
            Part(PrimitiveType.Cylinder, ship.transform, new Vector3(0, 0, -2.6f), new Vector3(0.9f, 0.4f, 0.9f), Quaternion.Euler(90, 0, 0), hull);
            var mover = ship.AddComponent<SlowMover>();
            mover.from = new Vector3(-120f, 38f, 210f);
            mover.to = new Vector3(160f, 52f, 240f);
            mover.seconds = 150f;
            }

            // Camera
            var cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(-2f, 6f, 0f));
            cam.fieldOfView = 50f;
            cam.farClipPlane = 2000f;
            cam.gameObject.AddComponent<AudioListener>();
            cam.gameObject.AddComponent<MenuMotion>();

            VisualSetup.AddGlobalVolume();
            new GameObject("Main Menu").AddComponent<UI.MainMenu>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[Event Horizon] Saved Scenes/MainMenu.unity");
        }

        static GameObject Sphere(string name, Transform parent, float scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            // Planet prefabs use a radius-1 mesh; Unity's sphere has radius 0.5, so double the scale.
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * scale * 2f;
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{AssetIntegrationBuilder.Out}/Meshes/PlanetSphere.asset");
            if (mesh != null) { go.GetComponent<MeshFilter>().sharedMesh = mesh; go.transform.localScale = Vector3.one * scale; }
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject Place(string prefab, Vector3 pos, float scale)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab(prefab));
            if (asset == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        static void Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        /// <summary>Scenes in build order: the menu loads first.</summary>
        public static void SetBuildScenes()
        {
            var list = new List<EditorBuildSettingsScene>();
            foreach (var p in new[] { ScenePath, "Assets/EventHorizon/Scenes/MarsColony.unity", "Assets/EventHorizon/Scenes/SpaceLab.unity",
                                      "Assets/EventHorizon/Scenes/MarsOrbit.unity", "Assets/EventHorizon/Scenes/BlackHoleLab.unity" })
                if (System.IO.File.Exists(p)) list.Add(new EditorBuildSettingsScene(p, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
