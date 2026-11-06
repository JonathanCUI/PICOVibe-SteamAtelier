using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SwanJourney.Editor
{
    // Fixed workshop track only. Never bake a removable train's shadow onto its neighbours.
    public static class SwanTrackContactBake
    {
        public const string Folder="Assets/SwanJourney/Branding/TrackContact";
        const int Size=2048,Samples=32;
        [Serializable] public class Binding {public string name,mesh;public Vector3 position;public int triangles;}
        [Serializable] public class Report {public Binding[] surfaces;public int samples=Samples,size=Size;public float radius=.24f;}
        static MeshRenderer[] Receivers(SteamJourney j)=>j.workshopRoot.transform.Cast<Transform>()
            .Where(t=>t.name=="Timber sleeper" || t.name=="Individual mineral ballast" || t.name=="Swan rigid detail batch 100" || t.name=="Railway teaching plinth")
            .Select(t=>t.GetComponent<MeshRenderer>()).Where(r=>r && r.enabled).ToArray();
        public static void Prepare()
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            EditorSceneManager.OpenScene("Assets/SwanJourney/Scenes/SteamJourneyXRTrial.unity");
            var j=UnityEngine.Object.FindObjectOfType<SteamJourney>(true);var receivers=Receivers(j);
            if(receivers.Length<28)throw new Exception("Incomplete fixed track receivers");
            var matrices=receivers.Select(r=>j.workshopRoot.transform.worldToLocalMatrix*r.localToWorldMatrix).ToArray();
            var meshes=receivers.Select(r=>SwanCanyonLightingStudy.UnwrapWithoutWelding(r.GetComponent<MeshFilter>().sharedMesh)).ToArray();
            var bindings=receivers.Select((r,i)=>new Binding{name=r.name,position=r.transform.localPosition,mesh=Folder+"/Surface-"+i.ToString("D2")+".asset",triangles=meshes[i].triangles.Length/3}).ToArray();
            // Persist before replacing the scene: Unity unloads temporary mesh objects
            // with the source scene, even if they are still referenced by managed arrays.
            for(int i=0;i<meshes.Length;i++)
            {
                var saved=AssetDatabase.LoadAssetAtPath<Mesh>(bindings[i].mesh);
                if(saved){EditorUtility.CopySerialized(meshes[i],saved);UnityEngine.Object.DestroyImmediate(meshes[i]);meshes[i]=saved;}
                else AssetDatabase.CreateAsset(meshes[i],bindings[i].mesh);
            }
            AssetDatabase.SaveAssets();
            var tiles=receivers.Select(r=>new Texture2D(r.name=="Timber sleeper"?128:512,r.name=="Timber sleeper"?128:512)).ToArray();
            var packing=new Texture2D(Size,Size);var rects=packing.PackTextures(tiles,8,Size,false);
            foreach(var t in tiles)UnityEngine.Object.DestroyImmediate(t);UnityEngine.Object.DestroyImmediate(packing);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var colliders=new List<GameObject>();
            for(int i=0;i<meshes.Length;i++)
            {
                var colliderMesh=UnityEngine.Object.Instantiate(meshes[i]);colliderMesh.vertices=colliderMesh.vertices.Select(matrices[i].MultiplyPoint3x4).ToArray();colliderMesh.RecalculateBounds();
                var go=new GameObject("Track bake collider "+i);go.layer=31;go.AddComponent<MeshCollider>().sharedMesh=colliderMesh;colliders.Add(go);
            }
            Physics.SyncTransforms();bool backfaces=Physics.queriesHitBackfaces;Physics.queriesHitBackfaces=true;
            var pixels=Enumerable.Repeat(Color.white,Size*Size).ToArray();var written=new bool[pixels.Length];
            var samples=Enumerable.Range(0,Samples).Select(s=>{float r=Mathf.Sqrt((s+.5f)/Samples),a=s*2.39996323f;return new Vector3(r*Mathf.Cos(a),r*Mathf.Sin(a),Mathf.Sqrt(1-r*r));}).ToArray();
            int count=0;
            try
            {
                for(int i=0;i<meshes.Length;i++)
                {
                    var mesh=meshes[i];var rect=rects[i];var uv=mesh.uv2;
                    for(int k=0;k<uv.Length;k++)uv[k]=rect.min+Vector2.Scale(uv[k],rect.size);mesh.uv2=uv;
                    var v=mesh.vertices.Select(matrices[i].MultiplyPoint3x4).ToArray();var normalMatrix=matrices[i].inverse.transpose;
                    var normals=mesh.normals.Select(n=>normalMatrix.MultiplyVector(n).normalized).ToArray();var tri=mesh.triangles;
                    for(int k=0;k<tri.Length;k+=3)
                    {
                        int ia=tri[k],ib=tri[k+1],ic=tri[k+2];var a=uv[ia]*Size-Vector2.one*.5f;var b=uv[ib]*Size-Vector2.one*.5f;var c=uv[ic]*Size-Vector2.one*.5f;
                        float area=Cross(b-a,c-a);if(Mathf.Abs(area)<1e-8f)continue;
                        int x0=Mathf.Max(0,Mathf.CeilToInt(Mathf.Min(a.x,Mathf.Min(b.x,c.x)))),x1=Mathf.Min(Size-1,Mathf.FloorToInt(Mathf.Max(a.x,Mathf.Max(b.x,c.x))));
                        int y0=Mathf.Max(0,Mathf.CeilToInt(Mathf.Min(a.y,Mathf.Min(b.y,c.y)))),y1=Mathf.Min(Size-1,Mathf.FloorToInt(Mathf.Max(a.y,Mathf.Max(b.y,c.y))));
                        for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                        {
                            var p=new Vector2(x,y);float wb=Cross(p-a,c-a)/area,wc=Cross(b-a,p-a)/area,wa=1-wb-wc;
                            if(wa<0||wb<0||wc<0)continue;
                            int index=y*Size+x;if(written[index])continue;
                            var n=(normals[ia]*wa+normals[ib]*wb+normals[ic]*wc).normalized;
                            var pos=v[ia]*wa+v[ib]*wb+v[ic]*wc+n*.0012f;
                            var tangent=Vector3.Cross(n,Mathf.Abs(n.y)<.9f?Vector3.up:Vector3.right).normalized;var bitangent=Vector3.Cross(n,tangent);
                            float occlusion=0;
                            foreach(var sample in samples)
                                if(Physics.Raycast(pos,tangent*sample.x+bitangent*sample.y+n*sample.z,out var hit,.24f,1<<31,QueryTriggerInteraction.Ignore))
                                    occlusion+=1-Mathf.SmoothStep(0,1,hit.distance/.24f);
                            float value=1-.9f*occlusion/Samples;pixels[index]=new Color(value,value,value,1);written[index]=true;count++;
                        }
                    }
                    mesh.hideFlags=HideFlags.None;
                    var saved=AssetDatabase.LoadAssetAtPath<Mesh>(bindings[i].mesh);
                    if(saved && saved!=mesh)EditorUtility.CopySerialized(mesh,saved);
                    EditorUtility.SetDirty(mesh);
                    Debug.Log($"TRACK_CONTACT_BAKED receiver={i+1}/{meshes.Length} pixels={count}");
                }
            }
            finally{Physics.queriesHitBackfaces=backfaces;foreach(var go in colliders){UnityEngine.Object.DestroyImmediate(go.GetComponent<MeshCollider>().sharedMesh);UnityEngine.Object.DestroyImmediate(go);}}
            // Fill UV gutters, without blurring contact edges or introducing white seams.
            for(int pass=0;pass<5;pass++)
            {
                var next=(bool[])written.Clone();var colors=(Color[])pixels.Clone();
                for(int y=1;y<Size-1;y++)for(int x=1;x<Size-1;x++)
                {
                    int index=y*Size+x;if(written[index])continue;Color sum=Color.clear;int n=0;
                    for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){int a=index+dy*Size+dx;if(written[a]){sum+=pixels[a];n++;}}
                    if(n>0){colors[index]=sum/n;next[index]=true;}
                }
                pixels=colors;written=next;
            }
            var image=new Texture2D(Size,Size,TextureFormat.RGB24,false,true);image.SetPixels(pixels);image.Apply();
            string texturePath=Folder+"/Fixed track contact.png";File.WriteAllBytes(texturePath,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
            AssetDatabase.ImportAsset(texturePath,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.sRGBTexture=false;importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;importer.maxTextureSize=Size;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name="Android",overridden=true,maxTextureSize=Size,format=TextureImporterFormat.ASTC_6x6});importer.SaveAndReimport();
            File.WriteAllText(Folder+"/Bindings.json",JsonUtility.ToJson(new Report{surfaces=bindings},true));AssetDatabase.Refresh();AssetDatabase.SaveAssets();
            Debug.Log($"TRACK_CONTACT_COMPLETE receivers={bindings.Length} shadedPixels={count} triangles={bindings.Sum(b=>b.triangles)} runtimeRays=0");
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        public static void Apply(SteamJourney j)
        {
            if(!File.Exists(Folder+"/Bindings.json"))return;
            var bindings=JsonUtility.FromJson<Report>(File.ReadAllText(Folder+"/Bindings.json"));var receivers=Receivers(j);
            var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/SwanJourney/XrStructureSurface.shader");var map=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Fixed track contact.png");
            var materials=new Dictionary<Material,Material>();
            foreach(var b in bindings.surfaces)
            {
                var r=receivers.Single(x=>x.name==b.name && Vector3.Distance(x.transform.localPosition,b.position)<.00001f);
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(b.mesh);var source=r.GetComponent<MeshFilter>().sharedMesh;
                if(!mesh || mesh.triangles.Length!=source.triangles.Length)throw new Exception("Track contact geometry mismatch");
                if(!SwanCanyonLightingStudy.SurfaceSignature(mesh).SequenceEqual(SwanCanyonLightingStudy.SurfaceSignature(source)))
                    throw new Exception("Track contact changed a visible surface, UV or normal: "+r.name);
                r.GetComponent<MeshFilter>().sharedMesh=mesh;
                r.sharedMaterials=r.sharedMaterials.Select(src=>
                {
                    if(!materials.TryGetValue(src,out var mat))
                    {
                        var path=Folder+"/"+AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(src))+".mat";mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                        if(!mat){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}mat.shader=shader;mat.CopyPropertiesFromMaterial(src);
                        ApplyMetalFinish(src,mat);
                        mat.EnableKeyword("_TRACK_CONTACT");mat.SetTexture("_TrackContactMap",map);EditorUtility.SetDirty(mat);materials[src]=mat;
                    }
                    return mat;
                }).ToArray();
            }
            Debug.Log($"TRACK_CONTACT_APPLIED receivers={bindings.surfaces.Length} trianglesUnchanged=true additionalDraws=0");
        }
        // Only called on fixed railway receivers; shared locomotive/console steel
        // is deliberately left alone. Values always derive from the source asset.
        internal static void ApplyMetalFinish(Material source,Material target)
        {
            string name=source.name;
            bool top=name.Contains("Rail running face");
            bool valley=name.Contains("Fine rail steel");
            bool web=name.Contains("Rail web");
            bool fixing=name.Contains("Machined steel");
            if(!top && !valley && !web && !fixing)return;
            float gloss=top?.58f:valley?.48f:web?.68f:.46f;
            float bump=top?.18f:valley?.25f:web?.65f:.35f;
            float tint=top?.85f:valley?.80f:web?.86f:.78f;
            var color=source.GetColor("_BaseColor")*tint;color.a=1;
            target.SetColor("_BaseColor",color);target.SetColor("_Color",color);
            target.SetFloat("_Smoothness",source.GetFloat("_Smoothness")*gloss);
            target.SetFloat("_BumpScale",source.GetFloat("_BumpScale")*bump);
            Debug.Log($"XR_TRACK_FINISH source={name} smoothness={target.GetFloat("_Smoothness"):F3} bump={target.GetFloat("_BumpScale"):F3}");
        }
    }
}
