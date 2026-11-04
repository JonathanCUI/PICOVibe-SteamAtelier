using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SwanJourney.Editor
{
    // Read-only export for geometric overlap inspection, including hidden assembly stages.
    public static class SwanSurfaceAudit
    {
        [Serializable] class Catalogue { public List<Group> groups=new List<Group>(); public List<Geometry> meshes=new List<Geometry>(); }
        [Serializable] class Group {public string name; public List<Surface> surfaces=new List<Surface>();}
        [Serializable] class Geometry {public int id; public string name,path,file; public int vertices,triangles;}
        [Serializable] class Surface {public string path; public int mesh; public bool enabled,active; public float[] matrix; public string[] materials; public int[] cull;}
        public static void Export()
        {
            string folder=Environment.GetEnvironmentVariable("SWAN_SURFACE_AUDIT_DIR");
            if(string.IsNullOrEmpty(folder))throw new InvalidOperationException("Set SWAN_SURFACE_AUDIT_DIR");
            Directory.CreateDirectory(folder);
            EditorSceneManager.OpenScene("Assets/SwanJourney/Scenes/SteamJourneyXRTrial.unity");
            var journey=UnityEngine.Object.FindObjectOfType<SteamJourney>(true);
            var catalogue=new Catalogue();var ids=new Dictionary<Mesh,int>();
            string PathOf(Transform t){return t.parent?PathOf(t.parent)+"/"+t.name:t.name;}
            void Collect(string label,Transform space,IEnumerable<MeshRenderer> renderers)
            {
                var group=new Group{name=label};catalogue.groups.Add(group);
                foreach(var r in renderers.Distinct())
                {
                    var f=r.GetComponent<MeshFilter>();if(!f || !f.sharedMesh)continue;
                    var mesh=f.sharedMesh;
                    if(!ids.TryGetValue(mesh,out int id))
                    {
                        id=ids.Count;ids[mesh]=id;string file=$"mesh-{id:D4}.bin";
                        var vertices=mesh.vertices;
                        int tris=0;
                        using(var writer=new BinaryWriter(File.Create(System.IO.Path.Combine(folder,file))))
                        {
                            writer.Write(vertices.Length);
                            foreach(var v in vertices){writer.Write(v.x);writer.Write(v.y);writer.Write(v.z);}
                            writer.Write(mesh.subMeshCount);
                            for(int s=0;s<mesh.subMeshCount;s++)
                            {
                                int[] indices=mesh.GetTopology(s)==UnityEngine.MeshTopology.Triangles?mesh.GetTriangles(s):Array.Empty<int>();
                                writer.Write(indices.Length);foreach(int i in indices)writer.Write(i);tris+=indices.Length/3;
                            }
                        }
                        catalogue.meshes.Add(new Geometry{id=id,name=mesh.name,path=AssetDatabase.GetAssetPath(mesh),file=file,vertices=vertices.Length,triangles=tris});
                    }
                    var m=space.worldToLocalMatrix*r.localToWorldMatrix;
                    var values=new float[16];for(int i=0;i<16;i++)values[i]=m[i];
                    var materials=r.sharedMaterials;
                    group.surfaces.Add(new Surface{path=PathOf(r.transform),mesh=id,enabled=r.enabled,active=r.gameObject.activeInHierarchy,
                        matrix=values,materials=materials.Select(x=>x?AssetDatabase.GetAssetPath(x)+" | "+x.name+" | "+x.shader.name:"null").ToArray(),
                        cull=materials.Select(x=>x && x.HasProperty("_Cull")?(int)x.GetFloat("_Cull"):2).ToArray()});
                }
            }
            int index=0;
            foreach(var step in journey.modules.Concat(journey.finishing))
            {
                index++;
                Collect($"preview-{index:D2}",step.preview.transform,step.preview.GetComponentsInChildren<MeshRenderer>(true));
                Collect($"installed-{index:D2}",journey.engine.locomotive,
                    step.objects.Where(x=>x).SelectMany(x=>x.GetComponentsInChildren<MeshRenderer>(true)));
            }
            Collect("train-all",journey.engine.locomotive,journey.engine.locomotive.GetComponentsInChildren<MeshRenderer>(true));
            journey.SwanInputActive=true;journey.suppressPersistence=true;
            foreach(var step in journey.modules.Concat(journey.finishing))step.installed=true;
            journey.stage=journey.modules.Length;journey.finishIndex=journey.finishing.Length;
            for(int mode=0;mode<3;mode++)
            {
                journey.inspectionMode=mode;journey.cutaway=mode!=0;journey.ApplyVisibility();
                Collect("visible-solid-mode-"+mode,journey.engine.locomotive,
                    journey.engine.locomotive.GetComponentsInChildren<MeshRenderer>()
                        .Where(r=>r.enabled && r.sharedMaterials.All(m=>m && m.renderQueue<2450)));
            }
            File.WriteAllText(System.IO.Path.Combine(folder,"catalogue.json"),JsonUtility.ToJson(catalogue,true));
            Debug.Log($"XR_SURFACE_EXPORT groups={catalogue.groups.Count} meshes={catalogue.meshes.Count} directory={folder}");
        }
    }
}
