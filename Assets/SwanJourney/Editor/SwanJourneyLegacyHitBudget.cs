using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ByteDance.PICO.SpatialAdapter.Component;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace SwanJourney.Editor
{
    // The source's mesh consolidation left the old desktop raycast meshes on
    // rendererless leaves. Swan selects the current visible meshes instead.
    public static class SwanJourneyLegacyHitBudget
    {
        [Serializable] public class Report
        {
            public bool passed, nativePerformanceValidated;
            public int candidates, removedColliders, removedNodes, retained, renderersBefore, renderersAfter;
            public long collisionTrianglesRemoved;
            public List<string> removedPaths=new List<string>();
        }
        public static Report Apply(SteamJourney journey)
        {
            if(Application.isPlaying || !journey || !journey.SwanInputActive)
                throw new InvalidOperationException("Derived Swan content required");
            var root=journey.spatialRoot;
            var references=new HashSet<Object>();
            foreach(var c in journey.gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Component>(true)))
            {
                if(!c || c is Transform || c is Renderer || c is MeshFilter || c is Collider)continue;
                using(var so=new SerializedObject(c))
                {
                    var p=so.GetIterator();
                    while(p.Next(true))
                        if(p.propertyType==SerializedPropertyType.ObjectReference && p.propertyPath!="m_GameObject" && p.propertyPath!="m_Script" && p.objectReferenceValue)
                            references.Add(p.objectReferenceValue);
                }
            }
            var report=new Report { renderersBefore=root.GetComponentsInChildren<Renderer>(true).Length };
            foreach(var c in root.GetComponentsInChildren<MeshCollider>(true))
            {
                if(c.GetComponent<Renderer>() || c.GetComponent<MeshFilter>())continue;
                report.candidates++;
                var part=c.GetComponentInParent<LocomotivePart>(true);
                if(!CanRemove(c,part?part.transform:null,references)) { report.retained++;continue; }
                if(c.sharedMesh) for(int s=0;s<c.sharedMesh.subMeshCount;s++) report.collisionTrianglesRemoved+=(long)c.sharedMesh.GetIndexCount(s)/3;
                report.removedPaths.Add(PathOf(c.transform));
                Object.DestroyImmediate(c.gameObject);
                report.removedColliders++;report.removedNodes++;
            }
            report.renderersAfter=root.GetComponentsInChildren<Renderer>(true).Length;
            report.passed=report.renderersBefore==report.renderersAfter && report.candidates==report.removedColliders+report.retained;
            if(!report.passed)throw new InvalidOperationException("Legacy hit cleanup changed visible renderers");
            File.WriteAllText(SwanJourneyBuild.Output+"/legacy-hit-budget.json",JsonUtility.ToJson(report,true));
            Debug.Log("SWAN_JOURNEY_LEGACY_HITS removed="+report.removedColliders+" collisionTriangles="+report.collisionTrianglesRemoved+" retained="+report.retained+" visibleGeometryUnchanged=true nativeBenefit=unmeasured");
            return report;
        }
        public static bool CanRemove(MeshCollider collider,Transform owner,HashSet<Object> references)
        {
            if(!collider || !owner || collider.transform==owner || !collider.transform.IsChildOf(owner) ||
                collider.transform.childCount!=0 || collider.isTrigger || collider.sharedMaterial ||
                references.Contains(collider.gameObject) || references.Contains(collider.transform) || references.Contains(collider))return false;
            var components=collider.GetComponents<Component>();
            if(components.Any(c=>!c || !(c is Transform || c==collider)))return false;
            // Replacement input must belong to this exact rigid part, be visible
            // when that part is activated, and already carry native hover + box.
            foreach(var renderer in owner.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(!renderer.enabled || !renderer.GetComponent<SpatialAdapterHoverEffect>())continue;
                var filter=renderer.GetComponent<MeshFilter>();
                if(!filter || !filter.sharedMesh || filter.sharedMesh.vertexCount==0)continue;
                bool active=true;
                for(var t=renderer.transform;t && t!=owner;t=t.parent)
                    if(!t.gameObject.activeSelf || t.GetComponent<LocomotivePart>()) { active=false;break; }
                if(!active)continue;
                if(renderer.GetComponents<BoxCollider>().Any(b=>b.enabled&&!b.isTrigger))return true;
                var proxy=renderer.transform.Find("Swan positive-scale hit proxy");
                if(proxy && proxy.gameObject.activeSelf && proxy.GetComponents<BoxCollider>().Any(b=>b.enabled&&!b.isTrigger))return true;
            }
            return false;
        }
        static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
    }
}
