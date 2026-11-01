using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SwanJourney.Editor
{
    // -executeMethod SwanJourney.Editor.SwanJourneyDeferredChecks.Run -quit
    // Uses the saved scenes in Edit Mode; never enters Play/P2D or saves a fixture.
    public static class SwanJourneyDeferredChecks
    {
        const string ReportPath = "Builds/SwanJourney/deferred-checks.json";
        const float Tolerance = .0002f;

        [Serializable] sealed class Report
        {
            public string scope = "EditMode scene dependencies, ownership, support binding, pose and progress guards; no Play/P2D or native validation";
            public bool passed, nativeRenderingValidated, asyncLifecycleValidated, memoryReleaseValidated;
            public int startupMeshAssets, deferredMeshAssets, exclusiveDeferredMeshAssets, startupDependencies, loadCycles;
            public float maximumPositionError, maximumRotationError, maximumScaleError;
            public string[] unexpectedDependencies;
            public List<string> checks = new List<string>();
            public string failure;
        }

        sealed class Pose
        {
            readonly Transform target, parent;
            readonly Vector3 position, scale;
            readonly Quaternion rotation;
            readonly bool active;
            public Pose(Transform value)
            {
                target = value; parent = value.parent; position = value.localPosition;
                rotation = value.localRotation; scale = value.localScale; active = value.gameObject.activeSelf;
            }
            public bool Unchanged => target && target.parent == parent && target.localPosition == position
                && target.localRotation == rotation && target.localScale == scale && target.gameObject.activeSelf == active;
            public void Restore()
            {
                target.SetParent(parent, false); target.localPosition = position;
                target.localRotation = rotation; target.localScale = scale; target.gameObject.SetActive(active);
            }
        }

        sealed class Progress
        {
            readonly SteamJourney journey;
            readonly JourneyModule[] groups;
            readonly string serialized;
            readonly bool[] parked;
            readonly Pose[] objects;
            readonly int sceneHandle;
            public Progress(SteamJourney value)
            {
                journey = value; groups = Groups(value); serialized = JsonUtility.ToJson(value);
                parked = groups.Select(m => m.parked).ToArray(); sceneHandle = value.gameObject.scene.handle;
                objects = GroupObjects(groups).Concat(new[] { value.workshopRoot, value.sandboxRoot, value.consoleRoot,
                        value.previewRoot, value.trainAnchor.gameObject })
                    .Where(g => g).Distinct().Select(g => new Pose(g.transform)).ToArray();
            }
            public void CheckUnchanged(string label, Report report)
            {
                Check(journey && journey.gameObject.scene.handle == sceneHandle && Groups(journey).SequenceEqual(groups),
                    label + ": same 17 module instances remain in the startup scene", report);
                Check(JsonUtility.ToJson(journey) == serialized && groups.Select(m => m.parked).SequenceEqual(parked),
                    label + ": serialized lesson/driving state and nonserialized parked flags are unchanged", report);
                Check(objects.All(p => p.Unchanged) && GroupObjects(groups).All(g => g && g.scene.handle == sceneHandle),
                    label + ": module objects, previews, mode roots and train retain ownership, local pose and visibility", report);
            }
        }

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Deferred scene checks require Edit Mode.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or close dirty scenes before running the independent deferred scene checks.");

            var setup = EditorSceneManager.GetSceneManagerSetup();
            var report = new Report();
            bool opened = false;
            SwanJourneySceneLoader loader = null;
            try
            {
                var main = EditorSceneManager.OpenScene(SwanJourneyBuild.ScenePath, OpenSceneMode.Single);
                opened = true;
                loader = Components<SwanJourneySceneLoader>(main).Single();
                var journey = loader.journey;
                var presentation = loader.presentation;
                Check(journey && presentation && loader.visuals && loader.environmentAnchor && presentation.content,
                    "Saved startup scene binds journey, presentation, visual bridge and environment anchor", report);
                Check(loader.environmentScenePath == SwanJourneyBuild.EnvironmentScenePath
                    && presentation.journey == journey && loader.visuals.journey == journey,
                    "Loader references the generated canyon path and common lesson", report);
                Check(loader.environmentAnchor.gameObject.scene == main
                    && loader.environmentAnchor.GetComponentsInChildren<MeshFilter>(true).Length == 0
                    && Components<SwanJourneyEnvironment>(main).Length == 0,
                    "Startup holds an empty placement anchor and no canyon payload", report);
                Check(!loader.Environment && !loader.visuals.deferredEnvironment && !loader.Busy,
                    "Saved startup has no attached environment or busy loader", report);
                Check(journey.modules.Length == 7 && journey.finishing.Length == 10 && Groups(journey).All(m => m != null),
                    "All seven lesson modules and ten finishing groups remain in startup", report);
                Check(GroupObjects(Groups(journey)).All(g => g && g.scene == main)
                    && Groups(journey).All(m => m.preview && m.objects != null && m.objects.Length > 0),
                    "All 17 previews and installed object references belong to startup", report);
                Check(journey.trainAnchor && journey.trainAnchor.gameObject.scene == main
                    && journey.coaches.Length > 0 && journey.coaches.All(t => t && t.gameObject.scene == main)
                    && journey.consoleRoot && journey.consoleRoot.scene == main
                    && Components<JourneyControl>(main).Length > 0 && Components<MesaDayNightV13>(main).Length > 0,
                    "Train, coaches, console, controls and celestial logic remain in startup", report);
                var enabledScenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                Check(Components<SwanJourneyBuildDependencies>(main).Single().canyonPrefab
                    && AssetDatabase.GetDependencies(main.path,true).Contains(SwanJourneyBuild.EnvironmentPrefabPath),
                    "Editor-only dependency ensures the SDK exports the on-demand prefab",report);
                int nodeCount=Components<Transform>(main).Length;
                SwanJourneyBuildDependencyStripper.Strip(main);
                Check(Components<SwanJourneyBuildDependencies>(main).Length==0 && Components<Transform>(main).Length==nodeCount,
                    "Player build strips eager prefab references without changing native node order",report);
                Check(enabledScenes.Length == 2 && enabledScenes[0] == SwanJourneyBootScene.Path && enabledScenes[1] == main.path,
                    "Build contains loading and workshop scenes; canyon is a separately loaded prefab", report);

                var coreSupports = presentation.sandboxSupports.ToArray();
                Check(coreSupports.Length > 0 && coreSupports.All(t => t && t.gameObject.scene == main)
                    && coreSupports.Distinct().Count() == coreSupports.Length,
                    "Initial sandbox support list contains unique startup-owned supports", report);
                // Exercise the actual Awake path, including its core-support cache.
                // No other Start/Awake or load coroutine is invoked by this harness.
                // Direct invocation avoids Unity's EditMode behaviour-dispatch assertion.
                var awake = typeof(SwanJourneySceneLoader).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
                if (awake == null) throw new MissingMethodException(typeof(SwanJourneySceneLoader).FullName, "Awake");
                awake.Invoke(loader, null);
                Check(journey.SwanModeRequest != null && journey.SwanModeRequest.Target == loader,
                    "Loader Awake installs the mode request hook", report);
                CheckBlockedMode(journey, report);
                var progress = new Progress(journey);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(SwanJourneyBuild.EnvironmentPrefabPath);
                Check(prefab && prefab.GetComponent<SwanJourneyEnvironment>()
                    && prefab.transform.localPosition==Vector3.zero && prefab.transform.localScale==Vector3.one,
                    "Canyon resource prefab has a metre-sized identity root",report);
                var sourceScene=EditorSceneManager.OpenScene(SwanJourneyBuild.EnvironmentScenePath,OpenSceneMode.Additive);
                CheckDependencies(main,sourceScene,report);
                EditorSceneManager.CloseScene(sourceScene,true);

                for (int cycle = 0; cycle < 2; cycle++)
                {
                    report.loadCycles++;
                    var payload = loader.InstantiateEnvironment(prefab);
                    Check(payload.GetComponent<BoxCollider>() && !payload.GetComponent<BoxCollider>().enabled,
                        "Canyon root has an explicit disabled sync component and cannot intercept input", report);
                    Check(payload.gameObject.scene==main && payload.transform.parent==presentation.transform,
                        "Cycle " + cycle + ": prefab instance is born under the registered stage", report);
                    Check(payload.supports.Length > 0 && payload.supports.All(t => t && t.gameObject.scene == main
                        && (t == payload.transform || t.IsChildOf(payload.transform))),
                        "Cycle " + cycle + ": deferred supports belong to the payload hierarchy", report);

                    var expected = coreSupports.Concat(payload.supports).Where(t => t).Distinct().ToArray();
                    loader.AttachEnvironment(payload);
                    loader.AttachEnvironment(payload);
                    Check(loader.Environment == payload && loader.visuals.deferredEnvironment == payload.transform
                        && presentation.sandboxSupports.SequenceEqual(expected),
                        "Cycle " + cycle + ": repeated attach binds visuals and unions supports without duplicates", report);
                    loader.AdoptEnvironment();loader.AdoptEnvironment();
                    loader.AttachEnvironment(payload);
                    Check(payload.gameObject.activeSelf && payload.gameObject.scene==main && payload.transform.parent==presentation.transform,
                        "Cycle " + cycle + ": adoption joins the registered workbench hierarchy and transfers Unity ownership", report);
                    progress.CheckUnchanged("Cycle " + cycle + " attach", report);
                    CheckPoses(loader, main.handle, report);
                    progress.CheckUnchanged("Cycle " + cycle + " pose restoration", report);

                    loader.DetachEnvironment(); loader.DetachEnvironment();
                    Check(!loader.Environment && !loader.visuals.deferredEnvironment
                        && presentation.sandboxSupports.SequenceEqual(coreSupports),
                        "Cycle " + cycle + ": repeated detach clears visual references and restores original supports", report);
                    Check(!payload.gameObject.activeSelf && payload.gameObject.scene==main && payload.transform.parent==presentation.transform,
                        "Cycle " + cycle + ": detach hides the adopted payload before explicit destruction", report);
                    progress.CheckUnchanged("Cycle " + cycle + " detach", report);
                    loader.ReleaseOwnedEnvironment();loader.ReleaseOwnedEnvironment();
                    var closed = SceneManager.GetSceneByPath(SwanJourneyBuild.EnvironmentScenePath);
                    Check(!payload && (!closed.IsValid() || !closed.isLoaded)
                        && presentation.sandboxSupports.All(t => t && t.gameObject.scene == main),
                        "Cycle " + cycle + ": explicit disposal destroys adopted canyon and leaves no support references", report);
                    progress.CheckUnchanged("Cycle " + cycle + " unload", report);
                }
                report.passed = true;
                Debug.Log("SWAN_JOURNEY_DEFERRED_CHECKS_PASS checks=" + report.checks.Count
                    + " loadCycles=" + report.loadCycles + " exclusiveMeshAssets=" + report.exclusiveDeferredMeshAssets);
            }
            catch (Exception exception)
            {
                report.failure = exception.ToString();
                Debug.LogError("SWAN_JOURNEY_DEFERRED_CHECKS_FAIL " + exception);
                throw;
            }
            finally
            {
                try
                {
                    if (loader) loader.DetachEnvironment();
                    // Discard mutations and restore the previous clean editor setup.
                    if (opened)
                    {
                        if (setup.Length > 0 && setup.All(s => !string.IsNullOrEmpty(s.path)))
                            EditorSceneManager.RestoreSceneManagerSetup(setup);
                        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    }
                }
                catch (Exception cleanupFailure)
                {
                    report.passed = false;
                    report.failure = (report.failure ?? "") + "\nFixture cleanup failed: " + cleanupFailure;
                    throw;
                }
                finally
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                    File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
                }
            }
        }

        static void CheckBlockedMode(SteamJourney journey, Report report)
        {
            // Complete must be true, otherwise SetMode returns before consulting the hook.
            var groups = Groups(journey);
            for (int i = 0; i < groups.Length; i++)
            {
                groups[i].installed = true; groups[i].verified = i % 2 == 0;
                groups[i].observed = i + .375f; groups[i].parked = i % 3 == 1;
            }
            journey.stage = journey.modules.Length; journey.finishIndex = journey.finishing.Length - 1;
            journey.running = true; journey.paused = true; journey.speed = .47f;
            journey.throttle = .63f; journey.brake = .21f; journey.routeDistance = 17.25f;
            Check(journey.Complete && journey.SwanInputActive, "Mode guard fixture is complete and uses native input", report);
            var progress = new Progress(journey);
            var originalHook = journey.SwanModeRequest;
            var requests = new List<bool>();
            try
            {
                journey.SwanModeRequest = sandbox => { requests.Add(sandbox); return false; };
                journey.SetMode(true); journey.SetMode(false);
                Check(requests.SequenceEqual(new[] { true, false }), "Both actual SetMode calls consult the veto hook", report);
                progress.CheckUnchanged("Vetoed SetMode", report);
                bool previousLoading = journey.SwanSceneLoading;
                try
                {
                    journey.SwanSceneLoading = true;
                    Check(journey.SwanInputBlocked && !journey.SwanAct(JourneyAction.Pause)
                        && !journey.SwanAct(JourneyAction.Reset) && !journey.SwanSetControl(JourneyAction.Brake, 1),
                        "Scene loading blocks lesson actions, reset and analog controls", report);
                    progress.CheckUnchanged("Loading input guard", report);
                }
                finally { journey.SwanSceneLoading = previousLoading; }
            }
            finally { journey.SwanModeRequest = originalHook; }
        }

        static void CheckDependencies(Scene main, Scene canyon, Report report)
        {
            var mainMeshes = MeshPaths(main); var canyonMeshes = MeshPaths(canyon);
            var exclusive = canyonMeshes.Except(mainMeshes, StringComparer.OrdinalIgnoreCase).ToArray();
            var dependencies = new HashSet<string>(EditorUtility.CollectDependencies(main.GetRootGameObjects())
                .Where(o=>o).Select(AssetDatabase.GetAssetPath).Where(p=>!string.IsNullOrEmpty(p)),StringComparer.OrdinalIgnoreCase);
            report.startupMeshAssets = mainMeshes.Count; report.deferredMeshAssets = canyonMeshes.Count;
            report.exclusiveDeferredMeshAssets = exclusive.Length; report.startupDependencies = dependencies.Count;
            report.unexpectedDependencies = exclusive.Where(dependencies.Contains).OrderBy(p => p).ToArray();
            Check(exclusive.Length > 0, "Canyon has exclusive saved mesh assets, making dependency isolation test non-vacuous", report);
            Check(!dependencies.Contains(canyon.path), "Startup asset dependency graph excludes the additive scene", report);
            Check(report.unexpectedDependencies.Length == 0,
                "Startup dependency graph excludes every canyon-exclusive mesh asset; shared assets/global material lookup remain allowed"
                + (report.unexpectedDependencies.Length == 0 ? "" : ": " + string.Join(", ", report.unexpectedDependencies.Take(10))), report);
        }

        static void CheckPoses(SwanJourneySceneLoader loader, int owner, Report report)
        {
            var presentation = loader.presentation; var journey = loader.journey;
            var stage = presentation.transform; var content = presentation.content;
            var stagePose = new Pose(stage); var contentPose = new Pose(content);
            bool originalMode = journey.inSandbox;
            try
            {
                // Begin in the opposite mode so both checks exercise a real mode
                // transition even when this fixture runs a second load cycle.
                journey.inSandbox = true; presentation.ApplyModeNow();
                stage.position += new Vector3(.38f, .24f, -.51f);
                stage.rotation = Quaternion.Euler(0, 67, 0) * stage.rotation;
                stage.localScale = Vector3.one * .73f;
                foreach (bool sandbox in new[] { false, true })
                {
                    journey.inSandbox = sandbox; presentation.ApplyModeNow();
                    Check(Vector3.Distance(content.localScale, Vector3.one * (sandbox ? presentation.sandboxScale : presentation.workshopScale)) < Tolerance,
                        "Presentation applies " + (sandbox ? "sandbox" : "workshop") + " content scale synchronously", report);
                    var root = loader.Environment.transform; var anchor = loader.environmentAnchor;
                    float position = Vector3.Distance(root.position, anchor.position);
                    float rotation = Quaternion.Angle(root.rotation, anchor.rotation);
                    float scale = Vector3.Distance(loader.Environment.EffectiveScale, anchor.lossyScale);
                    report.maximumPositionError = Mathf.Max(report.maximumPositionError, position);
                    report.maximumRotationError = Mathf.Max(report.maximumRotationError, rotation);
                    report.maximumScaleError = Mathf.Max(report.maximumScaleError, scale);
                    Check(position < Tolerance && rotation < .02f && scale < Tolerance
                        && root.gameObject.scene.handle == owner && root.parent==stage,
                        "ApplyModeNow synchronizes adopted canyon geometry to moved, yawed, uniformly scaled " + (sandbox ? "sandbox" : "workshop") + " anchor", report);
                    // Disturb the stage independently to exercise RefreshFootprint's
                    // synchronization callback, not just the previous aligned pose.
                    stage.position += new Vector3(.05f, .02f, -.03f);
                    presentation.RefreshFootprint();
                    Check(Vector3.Distance(root.position, anchor.position) < Tolerance
                        && Quaternion.Angle(root.rotation, anchor.rotation) < .02f
                        && Vector3.Distance(loader.Environment.EffectiveScale, anchor.lossyScale) < Tolerance
                        && root.gameObject.scene.handle == owner && root.parent==stage,
                        "RefreshFootprint resynchronizes " + (sandbox ? "sandbox" : "workshop") + " canyon pose without changing scene ownership", report);
                    Check(presentation.tabletop && presentation.tabletop.supportPoints != null && presentation.tabletop.supportPoints.Length > 0
                        && presentation.tabletop.supportPoints.All(Finite),
                        "Combined " + (sandbox ? "sandbox" : "workshop") + " support footprint is finite", report);
                }
            }
            finally
            {
                stagePose.Restore(); journey.inSandbox = originalMode;
                presentation.ApplyModeNow(); contentPose.Restore();
                presentation.RefreshFootprint();
            }
        }

        static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsInfinity(v.x)
            && !float.IsNaN(v.y) && !float.IsInfinity(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
        static JourneyModule[] Groups(SteamJourney journey) => journey.modules.Concat(journey.finishing).ToArray();
        static IEnumerable<GameObject> GroupObjects(IEnumerable<JourneyModule> groups)
            => groups.SelectMany(m => (m.objects ?? Array.Empty<GameObject>()).Concat(new[] { m.preview }));
        static T[] Components<T>(Scene scene) where T : Component
            => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        static HashSet<string> MeshPaths(Scene scene) => new HashSet<string>(Components<MeshFilter>(scene)
            .Where(f => f.sharedMesh).Select(f => AssetDatabase.GetAssetPath(f.sharedMesh)).Where(p => !string.IsNullOrEmpty(p)),
            StringComparer.OrdinalIgnoreCase);
        static void Check(bool condition, string message, Report report)
        {
            if (!condition) throw new InvalidOperationException(message);
            report.checks.Add(message);
        }
    }
}
