using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SwanJourney.Editor
{
    public static class SwanContactFinishBake
    {
        public const string Folder="Assets/SwanJourney/Branding/AssemblyContact";
        const int Samples=32;
        [Serializable] public class Surface {public string path,source,mesh,map;public int triangles;}
        [Serializable] public class Report {public Surface[] surfaces;public int samples=Samples;public int addedTriangles=0;public int realtimeRays=0;public string scope="Rigid assembly groups and fixed console fittings only; no shadows from other removable groups or moving controls.";}
        sealed class Group {public string name;public Transform space;public MeshRenderer[] renderers;public int size;public float radius;}
        static bool Eligible(MeshRenderer r)
        {
            var f=r?r.GetComponent<MeshFilter>():null;
            // Distance-selected meshes have independent topology/UV2. Keep their
            // existing per-level vertex AO and LOD selection, never pin a high LOD.
            return f && !r.GetComponent<LocomotiveXRMesh>() && f.sharedMesh && f.sharedMesh.vertexCount>0 && f.sharedMesh.uv.Length==f.sharedMesh.vertexCount && f.sharedMesh.normals.Length==f.sharedMesh.vertexCount && r.sharedMaterials.Length>0 &&
                r.sharedMaterials.All(m=>m && m.renderQueue<2450 && (m.shader.name=="Universal Render Pipeline/Lit" || m.shader.name=="SwanJourney/XR Structure Surface"));
        }
        static bool ActiveBelow(Transform t,Transform root)
        {for(;t && t!=root;t=t.parent)if(!t.gameObject.activeSelf)return false;return true;}
        static List<Group> Groups(SteamJourney j)
        {
            var groups=new List<Group>();int index=0;
            foreach(var module in j.modules.Take(4).Concat(j.finishing))
            {
                // Each group is installed/removed as one unit. The moving engine
                // mechanism retains its existing self AO, so no pose is baked in.
                var rs=module.objects.Where(o=>o).SelectMany(o=>o.GetComponentsInChildren<MeshRenderer>(true)).Where(Eligible).Distinct().ToArray();
                if(rs.Length>0)groups.Add(new Group{name="Assembly-"+(index++).ToString("D2"),space=j.engine.locomotive,renderers=rs,size=module.title.Contains("水箱") || module.title.Contains("锅炉")?1024:512,radius=.16f});
            }
            var controls=j.GetComponent<StudioSpatialConsoleV18>();
            var fixedNames=new HashSet<string>{"Swan rigid detail batch 105","Swan rigid detail batch 113","Swan rigid detail batch 115","Swan rigid detail batch 116","Swan rigid detail batch 117","Swan rigid detail batch 118","Swan rigid detail batch 119","Swan rigid detail batch 120","Brass foot"};
            groups.Add(new Group{name="Workshop-console",space=controls.console,size=1024,radius=.18f,
                renderers=controls.console.GetComponentsInChildren<MeshRenderer>(true).Where(r=>fixedNames.Contains(r.name) && ActiveBelow(r.transform,controls.console) && Eligible(r)).ToArray()});
            groups.Add(new Group{name="Driving-console",space=j.consoleRoot.transform,size=1024,radius=.18f,
                renderers=j.consoleRoot.GetComponentsInChildren<MeshRenderer>(true).Where(r=>(r.name=="Swan rigid detail batch 169" || r.name=="Swan rigid detail batch 163") && Eligible(r)).ToArray()});
            return groups;
        }
        public static void Prepare()
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            EditorSceneManager.OpenScene("Assets/SwanJourney/Scenes/SteamJourneyXRTrial.unity");
            var j=UnityEngine.Object.FindObjectOfType<SteamJourney>(true);
            foreach(var c in UnityEngine.Object.FindObjectsOfType<Collider>(true))c.enabled=false;
            var previous=File.Exists(Folder+"/Bindings.json")
                ? JsonUtility.FromJson<Report>(File.ReadAllText(Folder+"/Bindings.json")).surfaces.ToDictionary(b=>b.path,b=>b.source)
                : new Dictionary<string,string>();
            var all=new List<Surface>();var groups=Groups(j);
            foreach(var group in groups)
            {
                if(group.renderers.Length==0)throw new Exception("Empty contact group "+group.name);
                Bake(group,j,all,previous);
            }
            File.WriteAllText(Folder+"/Bindings.json",JsonUtility.ToJson(new Report{surfaces=all.ToArray()},true));
            AssetDatabase.Refresh();AssetDatabase.SaveAssets();
            Debug.Log($"ASSEMBLY_CONTACT_COMPLETE groups={groups.Count} receivers={all.Count} triangles={all.Sum(s=>s.triangles)}");
        }
        static void Bake(Group group,SteamJourney j,List<Surface> report,Dictionary<string,string> previous)
        {
            int size=group.size;var rs=group.renderers;
            var sources=rs.Select(r=>
            {
                var current=r.GetComponent<MeshFilter>().sharedMesh;
                if(!AssetDatabase.GetAssetPath(current).StartsWith(Folder+"/",StringComparison.Ordinal))return current;
                // Re-bakes always unwrap the original source, never yesterday's
                // already split UV atlas, so repeated runs cannot add seam vertices.
                string key=PathOf(r.transform,j.transform);
                if(!previous.TryGetValue(key,out var path) || path.StartsWith(Folder+"/",StringComparison.Ordinal))throw new Exception("Missing original contact source "+key);
                var original=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(!original || !SwanCanyonLightingStudy.SurfaceSignature(current).SequenceEqual(SwanCanyonLightingStudy.SurfaceSignature(original)))throw new Exception("Changed contact source "+key);
                return original;
            }).ToArray();
            var meshes=sources.Select(SwanCanyonLightingStudy.UnwrapWithoutWelding).ToArray();
            var matrices=rs.Select(r=>group.space.worldToLocalMatrix*r.localToWorldMatrix).ToArray();
            var areas=meshes.Select((m,i)=>Area(m,matrices[i])).ToArray();float area=areas.Sum();
            var tiles=areas.Select(a=>new Texture2D(Mathf.Clamp(Mathf.RoundToInt(size*Mathf.Sqrt(a/Mathf.Max(.001f,area))),64,size),Mathf.Clamp(Mathf.RoundToInt(size*Mathf.Sqrt(a/Mathf.Max(.001f,area))),64,size))).ToArray();
            var packing=new Texture2D(size,size);var rects=packing.PackTextures(tiles,8,size,false);
            foreach(var t in tiles)UnityEngine.Object.DestroyImmediate(t);UnityEngine.Object.DestroyImmediate(packing);
            var colliders=new List<GameObject>();var pixels=Enumerable.Repeat(Color.white,size*size).ToArray();var written=new bool[pixels.Length];
            var samples=Enumerable.Range(0,Samples).Select(s=>{float r=Mathf.Sqrt((s+.5f)/Samples),a=s*2.39996323f;return new Vector3(r*Mathf.Cos(a),r*Mathf.Sin(a),Mathf.Sqrt(1-r*r));}).ToArray();
            bool backfaces=Physics.queriesHitBackfaces;Physics.queriesHitBackfaces=true;
            try
            {
                for(int i=0;i<meshes.Length;i++)
                {
                    var cm=UnityEngine.Object.Instantiate(meshes[i]);cm.vertices=cm.vertices.Select(matrices[i].MultiplyPoint3x4).ToArray();cm.RecalculateBounds();
                    var go=new GameObject("Temporary contact bake collider");go.layer=31;go.AddComponent<MeshCollider>().sharedMesh=cm;colliders.Add(go);
                }
                Physics.SyncTransforms();
                for(int i=0;i<meshes.Length;i++)
                {
                    var m=meshes[i];var uv=m.uv2;for(int k=0;k<uv.Length;k++)uv[k]=rects[i].min+Vector2.Scale(uv[k],rects[i].size);m.uv2=uv;
                    var v=m.vertices.Select(matrices[i].MultiplyPoint3x4).ToArray();var nm=matrices[i].inverse.transpose;var normals=m.normals.Select(n=>nm.MultiplyVector(n).normalized).ToArray();var tri=m.triangles;
                    for(int k=0;k<tri.Length;k+=3)
                    {
                        int ia=tri[k],ib=tri[k+1],ic=tri[k+2];var a=uv[ia]*size-Vector2.one*.5f;var b=uv[ib]*size-Vector2.one*.5f;var c=uv[ic]*size-Vector2.one*.5f;
                        float det=Cross(b-a,c-a);if(Mathf.Abs(det)<1e-8f)continue;
                        int x0=Mathf.Max(0,Mathf.CeilToInt(Mathf.Min(a.x,Mathf.Min(b.x,c.x)))),x1=Mathf.Min(size-1,Mathf.FloorToInt(Mathf.Max(a.x,Mathf.Max(b.x,c.x))));
                        int y0=Mathf.Max(0,Mathf.CeilToInt(Mathf.Min(a.y,Mathf.Min(b.y,c.y)))),y1=Mathf.Min(size-1,Mathf.FloorToInt(Mathf.Max(a.y,Mathf.Max(b.y,c.y))));
                        for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                        {
                            var p=new Vector2(x,y);float wb=Cross(p-a,c-a)/det,wc=Cross(b-a,p-a)/det,wa=1-wb-wc;
                            if(wa<0||wb<0||wc<0)continue;int pixel=y*size+x;if(written[pixel])continue;
                            var n=(normals[ia]*wa+normals[ib]*wb+normals[ic]*wc).normalized;
                            var pos=v[ia]*wa+v[ib]*wb+v[ic]*wc+n*.001f;
                            var t=Vector3.Cross(n,Mathf.Abs(n.y)<.9f?Vector3.up:Vector3.right).normalized;var bt=Vector3.Cross(n,t);float blocked=0;
                            foreach(var s in samples)if(Physics.Raycast(pos,t*s.x+bt*s.y+n*s.z,out var hit,group.radius,1<<31,QueryTriggerInteraction.Ignore))blocked+=1-Mathf.SmoothStep(0,1,hit.distance/group.radius);
                            float value=1-.92f*blocked/Samples;pixels[pixel]=new Color(value,value,value,1);written[pixel]=true;
                        }
                    }
                    if(!SwanCanyonLightingStudy.SurfaceSignature(m).SequenceEqual(SwanCanyonLightingStudy.SurfaceSignature(sources[i])))throw new Exception("Contact bake changed visible geometry/UV/normal");
                    string meshPath=Folder+"/"+group.name+"-"+i.ToString("D3")+".asset";m.name=group.name+" contact surface "+i;
                    var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);if(existing){EditorUtility.CopySerialized(m,existing);UnityEngine.Object.DestroyImmediate(m);meshes[i]=existing;}else AssetDatabase.CreateAsset(m,meshPath);
                    report.Add(new Surface{path=PathOf(rs[i].transform,j.transform),source=AssetDatabase.GetAssetPath(sources[i]),mesh=meshPath,map=Folder+"/"+group.name+".png",triangles=tri.Length/3});
                }
            }
            finally{Physics.queriesHitBackfaces=backfaces;foreach(var go in colliders){UnityEngine.Object.DestroyImmediate(go.GetComponent<MeshCollider>().sharedMesh);UnityEngine.Object.DestroyImmediate(go);}}
            for(int pass=0;pass<5;pass++)
            {
                var next=(bool[])written.Clone();var colors=(Color[])pixels.Clone();
                for(int y=1;y<size-1;y++)for(int x=1;x<size-1;x++)
                {
                    int p=y*size+x;if(written[p])continue;Color sum=Color.clear;int count=0;
                    for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){int q=p+dy*size+dx;if(written[q]){sum+=pixels[q];count++;}}
                    if(count>0){colors[p]=sum/count;next[p]=true;}
                }
                pixels=colors;written=next;
            }
            var image=new Texture2D(size,size,TextureFormat.RGB24,false,true);image.SetPixels(pixels);image.Apply();string path=Folder+"/"+group.name+".png";
            File.WriteAllBytes(path,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.sRGBTexture=false;importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;importer.maxTextureSize=size;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name="Android",overridden=true,maxTextureSize=size,format=TextureImporterFormat.ASTC_6x6});importer.SaveAndReimport();
            Debug.Log($"ASSEMBLY_CONTACT_BAKED group={group.name} surfaces={rs.Length} size={size} shadedPixels={written.Count(x=>x)}");
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        static float Area(Mesh m,Matrix4x4 matrix)
        {
            var v=m.vertices.Select(matrix.MultiplyPoint3x4).ToArray();var t=m.triangles;float area=0;
            for(int i=0;i<t.Length;i+=3)area+=Vector3.Cross(v[t[i+1]]-v[t[i]],v[t[i+2]]-v[t[i]]).magnitude*.5f;return area;
        }
        public static void Apply(SteamJourney j)
        {
            if(!File.Exists(Folder+"/Bindings.json"))throw new Exception("Prepare assembly contact before building");
            var report=JsonUtility.FromJson<Report>(File.ReadAllText(Folder+"/Bindings.json"));
            var renderers=UnityEngine.Object.FindObjectsOfType<MeshRenderer>(true).Where(r=>r.gameObject.scene==j.gameObject.scene).ToDictionary(r=>PathOf(r.transform,j.transform));
            var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/SwanJourney/XrStructureSurface.shader");
            var materialCache=new Dictionary<(Material,string),Material>();
            var requiredMaps=report.surfaces.Select(b=>b.map).Distinct().ToDictionary(p=>p,p=>AssetDatabase.LoadAssetAtPath<Texture2D>(p));
            if(requiredMaps.Any(p=>!p.Value))throw new Exception("Missing contact texture: "+string.Join(",",requiredMaps.Where(p=>!p.Value).Select(p=>p.Key)));
            foreach(var b in report.surfaces)
            {
                if(!renderers.TryGetValue(b.path,out var r))throw new Exception("Missing assembly contact receiver "+b.path);
                var f=r.GetComponent<MeshFilter>();var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(b.mesh);var source=f.sharedMesh;
                if(!mesh || !SwanCanyonLightingStudy.SurfaceSignature(mesh).SequenceEqual(SwanCanyonLightingStudy.SurfaceSignature(source)))throw new Exception("Assembly contact geometry mismatch "+b.path);
                f.sharedMesh=mesh;
                // The workshop's high-detail selection must retain its new UV2.
                // The separate driving-train renderer keeps its existing bake.
                if(j.detailMeshes!=null)foreach(var detail in j.detailMeshes)if(detail.filter==f){detail.inspection=mesh;if(detail.miniature==source)detail.miniature=mesh;}
                r.sharedMaterials=r.sharedMaterials.Select(src=>
                {
                    var key=(src,b.map);if(!materialCache.TryGetValue(key,out var mat))
                    {
                        string path=Folder+"/"+Path.GetFileNameWithoutExtension(b.map)+"-"+AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(src))+".mat";
                        mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(!mat){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}
                        mat.shader=shader;mat.CopyPropertiesFromMaterial(src);mat.DisableKeyword("_TRACK_CONTACT");mat.EnableKeyword("_DETAIL_CONTACT");mat.SetTexture("_TrackContactMap",requiredMaps[b.map]);mat.renderQueue=-1;EditorUtility.SetDirty(mat);materialCache.Add(key,mat);
                    }
                    return mat;
                }).ToArray();
            }
            // Bind the persisted texture entry explicitly after copying Lit
            // materials, including sources with no saved value for this property.
            foreach(var pair in materialCache)
            {
                var mat=pair.Value;var map=requiredMaps[pair.Key.Item2];
                mat.SetTexture("_TrackContactMap",map);
                var serialized=new SerializedObject(mat);
                var textures=serialized.FindProperty("m_SavedProperties.m_TexEnvs");
                SerializedProperty entry=null;
                for(int i=0;i<textures.arraySize;i++)
                {
                    var item=textures.GetArrayElementAtIndex(i);
                    if(item.FindPropertyRelative("first").stringValue=="_TrackContactMap"){entry=item;break;}
                }
                if(entry==null){textures.InsertArrayElementAtIndex(textures.arraySize);entry=textures.GetArrayElementAtIndex(textures.arraySize-1);}
                entry.FindPropertyRelative("first").stringValue="_TrackContactMap";
                entry.FindPropertyRelative("second.m_Texture").objectReferenceValue=map;
                entry.FindPropertyRelative("second.m_Scale").vector2Value=Vector2.one;
                entry.FindPropertyRelative("second.m_Offset").vector2Value=Vector2.zero;
                serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
            foreach(var pair in materialCache)
            {
                string path=AssetDatabase.GetAssetPath(pair.Value);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var saved=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(saved.GetTexture("_TrackContactMap")!=requiredMaps[pair.Key.Item2])throw new Exception("Contact map did not persist: "+path);
            }
            Debug.Log($"ASSEMBLY_CONTACT_APPLIED receivers={report.surfaces.Length} trianglesUnchanged=true additionalRenderers=0 realtimeRays=0");
        }
        public static string PathOf(Transform t,Transform root)
        {
            if(t==root)return "";
            if(!t.parent)return "/"+t.name;
            var siblings=t.parent.Cast<Transform>().Where(x=>x.name==t.name).ToArray();
            return PathOf(t.parent,root)+"/"+t.name+"["+Array.IndexOf(siblings,t)+"]";
        }
        public static void Inspect()
        {
            var bindings=JsonUtility.FromJson<Report>(File.ReadAllText(Folder+"/Bindings.json"));
            foreach(var b in bindings.surfaces)
            {
                var source=AssetDatabase.LoadAssetAtPath<Mesh>(b.source);var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(b.mesh);
                if(b.source==b.mesh || !source || !mesh || !SwanCanyonLightingStudy.SurfaceSignature(source).SequenceEqual(SwanCanyonLightingStudy.SurfaceSignature(mesh)))throw new Exception("Invalid original contact source "+b.path);
            }
            EditorSceneManager.OpenScene("Assets/SwanJourney/Scenes/SteamJourneyXRTrial.unity");
            var j=UnityEngine.Object.FindObjectOfType<SteamJourney>(true);
            var lines=new List<string>();
            foreach(var r in UnityEngine.Object.FindObjectsOfType<MeshRenderer>(true))foreach(var m in r.sharedMaterials)
                if(m && m.IsKeywordEnabled("_DETAIL_CONTACT"))
                {
                    var mesh=r.GetComponent<MeshFilter>().sharedMesh;
                    if(!m.GetTexture("_TrackContactMap") || mesh.uv2.Length!=mesh.vertexCount)
                        Debug.LogError($"CONTACT_EDITOR_INVALID path={PathOf(r.transform,j.transform)} mesh={mesh.name} vertices={mesh.vertexCount} uv2={mesh.uv2.Length} material={m.name} shader={m.shader.name} map={m.GetTexture("_TrackContactMap")}");
                }
            lines.Add("WORKSHOP_SCALE "+j.workshopRoot.transform.lossyScale);
            foreach(var m in j.modules.Concat(j.finishing))
            {
                lines.Add("MODULE "+m.title);
                foreach(var o in m.objects)if(o)lines.Add("  "+PathOf(o.transform,j.transform));
            }
            var controls=j.GetComponent<StudioSpatialConsoleV18>();
            foreach(var root in new[]{j.workshopRoot.transform,controls.console,j.consoleRoot.transform})
            {
                lines.Add("ROOT "+PathOf(root,j.transform));
                foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var f=r.GetComponent<MeshFilter>();if(!f || !f.sharedMesh)continue;
                    if(root==j.workshopRoot.transform && r.transform.parent!=root)continue;
                    var b=f.sharedMesh.bounds;var matrix=root.worldToLocalMatrix*r.localToWorldMatrix;
                    lines.Add(PathOf(r.transform,j.transform)+" mesh="+AssetDatabase.GetAssetPath(f.sharedMesh)+" v="+f.sharedMesh.vertexCount+" bounds="+b+" rootCentre="+matrix.MultiplyPoint3x4(b.center)+" materials="+string.Join(";",r.sharedMaterials.Select(m=>m?m.name:"NULL")));
                }
            }
            Directory.CreateDirectory("Builds/Evidence/ContactFinish");
            File.WriteAllLines("Builds/Evidence/ContactFinish/inspection.txt",lines);
            Debug.Log("CONTACT_INSPECTION_COMPLETE");
        }
    }
}
