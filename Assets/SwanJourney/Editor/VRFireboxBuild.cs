using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SwanJourney.Editor
{
    internal static class VRFireboxBuild
    {
        internal const string Art = "Assets/SwanJourney/Branding/FurnaceFlame";
        internal const string Coal = "Assets/SwanJourney/Generated/WorkshopBudget/SmallFireboxCoal.asset";

        internal static void Configure(SteamJourney journey, SwanJourneyVisualBridge bridge)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Art + ".mat");
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Art + ".asset");
            if (!material || !mesh) throw new InvalidOperationException("Missing baked firebox art");
            var r = journey.flameVolume;
            r.GetComponent<MeshFilter>().sharedMesh = mesh;
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            // The shape is authored about the coal bed rather than its bounding-box centre.
            if (!journey.bakedFirebox) r.transform.localPosition -= Vector3.up * (.48f * r.transform.localScale.y);
            r.enabled = false;
            r.gameObject.SetActive(false);
            if (journey.furnaceLight) { journey.furnaceLight.enabled = false; journey.furnaceLight.gameObject.SetActive(false); }
            journey.bakedFirebox = true;
            bridge.flameVolume = mesh;
            var waves = journey.GetComponent<StudioWaterImpulseV10>();
            if (waves) waves.enabled = false;
            SwanJourneyCampfireBuild.Configure(journey);
            ApplyCoal(journey);
            EditorUtility.SetDirty(journey);
            EditorUtility.SetDirty(bridge);
        }

        static void ApplyCoal(SteamJourney journey)
        {
            var meshes = AssetDatabase.LoadAllAssetsAtPath(Coal).OfType<Mesh>().ToDictionary(m => m.name);
            if (meshes.Count == 0) throw new InvalidOperationException("Missing tabletop coal meshes");
            foreach (var r in journey.emberRenderers)
            {
                if (!r || !r.TryGetComponent<MeshFilter>(out var filter) || !filter.sharedMesh) continue;
                string path = AssetDatabase.GetAssetPath(filter.sharedMesh);
                if (path == Coal) continue;
                var extent = filter.sharedMesh.bounds.size;
                var scale = filter.transform.lossyScale;
                float mm = Mathf.Max(extent.x, extent.y, extent.z) * Mathf.Max(scale.x, scale.y, scale.z) * 1000f;
                int tier = mm < 18 ? 0 : mm < 34 ? 1 : 2;
                if (meshes.TryGetValue("Coal_" + AssetDatabase.AssetPathToGUID(path) + "_" + tier, out var reduced))
                {
                    filter.sharedMesh = reduced;
                    // Mode changes restore inspection/miniature bindings from this list.
                    foreach (var detail in journey.detailMeshes)
                    {
                        if (detail.filter != filter) continue;
                        if (AssetDatabase.GetAssetPath(detail.inspection) == path) detail.inspection = reduced;
                        if (AssetDatabase.GetAssetPath(detail.miniature) == path) detail.miniature = reduced;
                    }
                }
            }
        }
    }
}
