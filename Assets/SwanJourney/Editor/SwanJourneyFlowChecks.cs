using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ByteDance.PICO.SpatialAdapter.Component;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SwanJourney.Editor
{
    // Run via -executeMethod SwanJourney.Editor.SwanJourneyFlowChecks.Run -quit.
    // No Play mode, P2D connection, SpatialCamera.Awake or native-rendering claim.
    public static class SwanJourneyFlowChecks
    {
        const string ScenePath = "Assets/SwanJourney/Scenes/SteamJourneySwan.unity";
        const string ReportPath = "Builds/SwanJourney/flow-checks.json";
        const float Tolerance = .00003f;

        [Serializable] sealed class Report
        {
            public string scope = "Editor-only source CPU particle and mesh-bridge logic; no Play/P2D/native device validation";
            public bool passed, nativeRenderingValidated;
            public string sourcePath;
            public int sourceStage, sourceCount, poolCapacity, sourceParticles, visibleMarkers, representedFields;
            public float maximumPositionError, maximumScaleError;
            public List<string> checks = new List<string>();
            public string failure;
        }

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Flow smoke check must run in Edit Mode.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or close the current dirty scene before running this independent check.");

            var report = new Report();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            bool opened = false;
            try
            {
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single); opened = true;
                var flow = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SwanJourneyFlowParticles>(true)).Single();
                var journey = flow.journey;
                Check(journey && flow.poolRoot, "Journey and pool root are bound", report);
                report.sourceCount = flow.SourceCount; report.poolCapacity = flow.PoolCapacity;
                Check(flow.SourceCount == 125 && flow.sources.All(s => s.field && s.field.system), "All 125 authored fields remain bound", report);
                Check(flow.PoolCapacity == 288 && flow.markers.All(r => r), "Fixed 288-slot pool is complete", report);
                Check(flow.poolRoot.GetComponentsInChildren<Collider>(true).Length == 0
                    && flow.poolRoot.GetComponentsInChildren<SpatialAdapterHoverEffect>(true).Length == 0,
                    "Flow pool has no input collider or hover component", report);
                Check(flow.markers.Select(r => r.GetComponent<MeshFilter>().sharedMesh).Distinct().Count() == 1,
                    "All slots share one pre-exported mesh", report);
                Check(flow.sources.All(s => s.alphaMaterials != null && s.alphaMaterials.Length == SwanJourneyFlowParticles.AlphaLevels.Length
                    && s.alphaMaterials.All(m => m && AssetDatabase.Contains(m))), "All flow colors use saved material palettes", report);

                // A real authored, local-space upstream source avoids manually inventing particle
                // state or forcing downstream fronts. Other fields are left intact and unadvanced.
                var source = flow.sources.Where(s => s.field && s.field.system && !s.field.upstream
                    && s.field.points != null && s.field.points.Length > 1
                    && s.field.system.main.simulationSpace == ParticleSystemSimulationSpace.Local)
                    .OrderByDescending(s => PathLength(s.field.points)).FirstOrDefault();
                Check(source != null, "An actual non-upstream local-space field is available", report);
                var field = source.field; var system = field.system;
                report.sourcePath = ObjectPath(field.transform); report.sourceStage = field.stage;
                Transform workbench = flow.poolRoot.parent;
                Check(workbench && system.transform.IsChildOf(workbench), "Source and output share the movable workbench", report);
                Check(system.main.scalingMode == ParticleSystemScalingMode.Hierarchy, "Source sizes inherit the Swan workbench scale", report);
                ActivateAncestors(system.transform); ActivateAncestors(flow.poolRoot); ActivateAncestors(flow.transform);
                journey.SwanInputActive = true; journey.cutaway = true; journey.inSandbox = false;
                foreach (var entry in flow.sources) entry.field.system.GetComponent<ParticleSystemRenderer>().enabled = false;
                field.ResetField();
                // Use the real source method, including its path, speed, spawning and fade rules.
                for (int step = 0; step < 40; step++) field.Advance(.05f, 1f);
                var particles = new ParticleSystem.Particle[384];
                int count = system.GetParticles(particles); report.sourceParticles = count;
                Check(count > 0 && particles.Take(count).Any(p => p.remainingLifetime > 0 && p.GetCurrentColor(system).a > 5),
                    "Authored Advance produces visible particles before the bridge runs", report);

                flow.SendMessage("Awake", SendMessageOptions.RequireReceiver);
                Refresh(flow, system);
                report.visibleMarkers = flow.ActiveMarkerCount; report.representedFields = flow.RepresentedFieldCount;
                Check(flow.ActiveMarkerCount > 0 && flow.ActiveMarkerCount <= flow.PoolCapacity,
                    "GetParticles drives a nonempty bounded marker set", report);
                Check(flow.ActiveFieldCount == 1 && flow.RepresentedFieldCount == 1,
                    "Exactly the advanced source path is represented", report);
                Check(flow.ActiveMarkerCount <= SwanJourneyFlowParticles.SamplesPerField,
                    "One source uses at most four sparse samples", report);
                Check(flow.markers.Count(r => r.enabled && r.gameObject.activeInHierarchy) == flow.ActiveMarkerCount,
                    "Active count matches actual enabled output renderers", report);
                ValidateSourceMapping(flow, system, particles, count, field.tint, report);
                Check(report.maximumPositionError < Tolerance && report.maximumScaleError < Tolerance,
                    "Marker positions and sizes match actual source particles", report);

                var visible = flow.markers.Where(r => r.enabled).ToArray();
                var localPositions = visible.Select(r => workbench.InverseTransformPoint(r.transform.position)).ToArray();
                var sizes = visible.Select(r => Abs(r.transform.lossyScale)).ToArray();
                var frozen = visible.Select(r => r.transform.position).ToArray();
                Refresh(flow, system);
                Check(visible.Select((r, i) => Vector3.Distance(r.transform.position, frozen[i])).Max() < Tolerance,
                    "Refreshing without advancing the source does not advance the flow", report);

                workbench.position += new Vector3(.37f, .19f, -.22f);
                workbench.rotation = Quaternion.Euler(0, 29, 0) * workbench.rotation;
                workbench.localScale *= 1.6f;
                // Disturb the output before refreshing so this tests the bridge's mapping,
                // rather than merely inheriting the parent's transform automatically.
                foreach (var renderer in visible) renderer.transform.position += Vector3.one * .1f;
                Refresh(flow, system);
                float movedPositionError = 0, movedScaleError = 0;
                for (int i = 0; i < visible.Length; i++)
                {
                    movedPositionError = Mathf.Max(movedPositionError,
                        Vector3.Distance(visible[i].transform.position, workbench.TransformPoint(localPositions[i])));
                    movedScaleError = Mathf.Max(movedScaleError,
                        Vector3.Distance(Abs(visible[i].transform.lossyScale), sizes[i] * 1.6f));
                }
                report.maximumPositionError = Mathf.Max(report.maximumPositionError, movedPositionError);
                report.maximumScaleError = Mathf.Max(report.maximumScaleError, movedScaleError);
                Check(movedPositionError < Tolerance && movedScaleError < Tolerance,
                    "Translation, yaw and positive 1.6x scale preserve source-to-marker mapping", report);
                ValidateSourceMapping(flow, system, particles, count, field.tint, report);

                journey.cutaway = false; Refresh(flow, system);
                Check(flow.ActiveMarkerCount == 0 && flow.markers.All(r => !r.enabled), "Closing cutaway hides every flow slot", report);
                journey.cutaway = true; Refresh(flow, system);
                Check(flow.ActiveMarkerCount > 0, "Reopening cutaway restores the existing source particles", report);
                journey.inSandbox = true; Refresh(flow, system);
                Check(flow.ActiveMarkerCount == 0 && flow.markers.All(r => !r.enabled), "Sandbox mode hides every workshop flow slot", report);
                journey.inSandbox = false; Refresh(flow, system);
                flow.SendMessage("OnDisable", SendMessageOptions.RequireReceiver);
                Check(flow.ActiveMarkerCount == 0 && flow.markers.All(r => !r.enabled), "OnDisable hides every pool slot", report);
                Check(flow.poolRoot.GetComponentsInChildren<MeshRenderer>(true).Length == 288,
                    "Sampling and transforms never grow the renderer pool", report);
                report.passed = true;
                Debug.Log("SWAN_JOURNEY_FLOW_CHECKS_PASS checks=" + report.checks.Count + " sourceParticles=" + report.sourceParticles + " markers=" + report.visibleMarkers);
            }
            catch (Exception exception)
            {
                report.failure = exception.ToString();
                Debug.LogError("SWAN_JOURNEY_FLOW_CHECKS_FAIL " + exception);
                throw;
            }
            finally
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
                // Discard all activation, particle and transform changes. Never save the fixture.
                if (opened)
                {
                    if (setup.Length > 0 && setup.All(entry => !string.IsNullOrEmpty(entry.path))) EditorSceneManager.RestoreSceneManagerSetup(setup);
                    else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
            }
        }

        static void Refresh(SwanJourneyFlowParticles flow, ParticleSystem source)
        {
            if (source.GetComponent<ParticleSystemRenderer>().enabled) throw new InvalidOperationException("Native flow must have only one drawing owner");
            var refresh = typeof(SwanJourneyFlowParticles).GetField("nextRefresh", BindingFlags.Instance | BindingFlags.NonPublic);
            if (refresh == null) throw new MissingFieldException(typeof(SwanJourneyFlowParticles).FullName, "nextRefresh");
            refresh.SetValue(flow, float.NegativeInfinity);
            flow.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
        }
        static void ValidateSourceMapping(SwanJourneyFlowParticles flow, ParticleSystem system,
            ParticleSystem.Particle[] particles, int count, Color tint, Report report)
        {
            foreach (var renderer in flow.markers.Where(r => r.enabled))
            {
                float positionError = float.MaxValue, scaleError = float.MaxValue;
                for (int i = 0; i < count; i++)
                {
                    float error = Vector3.Distance(renderer.transform.position, system.transform.TransformPoint(particles[i].position));
                    if (error >= positionError) continue;
                    positionError = error;
                    scaleError = Vector3.Distance(Abs(renderer.transform.lossyScale),
                        Vector3.Scale(particles[i].GetCurrentSize3D(system), Abs(system.transform.lossyScale)));
                }
                report.maximumPositionError = Mathf.Max(report.maximumPositionError, positionError);
                report.maximumScaleError = Mathf.Max(report.maximumScaleError, scaleError);
                var color = renderer.sharedMaterial.color;
                if (Mathf.Abs(color.r - tint.r) > .008f || Mathf.Abs(color.g - tint.g) > .008f || Mathf.Abs(color.b - tint.b) > .008f)
                    throw new InvalidOperationException("Marker palette changed the source flow's RGB tint.");
            }
        }
        static float PathLength(Vector3[] points) { float length = 0; for (int i = 1; i < points.Length; i++) length += Vector3.Distance(points[i - 1], points[i]); return length; }
        static Vector3 Abs(Vector3 value) => new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        static void ActivateAncestors(Transform target) { for (var current = target; current; current = current.parent) current.gameObject.SetActive(true); }
        static string ObjectPath(Transform target) { var names = new List<string>(); for (var current = target; current; current = current.parent) names.Add(current.name); names.Reverse(); return string.Join("/", names); }
        static void Check(bool condition, string message, Report report)
        {
            if (!condition) throw new InvalidOperationException(message);
            report.checks.Add(message);
        }
    }
}
