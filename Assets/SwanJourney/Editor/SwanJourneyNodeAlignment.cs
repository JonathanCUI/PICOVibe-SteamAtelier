using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ByteDance.PICO.SpatialAdapter;
using SwanAtelier;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SwanJourney.Editor
{
    public static class SwanJourneyNodeAlignment
    {
        [Serializable] sealed class TagChange
        {
            public string path, previousTag = "EditorOnly", replacementTag = "Untagged";
            public int subtreeNodeCount;
            public bool activeSelf;
        }
        [Serializable] sealed class NodeRecord
        {
            public string role, path;
            public int sceneLocalIndex, parentSceneLocalIndex;
            public bool activeSelf;
        }
        [Serializable] sealed class SceneRecord
        {
            public string path;
            public int nodes;
            public string[] rootOrder;
            public bool zeroEditorOnly, zeroDontSaveInBuild;
            public string uidScope;
            public string strippingGuardScope = "Expected saved GameObject/Transform hierarchy; only TMP_SubMesh/TMP_SubMeshUI DontSave transient subtrees excluded. APK GLB count/index verification is required.";
            public int temporaryComponentCount, tmpOwnedTemporaryComponentCount;
            public int transientTmpSubtreeCount, excludedTransientTmpNodes;
            public List<ComponentException> temporaryComponents = new List<ComponentException>();
            public List<TransientSubtree> transientTmpSubtrees = new List<TransientSubtree>();
            public List<NodeRecord> keyNodes = new List<NodeRecord>();
        }
        [Serializable] sealed class ComponentException
        {
            public string path, componentType, hideFlags, reason;
            public bool tmpOwned;
        }
        [Serializable] sealed class TransientSubtree
        {
            public string path, componentType, gameObjectHideFlags, transformHideFlags;
            public int nodes;
            public string reason = "TMP-generated DontSave fallback subtree is not serialized. This is an expected-saved-tree exclusion, not a claim that SDK runtime ignores hideFlags; final GLB and runtime canaries must agree.";
        }
        [Serializable] sealed class Report
        {
            public bool passed, nativeRenderingValidated;
            public string scope = "Derived tags and saved DFS order; runtime tracker canaries are separate from native visibility validation.";
            public List<TagChange> tagChanges = new List<TagChange>();
            public List<SceneRecord> scenes = new List<SceneRecord>();
            public string failure;
        }
        static readonly List<TagChange> changes = new List<TagChange>();

        // Call once on the newly derived content, before pruning/splitting. Only
        // the tag changes; activation, components, geometry and references stay intact.
        public static int NormalizeContent(Transform content)
        {
            if (Application.isPlaying || !content) throw new InvalidOperationException("A derived Edit Mode content root is required.");
            changes.Clear();
            foreach (var target in content.GetComponentsInChildren<Transform>(true))
            {
                if (!target.CompareTag("EditorOnly")) continue;
                changes.Add(new TagChange { path = PathOf(target), activeSelf = target.gameObject.activeSelf,
                    subtreeNodeCount = target.GetComponentsInChildren<Transform>(true).Length });
                target.gameObject.tag = "Untagged";
                EditorUtility.SetDirty(target.gameObject);
            }
            Debug.Log("SWAN_JOURNEY_EDITOR_ONLY_NORMALIZED objects=" + changes.Count);
            return changes.Count;
        }

        // Call after Split, before saving the main scene. Does not create any GO
        // or alter sibling/root order. The temporary additive scene is only read.
        public static void ValidateAndBind(Scene mainScene, string environmentScenePath)
        {
            if (Application.isPlaying || !mainScene.IsValid() || !mainScene.isLoaded)
                throw new InvalidOperationException("Loaded main scene in Edit Mode required.");
            var report = new Report { tagChanges = new List<TagChange>(changes) };
            Scene opened = default;
            var active = SceneManager.GetActiveScene();
            try
            {
                var main = Describe(mainScene, SwanJourneyBuild.ScenePath,
                    "Loaded after boot: scene-local DFS; runtime compares relative to observed scene-root UID, not an assumed global offset.", out var mainNodes);
                report.scenes.Add(main);
                var ids = mainNodes.Select((t, i) => new { t, i }).ToDictionary(x => x.t, x => x.i);
                var hud = FindOne<SwanFrameRateHud>(mainNodes);
                var presentation = FindOne<SwanJourneyPresentation>(mainNodes);
                var targets = new List<SwanJourneyNodeAlignmentDiagnostics.Binding>();
                void Bind(string role, Transform target)
                {
                    if (!target || !ids.TryGetValue(target, out int id)) throw new InvalidOperationException("Missing node alignment target: " + role);
                    int parentId = target.parent && ids.TryGetValue(target.parent, out int found) ? found : -1;
                    targets.Add(new SwanJourneyNodeAlignmentDiagnostics.Binding {
                        role = role, path = PathOf(target), target = target, expectedUid = id, expectedParentUid = parentId });
                    main.keyNodes.Add(Record(role, target, id, parentId));
                }
                Bind("stage", presentation.transform);
                Bind("fps-controller", hud.transform);
                Bind("fps-visual", hud.VisualTransform);
                Bind("move-handle-root", presentation.moveHandle);
                var handleTargets = presentation.moveHandle.GetComponentsInChildren<SwanJourneyTarget>(true)
                    .Where(t => t.kind == SwanJourneyTargetKind.MoveWorkbench).ToArray();
                if (handleTargets.Length != 1) throw new InvalidOperationException("Expected one MoveWorkbench touch surface.");
                Bind("move-handle-surface", handleTargets[0].transform);
                var cards = FindOne<StudioPartCards>(mainNodes);
                if (!cards.PreparedSpatialCanvas) throw new InvalidOperationException("Prepared original part-card canvas is missing.");
                Bind("card-canvas", cards.PreparedSpatialCanvas.transform);
                var labels = cards.PreparedSpatialCanvas.GetComponentsInChildren<TMP_Text>(true);
                if (labels.Length != 6) throw new InvalidOperationException("Expected six prepared card labels.");
                for (int i = 0; i < labels.Length; i++) Bind("card-label-" + i, labels[i].transform);
                var loader = FindOne<SwanJourneySceneLoader>(mainNodes);
                if (loader.environmentAnchor) Bind("deferred-placement-anchor", loader.environmentAnchor);

                var environment = SceneManager.GetSceneByPath(environmentScenePath);
                if (!environment.IsValid() || !environment.isLoaded) {
                    opened = EditorSceneManager.OpenScene(environmentScenePath, OpenSceneMode.Additive);
                    environment = opened;
                }
                var deferred = Describe(environment, SwanJourneyBuild.EnvironmentPrefabPath,
                    "Scene-local DFS only. Runtime uses the shared UID manager; additive absolute IDs are not assumed to start at zero.", out var deferredNodes);
                report.scenes.Add(deferred);
                foreach (var root in environment.GetRootGameObjects()) {
                    int index = deferredNodes.IndexOf(root.transform);
                    if (index >= 0) deferred.keyNodes.Add(Record("deferred-root", root.transform, index, -1));
                }
                var payload = FindOne<SwanJourneyEnvironment>(deferredNodes);
                var extraDiagnostics = payload.GetComponent<SwanJourneyNodeAlignmentDiagnostics>();
                if (!extraDiagnostics) extraDiagnostics = payload.gameObject.AddComponent<SwanJourneyNodeAlignmentDiagnostics>();
                extraDiagnostics.expectedScenePath = SwanJourneyBuild.EnvironmentPrefabPath;
                extraDiagnostics.expectedSceneNodes = deferredNodes.Count;
                extraDiagnostics.sceneRoot = deferredNodes[0];
                extraDiagnostics.relativeSceneIds = true;
                var sampleIds = new[] { 0, 1, deferredNodes.Count / 2, deferredNodes.Count - 1 }.Distinct().ToArray();
                extraDiagnostics.bindings = sampleIds.Select(i => new SwanJourneyNodeAlignmentDiagnostics.Binding {
                    role = "canyon-node-" + i, path = PathOf(deferredNodes[i]), target = deferredNodes[i], expectedUid = i,
                    expectedParentUid = deferredNodes[i].parent ? deferredNodes.IndexOf(deferredNodes[i].parent) : -1
                }).ToArray();
                EditorSceneManager.SaveScene(environment,environmentScenePath);
                // Export through the SDK's supported prefab path. Runtime loads
                // this resource on Drive and instantiates it under the live stage;
                // the authoring scene above is not included in Build Settings.
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SwanJourneyBuild.EnvironmentPrefabPath));
                var position=payload.transform.position;
                var rotation=payload.transform.rotation;
                var scale=payload.transform.localScale;
                try {
                    payload.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                    payload.transform.localScale=Vector3.one;
                    PrefabUtility.SaveAsPrefabAsset(payload.gameObject,SwanJourneyBuild.EnvironmentPrefabPath);
                } finally {
                    payload.transform.SetPositionAndRotation(position,rotation);
                    payload.transform.localScale=scale;
                }

                var diagnostics = presentation.GetComponent<SwanJourneyNodeAlignmentDiagnostics>();
                if (!diagnostics) diagnostics = presentation.gameObject.AddComponent<SwanJourneyNodeAlignmentDiagnostics>();
                diagnostics.expectedScenePath = SwanJourneyBuild.ScenePath;
                diagnostics.expectedSceneNodes = mainNodes.Count;
                diagnostics.sceneRoot = mainNodes[0];
                diagnostics.relativeSceneIds = true;
                diagnostics.bindings = targets.ToArray();
                EditorUtility.SetDirty(diagnostics);
                EditorSceneManager.MarkSceneDirty(mainScene);
                report.passed = true;
                Debug.Log("SWAN_JOURNEY_NODE_ALIGNMENT_PREPARED mainNodes=" + main.nodes + " deferredNodes=" + deferred.nodes
                    + " canaries=" + targets.Count + " normalizedTags=" + changes.Count);
            }
            catch (Exception e) { report.failure = e.ToString(); throw; }
            finally
            {
                try {
                    if (opened.IsValid() && opened.isLoaded) EditorSceneManager.CloseScene(opened, true);
                    if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
                }
                finally {
                    Directory.CreateDirectory(SwanJourneyBuild.Output);
                    File.WriteAllText(SwanJourneyBuild.Output + "/node-alignment.json", JsonUtility.ToJson(report, true));
                }
            }
        }

        static SceneRecord Describe(Scene scene, string path, string scope, out List<Transform> nodes)
        {
            // Same root filtering and child order as the official exporter/runtime,
            // with explicit TMP editor-transient exclusions from the saved tree.
            var roots = scene.GetRootGameObjects().Where(g => !g.GetComponent<SpatialAdapterRuntime>()).ToArray();
            nodes = new List<Transform>();
            var result = new SceneRecord { path = path, uidScope = scope,
                rootOrder = roots.Select(r => r.name).ToArray() };
            foreach (var root in roots) {
                // Even excluded temporary subtrees may not hide an EditorOnly tag.
                foreach (var target in root.GetComponentsInChildren<Transform>(true))
                    if (target.CompareTag("EditorOnly")) throw new InvalidOperationException("EditorOnly would desynchronize native IDs: " + path + ":" + PathOf(target));
                Walk(root.transform, nodes, result.transientTmpSubtrees);
            }
            result.nodes = nodes.Count;
            result.transientTmpSubtreeCount = result.transientTmpSubtrees.Count;
            result.excludedTransientTmpNodes = result.transientTmpSubtrees.Sum(t => t.nodes);
            foreach (var target in nodes) {
                if (target.CompareTag("EditorOnly")) throw new InvalidOperationException("EditorOnly would desynchronize native IDs: " + path + ":" + PathOf(target));
                if ((target.gameObject.hideFlags & HideFlags.DontSaveInBuild) != 0)
                    throw new InvalidOperationException("GameObject DontSaveInBuild would desynchronize native IDs: " + PathOf(target));
                if ((target.hideFlags & HideFlags.DontSaveInBuild) != 0)
                    throw new InvalidOperationException("Transform DontSaveInBuild would desynchronize native IDs: " + PathOf(target));
                foreach (var component in target.GetComponents<Component>())
                {
                    if (!component || component is Transform || (component.hideFlags & HideFlags.DontSaveInBuild) == 0) continue;
                    // TMP 3.0.7 intentionally recreates its MeshFilter and marks it
                    // HideAndDontSave. Removing that component does not remove a
                    // GameObject from either DFS; never clear TMP-owned hideFlags.
                    bool tmpOwned = target.GetComponent<TMP_Text>() || target.GetComponent<TMP_SubMesh>() || target.GetComponent<TMP_SubMeshUI>();
                    result.temporaryComponents.Add(new ComponentException { path = PathOf(target),
                        componentType = component.GetType().FullName, hideFlags = component.hideFlags.ToString(), tmpOwned = tmpOwned,
                        reason = tmpOwned ? "TMP-owned temporary component; original flags preserved; no transform node removed."
                            : "Component-only stripping; original flags preserved; no transform node removed." });
                    result.temporaryComponentCount++;
                    if (tmpOwned) result.tmpOwnedTemporaryComponentCount++;
                }
            }
            result.zeroEditorOnly = result.zeroDontSaveInBuild = true;
            return result;
        }
        static void Walk(Transform target, List<Transform> nodes, List<TransientSubtree> transients)
        {
            Component tmp = target.GetComponent<TMP_SubMesh>();
            if (!tmp) tmp = target.GetComponent<TMP_SubMeshUI>();
            if (tmp && (target.gameObject.hideFlags & HideFlags.DontSave) == HideFlags.DontSave)
            {
                transients.Add(new TransientSubtree { path = PathOf(target), componentType = tmp.GetType().FullName,
                    gameObjectHideFlags = target.gameObject.hideFlags.ToString(), transformHideFlags = target.hideFlags.ToString(),
                    nodes = target.GetComponentsInChildren<Transform>(true).Length });
                return;
            }
            nodes.Add(target);
            for (int i = 0; i < target.childCount; i++) Walk(target.GetChild(i), nodes, transients);
        }
        static T FindOne<T>(List<Transform> nodes) where T : Component
        {
            var found = nodes.Select(t => t.GetComponent<T>()).Where(c => c).ToArray();
            if (found.Length != 1) throw new InvalidOperationException("Expected one " + typeof(T).Name + ", found " + found.Length);
            return found[0];
        }
        static NodeRecord Record(string role, Transform target, int index, int parent)
            => new NodeRecord { role = role, path = PathOf(target), sceneLocalIndex = index,
                parentSceneLocalIndex = parent, activeSelf = target.gameObject.activeSelf };
        static string PathOf(Transform target)
        {
            var names = new List<string>();
            for (var current = target; current; current = current.parent) names.Add(current.name);
            names.Reverse(); return string.Join("/", names);
        }
    }
}
