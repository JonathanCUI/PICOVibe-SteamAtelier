using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SwanJourney.Editor
{
    public static class SwanJourneySceneSplit
    {
        [Serializable] sealed class Report
        {
            public string startupScene, deferredScene, trigger = "Complete assembly then choose Drive";
            public int startupMeshFilters, deferredMeshFilters, removedDesktopContourReferences;
            public long startupTriangles, deferredTriangles;
            public bool crossSceneObjectReferencesAbsent;
            public string materialRegistration = "SDK global material lookup retained; geometry is deferred, not all texture registration";
        }

        public static void Split(SteamJourney journey, SwanJourneyPresentation presentation,
            SwanJourneyVisualBridge visuals, Material panelMaterial)
        {
            var coreScene = journey.gameObject.scene;
            var canyon = journey.sandboxRoot.GetComponentsInChildren<Transform>(true)
                .Single(t => t.name.StartsWith("Mesa original canyon", StringComparison.Ordinal));
            var report = new Report { startupScene = SwanJourneyBuild.ScenePath, deferredScene = SwanJourneyBuild.EnvironmentScenePath };
            ValidateBoundary(coreScene, canyon);

            var anchor = new GameObject("Deferred canyon placement anchor").transform;
            anchor.SetParent(canyon.parent, false);
            anchor.localPosition = canyon.localPosition;
            anchor.localRotation = canyon.localRotation;
            anchor.localScale = canyon.localScale;

            var payload = canyon.gameObject.AddComponent<SwanJourneyEnvironment>();
            canyon.gameObject.AddComponent<SwanJourneyTransformSyncBudget>();
            payload.supports = presentation.sandboxSupports.Where(t => Inside(t, canyon)).ToArray();
            presentation.sandboxSupports = presentation.sandboxSupports.Where(t => !Inside(t, canyon)).ToArray();
            foreach (var sky in journey.spatialRoot.GetComponentsInChildren<MesaDayNightV13>(true))
                if (sky.environment && Inside(sky.environment.transform, canyon)) sky.environment = anchor.gameObject;
            foreach (var refinement in journey.spatialRoot.GetComponentsInChildren<StudioSpatialRefineV19>(true))
            {
                foreach (var scenery in refinement.scenery.Where(g => g && Inside(g.transform, canyon))) scenery.SetActive(false);
                refinement.scenery = refinement.scenery.Where(g => !g || !Inside(g.transform, canyon)).ToArray();
            }

            // Swan has no scene-reveal sweep. Keep its disabled controller for
            // callers that query Active, without desktop shader dependencies.
            var arrival = journey.GetComponent<JourneySceneArrival>();
            if (!arrival || !journey.SwanInputActive || !visuals || visuals.journey != journey)
                throw new InvalidOperationException("Swan visual binding must exist before dropping desktop contour dependencies");
            report.removedDesktopContourReferences = arrival.contours?.Length ?? 0;
            arrival.contours = Array.Empty<JourneyArrivalEdges>();
            SwanJourneyCanyonInactiveGeometry.Apply(canyon);
            SwanJourneyCanyonMeshBudget.Apply(canyon, SwanJourneyBuild.Generated);

            // An additive payload needs its own explicitly registered root, even
            // when all geometry is below empty grouping transforms. Disabled:
            // never participates in hit testing, physics, or tabletop placement.
            var rootSync = canyon.gameObject.AddComponent<BoxCollider>();
            rootSync.size = Vector3.one * .001f;
            rootSync.enabled = false;

            var loader = presentation.gameObject.AddComponent<SwanJourneySceneLoader>();
            loader.journey = journey; loader.environmentAnchor = anchor;
            loader.presentation = presentation; loader.visuals = visuals;
            loader.environmentScenePath = SwanJourneyBuild.EnvironmentScenePath;

            var environmentScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                // Export the same metre-sized miniature used by driving mode,
                // rather than a 30-metre identity-pose landscape at the origin.
                // Runtime Follow still owns placement and two-hand scaling.
                float ratio=presentation.sandboxScale/presentation.workshopScale;
                var initialPosition=presentation.transform.TransformPoint(
                    presentation.transform.InverseTransformPoint(anchor.position)*ratio);
                var initialRotation=anchor.rotation;
                var initialScale=anchor.lossyScale*ratio;
                canyon.SetParent(null, false);
                SceneManager.MoveGameObjectToScene(canyon.gameObject, environmentScene);
                canyon.SetPositionAndRotation(initialPosition, initialRotation);
                canyon.localScale = initialScale;
                SwanJourneyMiniatureGeometry.Apply(payload, presentation.sandboxScale);
                SwanJourneyPebbleBudget.Apply(payload);
                SwanJourneyTerrainTileOrigins.Apply(payload);
                SwanJourneyEmptyNodes.Prune(canyon);
                // This file is loaded only after Drive is requested. Export an
                // active root: the adapter initializes its activation cache to
                // true, so activating an initially hidden GLB before its first
                // frame can otherwise omit the native activation transition.
                canyon.gameObject.SetActive(true);
                // A gaze landing on the landscape tray can start two-hand
                // manipulation, even though this scene is not parented to stage.
                foreach(var support in payload.supports)
                    if(SwanJourneyPresentation.TryBounds(support,support,out var bounds))
                    {
                        var hit=support.gameObject.AddComponent<BoxCollider>();
                        hit.center=bounds.center;hit.size=bounds.size;
                    }
                var environmentUi = canyon.gameObject.AddComponent<SwanJourneyNativeUi>();
                environmentUi.content = canyon; environmentUi.panelMaterial = panelMaterial;
                ValidateNoCrossSceneReferences(coreScene);
                ValidateNoCrossSceneReferences(environmentScene);
                report.crossSceneObjectReferencesAbsent = true;
                var coreMeshes = coreScene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshFilter>(true)).ToArray();
                var extraMeshes = canyon.GetComponentsInChildren<MeshFilter>(true);
                report.startupMeshFilters = coreMeshes.Length; report.deferredMeshFilters = extraMeshes.Length;
                report.startupTriangles = Triangles(coreMeshes); report.deferredTriangles = Triangles(extraMeshes);
                EditorSceneManager.SaveScene(environmentScene, SwanJourneyBuild.EnvironmentScenePath);
            }
            finally
            {
                EditorSceneManager.CloseScene(environmentScene, true);
                SceneManager.SetActiveScene(coreScene);
            }
            File.WriteAllText(SwanJourneyBuild.Output + "/scene-split.json", JsonUtility.ToJson(report, true));
            Debug.Log("SWAN_JOURNEY_SCENE_SPLIT startupTriangles=" + report.startupTriangles
                + " deferredTriangles=" + report.deferredTriangles + " deferredMeshes=" + report.deferredMeshFilters
                + " desktopContourEntriesRemoved=" + report.removedDesktopContourReferences);
        }

        static bool Inside(Transform target, Transform root) => target && (target == root || target.IsChildOf(root));
        static Transform ObjectTransform(UnityEngine.Object value) => value is GameObject go ? go.transform : value is Component c ? c.transform : null;
        static long Triangles(IEnumerable<MeshFilter> filters)
        {
            long result = 0;
            foreach (var f in filters) if (f && f.sharedMesh)
                for (int i = 0; i < f.sharedMesh.subMeshCount; i++)
                    if (f.sharedMesh.GetTopology(i) == MeshTopology.Triangles) result += (long)f.sharedMesh.GetIndexCount(i) / 3;
            return result;
        }

        static void ValidateBoundary(Scene scene, Transform canyon)
        {
            foreach (var c in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Component>(true)))
            {
                if (!c || c is Transform || Inside(c.transform, canyon)) continue;
                var serialized = new SerializedObject(c);
                var p = serialized.GetIterator();
                while (p.Next(true))
                {
                    if (p.propertyType != SerializedPropertyType.ObjectReference || !Inside(ObjectTransform(p.objectReferenceValue), canyon)) continue;
                    bool handled = c is SwanJourneyPresentation && p.propertyPath.StartsWith("sandboxSupports.Array.data[", StringComparison.Ordinal)
                        || c is MesaDayNightV13 && p.propertyPath == "environment"
                        || c is StudioSpatialRefineV19 && p.propertyPath.StartsWith("scenery.Array.data[", StringComparison.Ordinal);
                    if (!handled) throw new InvalidOperationException("Unhandled canyon reference: " + c.GetType().Name + "." + p.propertyPath);
                }
            }
        }

        static void ValidateNoCrossSceneReferences(Scene scene)
        {
            foreach (var c in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Component>(true)))
            {
                if (!c) continue;
                var serialized = new SerializedObject(c);
                var p = serialized.GetIterator();
                while (p.Next(true))
                {
                    if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var target = ObjectTransform(p.objectReferenceValue);
                    if (target && target.gameObject.scene.IsValid() && target.gameObject.scene != scene)
                        throw new InvalidOperationException("Cross-scene reference: " + c.GetType().Name + "." + p.propertyPath + " -> " + target.name);
                }
            }
        }
    }
}
