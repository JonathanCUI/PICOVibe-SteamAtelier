using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SwanJourney.Editor
{
    public static class SwanStructureOcclusionBuild
    {
        const string Folder="Assets/SwanJourney/Branding/StructureOcclusion";
        [Serializable] public class Entry {public string source,mesh;public long fileId;public int vertices,triangles;public float mean,minimum;}
        [Serializable] public class Report {public Entry[] meshes;public int samples=24;public string scope="Self-occlusion of each rigid mesh; no shadows from removable neighbours are baked into parts.";}
        public static void Prepare()
        {
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/SwanJourney/Branding","StructureOcclusion");
            EditorSceneManager.OpenScene("Assets/SwanJourney/Scenes/SteamJourneyXRTrial.unity");
            var j=UnityEngine.Object.FindObjectOfType<SteamJourney>(true);
            var candidates=new HashSet<MeshFilter>(j.engine.locomotive.GetComponentsInChildren<MeshFilter>(true));
            foreach(var m in j.modules.Concat(j.finishing))if(m.preview)foreach(var f in m.preview.GetComponentsInChildren<MeshFilter>(true))candidates.Add(f);
            foreach(var v in j.valleyTrain.vehicles)foreach(var f in v.GetComponentsInChildren<MeshFilter>(true))candidates.Add(f);
            foreach(var f in UnityEngine.Object.FindObjectsOfType<MeshFilter>(true))
            {
                for(var t=f.transform;t && t!=j.transform;t=t.parent)
                    if(t.name.IndexOf("rail",StringComparison.OrdinalIgnoreCase)>=0 || t.name.IndexOf("track",StringComparison.OrdinalIgnoreCase)>=0)
                    {candidates.Add(f);break;}
            }
            var meshes=candidates.Where(f=>f && f.sharedMesh && f.sharedMesh.vertexCount>24 && Eligible(f.GetComponent<MeshRenderer>()))
                .Select(f=>f.sharedMesh).Distinct().Where(m=>!AssetDatabase.GetAssetPath(m).StartsWith(Folder)).ToArray();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var go=new GameObject("Offline AO ray target");var collider=go.AddComponent<MeshCollider>();
            bool backfaces=Physics.queriesHitBackfaces;Physics.queriesHitBackfaces=true;
            var entries=new List<Entry>();
            try
            {
                foreach(var source in meshes)
                {
                    string path=AssetDatabase.GetAssetPath(source);
                    if(string.IsNullOrEmpty(path) || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out string guid,out long id))continue;
                    var v=source.vertices;var normals=source.normals;if(normals.Length!=v.Length)continue;
                    var colors=source.colors32;if(colors.Length!=v.Length)colors=Enumerable.Repeat(new Color32(255,255,255,255),v.Length).ToArray();
                    float extent=Mathf.Max(source.bounds.size.x,Mathf.Max(source.bounds.size.y,source.bounds.size.z));
                    float distance=extent*.055f,epsilon=extent*.00015f;
                    collider.sharedMesh=source;Physics.SyncTransforms();
                    var cache=new Dictionary<(Vector3,Vector3),byte>();float sum=0,minimum=1;
                    for(int i=0;i<v.Length;i++)
                    {
                        var key=(v[i],normals[i]);
                        if(!cache.TryGetValue(key,out byte value))
                        {
                            if(normals[i].sqrMagnitude<.001f){colors[i].a=255;sum+=1;continue;}
                            var n=normals[i].normalized;var tangent=Vector3.Cross(n,Mathf.Abs(n.y)<.9f?Vector3.up:Vector3.right).normalized;
                            var bitangent=Vector3.Cross(n,tangent);float blocked=0;
                            for(int s=0;s<24;s++)
                            {
                                float r=Mathf.Sqrt((s+.5f)/24f),angle=s*2.39996323f;
                                var d=tangent*(r*Mathf.Cos(angle))+bitangent*(r*Mathf.Sin(angle))+n*Mathf.Sqrt(1-r*r);
                                if(collider.Raycast(new Ray(v[i]+n*epsilon,d.normalized),out var hit,distance))
                                    blocked+=1-Mathf.SmoothStep(0,1,hit.distance/distance);
                            }
                            value=(byte)Mathf.RoundToInt(Mathf.Clamp01(1-.80f*blocked/24)*255);cache.Add(key,value);
                        }
                        colors[i].a=value;sum+=value/255f;minimum=Mathf.Min(minimum,value/255f);
                    }
                    var baked=UnityEngine.Object.Instantiate(source);baked.name=source.name+" · baked structural AO";baked.colors32=colors;
                    string output=Folder+"/"+guid+"-"+id+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(output);
                    if(saved){EditorUtility.CopySerialized(baked,saved);UnityEngine.Object.DestroyImmediate(baked);baked=saved;}else AssetDatabase.CreateAsset(baked,output);
                    if(!baked.vertices.SequenceEqual(source.vertices) || !baked.triangles.SequenceEqual(source.triangles) || !baked.uv.SequenceEqual(source.uv))throw new Exception("AO changed geometry or UV");
                    entries.Add(new Entry{source=path,fileId=id,mesh=output,vertices=v.Length,triangles=source.triangles.Length/3,mean=sum/v.Length,minimum=minimum});
                    Debug.Log($"STRUCTURE_AO_BAKED mesh={source.name} vertices={v.Length} unique={cache.Count} mean={sum/v.Length:F3} min={minimum:F3} count={entries.Count}/{meshes.Length}");
                }
            }
            finally{Physics.queriesHitBackfaces=backfaces;UnityEngine.Object.DestroyImmediate(go);}
            File.WriteAllText(Folder+"/Bindings.json",JsonUtility.ToJson(new Report{meshes=entries.ToArray()},true));
            AssetDatabase.Refresh();AssetDatabase.SaveAssets();Debug.Log("STRUCTURE_AO_COMPLETE meshes="+entries.Count);
        }
        static bool Eligible(MeshRenderer r)=>r && r.sharedMaterials.Length>0 && r.sharedMaterials.All(m=>m && m.renderQueue<2450 &&
            (m.shader.name=="Universal Render Pipeline/Lit" || m.shader.name=="SwanJourney/XR Valley Surface"));
        internal static void Apply(SteamJourney j)
        {
            if(!File.Exists(Folder+"/Bindings.json"))throw new Exception("Prepare structural occlusion before the final lighting build");
            var report=JsonUtility.FromJson<Report>(File.ReadAllText(Folder+"/Bindings.json"));
            var mapping=new Dictionary<Mesh,Mesh>();
            foreach(var entry in report.meshes)
            {
                var source=AssetDatabase.LoadAllAssetsAtPath(entry.source).OfType<Mesh>().FirstOrDefault(m=>
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m,out string guid,out long id) && id==entry.fileId);
                var baked=AssetDatabase.LoadAssetAtPath<Mesh>(entry.mesh);if(!source || !baked)throw new Exception("Missing structural AO binding");
                mapping[source]=baked;
            }
            var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/SwanJourney/XrStructureSurface.shader");
            if(!shader || ShaderUtil.ShaderHasError(shader))throw new Exception("Structural shader invalid");
            var materials=new Dictionary<Material,Material>();int count=0;
            foreach(var f in UnityEngine.Object.FindObjectsOfType<MeshFilter>(true))
            {
                var r=f.GetComponent<MeshRenderer>();if(!f.sharedMesh || !mapping.TryGetValue(f.sharedMesh,out var mesh) || !Eligible(r))continue;
                f.sharedMesh=mesh;count++;
                r.sharedMaterials=r.sharedMaterials.Select(src=>
                {
                    if(!materials.TryGetValue(src,out var mat))
                    {
                        string key=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(src));
                        if(string.IsNullOrEmpty(key))throw new Exception("Transient structural material");
                        string path=Folder+"/"+key+".mat";mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                        if(!mat){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}
                        mat.shader=shader;mat.CopyPropertiesFromMaterial(src);mat.EnableKeyword("_STRUCTURE_AO");mat.renderQueue=-1;
                        EditorUtility.SetDirty(mat);materials.Add(src,mat);
                    }
                    return mat;
                }).ToArray();
            }
            // Detail selectors must keep the baked colors when exchanging their high/low meshes.
            foreach(var component in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true))
            {
                if(!component || component.gameObject.scene!=j.gameObject.scene)continue;
                var so=new SerializedObject(component);var p=so.GetIterator();bool changed=false;
                while(p.Next(true))if(p.propertyType==SerializedPropertyType.ObjectReference && p.objectReferenceValue is Mesh source && mapping.TryGetValue(source,out var baked))
                {p.objectReferenceValue=baked;changed=true;}
                if(changed)so.ApplyModifiedPropertiesWithoutUndo();
            }
            Debug.Log($"STRUCTURE_AO_APPLIED renderers={count} materials={materials.Count} addedTriangles=0 runtimeRays=0");
        }
    }
}
