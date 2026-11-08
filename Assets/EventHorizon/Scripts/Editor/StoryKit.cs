using EventHorizon.Astronaut;
using EventHorizon.Colony;
using EventHorizon.Interaction;
using EventHorizon.Story;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static EventHorizon.EditorTools.ColonyKit;

namespace EventHorizon.EditorTools
{
    /// <summary>
    /// Builds the story's places in the colony scene from <see cref="StorySites"/>: a prop and a
    /// <see cref="StoryInteractable"/> (or <see cref="StoryMarker"/>) at every site, the larger locations (the abandoned
    /// Ares-0 survey station, the recycling plant, the Ares-0 wreck, the lava-tube skylight), the colonists, the two
    /// extra terminals and the cinematic director.
    /// </summary>
    public static class StoryKit
    {
        const string Prefabs = AssetIntegrationBuilder.Out + "/Prefabs/";

        public static void Build(Terrain terrain, Transform colony)
        {
            var root = new GameObject("Story").transform;

            foreach (var s in StorySites.All) Site(s, root);
            SurveyStation(root);
            Recycler(root);
            AresWreck(root);
            Skylight(root);
            Npcs(root);
            Terminal(TerminalType.Observatory, new Vector3(-18.5f, 0f, 29.5f), 200f, colony);
            Terminal(TerminalType.Recycling, new Vector3(9f, 0f, 29.5f), 180f, colony);
            Director(root);

            foreach (Transform t in root) Snap(t, terrain);
        }

        static void Snap(Transform t, Terrain terrain)
        {
            var p = t.position;
            p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
            t.position = p;
        }

        // ------------------------------------------------------------------ sites

        static void Site(SiteDef s, Transform parent)
        {
            var go = new GameObject("Site_" + s.id);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(s.pos, Quaternion.Euler(0f, s.yaw, 0f));
            var t = go.transform;
            switch (s.kind)
            {
                case "marker":
                    var m = go.AddComponent<StoryMarker>();
                    m.siteId = s.id;
                    m.radius = s.radius;
                    if (s.id == "pro_beacon") Beacon(t);
                    return;
                case "console":
                    Part(PrimitiveType.Cube, t, new Vector3(0f, 0.55f, 0f), new Vector3(0.7f, 1.1f, 0.45f), Vector3.zero, "Steel", collider: true);
                    Part(PrimitiveType.Cube, t, new Vector3(0f, 1.15f, -0.1f), new Vector3(0.62f, 0.4f, 0.05f), new Vector3(-20f, 0f, 0f), "LED");
                    break;
                case "door":
                    Part(PrimitiveType.Cube, t, new Vector3(0f, 1.3f, 0f), new Vector3(0.4f, 0.55f, 0.12f), Vector3.zero, "DarkComposite", collider: true);
                    Part(PrimitiveType.Cube, t, new Vector3(0f, 1.35f, -0.07f), new Vector3(0.22f, 0.12f, 0.02f), Vector3.zero, "StatusGreen");
                    break;
                case "panel":
                    Part(PrimitiveType.Cube, t, new Vector3(0f, 0.9f, 0f), new Vector3(0.9f, 1.0f, 0.35f), Vector3.zero, "Cladding", collider: true);
                    Part(PrimitiveType.Cube, t, new Vector3(0f, 0.9f, -0.19f), new Vector3(0.75f, 0.85f, 0.02f), Vector3.zero, "Hazard");
                    Part(PrimitiveType.Cube, t, new Vector3(0.3f, 1.3f, -0.21f), new Vector3(0.08f, 0.08f, 0.02f), Vector3.zero, "StatusAmber");
                    break;
                case "valve":
                    Part(PrimitiveType.Cylinder, t, new Vector3(0f, 0.6f, 0f), new Vector3(0.35f, 0.6f, 0.35f), Vector3.zero, "Steel", collider: true);
                    Part(PrimitiveType.Cylinder, t, new Vector3(0f, 1.25f, 0f), new Vector3(0.6f, 0.04f, 0.6f), Vector3.zero, "OrangeStripe");
                    Pipe(t, new Vector3(0f, 0.4f, 0f), new Vector3(1.2f, 0.4f, 0f), 0.1f);
                    break;
                case "pump":
                    Part(PrimitiveType.Cube, t, new Vector3(0f, 0.3f, 0f), new Vector3(1.6f, 0.6f, 1.0f), Vector3.zero, "DarkComposite", collider: true);
                    Part(PrimitiveType.Cylinder, t, new Vector3(-0.3f, 0.9f, 0f), new Vector3(0.55f, 0.35f, 0.55f), new Vector3(0f, 0f, 90f), "Steel");
                    Pipe(t, new Vector3(0.4f, 0.8f, 0f), new Vector3(0.4f, 1.6f, 0f), 0.08f);
                    Part(PrimitiveType.Cube, t, new Vector3(0.6f, 0.75f, 0.52f), new Vector3(0.1f, 0.1f, 0.02f), Vector3.zero, "StatusAmber");
                    break;
                case "scanner":
                    Part(PrimitiveType.Cylinder, t, new Vector3(0f, 0.6f, 0f), new Vector3(0.12f, 0.6f, 0.12f), Vector3.zero, "Aluminium", collider: true);
                    Part(PrimitiveType.Cube, t, new Vector3(0f, 1.25f, 0f), new Vector3(0.5f, 0.25f, 0.35f), Vector3.zero, "OrangeStripe");
                    Part(PrimitiveType.Cylinder, t, new Vector3(0f, 0.05f, 0f), new Vector3(0.9f, 0.04f, 0.9f), Vector3.zero, "DarkComposite");
                    break;
                case "drive":
                    Part(PrimitiveType.Cube, t, new Vector3(0f, 0.08f, 0f), new Vector3(0.3f, 0.08f, 0.2f), new Vector3(0f, 25f, 8f), "OrangeStripe", collider: true);
                    Beacon(t, 0.6f);
                    break;
                case "wreck":
                    Part(PrimitiveType.Cube, t, new Vector3(0f, 0.45f, 0f), new Vector3(2.2f, 0.9f, 1.4f), new Vector3(4f, 0f, -9f), "DarkComposite", collider: true);
                    Part(PrimitiveType.Cylinder, t, new Vector3(0.9f, 0.4f, 0.8f), new Vector3(0.7f, 0.12f, 0.7f), new Vector3(90f, 0f, 0f), "Steel");
                    Part(PrimitiveType.Cylinder, t, new Vector3(-0.9f, 0.3f, 0.8f), new Vector3(0.7f, 0.12f, 0.7f), new Vector3(90f, 0f, 20f), "Steel");
                    Part(PrimitiveType.Cube, t, new Vector3(0f, 1.0f, 0f), new Vector3(1.8f, 0.04f, 1.0f), new Vector3(0f, 0f, 14f), "PV");
                    break;
                case "outcrop":
                    for (int i = 0; i < 4; i++)
                        Part(PrimitiveType.Sphere, t, new Vector3(Mathf.Cos(i * 1.7f) * 0.9f, 0.3f + 0.15f * i, Mathf.Sin(i * 1.7f) * 0.7f),
                            new Vector3(1.3f - 0.2f * i, 0.8f, 1.1f), new Vector3(10f * i, 40f * i, 0f), "DarkComposite", collider: i == 0);
                    string vein = s.id == "min_1" ? "Steel" : s.id == "min_2" ? "HullWhite" : "StatusGreen";
                    for (int i = 0; i < 5; i++)
                        Part(PrimitiveType.Sphere, t, new Vector3(-0.6f + 0.3f * i, 0.65f, -0.5f), Vector3.one * 0.14f, Vector3.zero, vein);
                    break;
                case "seismometer":
                    Part(PrimitiveType.Sphere, t, new Vector3(0f, 0.25f, 0f), new Vector3(0.9f, 0.5f, 0.9f), Vector3.zero, "MLI", collider: true);
                    Part(PrimitiveType.Cylinder, t, new Vector3(0f, 0.02f, 0f), new Vector3(1.4f, 0.02f, 1.4f), Vector3.zero, "Aluminium");
                    Pipe(t, new Vector3(0.6f, 0.05f, 0f), new Vector3(2.4f, 0.05f, 0.6f), 0.03f, "DarkComposite");
                    break;
                case "pendulum":
                    Part(PrimitiveType.Cube, t, new Vector3(-0.8f, 1.1f, 0f), new Vector3(0.08f, 2.2f, 0.08f), Vector3.zero, "Aluminium", collider: true);
                    Part(PrimitiveType.Cube, t, new Vector3(0.8f, 1.1f, 0f), new Vector3(0.08f, 2.2f, 0.08f), Vector3.zero, "Aluminium");
                    Part(PrimitiveType.Cube, t, new Vector3(0f, 2.2f, 0f), new Vector3(1.7f, 0.08f, 0.08f), Vector3.zero, "Aluminium");
                    Part(PrimitiveType.Cylinder, t, new Vector3(0f, 1.7f, 0f), new Vector3(0.01f, 0.5f, 0.01f), Vector3.zero, "DarkComposite");
                    Part(PrimitiveType.Sphere, t, new Vector3(0f, 1.18f, 0f), Vector3.one * 0.14f, Vector3.zero, "OrangeStripe");
                    break;
                case "cache":
                    Part(PrimitiveType.Cube, t, new Vector3(0f, 0.25f, 0f), new Vector3(0.6f, 0.5f, 0.4f), Vector3.zero, "Container", collider: true);
                    Beacon(t, 0.9f);
                    break;
            }
            var si = go.AddComponent<StoryInteractable>();
            si.siteId = s.id;
            si.workSeconds = s.work;
            si.alwaysActive = s.always;
            // A trigger volume so the interaction ray finds small props easily.
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.9f, 0f);
            box.size = new Vector3(1.6f, 1.8f, 1.6f);
        }

        static void Beacon(Transform parent, float height = 3.2f)
        {
            Part(PrimitiveType.Cylinder, parent, new Vector3(0f, height * 0.5f, 0f), new Vector3(0.06f, height * 0.5f, 0.06f), Vector3.zero, "Aluminium");
            Part(PrimitiveType.Sphere, parent, new Vector3(0f, height, 0f), Vector3.one * 0.18f, Vector3.zero, "LED");
            var l = new GameObject("Beacon Light").AddComponent<Light>();
            l.transform.SetParent(parent, false);
            l.transform.localPosition = new Vector3(0f, height + 0.2f, 0f);
            l.type = LightType.Point;
            l.color = new Color(0.3f, 0.85f, 1f);
            l.range = 6f;
            l.intensity = 2f;
            l.shadows = LightShadows.None;
        }

        // ------------------------------------------------------------------ locations

        /// <summary>Ares-0's survey station: a dust-covered module, a toppled mast and a dead solar wing.</summary>
        static void SurveyStation(Transform parent)
        {
            var t = new GameObject("Ares-0 Survey Station").transform;
            t.SetParent(parent, false);
            t.position = new Vector3(-176f, 0f, 151f);
            t.rotation = Quaternion.Euler(0f, 25f, 0f);
            Part(PrimitiveType.Capsule, t, new Vector3(0f, 1.6f, 0f), new Vector3(3.2f, 4f, 3.2f), new Vector3(0f, 0f, 90f), "Container", collider: true);
            Part(PrimitiveType.Cube, t, new Vector3(0f, 0.25f, 0f), new Vector3(7f, 0.5f, 3.6f), Vector3.zero, "Pad", collider: true);
            Part(PrimitiveType.Cylinder, t, new Vector3(2.5f, 0.3f, 3.2f), new Vector3(0.12f, 3f, 0.12f), new Vector3(0f, 0f, 82f), "Aluminium");
            Part(PrimitiveType.Cube, t, new Vector3(-3f, 0.5f, -3f), new Vector3(3.2f, 0.05f, 1.6f), new Vector3(0f, 30f, 12f), "PV");
            Part(PrimitiveType.Cube, t, new Vector3(4.2f, 0.4f, -1.5f), new Vector3(0.8f, 0.8f, 0.8f), new Vector3(0f, 20f, 0f), "Container", collider: true);
        }

        /// <summary>Water recycling plant behind the habitat: tanks, a membrane skid, a bioreactor and piping.</summary>
        static void Recycler(Transform parent)
        {
            var t = new GameObject("Recycling Plant").transform;
            t.SetParent(parent, false);
            t.position = new Vector3(14f, 0f, 34f);
            Part(PrimitiveType.Cube, t, new Vector3(0f, 0.15f, 0f), new Vector3(11f, 0.3f, 8f), Vector3.zero, "Pad", collider: true);
            Part(PrimitiveType.Cylinder, t, new Vector3(-3.5f, 1.6f, -1f), new Vector3(2f, 1.4f, 2f), Vector3.zero, "HullWhite", collider: true);
            Part(PrimitiveType.Cylinder, t, new Vector3(-3.5f, 1.6f, 2f), new Vector3(1.6f, 1.2f, 1.6f), Vector3.zero, "HullWhite", collider: true);
            Part(PrimitiveType.Cube, t, new Vector3(1f, 1f, 0f), new Vector3(3f, 1.6f, 2f), Vector3.zero, "Cladding", collider: true);
            Part(PrimitiveType.Cylinder, t, new Vector3(4f, 1.3f, -0.5f), new Vector3(1.4f, 1.1f, 1.4f), Vector3.zero, "MLI", collider: true);
            Pipe(t, new Vector3(-2.5f, 0.8f, -1f), new Vector3(-0.5f, 0.8f, 0f), 0.12f);
            Pipe(t, new Vector3(2.5f, 0.9f, 0f), new Vector3(3.4f, 0.9f, -0.5f), 0.12f);
            Pipe(t, new Vector3(-14f, 0.4f, -14f), new Vector3(-3.5f, 0.4f, -2f), 0.1f);
            Floodlight(t, new Vector3(0f, 3.4f, -3.8f), new Vector3(0f, 180f, 0f));
        }

        /// <summary>The Ares-0 descent stage, tipped over on the plain since 2163.</summary>
        static void AresWreck(Transform parent)
        {
            var t = new GameObject("Ares-0 Landing Site").transform;
            t.SetParent(parent, false);
            t.position = new Vector3(165f, 0f, -168f);
            t.rotation = Quaternion.Euler(0f, 60f, 0f);
            Part(PrimitiveType.Cylinder, t, new Vector3(0f, 1.7f, 0f), new Vector3(3.6f, 1.6f, 3.6f), new Vector3(0f, 0f, 22f), "DarkComposite", collider: true);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f * Mathf.Deg2Rad;
                Pipe(t, new Vector3(Mathf.Cos(a) * 1.4f, 1.2f, Mathf.Sin(a) * 1.4f), new Vector3(Mathf.Cos(a) * 2.8f, 0.1f, Mathf.Sin(a) * 2.8f), 0.08f, "Aluminium");
            }
            Part(PrimitiveType.Cylinder, t, new Vector3(-0.8f, 0.4f, 0f), new Vector3(1.4f, 0.5f, 1.4f), Vector3.zero, "Steel");
            Part(PrimitiveType.Cube, t, new Vector3(3.5f, 0.05f, 2f), new Vector3(1.2f, 0.1f, 0.8f), new Vector3(0f, 0f, 0f), "HullWhite");
        }

        /// <summary>A collapsed lava-tube roof: a dark pit ringed by rubble.</summary>
        static void Skylight(Transform parent)
        {
            var t = new GameObject("Lava Tube Skylight").transform;
            t.SetParent(parent, false);
            t.position = new Vector3(-140f, 0f, -150f);
            var pit = Part(PrimitiveType.Cylinder, t, new Vector3(0f, 0.04f, 0f), new Vector3(9f, 0.02f, 7f), Vector3.zero, "DarkComposite");
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Skylight_Void" };
            mat.SetColor("_BaseColor", new Color(0.01f, 0.008f, 0.006f));
            AssetDatabase.CreateAsset(mat, AssetIntegrationBuilder.Out + "/Materials/Skylight_Void.mat");
            pit.GetComponent<Renderer>().sharedMaterial = mat;
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2f;
                Part(PrimitiveType.Sphere, t, new Vector3(Mathf.Cos(a) * 5f, 0.3f, Mathf.Sin(a) * 4f), new Vector3(1.4f, 0.7f, 1.1f) * (0.7f + 0.3f * (i % 3)),
                    new Vector3(0f, i * 25f, 0f), "DarkComposite", collider: true);
            }
        }

        // ------------------------------------------------------------------ people

        static void Npcs(Transform parent)
        {
            Npc(parent, Character.Reyes, new Vector3(-6.5f, 0f, 13f), 160f);
            Npc(parent, Character.Chen, new Vector3(-20f, 0f, 31.5f), 200f);
            Npc(parent, Character.Erdene, new Vector3(13f, 0f, 7.5f), 250f);
            Npc(parent, Character.Okafor, new Vector3(-22.5f, 0f, 9f), 90f);
            Npc(parent, Character.Park, new Vector3(-26.5f, 0f, 29.5f), 160f);
            Npc(parent, Character.Ortiz, new Vector3(-4.5f, 0f, -16.5f), 330f);
            Npc(parent, Character.Sorensen, new Vector3(5.5f, 0f, 15.5f), 200f);
        }

        static void Npc(Transform parent, Character who, Vector3 pos, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Astronaut.prefab");
            if (prefab == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = "NPC_" + who;
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            foreach (var c in go.GetComponents<AstronautController>()) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponents<Interactor>()) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponents<SuitLifeSupport>()) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponents<CharacterController>()) Object.DestroyImmediate(c);
            var col = go.AddComponent<CapsuleCollider>();
            col.height = 1.85f;
            col.radius = 0.4f;
            col.center = new Vector3(0f, 0.92f, 0f);
            go.AddComponent<Npc>().character = who;
        }

        static void Terminal(TerminalType type, Vector3 pos, float yaw, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Terminal.prefab");
            if (prefab == null) return;
            var t = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            t.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            t.GetComponent<ColonyTerminal>().type = type;
        }

        // ------------------------------------------------------------------ cinematics

        static void Director(Transform parent)
        {
            var dir = new GameObject("Story Director").AddComponent<StoryDirector>();
            dir.transform.SetParent(parent, false);
            var lander = GameObject.Find("Lander");
            if (lander != null) dir.lander = lander.transform;

            // Rocket exhaust kicking up regolith during touchdown and lift-off.
            var dustGo = new GameObject("Engine Dust");
            dustGo.transform.SetParent(dir.transform, false);
            var ps = dustGo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = 2.6f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 14f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 4f);
            main.startColor = new Color(0.8f, 0.6f, 0.45f, 0.5f);
            main.maxParticles = 1500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 2f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var em = ps.emission;
            em.rateOverTime = 260f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var r = dustGo.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(AssetIntegrationBuilder.Out + "/Materials/DustMotes.mat");
            r.shadowCastingMode = ShadowCastingMode.Off;
            dir.engineDust = ps;

            var glow = new GameObject("Engine Glow").AddComponent<Light>();
            glow.transform.SetParent(dir.lander != null ? dir.lander : dir.transform, false);
            glow.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.6f, 0.3f);
            glow.range = 40f;
            glow.intensity = 30f;
            glow.enabled = false;
            dir.engineGlow = glow;

            // Chapter 3: the ice field at survey site B, hidden until the player chooses it.
            var icePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "IceDeposit.prefab");
            if (icePrefab != null)
            {
                var ice = (GameObject)PrefabUtility.InstantiatePrefab(icePrefab, parent);
                ice.name = "IceDeposit_SiteB";
                ice.transform.position = new Vector3(-190.5f, 0f, 172f);
                var d = ice.GetComponent<IceDeposit>();
                if (d != null) { d.kgPerExtraction = 400f; d.iceKg = 6000f; }
                ice.SetActive(false);
                dir.siteBDeposit = ice;
            }
        }
    }
}
