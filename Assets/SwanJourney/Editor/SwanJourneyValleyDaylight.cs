using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SwanJourney.Editor
{
    internal static class SwanJourneyValleyDaylight
    {
        internal static void Configure(SteamJourney journey, SwanJourneyPresentation presentation)
        {
            if (!journey.SwanInputActive || !journey.bakedChimney || !journey.furnaceParticles)
                throw new InvalidOperationException("Expected the Swan locomotive with native chimney and furnace effects.");
            var root = journey.spatialRoot;
            var weather = journey.GetComponent<MesaAtmosphereV14>();
            var enhance = journey.GetComponent<MesaEnhanceV15>();
            var clouds = journey.GetComponent<MesaAtmosV16>();
            var sky = root.GetComponentInChildren<MesaDayNightV13>(true);
            var refine = journey.GetComponent<StudioSpatialRefineV19>();
            if (!sky || !weather || !enhance || !clouds || !refine || sky.clouds.Length != 3)
                throw new InvalidOperationException("Missing source sky, cloud or lamp bindings.");
            journey.fixedValleyDaylight = true;
            clouds.cloudTransforms = sky.clouds.ToArray();
            clouds.sky = null;
            clouds.previous = null;
            refine.sky = null;
            var retired = new HashSet<GameObject>();
            foreach (var emitter in weather.dust) retired.Add(emitter.gameObject);
            foreach (var lamp in weather.lamps) retired.Add(lamp.gameObject);
            foreach (var r in weather.beams.Concat(weather.halos).Concat(enhance.flares)) retired.Add(r.gameObject);
            foreach (var body in new[] {sky.sun, sky.moon, sky.sunHalo, sky.moonHalo}) retired.Add(body.gameObject);
            // The native chimney owns the plume in both modes.
            retired.Add(enhance.plume.gameObject);
            if (refine.cabLight) { retired.Add(refine.cabLight.gameObject); refine.cabLight = null; }

            foreach (var bulb in weather.bulbs.Concat(refine.passengerWindows).Where(r => r))
                foreach (var material in bulb.sharedMaterials) TurnOffEmission(material);
            foreach (var t in journey.trainAnchor.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "真实车灯聚光灯" || t.name == "可开关车灯光束") retired.Add(t.gameObject);
                if (t.name == "车灯发光芯" && t.TryGetComponent<MeshRenderer>(out var lens))
                    foreach (var material in lens.sharedMaterials) TurnOffEmission(material);
            }
            var anchor = presentation.sandboxMoveHandleAnchor;
            if (!anchor || !anchor.GetComponentsInChildren<SwanJourneyTarget>(true).Any(t=>t.action==JourneyAction.Lights)
                || !SwanJourneyPresentation.TryBounds(anchor, anchor, out presentation.sandboxHandleFootprint))
                throw new InvalidOperationException("Missing driving handle layout footprint.");
            foreach (Transform child in anchor) retired.Add(child.gameObject);
            anchor.name = "Driving move handle layout anchor";

            journey.detailMeshes = journey.detailMeshes.Where(d => d.filter && !retired.Contains(d.filter.gameObject)).ToArray();
            journey.finishing.Last().observation = "观察车头两侧的灯座，确认两盏前照灯同时就位。";
            foreach (var go in retired) if (go) Object.DestroyImmediate(go);
            Object.DestroyImmediate(enhance);
            Object.DestroyImmediate(weather);
            if (sky.transform.childCount == 0 && sky.GetComponents<Component>().Length == 2)
                Object.DestroyImmediate(sky.gameObject);
            else Object.DestroyImmediate(sky);
            EditorUtility.SetDirty(journey); EditorUtility.SetDirty(refine); EditorUtility.SetDirty(clouds);
            EditorUtility.SetDirty(presentation);
            // Purge material bindings for the removed effects after all scene refs are gone.
            SwanJourneyEffectCleanup.Apply(journey);
        }

        static void TurnOffEmission(Material material)
        {
            if (!material || !material.HasProperty("_EmissionColor")) return;
            if (!AssetDatabase.GetAssetPath(material).StartsWith(SwanJourneyBuild.Generated + "/", StringComparison.Ordinal))
                throw new InvalidOperationException("Expected an exported lamp material: " + material.name);
            material.SetColor("_EmissionColor", Color.black);
            if (material.HasProperty("_EmissionMap")) material.SetTexture("_EmissionMap", null);
            material.DisableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            EditorUtility.SetDirty(material);
        }
    }
}
