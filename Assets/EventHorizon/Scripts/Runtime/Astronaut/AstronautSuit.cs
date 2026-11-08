using UnityEngine;

namespace EventHorizon.Astronaut
{
    /// <summary>
    /// Adds a mirrored gold sun visor and a life-support backpack (PLSS) to any rig, so all imported characters look
    /// like the same expedition crew. On the Quaternius astronauts the visor also covers the cartoon faces.
    /// Parts attach to the head and chest bones, so they follow the animation.
    /// </summary>
    public class AstronautSuit : MonoBehaviour
    {
        public Transform head;
        public Transform chest;
        public bool addVisor = true;
        public bool addBackpack = true;
        public Material visorMaterial;
        public Material backpackMaterial;
        public Color visorColor = new Color(0.85f, 0.62f, 0.2f);
        public Color backpackColor = new Color(0.9f, 0.9f, 0.88f);
        [Tooltip("Props baked into a rig that don't belong on a science expedition (the Quaternius astronauts hold a pistol).")]
        public string[] hideObjectsNamed = { "Pistol", "Gun", "Weapon" };

        const string VisorName = "EH_Visor";
        const string BackpackName = "EH_Backpack";

        void Awake() => Build();

        [ContextMenu("Build suit parts")]
        public void Build()
        {
            FindBones();
            foreach (var t in GetComponentsInChildren<Transform>(true))
                foreach (var n in hideObjectsNamed)
                    if (t.name.Equals(n, System.StringComparison.OrdinalIgnoreCase) && t.GetComponent<Renderer>() != null)
                        t.GetComponent<Renderer>().enabled = false;
            var bounds = RendererBounds();
            float height = Mathf.Max(bounds.size.y, 0.5f);

            if (addVisor && head != null && head.Find(VisorName) == null)
            {
                // Head size from the top of the model down to the head bone: works for realistic and big-headed rigs.
                float r = Mathf.Clamp(bounds.max.y - head.position.y, height * 0.06f, height * 0.25f);
                var visor = Part(PrimitiveType.Sphere, VisorName, visorMaterial, visorColor, metallic: 1f, smoothness: 0.95f);
                visor.transform.SetPositionAndRotation(head.position + transform.forward * r * 0.45f + Vector3.up * r * 0.1f, transform.rotation);
                visor.transform.localScale = new Vector3(r * 1.45f, r * 1.15f, r * 0.9f);
                visor.transform.SetParent(head, true);
            }

            if (addBackpack && chest != null && chest.Find(BackpackName) == null)
            {
                float s = height / 1.8f;
                var pack = Part(PrimitiveType.Cube, BackpackName, backpackMaterial, backpackColor, metallic: 0.1f, smoothness: 0.4f);
                float depth = Mathf.Max(bounds.extents.z, 0.12f * s);
                pack.transform.SetPositionAndRotation(chest.position - transform.forward * (depth * 0.6f + 0.1f * s), transform.rotation);
                pack.transform.localScale = new Vector3(0.45f, 0.6f, 0.22f) * s;
                pack.transform.SetParent(chest, true);
            }
        }

        void FindBones()
        {
            var anim = GetComponentInChildren<Animator>();
            if (anim != null && anim.isHuman)
            {
                if (head == null) head = anim.GetBoneTransform(HumanBodyBones.Head);
                if (chest == null) chest = anim.GetBoneTransform(HumanBodyBones.UpperChest);
                if (chest == null) chest = anim.GetBoneTransform(HumanBodyBones.Chest);
            }
            // Generic rigs: find bones by name.
            if (head == null) head = FindByName("head", "neck");
            if (chest == null) chest = FindByName("chest", "spine2", "upperchest", "torso", "spine");
        }

        Transform FindByName(params string[] keys)
        {
            var all = GetComponentsInChildren<Transform>();
            foreach (var key in keys)
                foreach (var t in all)
                {
                    string n = t.name.ToLowerInvariant();
                    if (n.Contains(key) && !n.Contains("end") && !n.Contains("top") && t.GetComponent<Renderer>() == null) return t;
                }
            return null;
        }

        Bounds RendererBounds()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            var b = new Bounds(transform.position + Vector3.up * 0.9f, Vector3.one * 0.1f);
            bool first = true;
            foreach (var r in renderers)
            {
                if (!r.enabled || r.name == VisorName || r.name == BackpackName) continue;
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        static GameObject Part(PrimitiveType type, string partName, Material mat, Color color, float metallic, float smoothness)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = partName;
            var col = go.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);
            var r = go.GetComponent<MeshRenderer>();
            if (mat != null)
            {
                r.sharedMaterial = mat;
            }
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader != null)
                {
                    var m = new Material(shader) { name = partName };
                    m.SetColor("_BaseColor", color);
                    m.SetFloat("_Metallic", metallic);
                    m.SetFloat("_Smoothness", smoothness);
                    r.sharedMaterial = m;
                }
            }
            return go;
        }
    }
}
