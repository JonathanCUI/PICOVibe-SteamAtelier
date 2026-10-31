using System;
using ByteDance.PICO.SpatialAdapter;
using UnityEngine;

namespace SwanJourney
{
    // Read-only GLB/Player traversal canaries. Additive checks are relative to
    // the observed root UID; they do not prove the native engine's UID mapping.
    // Add this component to an existing root; never add a diagnostic GameObject.
    [DefaultExecutionOrder(9990)]
    public sealed class SwanJourneyNodeAlignmentDiagnostics : MonoBehaviour
    {
        [Serializable] public sealed class Binding
        {
            public string role, path;
            public Transform target;
            public int expectedUid, expectedParentUid;
        }
        [Serializable] public sealed class Observation
        {
            public string role, path;
            public int expectedUid, actualUid, expectedParentUid, actualParentUid;
            public bool valid, matches, active;
            public Vector3 localPosition, localScale, worldPosition, worldScale;
            public Quaternion localRotation;
        }
        [Serializable] public sealed class Snapshot
        {
            public string scenePath;
            public int expectedSceneNodes, checkedNodes, missingTrackers, mismatches;
            public bool nativeRenderingValidated;
            public Observation[] nodes;
        }
        public string expectedScenePath;
        public int expectedSceneNodes;
        public Transform sceneRoot;
        public bool relativeSceneIds;
        public Binding[] bindings = Array.Empty<Binding>();
        double nextCheck;
        int attempts;
        bool completed;
        Transform expectedRuntimeParent;

        public void ExpectRuntimeParent(Transform parent)
        { expectedRuntimeParent=parent;attempts=0;completed=false;nextCheck=0; }

        void OnEnable() { attempts = 0; completed = false; nextCheck = 0; }
        void LateUpdate() { using (SwanJourney.SwanJourneyCpuTimings.Measure(SwanJourney.SwanJourneyCpuTimings.Stage.NodeAlignment)) CpuMeasuredLateUpdate(); }
    void CpuMeasuredLateUpdate()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (completed || !Application.isFocused || Time.realtimeSinceStartupAsDouble < nextCheck) return;
            nextCheck = Time.realtimeSinceStartupAsDouble + 10;
            attempts++;
            var result = Capture();
            completed = result.missingTrackers == 0 || attempts >= 6;
            string prefix = result.mismatches > 0 ? "SWAN_JOURNEY_NODE_ALIGNMENT_MISMATCH "
                : result.missingTrackers > 0 ? "SWAN_JOURNEY_NODE_ALIGNMENT_WAIT " : "SWAN_JOURNEY_NODE_ALIGNMENT_ALL_OK ";
            string message = prefix + "attempt=" + attempts + " expectedSceneNodes=" + expectedSceneNodes
                + " checked=" + result.checkedNodes + " missing=" + result.missingTrackers + " mismatches=" + result.mismatches;
            if (result.mismatches > 0 || completed && result.missingTrackers > 0) Debug.LogWarning(message);
            else Debug.Log(message);
            // Android limits each log entry. Keep every canary independently
            // readable and only print details on the first/final bounded attempt.
            if (attempts == 1 || completed)
                foreach (var node in result.nodes) Debug.Log("SWAN_JOURNEY_NODE_ALIGNMENT_ITEM " + JsonUtility.ToJson(node));
#endif
        }

        // Only reads public tracker properties. Does not register, reparent or
        // assign an ID, and does not claim that a valid tracker rendered a mesh.
        public Snapshot Capture()
        {
            var result = new Snapshot { scenePath = expectedScenePath, expectedSceneNodes = expectedSceneNodes,
                checkedNodes = bindings == null ? 0 : bindings.Length,
                nodes = new Observation[bindings == null ? 0 : bindings.Length] };
            var rootTracker = sceneRoot ? sceneRoot.GetComponent<SpatialAdapterGameObjectTracker>() : null;
            int offset = relativeSceneIds && rootTracker ? rootTracker.uid : 0;
            if (result.checkedNodes == 0) result.missingTrackers = 1;
            for (int i = 0; i < result.nodes.Length; i++)
            {
                var binding = bindings[i];
                var target = binding.target;
                var tracker = target ? target.GetComponent<SpatialAdapterGameObjectTracker>() : null;
                var parent = target && target.parent ? target.parent.GetComponent<SpatialAdapterGameObjectTracker>() : null;
                var observation = new Observation {
                    role = binding.role, path = binding.path, expectedUid = binding.expectedUid + offset,
                    expectedParentUid = binding.expectedParentUid < 0 ? -1 : binding.expectedParentUid + offset, actualUid = tracker ? tracker.uid : -1,
                    actualParentUid = parent ? parent.uid : -1, valid = tracker && tracker.isValid,
                    active = target && target.gameObject.activeInHierarchy
                };
                if(target==transform && expectedRuntimeParent)
                {
                    var expectedParent=expectedRuntimeParent.GetComponent<SpatialAdapterGameObjectTracker>();
                    observation.expectedParentUid=expectedParent?expectedParent.uid:-1;
                }
                if (target) {
                    observation.localPosition = target.localPosition; observation.localRotation = target.localRotation;
                    observation.localScale = target.localScale; observation.worldPosition = target.position;
                    observation.worldScale = target.lossyScale;
                }
                observation.matches = observation.valid && observation.actualUid == observation.expectedUid
                    && observation.actualParentUid == observation.expectedParentUid;
                if (!observation.valid) result.missingTrackers++;
                else if (!observation.matches) result.mismatches++;
                result.nodes[i] = observation;
            }
            return result;
        }
    }
}
