using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace SwanJourney.Editor
{
    public static class SwanJourneyIcon
    {
        const string MainPath = "Assets/SwanJourney/Branding/SteamAtelierIcon.png";
        const string RoundPath = "Assets/SwanJourney/Branding/SteamAtelierIconRound.png";
        const string AdaptivePath = "Assets/SwanJourney/Branding/SteamAtelierIconAdaptive.png";
        const string ClearPath = "Assets/SwanJourney/Branding/SteamAtelierIconTransparent.asset";
        const string ReportPath = "Builds/SwanJourney/icon-config.json";

        [Serializable] sealed class Slot
        {
            public string kind;
            public int width, height, layers;
            public string[] textures;
        }
        [Serializable] sealed class Report
        {
            public string applicationName, defaultIcon, roundIcon, adaptiveBackground, adaptiveForeground;
            public int defaultSlots, androidSlots;
            public string scope = "Unity default and Android icon slots; generated PNG pixels unchanged; APK resources require build verification";
            public List<Slot> slots = new List<Slot>();
        }

        public static void Configure()
        {
            var main = ImportOpaqueIcon(MainPath);
            var round = ImportOpaqueIcon(RoundPath);
            var adaptive = ImportOpaqueIcon(AdaptivePath);
            var clear = TransparentForeground();
            var report = new Report
            {
                applicationName = PlayerSettings.productName,
                defaultIcon = MainPath,
                roundIcon = RoundPath,
                adaptiveBackground = AdaptivePath,
                adaptiveForeground = ClearPath
            };

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { main }, IconKind.Any);
            var defaults = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
            if (defaults.Length == 0 || defaults.Any(texture => texture != main))
                throw new InvalidOperationException("Unity default icon assignment did not persist.");
            report.defaultSlots = defaults.Length;

            ConfigureAndroid(AndroidPlatformIconKind.Legacy, "Legacy", new[] { main }, report);
            ConfigureAndroid(AndroidPlatformIconKind.Round, "Round", new[] { round }, report);
            // Unity 2022.3 Android layer order is background=0, foreground=1.
            // A transparent foreground preserves the single generated composition
            // without duplicating it into independently moving adaptive layers.
            ConfigureAndroid(AndroidPlatformIconKind.Adaptive, "Adaptive", new[] { adaptive, clear }, report);
            var settings = AssetDatabase.LoadMainAssetAtPath("ProjectSettings/ProjectSettings.asset");
            if (settings) EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            Debug.Log("SWAN_JOURNEY_ICON_CONFIGURED defaultSlots=" + report.defaultSlots
                + " androidSlots=" + report.androidSlots + " report=" + ReportPath);
        }

        static Texture2D ImportOpaqueIcon(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Generated launcher icon is missing.", path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new InvalidOperationException("Launcher icon is not a texture asset: " + path);
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            // An earlier per-platform override must not silently compress branding.
            importer.ClearPlatformTextureSettings("Android");
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!texture || texture.width != texture.height)
                throw new InvalidOperationException("Launcher artwork must import as a square Texture2D: " + path);
            return texture;
        }

        static Texture2D TransparentForeground()
        {
            var created = new Texture2D(4, 4, TextureFormat.RGBA32, false, false)
            {
                name = "Steam Atelier transparent adaptive foreground",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            created.SetPixels(new Color[16]);
            created.Apply(false, false);
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(ClearPath);
            if (existing)
            {
                EditorUtility.CopySerialized(created, existing);
                UnityEngine.Object.DestroyImmediate(created);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(created, ClearPath);
            return created;
        }

        static void ConfigureAndroid(PlatformIconKind kind, string name, Texture2D[] textures, Report report)
        {
            var slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            if (slots.Length == 0) throw new InvalidOperationException("Android icon slots unavailable: " + name);
            foreach (var slot in slots)
            {
                if (textures.Length < slot.minLayerCount || textures.Length != slot.maxLayerCount)
                    throw new InvalidOperationException("Unexpected Android icon layer count: " + name);
                slot.SetTextures(textures);
                report.slots.Add(new Slot
                {
                    kind = name, width = slot.width, height = slot.height,
                    layers = textures.Length, textures = textures.Select(AssetDatabase.GetAssetPath).ToArray()
                });
            }
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, slots);
            report.androidSlots += slots.Length;
        }
    }
}
