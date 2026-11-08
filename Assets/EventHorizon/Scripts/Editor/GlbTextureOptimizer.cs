using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EventHorizon.EditorTools
{
    /// <summary>
    /// glTFast imports textures embedded in .glb files uncompressed and at full size (the NASA rover alone took
    /// 200 MB in the build). This copies each embedded texture into a normal PNG asset capped at 1024 px, so Unity
    /// block-compresses it, and points cloned materials at the copies. The originals are then unreferenced and
    /// left out of builds.
    /// </summary>
    public static class GlbTextureOptimizer
    {
        const string TexDir = AssetIntegrationBuilder.Out + "/Textures";
        const string MatDir = AssetIntegrationBuilder.Out + "/Materials/Models";
        const int MaxSize = 1024;

        /// <summary>
        /// Regions to paint over while re-encoding (UV space, v up), for markings we may not use. The NASA Gateway
        /// model's Canadarm3 texture carries the Government of Canada wordmark, a protected official mark.
        /// </summary>
        static readonly (string model, string texture, Rect uv, Vector2 fillFrom)[] Scrubs =
        {
            ("Gateway", "DSXR_BaseColor", new Rect(0f, 0.18f, 0.1f, 0.22f), new Vector2(0.13f, 0.29f)),
        };

        /// <summary>glTFast only exposes pixel data when its importer is set to make textures readable.</summary>
        public static void EnsureReadable(string glbPath)
        {
            var importer = AssetImporter.GetAtPath(glbPath);
            if (importer == null) return;
            var so = new SerializedObject(importer);
            var prop = so.FindProperty("importSettings.texturesReadable");
            if (prop == null || prop.boolValue) return;
            prop.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            importer.SaveAndReimport();
        }

        /// <summary>Replaces the materials under <paramref name="root"/> with copies that use compressed, capped textures.</summary>
        public static void Optimize(GameObject root, string key)
        {
            Directory.CreateDirectory($"{TexDir}/{key}");
            Directory.CreateDirectory(MatDir);
            var copies = new Dictionary<Material, Material>();
            var textures = new Dictionary<Texture2D, Texture2D>();
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src == null) continue;
                    if (!copies.TryGetValue(src, out var copy))
                    {
                        copy = MaterialCopy(src, key, copies.Count, textures);
                        copies[src] = copy;
                    }
                    mats[i] = copy;
                }
                r.sharedMaterials = mats;
            }
            AssetDatabase.SaveAssets();
        }

        static Material MaterialCopy(Material src, string key, int index, Dictionary<Texture2D, Texture2D> cache)
        {
            string path = $"{MatDir}/{key}_{index:00}.mat";
            var copy = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (copy == null)
            {
                copy = new Material(src);
                AssetDatabase.CreateAsset(copy, path);
            }
            else
            {
                copy.shader = src.shader;
                copy.CopyPropertiesFromMaterial(src);
            }
            foreach (var prop in src.GetTexturePropertyNames())
            {
                if (!(src.GetTexture(prop) is Texture2D tex)) continue;
                if (!AssetDatabase.GetAssetPath(tex).EndsWith(".glb")) continue;
                if (!cache.TryGetValue(tex, out var small))
                {
                    small = Shrink(tex, key, cache.Count, prop);
                    cache[tex] = small;
                }
                if (small != null) copy.SetTexture(prop, small);
            }
            EditorUtility.SetDirty(copy);
            return copy;
        }

        static Texture2D Shrink(Texture2D tex, string key, int index, string property)
        {
            if (!tex.isReadable)
            {
                Debug.LogWarning($"[Event Horizon] {tex.name} in {key} is not readable; keeping the original.");
                return null;
            }
            string path = $"{TexDir}/{key}/{index:00}_{Sanitize(property)}.png";
            if (!File.Exists(path))
            {
                float scale = Mathf.Min(1f, MaxSize / (float)Mathf.Max(tex.width, tex.height));
                int w = Mathf.Max(4, Mathf.RoundToInt(tex.width * scale));
                int h = Mathf.Max(4, Mathf.RoundToInt(tex.height * scale));
                var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var pixels = new Color[w * h];
                Rect? scrub = null;
                Color fill = Color.white;
                foreach (var sc in Scrubs)
                    if (key.Contains(sc.model) && tex.name.Contains(sc.texture))
                    {
                        scrub = sc.uv;
                        fill = tex.GetPixelBilinear(sc.fillFrom.x, sc.fillFrom.y);
                    }
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                    pixels[y * w + x] = scrub.HasValue && scrub.Value.Contains(new Vector2(u, v)) ? fill : tex.GetPixelBilinear(u, v);
                }
                if (scrub.HasValue) Debug.Log($"[Event Horizon] Painted over a protected mark in {key}/{tex.name}");
                outTex.SetPixels(pixels);
                outTex.Apply();
                File.WriteAllBytes(path, outTex.EncodeToPNG());
                Object.DestroyImmediate(outTex);
                AssetDatabase.ImportAsset(path);
            }
            if (AssetImporter.GetAtPath(path) is TextureImporter ti)
            {
                string p = property.ToLowerInvariant();
                bool normal = p.Contains("normal") || p.Contains("bump");
                bool data = p.Contains("metallic") || p.Contains("rough") || p.Contains("occlusion") || p.Contains("specular");
                ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = !normal && !data;
                ti.maxTextureSize = MaxSize;
                ti.textureCompression = TextureImporterCompression.Compressed;
                ti.mipmapEnabled = true;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static string Sanitize(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.TrimStart('_');
        }
    }
}
