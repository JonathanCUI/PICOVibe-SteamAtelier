using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

namespace SwanJourney.Editor
{
    // Remove desktop-only dependencies from the native derivative. The desktop
    // source retains its shaders; PICO draws the already converted materials.
    internal static class SwanJourneyEffectCleanup
    {
        internal static void Apply(SteamJourney journey)
        {
            if (!journey || !journey.SwanInputActive) throw new InvalidOperationException("Swan scene required");
            var root = journey.spatialRoot;
            var bridge = journey.GetComponent<SwanJourneyVisualBridge>();
            var arrival = journey.GetComponent<JourneySceneArrival>();
            if (arrival)
            {
                arrival.enabled = false;
                arrival.solidShader = arrival.ghostShader = null;
                arrival.contours = Array.Empty<JourneyArrivalEdges>();
                EditorUtility.SetDirty(arrival);
            }
            foreach (var finish in root.GetComponentsInChildren<StudioAssemblyFinishV9>(true))
            {
                finish.finishShader = null;
                finish.sparkleMaterial = finish.pulseLineMaterial = null;
                EditorUtility.SetDirty(finish);
            }
            foreach (var volume in root.GetComponentsInChildren<Volume>(true))
            {
                volume.enabled = false;
                volume.sharedProfile = null;
                EditorUtility.SetDirty(volume);
            }
            foreach (var lighting in root.GetComponentsInChildren<SpatialLightingV20>(true))
            {
                lighting.enabled = false;
                lighting.tunnelAmbient = null;
                EditorUtility.SetDirty(lighting);
            }
            // Water heat is already translated into native color by VisualBridge.
            // These references existed only for the discarded shader's uniforms.
            journey.waterSkin = journey.waterBody = null;
            if (journey.bakedFirebox) journey.emberMaterial = journey.flameMaterial = null;
            if (journey.bakedChimney)
            {
                foreach (var steam in root.GetComponentsInChildren<WorkshopSteamV20>(true))
                { steam.material = null; EditorUtility.SetDirty(steam); }
                foreach (var steam in root.GetComponentsInChildren<MesaAtmosV16>(true))
                { steam.steamMaterial = null; EditorUtility.SetDirty(steam); }
            }
            var flow = journey.GetComponent<SwanJourneyFlowParticles>();
            if (flow && flow.UsesArrows)
            {
                foreach (var source in flow.sources) source.alphaMaterials = Array.Empty<Material>();
                EditorUtility.SetDirty(flow);
            }
            if (bridge)
            {
                bridge.scanMaterial = null;
                bridge.ringMesh = null;
                // Keep all materials reachable from scene components, including
                // sources used to create runtime puffs, windows and card leaders.
                var used = new HashSet<Material>();
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                {
                    if (!component || component == bridge) continue;
                    var serialized = new SerializedObject(component);
                    var property = serialized.GetIterator();
                    while (property.Next(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference
                            && property.objectReferenceValue is Material material) used.Add(material);
                }
                bridge.materials = bridge.materials.Where(d => d != null && d.material
                    && (d.kind == JourneyNativeMaterialKind.Surface || used.Contains(d.material))).ToArray();
                EditorUtility.SetDirty(bridge);
            }
            EditorUtility.SetDirty(journey);
        }
    }
}
