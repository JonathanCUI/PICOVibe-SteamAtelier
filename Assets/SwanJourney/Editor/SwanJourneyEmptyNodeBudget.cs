using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SwanJourney.Editor
{
    public static class SwanJourneyEmptyNodeBudget
    {
        [Serializable] public sealed class Report
        {
            public int nodesBefore, nodesAfter, removed;
            public bool geometryUnchanged, nativePerformanceValidated;
            public string[] removedPaths;
        }
        public static bool CanRemove(Transform t, HashSet<UnityEngine.Object> references)
        {
            if (!t || !t.parent || t is RectTransform || t.childCount != 0 ||
                t.gameObject.tag != "Untagged" || t.GetComponents<Component>().Length != 1 ||
                references.Contains(t) || references.Contains(t.gameObject)) return false;
            // Animation bindings can address transforms by path without a direct
            // serialized object reference. Preserve their complete hierarchy.
            for (var p=t; p; p=p.parent)
                if (p.GetComponent<Animator>() || p.GetComponent<Animation>()) return false;
            return true;
        }
        public static void Apply(SteamJourney journey)
        {
            if (Application.isPlaying || !journey.SwanInputActive) throw new InvalidOperationException("Derived Swan scene required");
            var roots=journey.gameObject.scene.GetRootGameObjects();
            var refs=new HashSet<UnityEngine.Object>(SwanJourneyBaselineAudit.References(roots).Keys);
            var before=roots.SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
            var geometry=roots.SelectMany(r=>r.GetComponentsInChildren<MeshFilter>(true))
                .ToDictionary(f=>f,f=>(mesh:f.sharedMesh,world:f.transform.localToWorldMatrix));
            var paths=new List<string>();
            // Bottom-up: after batching, an empty decoration folder can become
            // a leaf. Every pass still respects component/reference boundaries.
            for(int i=before.Length-1;i>=0;i--)
            {
                var t=before[i];
                if(!CanRemove(t,refs))continue;
                paths.Add(PathOf(t));UnityEngine.Object.DestroyImmediate(t.gameObject);
            }
            bool unchanged=geometry.All(p=>p.Key&&p.Key.sharedMesh==p.Value.mesh&&p.Key.transform.localToWorldMatrix==p.Value.world);
            if(!unchanged)throw new InvalidOperationException("Empty-node pruning changed geometry");
            foreach(var reference in refs)
                if(!reference)throw new InvalidOperationException("Empty-node pruning removed a serialized reference");
            var report=new Report{nodesBefore=before.Length,nodesAfter=roots.Sum(r=>r.GetComponentsInChildren<Transform>(true).Length),
                removed=paths.Count,geometryUnchanged=true,removedPaths=paths.ToArray()};
            File.WriteAllText(SwanJourneyBuild.Output+"/empty-node-budget.json",JsonUtility.ToJson(report,true));
            Debug.Log("SWAN_EMPTY_NODE_BUDGET nodes="+report.nodesBefore+"->"+report.nodesAfter+" geometryUnchanged=true nativeBenefit=unmeasured");
        }
        static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
    }
}
