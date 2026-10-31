using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SwanJourney.Editor
{
    public static class SwanJourneySceneCostAudit
    {
        [Serializable] public class Item
        {
            public string path, mesh, components;
            public bool active, enabled, hover, trigger, covered;
            public int vertices, triangles, primitiveCount;
            public Vector3 colliderSize, visibleSize;
        }
        [Serializable] public class Report { public int nodes, renderers, meshColliders, colliders; public Item[] items; }
        public static void Run()
        {
            var scene=EditorSceneManager.OpenScene(SwanJourneyBuild.ScenePath);
            var roots=scene.GetRootGameObjects();
            var colliders=roots.SelectMany(r=>r.GetComponentsInChildren<MeshCollider>(true)).ToArray();
            var report=new Report {
                nodes=roots.Sum(r=>r.GetComponentsInChildren<Transform>(true).Length),
                renderers=roots.Sum(r=>r.GetComponentsInChildren<Renderer>(true).Length),
                colliders=roots.Sum(r=>r.GetComponentsInChildren<Collider>(true).Length),
                meshColliders=colliders.Length,
                items=colliders.Select(c=>{
                    var f=c.GetComponent<MeshFilter>();var m=c.sharedMesh;
                    return new Item { path=PathOf(c.transform), mesh=AssetDatabase.GetAssetPath(m),
                        components=string.Join(",",c.GetComponents<Component>().Select(x=>x?x.GetType().Name:"missing")),
                        active=c.gameObject.activeInHierarchy, enabled=c.enabled, trigger=c.isTrigger,
                        hover=c.GetComponent<ByteDance.PICO.SpatialAdapter.Component.SpatialAdapterHoverEffect>(),
                        covered=c.GetComponentsInChildren<BoxCollider>(true).Any(b=>SwanJourneyColliderBudget.Covers(c,b)),
                        primitiveCount=c.GetComponents<Collider>().Count(x=>!(x is MeshCollider)),
                        vertices=m?m.vertexCount:0,triangles=m?(int)Enumerable.Range(0,m.subMeshCount).Sum(s=>(long)m.GetIndexCount(s)/3):0,
                        colliderSize=m?m.bounds.size:Vector3.zero, visibleSize=f&&f.sharedMesh?f.sharedMesh.bounds.size:Vector3.zero };
                }).ToArray()
            };
            File.WriteAllText(SwanJourneyBuild.Output+"/scene-cost-audit.json",JsonUtility.ToJson(report,true));
            Debug.Log("SWAN_JOURNEY_COST_AUDIT nodes="+report.nodes+" colliders="+report.colliders+" meshColliders="+report.meshColliders);
        }
        static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
    }
}
