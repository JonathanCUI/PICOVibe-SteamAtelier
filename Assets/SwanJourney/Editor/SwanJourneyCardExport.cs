using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SwanJourney.Editor
{
    public static class SwanJourneyCardExport
    {
        const string MaterialsPath = "Assets/SwanJourney/Generated/CardSurfaces";

        // Replace the builder's ui.PrepareForExport call with this entry, before
        // MeshAudit. Original partner scenes and runtime desktop UI are untouched.
        public static void Prepare(SwanJourneyNativeUi ui, bool writeReport = true)
        {
            if (!ui || !ui.panelMaterial) throw new InvalidOperationException("Card export requires the bound native UI and panel material.");
            ui.PrepareForExport();
            if (!AssetDatabase.IsValidFolder(MaterialsPath))
                AssetDatabase.CreateFolder("Assets/SwanJourney/Generated", "CardSurfaces");
            var materials = new Dictionary<string, Material>();
            Material Resolve(Color color)
            {
                string key = ColorUtility.ToHtmlStringRGBA(color);
                if (materials.TryGetValue(key, out var cached)) return cached;
                string path = MaterialsPath + "/Card_" + key + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material)
                {
                    material = new Material(ui.panelMaterial) { name = "Swan card " + key };
                    AssetDatabase.CreateAsset(material, path);
                }
                material.color = color;
                bool transparent = color.a < .999f;
                material.SetFloat("_Mode", transparent ? 2 : 0);
                material.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
                material.SetInt("_SrcBlend", (int)(transparent ? BlendMode.SrcAlpha : BlendMode.One));
                material.SetInt("_DstBlend", (int)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
                material.SetInt("_ZWrite", transparent ? 0 : 1);
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.DisableKeyword("_EMISSION");
                if (transparent) material.EnableKeyword("_ALPHABLEND_ON"); else material.DisableKeyword("_ALPHABLEND_ON");
                material.renderQueue = transparent ? (int)RenderQueue.Transparent : -1;
                EditorUtility.SetDirty(material);
                materials.Add(key, material);
                return material;
            }
            int converted = ui.ConvertPreparedCardImages(Resolve);
            int cards = 0, surfaces = 0;
            foreach (var controller in ui.content.GetComponentsInChildren<StudioPartCards>(true))
            {
                if (!controller.journey || !controller.journey.SwanInputActive) continue;
                var canvas = controller.PreparedSpatialCanvas;
                if (!canvas) throw new InvalidOperationException("Native card export lost its prepared canvas.");
                if (canvas.GetComponentsInChildren<UnityEngine.UI.Image>(true).Length != 0)
                    throw new InvalidOperationException("Native card export retained an Image path.");
                var renderers = canvas.GetComponentsInChildren<MeshRenderer>(true)
                    .Where(r => r.name.StartsWith("Swan card surface ", StringComparison.Ordinal)).ToArray();
                if (renderers.Length != 14) throw new InvalidOperationException("Expected two panels and twelve frame meshes per card canvas.");
                foreach (var renderer in renderers)
                {
                    if (!renderer.name.StartsWith("Swan card surface ", StringComparison.Ordinal) ||
                        !renderer.GetComponent<MeshFilter>() || !renderer.GetComponent<MeshFilter>().sharedMesh ||
                        renderer.transform.parent.GetComponent<CanvasRenderer>() ||
                        renderer.sharedMaterials.Any(m => !m || !EditorUtility.IsPersistent(m)))
                        throw new InvalidOperationException("Card surface is missing geometry/persistent material or retains a duplicate UI renderer: " + renderer.name);
                }
                cards++; surfaces += renderers.Length;
            }
            AssetDatabase.SaveAssets();
            if (writeReport)
            {
                Directory.CreateDirectory("Builds/SwanJourney");
                File.WriteAllText("Builds/SwanJourney/card-native-mesh-export.json", JsonUtility.ToJson(new Report
                {
                    passed = true, cardCanvases = cards, convertedImages = converted, meshSurfaces = surfaces,
                    materialAssets = materials.Values.Select(AssetDatabase.GetAssetPath).ToArray()
                }, true));
                Debug.Log("SWAN_JOURNEY_CARD_NATIVE_MESH_EXPORT cards=" + cards + " convertedImages=" + converted + " surfaces=" + surfaces);
            }
        }

        [Serializable] sealed class Report
        {
            public bool passed, nativeRenderingValidated;
            public string scope = "Derived part-card Image/CanvasRenderer removal; original TMP ancestry and persistent colored mesh backgrounds.";
            public int cardCanvases, convertedImages, meshSurfaces;
            public string[] materialAssets;
        }
    }
}
