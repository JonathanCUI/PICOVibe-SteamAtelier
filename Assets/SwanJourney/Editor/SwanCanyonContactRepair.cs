using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SwanJourney.Editor
{
    public static class SwanCanyonContactRepair
    {
        public const string Output="Builds/Evidence/CanyonContactRepair";
        [Serializable] public class Surface {public string path,mesh,materials;public Vector3 centre,size;public bool baked;}
        [Serializable] public class Piece {public string path;public int component,vertices;public Vector3 centre,size;public float gap,centreGap;}
        [Serializable] public class Report {public Surface[] surfaces;public Piece[] pieces;}
        internal static bool Rock(MeshRenderer r)=>r.sharedMaterials.Any(m=>m && (m.name.IndexOf("rock",StringComparison.OrdinalIgnoreCase)>=0 || m.name.IndexOf("boulder",StringComparison.OrdinalIgnoreCase)>=0));
        public static void Rebuild()
        {
            GroundLooseStones();SwanCanyonLightingStudy.BakeStudy();SwanCanyonLightingStudy.ApplyBindings();
            BakeContact();SwanLightingFinishBake.Bake();SwanLightingRepairReview.Canyon();Inspect();
        }
        public static void GroundLooseStones()
        {
            Directory.CreateDirectory(Output);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SwanCanyonLightingStudy.PrefabPath));
            var bake=root.GetComponent<SwanCanyonBakedLighting>();bake.SetReview(false);
            var rs=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled && r.GetComponent<MeshFilter>()).ToArray();
            var added=new List<MeshCollider>();var layers=new Dictionary<GameObject,int>();
            foreach(var r in rs.Where(r=>Rock(r) || SwanCanyonLightingStudy.Relative(r.transform,root.transform).Contains("Continuous terrain")))
            {
                layers[r.gameObject]=r.gameObject.layer;r.gameObject.layer=31;
                var c=r.gameObject.AddComponent<MeshCollider>();c.sharedMesh=r.GetComponent<MeshFilter>().sharedMesh;added.Add(c);
            }
            Physics.SyncTransforms();var changes=new List<string>();
            string folder=SwanCanyonLightingStudy.Folder+"/Grounded";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            foreach(var r in rs.Where(Rock))
            {
                // Cave lintels, cliff slabs and portal arches intentionally stand above
                // the ground. Only separate small perimeter stones may be lowered.
                if(bake.surfaces.Any(s=>s.renderer==r) || r.name=="Fixed rock batch 150" || r.name=="Fixed rock batch 151" || r.name=="Fixed rock batch 154" || r.name=="Fixed rock batch 155" || r.name.Contains("voussoir"))continue;
                var source=r.GetComponent<MeshFilter>().sharedMesh;var vertices=source.vertices;var world=vertices.Select(r.transform.TransformPoint).ToArray();bool changed=false;
                foreach(var indices in Components(source))
                {
                    var b=new Bounds(world[indices[0]],Vector3.zero);foreach(int i in indices)b.Encapsulate(world[i]);
                    if(b.size.y>.08f || b.size.magnitude<.008f)continue;
                    float gap=float.PositiveInfinity;
                    foreach(int i in indices.Where(i=>world[i].y<=b.min.y+.00003f))
                        foreach(var hit in Physics.RaycastAll(world[i]-Vector3.up*.00005f,Vector3.down,.04f,1<<31,QueryTriggerInteraction.Ignore))
                            if(hit.point.y<b.min.y-.00004f)gap=Mathf.Min(gap,world[i].y-hit.point.y);
                    if(gap<=.0007f || gap>.025f)continue;
                    float shift=gap+Mathf.Min(.0008f,b.size.y*.04f);
                    var local=r.transform.InverseTransformVector(Vector3.down*shift);
                    foreach(int i in indices)vertices[i]+=local;
                    changed=true;changes.Add(r.name+" centre="+b.center.ToString("F5")+" lowered="+shift.ToString("F6"));
                }
                if(changed)
                {
                    var copy=UnityEngine.Object.Instantiate(source);copy.vertices=vertices;copy.RecalculateBounds();
                    string path=folder+"/"+r.name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if(existing){EditorUtility.CopySerialized(copy,existing);UnityEngine.Object.DestroyImmediate(copy);copy=existing;}else AssetDatabase.CreateAsset(copy,path);
                    r.GetComponent<MeshFilter>().sharedMesh=copy;
                    foreach(var c in r.GetComponents<MeshCollider>().Where(c=>!added.Contains(c) && c.sharedMesh==source))c.sharedMesh=copy;
                }
            }
            foreach(var c in added)UnityEngine.Object.DestroyImmediate(c);foreach(var pair in layers)pair.Key.layer=pair.Value;
            PrefabUtility.SaveAsPrefabAsset(root,SwanCanyonLightingStudy.PrefabPath);AssetDatabase.SaveAssets();
            File.WriteAllLines(Output+"/grounding.txt",changes);Debug.Log("CANYON_GROUNDED pieces="+changes.Count);
        }
        public static void BakeContact()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SwanCanyonLightingStudy.PrefabPath));
            var b=root.GetComponent<SwanCanyonBakedLighting>();b.SetReview(true);
            foreach(var r in root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled && r.GetComponent<MeshFilter>()))
            {
                string path=SwanCanyonLightingStudy.Relative(r.transform,root.transform);
                if(!path.Contains("Fixed original rock") && !path.Contains("Continuous terrain") && !path.Contains("shared-spline railway") && !path.Contains("Closed mountain bore"))continue;
                r.gameObject.layer=31;var c=r.gameObject.AddComponent<MeshCollider>();c.sharedMesh=r.GetComponent<MeshFilter>().sharedMesh;
            }
            Physics.SyncTransforms();const int size=2048,samples=24;const float radius=.015f;
            var pixels=Enumerable.Repeat(Color.white,size*size).ToArray();var distance=Enumerable.Repeat(float.PositiveInfinity,size*size).ToArray();int count=0;
            var rays=Enumerable.Range(0,samples).Select(s=>{float r=Mathf.Sqrt((s+.5f)/samples),a=s*2.39996323f;return new Vector3(r*Mathf.Cos(a),r*Mathf.Sin(a),Mathf.Sqrt(1-r*r));}).ToArray();
            bool oldBackfaces=Physics.queriesHitBackfaces;Physics.queriesHitBackfaces=true;
            try
            {
                foreach(var surface in b.surfaces)
                {
                    var r=surface.renderer;var mesh=surface.bakedMesh;var uv=mesh.uv2;var tri=mesh.triangles;
                    var v=mesh.vertices.Select(r.transform.TransformPoint).ToArray();var nm=r.localToWorldMatrix.inverse.transpose;var n=mesh.normals.Select(p=>nm.MultiplyVector(p).normalized).ToArray();
                    for(int i=0;i<tri.Length;i+=3)
                    {
                        int ia=tri[i],ib=tri[i+1],ic=tri[i+2];var a=uv[ia]*size-Vector2.one*.5f;var d=uv[ib]*size-Vector2.one*.5f;var c=uv[ic]*size-Vector2.one*.5f;
                        float area=Cross(d-a,c-a);if(Mathf.Abs(area)<1e-7f)continue;
                        int x0=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.x,Mathf.Min(d.x,c.x))-2)),x1=Mathf.Min(size-1,Mathf.CeilToInt(Mathf.Max(a.x,Mathf.Max(d.x,c.x))+2));
                        int y0=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.y,Mathf.Min(d.y,c.y))-2)),y1=Mathf.Min(size-1,Mathf.CeilToInt(Mathf.Max(a.y,Mathf.Max(d.y,c.y))+2));
                        for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                        {
                            var p=new Vector2(x,y);var q=p;float wb=Cross(p-a,c-a)/area,wc=Cross(d-a,p-a)/area,wa=1-wb-wc;
                            if(wa<0 || wb<0 || wc<0){q=Nearest(p,a,d);var dc=Nearest(p,d,c);var ca=Nearest(p,c,a);if((p-dc).sqrMagnitude<(p-q).sqrMagnitude)q=dc;if((p-ca).sqrMagnitude<(p-q).sqrMagnitude)q=ca;wb=Cross(q-a,c-a)/area;wc=Cross(d-a,q-a)/area;wa=1-wb-wc;}
                            int index=y*size+x;float dist=(q-p).sqrMagnitude;if(dist>4 || dist>=distance[index])continue;
                            var normal=(n[ia]*wa+n[ib]*wb+n[ic]*wc).normalized;var pos=v[ia]*wa+v[ib]*wb+v[ic]*wc+normal*.00006f;
                            var tangent=Vector3.Cross(normal,Mathf.Abs(normal.y)<.9f?Vector3.up:Vector3.right).normalized;var bitangent=Vector3.Cross(normal,tangent);float occ=0;
                            foreach(var ray in rays)if(Physics.Raycast(pos,tangent*ray.x+bitangent*ray.y+normal*ray.z,out var hit,radius,1<<31,QueryTriggerInteraction.Ignore))occ+=1-Mathf.SmoothStep(0,1,hit.distance/radius);
                            float value=1-.85f*occ/samples;pixels[index]=new Color(value,value,value,1);distance[index]=dist;count++;
                        }
                    }
                }
            }
            finally{Physics.queriesHitBackfaces=oldBackfaces;}
            var image=new Texture2D(size,size,TextureFormat.RGB24,false,true);image.SetPixels(pixels);image.Apply();string pathOut=SwanCanyonLightingStudy.Folder+"/Local contact.png";File.WriteAllBytes(pathOut,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
            AssetDatabase.ImportAsset(pathOut,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(pathOut);
            importer.sRGBTexture=false;importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;importer.maxTextureSize=size;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name="Android",overridden=true,maxTextureSize=size,format=TextureImporterFormat.ASTC_6x6});importer.SaveAndReimport();
            Debug.Log($"CANYON_LOCAL_CONTACT_BAKED receivers={b.surfaces.Length} samples={count} radius={radius} map={size}");
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        static Vector2 Nearest(Vector2 p,Vector2 a,Vector2 b){var d=b-a;return a+d*Mathf.Clamp01(Vector2.Dot(p-a,d)/Mathf.Max(d.sqrMagnitude,1e-12f));}
        internal static List<int[]> Components(Mesh mesh)
        {
            var v=mesh.vertices;var tri=mesh.triangles;var parent=Enumerable.Range(0,v.Length).ToArray();
            int Find(int n){while(parent[n]!=n){parent[n]=parent[parent[n]];n=parent[n];}return n;}
            void Join(int a,int b){a=Find(a);b=Find(b);if(a!=b)parent[b]=a;}
            var weld=new Dictionary<Vector3Int,int>();
            for(int i=0;i<v.Length;i++){var p=v[i]*1000000;var key=new Vector3Int(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.y),Mathf.RoundToInt(p.z));if(weld.TryGetValue(key,out int a))Join(a,i);else weld[key]=i;}
            for(int i=0;i<tri.Length;i+=3){Join(tri[i],tri[i+1]);Join(tri[i],tri[i+2]);}
            return Enumerable.Range(0,v.Length).GroupBy(Find).Select(g=>g.ToArray()).ToList();
        }
        public static void Inspect()
        {
            Directory.CreateDirectory(Output);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SwanCanyonLightingStudy.PrefabPath));
            var bake=root.GetComponent<SwanCanyonBakedLighting>();bake.SetReview(false);
            var rs=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled && r.gameObject.activeInHierarchy && r.GetComponent<MeshFilter>() && r.GetComponent<MeshFilter>().sharedMesh).ToArray();
            foreach(var r in rs.Where(r=>SwanCanyonLightingStudy.Relative(r.transform,root.transform).Contains("Continuous terrain"))){r.gameObject.layer=31;var c=r.gameObject.AddComponent<MeshCollider>();c.sharedMesh=r.GetComponent<MeshFilter>().sharedMesh;}
            Physics.SyncTransforms();var pieces=new List<Piece>();
            foreach(var r in rs.Where(Rock))
            {
                var mesh=r.GetComponent<MeshFilter>().sharedMesh;var v=mesh.vertices.Select(r.transform.TransformPoint).ToArray();int id=0;
                foreach(var indices in Components(mesh))
                {
                    var b=new Bounds(v[indices[0]],Vector3.zero);foreach(int i in indices)b.Encapsulate(v[i]);
                    if(b.size.magnitude<.008f){id++;continue;}
                    float gap=float.PositiveInfinity;
                    foreach(int i in indices.Where(i=>v[i].y<b.min.y+Mathf.Max(.0002f,b.size.y*.04f)))
                        if(Physics.Raycast(v[i]+Vector3.up*.25f,Vector3.down,out var hit,.5f,1<<31,QueryTriggerInteraction.Ignore))gap=Mathf.Min(gap,v[i].y-hit.point.y);
                    float centreGap=999;if(Physics.Raycast(new Vector3(b.center.x,b.min.y+.25f,b.center.z),Vector3.down,out var h,.5f,1<<31,QueryTriggerInteraction.Ignore))centreGap=b.min.y-h.point.y;
                    pieces.Add(new Piece{path=SwanCanyonLightingStudy.Relative(r.transform,root.transform),component=id++,vertices=indices.Length,centre=b.center,size=b.size,gap=float.IsInfinity(gap)?999:gap,centreGap=centreGap});
                }
            }
            File.WriteAllText(Output+"/inspection.json",JsonUtility.ToJson(new Report{
                surfaces=rs.Select(r=>new Surface{path=SwanCanyonLightingStudy.Relative(r.transform,root.transform),mesh=AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>().sharedMesh),materials=string.Join(";",r.sharedMaterials.Select(m=>m.name+":"+m.shader.name)),centre=r.bounds.center,size=r.bounds.size,baked=bake.surfaces.Any(s=>s.renderer==r)}).ToArray(),pieces=pieces.ToArray()},true));
            Debug.Log($"CANYON_CONTACT_INSPECT pieces={pieces.Count} floating={pieces.Count(p=>p.gap>.0005f && p.gap<900)}");
        }
    }
}
