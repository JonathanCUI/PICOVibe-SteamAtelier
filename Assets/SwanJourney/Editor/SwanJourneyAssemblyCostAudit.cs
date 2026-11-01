using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SwanJourney.Editor
{
    // Enumerate the real assembly visibility + next-preview states without
    // starting simulation, touching a player save or claiming native draw counts.
    public static class SwanJourneyAssemblyCostAudit
    {
        [Serializable] public class Surface { public string material; public long triangles; public Vector3 min,max; }
        [Serializable] public class State
        {
            public int installed,inspection,meshRenderers,previewRenderers;
            public string lastInstalled,nextPreview;
            public long triangles;
            public Surface[] surfaces;
        }
        [Serializable] public class Report { public string scope="Editor assembly visibility, including next preview; not native FPS"; public State[] states; }
        public static void Baseline()=>Run("assembly-states-before.json");
        public static void Run()=>Run("assembly-states-after.json");
        static void Run(string file)
        {
            var scene=EditorSceneManager.OpenScene(SwanJourneyBuild.ScenePath);
            var journey=UnityEngine.Object.FindObjectOfType<SteamJourney>(true);
            journey.suppressPersistence=true;
            var modules=journey.modules.Concat(journey.finishing).ToArray();
            var states=new List<State>();
            for(int mode=0;mode<3;mode++)for(int count=0;count<=modules.Length;count++)
            {
                for(int i=0;i<modules.Length;i++){modules[i].installed=i<count;modules[i].parked=false;}
                journey.stage=Math.Min(count,journey.modules.Length);
                journey.finishIndex=Math.Min(Math.Max(count-journey.modules.Length,0),journey.finishing.Length-1);
                journey.inSandbox=false;journey.cutaway=mode!=0;journey.inspectionMode=mode;
                journey.ApplyVisibility();journey.RefreshPreview();
                var renderers=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshRenderer>(true))
                    .Where(r=>r.enabled&&r.gameObject.activeInHierarchy&&r.GetComponent<MeshFilter>()?.sharedMesh).ToArray();
                var surfaces=new Dictionary<Material,Surface>();
                foreach(var r in renderers)
                {
                    var mesh=r.GetComponent<MeshFilter>().sharedMesh;var materials=r.sharedMaterials;
                    var vertices=mesh.vertices;
                    for(int sub=0;sub<mesh.subMeshCount;sub++)
                    {
                        if(mesh.GetTopology(sub)!=UnityEngine.MeshTopology.Triangles)continue;
                        var mat=materials[sub];
                        if(!surfaces.TryGetValue(mat,out var surface))surfaces[mat]=surface=new Surface{material=AssetDatabase.GetAssetPath(mat),min=Vector3.positiveInfinity,max=Vector3.negativeInfinity};
                        var indices=mesh.GetIndices(sub);surface.triangles+=indices.Length/3;
                        foreach(var index in indices)
                        {
                            var p=r.transform.TransformPoint(vertices[index]);
                            surface.min=Vector3.Min(surface.min,p);surface.max=Vector3.Max(surface.max,p);
                        }
                    }
                }
                states.Add(new State{installed=count,inspection=mode,meshRenderers=renderers.Length,
                    previewRenderers=renderers.Count(r=>modules.Any(m=>m.preview&&r.transform.IsChildOf(m.preview.transform))),
                    lastInstalled=count>0?modules[count-1].title:"none",nextPreview=count<modules.Length?modules[count].title:"none",
                    triangles=surfaces.Values.Sum(s=>s.triangles),surfaces=surfaces.Values.OrderBy(s=>s.material).ToArray()});
            }
            File.WriteAllText(SwanJourneyBuild.Output+"/"+file,JsonUtility.ToJson(new Report{states=states.ToArray()},true));
            EditorSceneManager.OpenScene(SwanJourneyBuild.ScenePath);
            Debug.Log("SWAN_ASSEMBLY_STATE_AUDIT "+file+" states="+states.Count);
        }
        public static void Compare()
        {
            Run();
            var before=JsonUtility.FromJson<Report>(File.ReadAllText(SwanJourneyBuild.Output+"/assembly-states-before.json"));
            var after=JsonUtility.FromJson<Report>(File.ReadAllText(SwanJourneyBuild.Output+"/assembly-states-after.json"));
            if(before.states.Length!=after.states.Length)throw new InvalidOperationException("Assembly state count changed");
            for(int i=0;i<before.states.Length;i++)
            {
                var a=before.states[i];var b=after.states[i];
                if(a.installed!=b.installed||a.inspection!=b.inspection||a.triangles!=b.triangles||a.surfaces.Length!=b.surfaces.Length)
                    throw new InvalidOperationException("Assembly visibility/geometry changed at "+i);
                for(int s=0;s<a.surfaces.Length;s++)
                {
                    var x=a.surfaces[s];var y=b.surfaces[s];
                    if(x.material!=y.material||x.triangles!=y.triangles||(x.min-y.min).magnitude>.001f||(x.max-y.max).magnitude>.001f)
                        throw new InvalidOperationException("Assembly material/bounds changed at "+i+" "+x.material);
                }
            }
            Debug.Log("SWAN_ASSEMBLY_STATE_COMPARISON_PASS states="+after.states.Length+" perMaterialTrianglesAndBoundsPreserved=true");
            File.WriteAllText(SwanJourneyBuild.Output+"/assembly-state-comparison.json","{\"passed\":true,\"states\":"+after.states.Length+",\"perMaterialTrianglesAndBoundsPreserved\":true,\"nativePerformanceValidated\":false}");
        }
    }
}
