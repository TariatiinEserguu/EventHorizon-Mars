using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace EventHorizon.EditorTools
{
    /// <summary>
    /// Checks every third-party asset and writes a report to Logs/AssetValidation.txt:
    /// license record (SOURCE.json), URP-compatible shaders, missing materials, triangle budget, scale, animations,
    /// and texture size. "Fix" converts loose Built-in Standard materials to URP/Lit.
    /// </summary>
    public static class ThirdPartyValidator
    {
        const string Root = "Assets/ThirdParty";

        // Triangle budgets per model (RTX 4050 laptop target).
        const int PropBudget = 20000;
        const int CharacterBudget = 30000;
        const int LandmarkBudget = 450000; // NASA science models, one instance each, culled by LODGroup

        [MenuItem("Event Horizon/Assets/Validate Third-Party Assets", priority = 20)]
        public static void Validate() => Run(fix: false);

        [MenuItem("Event Horizon/Assets/Validate and Fix Legacy Materials", priority = 21)]
        public static void ValidateAndFix() => Run(fix: true);

        static void Run(bool fix)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            int models = 0, textures = 0, fixedCount = 0;

            if (!Directory.Exists(Root))
            {
                Debug.LogWarning("[Event Horizon] Assets/ThirdParty is empty: run python3 tools/fetch_assets.py first.");
                return;
            }

            // 1. License records: every pack folder needs SOURCE.json written by tools/fetch_assets.py.
            foreach (var vendor in Directory.GetDirectories(Root))
            foreach (var pack in Directory.GetDirectories(vendor))
            {
                bool hasFiles = Directory.EnumerateFiles(pack, "*", SearchOption.AllDirectories).Any(f => !f.EndsWith(".meta"));
                if (hasFiles && !File.Exists(Path.Combine(pack, "SOURCE.json")))
                    errors.Add($"{pack}: no SOURCE.json license record. Add it to tools/asset_manifest.json or remove it.");
            }

            // 2. Models
            foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                models++;
                string lower = path.ToLowerInvariant();
                bool character = lower.Contains("/characters/");
                bool landmark = lower.Contains("/nasa/");

                int tris = 0;
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true)) tris += Triangles(mf.sharedMesh);
                foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) tris += Triangles(smr.sharedMesh);
                int budget = landmark ? LandmarkBudget : character ? CharacterBudget : PropBudget;
                if (tris > budget) warnings.Add($"{path}: {tris:N0} triangles (budget {budget:N0}). Decimate in Blender or add LODs.");

                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.GetMesh() == null && !(r is ParticleSystemRenderer)) errors.Add($"{path}/{r.name}: missing mesh");
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) { errors.Add($"{path}/{r.name}: missing material"); continue; }
                        if (m.shader == null || m.shader.name == "Hidden/InternalErrorShader" || !m.shader.isSupported)
                            errors.Add($"{path}/{r.name}: material '{m.name}' has a broken shader (pink)");
                        else if (!IsUrpShader(m.shader.name))
                            warnings.Add($"{path}/{r.name}: material '{m.name}' uses non-URP shader '{m.shader.name}'");
                    }
                }

                // Scale sanity: our props run from 0.1 m to ~50 m. Far outside that means a unit mismatch (cm vs m).
                var rs = go.GetComponentsInChildren<Renderer>(true);
                if (rs.Length > 0)
                {
                    var b = new Bounds();
                    bool first = true;
                    foreach (var r in rs)
                    {
                        var mesh = r.GetMesh();
                        if (mesh == null) continue;
                        var mb = TransformBounds(r.transform, go.transform, mesh.bounds);
                        if (first) { b = mb; first = false; } else b.Encapsulate(mb);
                    }
                    float size = b.size.magnitude;
                    if (!first && (size > 500f || size < 0.01f))
                        warnings.Add($"{path}: bounds {b.size} look wrong for metres. Check the importer scale factor.");
                }

                if (character)
                {
                    int clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Count(c => !c.name.StartsWith("__preview__"));
                    if (clips == 0) warnings.Add($"{path}: character has no animation clips");
                    if (go.GetComponentInChildren<SkinnedMeshRenderer>(true) == null) warnings.Add($"{path}: character has no skinned mesh (not rigged)");
                }
            }

            // 3. Loose materials (extracted .mat files)
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".mat")) continue;
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null || IsUrpShader(m.shader.name)) continue;
                if (fix && ConvertToUrpLit(m)) { fixedCount++; continue; }
                warnings.Add($"{path}: non-URP shader '{m.shader.name}' (run 'Validate and Fix Legacy Materials')");
            }

            // 4. Textures over budget
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                textures++;
                if (AssetImporter.GetAtPath(path) is TextureImporter ti)
                {
                    bool justified = path.Contains("/SolarSystemScope/");
                    if (ti.maxTextureSize > 1024 && !justified) warnings.Add($"{path}: imports at {ti.maxTextureSize}px (props should be 512-1024)");
                    if (ti.textureCompression == TextureImporterCompression.Uncompressed) warnings.Add($"{path}: uncompressed");
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Third-party asset validation: {models} models, {textures} textures");
            sb.AppendLine($"{errors.Count} errors, {warnings.Count} warnings{(fix ? $", {fixedCount} materials converted to URP/Lit" : "")}");
            sb.AppendLine();
            foreach (var e in errors) sb.AppendLine("ERROR   " + e);
            foreach (var w in warnings) sb.AppendLine("WARNING " + w);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/AssetValidation.txt", sb.ToString());

            string summary = $"[Event Horizon] Asset validation: {errors.Count} errors, {warnings.Count} warnings. Full report: Logs/AssetValidation.txt";
            if (errors.Count > 0) Debug.LogError(summary + "\n" + string.Join("\n", errors.Take(20)));
            else if (warnings.Count > 0) Debug.LogWarning(summary + "\n" + string.Join("\n", warnings.Take(20)));
            else Debug.Log(summary);
        }

        static bool IsUrpShader(string n) =>
            n.StartsWith("Universal Render Pipeline/") || n.StartsWith("Shader Graphs/") || n.StartsWith("glTF/") ||
            n.StartsWith("EventHorizon/") || n.StartsWith("Skybox/");

        static int Triangles(Mesh m)
        {
            if (m == null) return 0;
            long n = 0;
            for (int i = 0; i < m.subMeshCount; i++) n += m.GetIndexCount(i);
            return (int)(n / 3);
        }

        static Mesh GetMesh(this Renderer r) =>
            r is SkinnedMeshRenderer smr ? smr.sharedMesh :
            r.TryGetComponent<MeshFilter>(out var mf) ? mf.sharedMesh : null;

        static Bounds TransformBounds(Transform from, Transform root, Bounds local)
        {
            var m = root.worldToLocalMatrix * from.localToWorldMatrix;
            var c = m.MultiplyPoint3x4(local.center);
            var e = local.extents;
            var ax = m.MultiplyVector(new Vector3(e.x, 0, 0));
            var ay = m.MultiplyVector(new Vector3(0, e.y, 0));
            var az = m.MultiplyVector(new Vector3(0, 0, e.z));
            var ext = new Vector3(
                Mathf.Abs(ax.x) + Mathf.Abs(ay.x) + Mathf.Abs(az.x),
                Mathf.Abs(ax.y) + Mathf.Abs(ay.y) + Mathf.Abs(az.y),
                Mathf.Abs(ax.z) + Mathf.Abs(ay.z) + Mathf.Abs(az.z));
            return new Bounds(c, ext * 2f);
        }

        /// <summary>Built-in Standard -> URP/Lit, keeping colour, albedo, normal map and smoothness.</summary>
        static bool ConvertToUrpLit(Material m)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return false;
            var mainTex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
            var color = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            var bump = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
            float gloss = m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness") : 0.3f;
            float metal = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0f;
            m.shader = lit;
            m.SetTexture("_BaseMap", mainTex);
            m.SetColor("_BaseColor", color);
            if (bump != null) { m.SetTexture("_BumpMap", bump); m.EnableKeyword("_NORMALMAP"); }
            m.SetFloat("_Smoothness", gloss);
            m.SetFloat("_Metallic", metal);
            EditorUtility.SetDirty(m);
            return true;
        }
    }
}
