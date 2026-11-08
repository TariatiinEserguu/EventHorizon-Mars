using System.IO;
using UnityEditor;
using UnityEngine;

namespace EventHorizon.EditorTools
{
    /// <summary>
    /// Import budgets for everything in Assets/ThirdParty, sized for an RTX 4050 laptop (6 GB VRAM) with 16 GB RAM:
    /// - Textures: 1024 px for props, 512 px for Kenney palettes, 2048 px for planets (they can fill the screen),
    ///   4096 px for the 360-degree star panorama. Always block-compressed with mipmaps and mip streaming.
    /// - Models: no cameras or lights, mesh compression, CPU copy dropped (not readable), animation only for characters.
    /// - Audio: mono (for 3D sound), Vorbis, decompressed on load because the clips are short.
    /// Bump <see cref="GetVersion"/> after changing a rule to reimport the affected assets.
    /// </summary>
    public class ThirdPartyImportRules : AssetPostprocessor
    {
        public const string Root = "Assets/ThirdParty/";

        public override uint GetVersion() => 3;

        static bool Applies(string path) => path.StartsWith(Root, System.StringComparison.Ordinal);

        static bool IsCharacter(string lowerPath) =>
            lowerPath.Contains("/characters/") || Path.GetFileName(lowerPath).StartsWith("astronaut_");

        void OnPreprocessTexture()
        {
            if (!Applies(assetPath)) return;
            var ti = (TextureImporter)assetImporter;
            string p = assetPath.ToLowerInvariant();
            string file = Path.GetFileNameWithoutExtension(p);

            int max = 1024;
            var compression = TextureImporterCompression.Compressed;
            bool planet = p.Contains("/solarsystemscope/") || p.Contains("/nasa/marsmoons/");
            if (planet)
            {
                max = file.Contains("stars") ? 4096 : 2048;
                compression = TextureImporterCompression.CompressedHQ; // BC7: keeps the colour in cloud and band detail
                ti.wrapModeU = TextureWrapMode.Repeat;                 // equirectangular: wrap around longitude
                ti.wrapModeV = TextureWrapMode.Clamp;                  // ...but not over the poles
            }
            if (p.Contains("/nasa/marsmoons/")) max = 1024;
            if (p.Contains("/kenney/"))
            {
                // Kenney's colormap is a palette of flat colour cells: mipmaps would blend neighbouring cells.
                max = 512;
                ti.mipmapEnabled = false;
                ti.filterMode = FilterMode.Point;
            }
            else
            {
                ti.mipmapEnabled = true;
                ti.streamingMipmaps = true;
            }

            if (file.Contains("_nor") || file.Contains("normal"))
            {
                ti.textureType = TextureImporterType.NormalMap;
            }
            else if (file.Contains("_rough") || file.Contains("_arm") || file.Contains("_ao") ||
                     file.Contains("_disp") || file.Contains("specular") || file.Contains("_metal"))
            {
                ti.sRGBTexture = false; // data, not colour
            }

            if (file.Contains("ring_alpha"))
            {
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
            }
            else if (file.Contains("clouds"))
            {
                // The cloud map is greyscale: brightness becomes opacity.
                ti.alphaSource = TextureImporterAlphaSource.FromGrayScale;
                ti.alphaIsTransparency = true;
            }

            ti.maxTextureSize = max;
            ti.textureCompression = compression;
        }

        void OnPreprocessModel()
        {
            if (!Applies(assetPath)) return;
            if (!(assetImporter is ModelImporter mi)) return; // .glb/.gltf go through glTFast's own importer
            string p = assetPath.ToLowerInvariant();
            bool character = IsCharacter(p);

            mi.importCameras = false;
            mi.importLights = false;
            mi.isReadable = false;
            mi.meshCompression = ModelImporterMeshCompression.Low;
            mi.optimizeMeshPolygons = true;
            mi.optimizeMeshVertices = true;
            mi.weldVertices = true;
            mi.addCollider = false;            // gameplay prefabs add simple colliders on purpose
            mi.generateSecondaryUV = false;
            mi.importBlendShapes = character;
            mi.importAnimation = character;
            mi.animationType = character ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription; // URP converts to URP/Lit
        }

        void OnPreprocessAnimation()
        {
            if (!Applies(assetPath)) return;
            if (!(assetImporter is ModelImporter mi) || !IsCharacter(assetPath.ToLowerInvariant())) return;
            var clips = mi.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            foreach (var clip in clips)
            {
                string n = clip.name.ToLowerInvariant();
                clip.loopTime = n.Contains("idle") || n.Contains("walk") || n.Contains("run");
            }
            mi.clipAnimations = clips;
        }

        void OnPreprocessAudio()
        {
            if (!Applies(assetPath)) return;
            var ai = (AudioImporter)assetImporter;
            ai.forceToMono = true;
            var s = ai.defaultSampleSettings;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.6f;
            ai.defaultSampleSettings = s;
        }

        void OnPostprocessMaterial(Material material)
        {
            if (!Applies(assetPath)) return;
            material.enableInstancing = true; // repeated props (rocks, barrels, pipes) batch on the GPU
        }
    }
}
