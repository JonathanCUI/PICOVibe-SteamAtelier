using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace SwanJourney.Editor
{
    // Only the derived scene's generated NativeVisuals materials are rebound.
    // Original textures/importers remain the partner project's editable source.
    public static class SwanJourneyTextureBudget
    {
        // Native rendering retains decoded texture resources in addition to Unity's
        // scene data. Previous builds exited LOW_MEMORY at 2.2-2.4 GB RSS on Swan;
        // this smaller budget is a mitigation experiment. Reserve 1024 for train
        // albedo viewed at arm's length; normal/mask/emission/environment maps use 512.
        const int TextureLimit = 512, HeroAlbedoLimit = 1024;
        const string Version = "SwanJourneyTextureBudget-v2-memory";

        // Keep each source's generated file so a later art edit can diverge again.
        // Only share material bindings for byte-identical PNGs with matching import settings.
        internal static int ShareIdenticalCopies(string generated)
        {
            var materials = AssetDatabase.FindAssets("t:Material", new[] { generated + "/NativeVisuals" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<Material>).Where(material => material).ToArray();
            var slots = new Dictionary<Texture2D, List<Slot>>();
            foreach (var material in materials)
            foreach (string property in material.GetTexturePropertyNames())
            {
                if (!(material.GetTexture(property) is Texture2D texture)) continue;
                string path = AssetDatabase.GetAssetPath(texture);
                if (!path.StartsWith(generated + "/TextureBudget/", StringComparison.Ordinal) &&
                    !path.StartsWith(generated + "/NativePbr/", StringComparison.Ordinal)) continue;
                if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
                if (!slots.TryGetValue(texture, out var bindings)) slots.Add(texture, bindings = new List<Slot>());
                bindings.Add(new Slot { material = material, property = property });
            }
            int count = 0;
            var canonical = new Dictionary<string, Texture2D>();
            using (var hash = System.Security.Cryptography.SHA256.Create())
            foreach (var pair in slots.OrderBy(item => AssetDatabase.GetAssetPath(item.Key), StringComparer.Ordinal))
            {
                string path = AssetDatabase.GetAssetPath(pair.Key);
                string importer = string.Join("\n", File.ReadLines(path + ".meta").Where(line =>
                    !line.StartsWith("guid:", StringComparison.Ordinal) && !line.StartsWith("  userData:", StringComparison.Ordinal)));
                string stem = Path.GetFileNameWithoutExtension(path);
                int suffix = stem.IndexOf('_');
                if (suffix < 0) continue;
                string key = Path.GetDirectoryName(path) + "/" + stem.Substring(suffix) + "\n" + importer;
                using (var source = File.OpenRead(path)) key += "\n" + Convert.ToBase64String(hash.ComputeHash(source));
                if (!canonical.TryGetValue(key, out var texture)) { canonical.Add(key, pair.Key); continue; }
                foreach (var slot in pair.Value)
                {
                    slot.material.SetTexture(slot.property, texture);
                    EditorUtility.SetDirty(slot.material);
                    count++;
                }
            }
            return count;
        }

        sealed class Slot { public Material material; public string property; }
        sealed class Request
        {
            public Texture2D source;
            public TextureImporter importer;
            public string sourcePath, key;
            public bool normal, srgb;
            public int limit;
            public readonly List<Slot> slots = new List<Slot>();
        }

        [Serializable]
        public sealed class TextureRecord
        {
            public string source, output, role, sourceEncoding, outputEncoding;
            public int sourceWidth, sourceHeight, importedWidth, importedHeight, width, height, references;
            public int pixelLimit, pngWidth, pngHeight;
            public bool srgb, normal, mipmaps, reused;
            public long sourceFileBytes, pngBytes, sourceRgba32MipEstimate, importedRgba32MipEstimate, outputRgba32MipEstimate;
        }

        [Serializable]
        public sealed class Report
        {
            public int materials, textureReferences, uniqueCopies, cacheHits;
            public int validatedPngFiles, largestOutputDimension;
            public string policy = "Train albedo <=1024; normal, data, emission and environment <=512; real PNG resampling; source textures unchanged";
            public long sourceFileBytes, pngBytes, sourceRgba32MipEstimate, importedRgba32MipEstimate, outputRgba32MipEstimate;
            public string estimate = "Unique texture RGBA32 allocations including mipmaps; excludes compression, driver overhead and duplicate GLB embeddings. Not measured device VRAM.";
            public TextureRecord[] textures;
        }

        public static Report Apply(Transform content, string generatedFolder)
        {
            if (!content) throw new ArgumentNullException(nameof(content));
            string generated = (generatedFolder ?? "").Replace('\\', '/').TrimEnd('/');
            if (!generated.StartsWith("Assets/", StringComparison.Ordinal)) throw new ArgumentException("Expected a generated Assets folder", nameof(generatedFolder));
            string materialFolder = generated + "/NativeVisuals/", textureFolder = generated + "/TextureBudget";
            var materials = ReferencedMaterials(content);
            materials.RemoveWhere(material => !AssetDatabase.GetAssetPath(material).Replace('\\', '/').StartsWith(materialFolder, StringComparison.Ordinal));
            var hero = HeroMaterials(content.GetComponentInChildren<SteamJourney>(true));
            var requests = new Dictionary<string, Request>();
            var report = new Report { materials = materials.Count };
            foreach (var material in materials.OrderBy(m => AssetDatabase.GetAssetPath(m)))
            foreach (string property in material.GetTexturePropertyNames())
            {
                if (!(material.GetTexture(property) is Texture2D source)) continue;
                string sourcePath = AssetDatabase.GetAssetPath(source).Replace('\\', '/');
                if (string.IsNullOrEmpty(sourcePath)) throw new InvalidOperationException("Material texture is not a saved asset: " + material.name + "." + property);
                // Safe to call twice on an already prepared derivative.
                if (sourcePath.StartsWith(textureFolder + "/", StringComparison.Ordinal)) continue;
                var importer = AssetImporter.GetAtPath(sourcePath) as TextureImporter;
                bool normal = property == "_BumpMap" || property == "_NormalMap" || importer && importer.textureType == TextureImporterType.NormalMap;
                bool srgb = !normal && (importer ? importer.sRGBTexture : source.isDataSRGB);
                bool albedo = property == "_MainTex" || property == "_BaseMap";
                int limit = !normal && albedo && hero.Contains(material) ? HeroAlbedoLimit : TextureLimit;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long localId);
                if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("Missing texture GUID: " + sourcePath);
                string key = guid + "_" + localId + "_" + (normal ? "normal" : srgb ? "srgb" : "linear");
                if (!requests.TryGetValue(key, out var request))
                {
                    request = new Request { source = source, importer = importer, sourcePath = sourcePath, key = key,
                        normal = normal, srgb = srgb, limit = limit };
                    requests.Add(key, request);
                }
                request.limit = Mathf.Max(request.limit, limit);
                request.slots.Add(new Slot { material = material, property = property });
                report.textureReferences++;
            }
            Directory.CreateDirectory(textureFolder);
            AssetDatabase.Refresh();
            var records = new List<TextureRecord>();
            Material resample = null;
            try
            {
                foreach (var request in requests.Values.OrderBy(r => r.key))
                {
                    var source = request.source;
                    float factor = Mathf.Min(1, request.limit / (float)Mathf.Max(source.width, source.height));
                    int width = Mathf.Max(1, Mathf.RoundToInt(source.width * factor));
                    int height = Mathf.Max(1, Mathf.RoundToInt(source.height * factor));
                    string outputPath = textureFolder + "/" + request.key + ".png";
                    string stamp = Version + ":" + request.key + ":" + AssetDatabase.GetAssetDependencyHash(request.sourcePath) +
                        ":" + width + "x" + height + ":" + request.limit;
                    var output = AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath);
                    var outputImporter = AssetImporter.GetAtPath(outputPath) as TextureImporter;
                    bool reused = output && outputImporter && outputImporter.userData == stamp && output.width == width && output.height == height;
                    if (!reused)
                    {
                        if (!resample) resample = CreateResampleMaterial(textureFolder);
                        SaveResampledPng(request, width, height, outputPath, resample);
                        AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                        outputImporter = (TextureImporter)AssetImporter.GetAtPath(outputPath);
                        if (request.importer)
                        {
                            var settings = new TextureImporterSettings();
                            request.importer.ReadTextureSettings(settings);
                            outputImporter.SetTextureSettings(settings);
                            outputImporter.textureCompression = request.importer.textureCompression;
                            outputImporter.compressionQuality = request.importer.compressionQuality;
                            outputImporter.crunchedCompression = request.importer.crunchedCompression;
                        }
                        outputImporter.textureType = request.normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                        outputImporter.textureShape = TextureImporterShape.Texture2D;
                        outputImporter.sRGBTexture = request.srgb;
                        // PNG now contains canonical XYZ, decoded from the GPU's
                        // platform packing. Do not run height conversion/green
                        // inversion again on these already resolved pixels.
                        outputImporter.convertToNormalmap = false;
                        if (request.normal) outputImporter.flipGreenChannel = false;
                        outputImporter.maxTextureSize = request.limit;
                        outputImporter.npotScale = TextureImporterNPOTScale.None;
                        outputImporter.isReadable = false;
                        outputImporter.userData = stamp;
                        outputImporter.SaveAndReimport();
                        output = AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath);
                    }
                    if (!output || output.width > request.limit || output.height > request.limit)
                        throw new InvalidOperationException("Texture budget import did not produce a bounded texture: " + outputPath);
                    // The Spatial exporter reads image assets, so an importer-only
                    // maxTextureSize reduction is insufficient. Verify the physical
                    // PNG's IHDR as well as Unity's imported dimensions on every run.
                    var pngSize = ReadPngDimensions(outputPath);
                    if (pngSize.x != width || pngSize.y != height || pngSize.x > request.limit || pngSize.y > request.limit
                        || output.width != width || output.height != height)
                        throw new InvalidOperationException("Texture PNG/import dimensions disagree with budget: " + outputPath +
                            " png=" + pngSize + " imported=" + output.width + "x" + output.height + " expected=" + width + "x" + height);
                    report.validatedPngFiles++;
                    report.largestOutputDimension = Mathf.Max(report.largestOutputDimension, Mathf.Max(pngSize.x, pngSize.y));
                    foreach (var slot in request.slots)
                    {
                        slot.material.SetTexture(slot.property, output);
                        EditorUtility.SetDirty(slot.material);
                    }
                    int sourceWidth = source.width, sourceHeight = source.height;
                    if (request.importer) request.importer.GetSourceTextureWidthAndHeight(out sourceWidth, out sourceHeight);
                    bool mipmaps = request.importer ? request.importer.mipmapEnabled : source.mipmapCount > 1;
                    var record = new TextureRecord { source = request.sourcePath, output = outputPath,
                        role = request.normal ? "Normal RGB" : request.srgb ? "Color sRGB" : "Linear data",
                        sourceEncoding = request.normal ? "Unity imported normal packing" : request.srgb ? "sRGB" : "linear",
                        outputEncoding = request.normal ? "canonical tangent-space XYZ in RGB; NormalMap importer; no second height/green conversion" : request.srgb ? "sRGB PNG" : "linear PNG",
                        sourceWidth = sourceWidth, sourceHeight = sourceHeight, importedWidth = source.width, importedHeight = source.height,
                        width = output.width, height = output.height, references = request.slots.Count, srgb = request.srgb,
                        pixelLimit = request.limit, pngWidth = pngSize.x, pngHeight = pngSize.y,
                        normal = request.normal, mipmaps = mipmaps, reused = reused,
                        sourceFileBytes = File.Exists(request.sourcePath) ? new FileInfo(request.sourcePath).Length : 0,
                        pngBytes = new FileInfo(outputPath).Length,
                        sourceRgba32MipEstimate = EstimateRgba32(sourceWidth, sourceHeight, mipmaps),
                        importedRgba32MipEstimate = EstimateRgba32(source.width, source.height, mipmaps),
                        outputRgba32MipEstimate = EstimateRgba32(output.width, output.height, mipmaps) };
                    records.Add(record);
                    report.sourceFileBytes += record.sourceFileBytes; report.pngBytes += record.pngBytes;
                    report.sourceRgba32MipEstimate += record.sourceRgba32MipEstimate;
                    report.importedRgba32MipEstimate += record.importedRgba32MipEstimate;
                    report.outputRgba32MipEstimate += record.outputRgba32MipEstimate;
                    if (reused) report.cacheHits++;
                }
            }
            finally { if (resample) UnityEngine.Object.DestroyImmediate(resample); }
            report.uniqueCopies = records.Count; report.textures = records.ToArray();
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Builds/SwanJourney");
            File.WriteAllText("Builds/SwanJourney/texture-budget.json", JsonUtility.ToJson(report, true));
            Debug.Log("SWAN_JOURNEY_TEXTURE_BUDGET materials=" + report.materials + " references=" + report.textureReferences +
                " copies=" + report.uniqueCopies + " reused=" + report.cacheHits + " sourceBytes=" + report.sourceFileBytes +
                " pngBytes=" + report.pngBytes + " validatedPNGs=" + report.validatedPngFiles + " limits=1024/512 estimatedRGBA32WithMips=" + report.importedRgba32MipEstimate + "->" + report.outputRgba32MipEstimate);
            return report;
        }

        static HashSet<Material> ReferencedMaterials(Transform content)
        {
            var result = new HashSet<Material>();
            foreach (var renderer in content.GetComponentsInChildren<Renderer>(true))
                if (!renderer.GetComponent<TMP_Text>()) foreach (var material in renderer.sharedMaterials) if (material) result.Add(material);
            // Also capture generated materials used by runtime puffs/feedback,
            // which do not have their live renderers until Play Mode.
            foreach (var component in content.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!component || component is TMP_Text) continue;
                using (var serialized = new SerializedObject(component))
                {
                    var property = serialized.GetIterator();
                    while (property.Next(true)) if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue is Material material) result.Add(material);
                }
            }
            return result;
        }

        static HashSet<Material> HeroMaterials(SteamJourney journey)
        {
            var result = new HashSet<Material>();
            if (!journey) return result;
            void Add(Transform root)
            {
                if (!root) return;
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                    foreach (var material in renderer.sharedMaterials) if (material) result.Add(material);
            }
            Add(journey.trainAnchor);
            foreach (var module in journey.modules.Concat(journey.finishing))
            {
                if (module.preview) Add(module.preview.transform);
                foreach (var item in module.objects) if (item) Add(item.transform);
            }
            return result;
        }

        static long EstimateRgba32(int width, int height, bool mipmaps)
        {
            long bytes = 0;
            do { bytes += (long)width * height * 4; if (!mipmaps || width == 1 && height == 1) break;
                width = Mathf.Max(1, width / 2); height = Mathf.Max(1, height / 2); } while (true);
            return bytes;
        }

        static Vector2Int ReadPngDimensions(string path)
        {
            var header = new byte[24];
            using (var stream = File.OpenRead(path))
                if (stream.Read(header, 0, header.Length) != header.Length)
                    throw new InvalidOperationException("Truncated budget texture PNG: " + path);
            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            for (int i = 0; i < signature.Length; i++)
                if (header[i] != signature[i]) throw new InvalidOperationException("Budget texture is not PNG: " + path);
            if (header[12] != 'I' || header[13] != 'H' || header[14] != 'D' || header[15] != 'R')
                throw new InvalidOperationException("Budget texture PNG has no IHDR: " + path);
            int ReadInt(int offset) => (header[offset] << 24) | (header[offset+1] << 16) | (header[offset+2] << 8) | header[offset+3];
            int width = ReadInt(16), height = ReadInt(20);
            if (width <= 0 || height <= 0) throw new InvalidOperationException("Invalid budget PNG size: " + path);
            return new Vector2Int(width, height);
        }

        static void SaveResampledPng(Request request, int width, int height, string path, Material material)
        {
            var previous = RenderTexture.active;
            bool previousWrite = GL.sRGBWrite;
            RenderTexture target = null; Texture2D pixels = null;
            try
            {
                // Keep the destination linear and explicitly encode color only
                // in Linear projects. Normal RGB must never receive sRGB gamma.
                target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                target.filterMode = FilterMode.Bilinear;
                GL.sRGBWrite = false;
                material.SetFloat("_IsNormal", request.normal ? 1 : 0);
                material.SetFloat("_PackedNormal", request.normal && request.importer && request.importer.textureType == TextureImporterType.NormalMap ? 1 : 0);
                material.SetFloat("_EncodeSRGB", request.srgb && QualitySettings.activeColorSpace == ColorSpace.Linear ? 1 : 0);
                Graphics.Blit(request.source, target, material);
                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                pixels.Apply(false, false);
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; GL.sRGBWrite = previousWrite;
                if (target) RenderTexture.ReleaseTemporary(target);
                if (pixels) UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        static Material CreateResampleMaterial(string folder)
        {
            string path = folder + "/TextureBudgetResample.shader";
            if (!File.Exists(path) || File.ReadAllText(path) != ResampleShader) File.WriteAllText(path, ResampleShader);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (!shader || !shader.isSupported) throw new InvalidOperationException("Texture resampling shader unavailable");
            return new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        const string ResampleShader = @"Shader ""Hidden/SwanJourney/TextureBudgetResample"" {
Properties { _MainTex (""Texture"", 2D) = ""white"" {} }
SubShader { Cull Off ZWrite Off ZTest Always Pass {
CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#include ""UnityCG.cginc""
sampler2D _MainTex;
float _IsNormal, _PackedNormal, _EncodeSRGB;
float4 frag(v2f_img input) : SV_Target {
    float4 value = tex2D(_MainTex, input.uv);
    if (_IsNormal > .5) {
        float3 direction = _PackedNormal > .5 ? UnpackNormal(value) : value.rgb * 2 - 1;
        direction = normalize(direction + float3(0, 0, 1e-8));
        return float4(direction * .5 + .5, 1);
    }
    if (_EncodeSRGB > .5) value.rgb = LinearToGammaSpace(value.rgb);
    return value;
}
ENDCG
} } }
";
    }
}
