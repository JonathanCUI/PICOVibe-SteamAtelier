using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace SwanJourney.Editor
{
    public static class SwanCanyonLightingStudy
    {
        public const string PrefabPath = "Assets/SwanJourney/Resources/SwanJourneyCanyon.prefab";
        public static readonly string Output = Path.GetFullPath("Builds/Evidence/CanyonLightingStudy");
        public const string Folder = "Assets/SwanJourney/Branding/CanyonLighting";
        static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            var parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
        }
        [Serializable] public class Binding
        {
            public string path,meshPath;
            public int index;
            public Vector4 scaleOffset;
        }
        [Serializable] public class BakeReport { public Binding[] bindings; public string[] maps; public int triangles; }
        public static void ApplyBindings()
        {
            Directory.CreateDirectory(Output);
            string reportFile=Folder+"/Bake bindings.json";
            if(!File.Exists(reportFile))throw new Exception("Bake the canyon sample before applying it");
            var report=JsonUtility.FromJson<BakeReport>(File.ReadAllText(reportFile));
            var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/SwanJourney/XrCanyonBaked.shader");
            if(!shader || ShaderUtil.ShaderHasError(shader))throw new Exception("Invalid canyon baked shader");
            EnsureFolder(Folder+"/Materials");var root=PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var component=root.GetComponent<SwanCanyonBakedLighting>();
                if(!component)component=root.AddComponent<SwanCanyonBakedLighting>();
                var surfaces=new List<SwanCanyonBakedLighting.Surface>();int triangles=0;
                foreach(var binding in report.bindings)
                {
                    var t=root.transform.Find(binding.path.TrimStart('/'));
                    if(!t)throw new Exception("Canyon bake target disappeared: "+binding.path);
                    var r=t.GetComponent<MeshRenderer>();var original=t.GetComponent<MeshFilter>().sharedMesh;
                    var unwrapped=AssetDatabase.LoadAssetAtPath<Mesh>(binding.meshPath);
                    // Check every triangle corner, including the original material UV, before integration.
                    var a=original.triangles;var b=unwrapped.triangles;
                    if(a.Length!=b.Length)throw new Exception("Bake changed triangle count");
                    // Unity's unwrap may reorder triangles. Compare the oriented corner multiset per submesh.
                    var sa=SurfaceSignature(original);var sb=SurfaceSignature(unwrapped);
                    if(!sa.SequenceEqual(sb))
                    {
                        File.WriteAllLines(Output+"/mesh-difference.txt",new[]{binding.path,
                            "ORIGINAL "+original.vertexCount+" "+original.subMeshCount,"BAKED "+unwrapped.vertexCount+" "+unwrapped.subMeshCount}
                            .Concat(sa.Except(sb).Take(8)).Concat(new[]{"BAKED ONLY"}).Concat(sb.Except(sa).Take(8)));
                        throw new Exception("Bake changed original surface, normal or UV: "+binding.path);
                    }
                    triangles+=a.Length/3;
                    var bound=UnityEngine.Object.Instantiate(unwrapped);bound.name=unwrapped.name+" bound";
                    var uv=bound.uv2;var st=binding.scaleOffset;
                    for(int i=0;i<uv.Length;i++)uv[i]=new Vector2(uv[i].x*st.x+st.z,uv[i].y*st.y+st.w);
                    bound.uv2=uv;string finalPath=binding.meshPath.Replace(".asset","-bound.asset");
                    var saved=AssetDatabase.LoadAssetAtPath<Mesh>(finalPath);
                    if(saved){EditorUtility.CopySerialized(bound,saved);UnityEngine.Object.DestroyImmediate(bound);bound=saved;}
                    else AssetDatabase.CreateAsset(bound,finalPath);
                    var materials=r.sharedMaterials.Select(m=>
                    {
                        string key=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(m)).Substring(0,10);
                        string file=Folder+"/Materials/"+key+"-atlas-"+binding.index+".mat";
                        var finish=AssetDatabase.LoadAssetAtPath<Material>(file);
                        if(!finish){finish=new Material(shader);AssetDatabase.CreateAsset(finish,file);}
                        finish.shader=shader;finish.CopyPropertiesFromMaterial(m);finish.renderQueue=-1;
                        finish.SetTexture("_CanyonBakedMap",AssetDatabase.LoadAssetAtPath<Texture2D>(report.maps[binding.index]));
                        EditorUtility.SetDirty(finish);return finish;
                    }).ToArray();
                    surfaces.Add(new SwanCanyonBakedLighting.Surface{renderer=r,originalMesh=original,bakedMesh=bound,
                        originalMaterials=r.sharedMaterials,bakedMaterials=materials});
                }
                component.surfaces=surfaces.ToArray();
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath,out bool savedPrefab);
                if(!savedPrefab)throw new Exception("Could not save canyon bake bindings");
                Debug.Log($"CANYON_BAKE_VALIDATED receivers={surfaces.Count} triangles={triangles} atlasCount={report.maps.Length} geometryAndUV0Unchanged=true");
            }
            finally {PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();
        }
        internal static string[] SurfaceSignature(Mesh mesh)
        {
            var vertices=mesh.vertices;var uv=mesh.uv;var normals=mesh.normals;
            string F(float v)=>Math.Round(v,5).ToString("F5",System.Globalization.CultureInfo.InvariantCulture);
            string Corner(int i)=>F(vertices[i].x)+","+F(vertices[i].y)+","+F(vertices[i].z)+","+
                F(uv[i].x)+","+F(uv[i].y)+","+F(normals[i].x)+","+F(normals[i].y)+","+F(normals[i].z);
            var corners=Enumerable.Range(0,vertices.Length).Select(Corner).ToArray();var result=new List<string>();
            for(int s=0;s<mesh.subMeshCount;s++)
            {
                var indices=mesh.GetTriangles(s);
                for(int i=0;i<indices.Length;i+=3)
                {
                    var a=corners[indices[i]];var b=corners[indices[i+1]];var c=corners[indices[i+2]];
                    var rotations=new[]{a+"|"+b+"|"+c,b+"|"+c+"|"+a,c+"|"+a+"|"+b};
                    Array.Sort(rotations,StringComparer.Ordinal);result.Add(s+":"+rotations[0]);
                }
            }
            result.Sort(StringComparer.Ordinal);return result.ToArray();
        }
        internal static Mesh UnwrapWithoutWelding(Mesh source)
        {
            UnwrapParam.SetDefaults(out var settings);settings.packMargin=.006f;
            var triangleUv=Unwrapping.GeneratePerTriangleUV(source,settings);
            var positions=source.vertices;var normals=source.normals;var tangents=source.tangents;
            var uv0=source.uv;var colors=source.colors;
            var v=new List<Vector3>();var n=new List<Vector3>();var t=new List<Vector4>();
            var uv=new List<Vector2>();var lightUv=new List<Vector2>();var c=new List<Color>();
            var lookup=new Dictionary<(int,Vector2),int>();var submeshes=new List<int[]>();int corner=0;
            for(int s=0;s<source.subMeshCount;s++)
            {
                var indices=source.GetTriangles(s);
                for(int i=0;i<indices.Length;i++,corner++)
                {
                    int original=indices[i];var baked=triangleUv[corner];var key=(original,baked);
                    if(!lookup.TryGetValue(key,out int id))
                    {
                        id=v.Count;lookup.Add(key,id);v.Add(positions[original]);n.Add(normals[original]);
                        uv.Add(uv0[original]);lightUv.Add(baked);
                        if(tangents.Length>0)t.Add(tangents[original]);if(colors.Length>0)c.Add(colors[original]);
                    }
                    indices[i]=id;
                }
                submeshes.Add(indices);
            }
            var result=new Mesh{indexFormat=IndexFormat.UInt32};result.SetVertices(v);result.SetNormals(n);
            result.SetUVs(0,uv);result.SetUVs(1,lightUv);
            if(t.Count>0)result.SetTangents(t);if(c.Count>0)result.SetColors(c);
            result.subMeshCount=submeshes.Count;
            for(int s=0;s<submeshes.Count;s++)result.SetTriangles(submeshes[s],s);
            result.bounds=source.bounds;return result;
        }
        public static void Preview()
        {
            ApplyBindings();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            var baked=root.GetComponent<SwanCanyonBakedLighting>();
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.44f,.48f,.54f);
            RenderSettings.ambientEquatorColor=new Color(.30f,.28f,.25f);
            RenderSettings.ambientGroundColor=new Color(.17f,.15f,.13f);
            RenderSettings.fog=false;
            var sun=new GameObject("Preview daylight").AddComponent<Light>();sun.type=LightType.Directional;
            sun.intensity=1.35f;sun.color=new Color(1,.91f,.78f);sun.transform.rotation=Quaternion.Euler(48,-32,0);
            RenderSettings.sun=sun;
            var camera=new GameObject("Preview camera").AddComponent<Camera>();camera.fieldOfView=42;
            camera.nearClipPlane=.005f;camera.farClipPlane=10;camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.19f,.21f,.24f);camera.allowHDR=false;
            camera.transform.position=new Vector3(.6f,.75f,-1.05f);camera.transform.LookAt(new Vector3(0,.065f,0));
            var rt=new RenderTexture(1400,1050,24);camera.targetTexture=rt;
            for(int i=0;i<3;i++)
            {
                if(i==2)GraphicsSettings.useScriptableRenderPipelineBatching=false;
                baked.SetReview(i>0);camera.Render();RenderTexture.active=rt;
                var image=new Texture2D(1400,1050,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1400,1050),0,0);image.Apply();
                File.WriteAllBytes(Output+(i==0?"/editor-before.png":i==1?"/editor-after.png":"/editor-nobatch.png"),image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
            RenderTexture.active=null;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);
            GraphicsSettings.useScriptableRenderPipelineBatching=true;
            Debug.Log("CANYON_PREVIEW_COMPLETE");
        }
        public static void BakeStudy()
        {
            Directory.CreateDirectory(Output);EnsureFolder(Folder);EnsureFolder(Folder+"/Meshes");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var source=PrefabUtility.LoadPrefabContents(PrefabPath);
            var pairs=new List<(MeshRenderer receiver,Binding binding)>();
            var neutral=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            neutral.SetColor("_BaseColor",Color.white);neutral.SetFloat("_Smoothness",0);
            neutral.SetFloat("_Metallic",0);int count=0,total=0;
            try
            {
                foreach(var r in source.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var filter=r.GetComponent<MeshFilter>();
                    if(!r.enabled || !r.gameObject.activeInHierarchy || !filter || !filter.sharedMesh)continue;
                    var path=Relative(r.transform,source.transform);var mesh=filter.sharedMesh;
                    // All adjacent rock batches must share one illumination path.
                    bool receive=path.Contains("Continuous terrain") || path.Contains("shared-spline railway") ||
                        path.Contains("Closed mountain bore") || SwanCanyonContactRepair.Rock(r);
                    var go=new GameObject("Bake "+count+" "+r.name);
                    // Bake at a useful world scale; final UVs are invariant under tabletop movement and resizing.
                    go.transform.SetPositionAndRotation(r.transform.position*20,r.transform.rotation);
                    go.transform.localScale=r.transform.lossyScale*20;
                    var mf=go.AddComponent<MeshFilter>();var mr=go.AddComponent<MeshRenderer>();
                    if(receive)
                    {
                        var copy=UnwrapWithoutWelding(mesh);copy.name="Canyon baked "+count;
                        string mp=Folder+"/Meshes/Surface-"+count.ToString("D3")+".asset";
                        var old=AssetDatabase.LoadAssetAtPath<Mesh>(mp);
                        if(old){EditorUtility.CopySerialized(copy,old);UnityEngine.Object.DestroyImmediate(copy);copy=old;}
                        else AssetDatabase.CreateAsset(copy,mp);
                        mesh=copy;
                        pairs.Add((mr,new Binding{path=path,meshPath=mp}));
                    }
                    mf.sharedMesh=mesh;mr.sharedMaterials=Enumerable.Repeat(neutral,mesh.subMeshCount).ToArray();
                    mr.receiveGI=ReceiveGI.Lightmaps;mr.scaleInLightmap=receive?1:0;
                    mr.stitchLightmapSeams=true;mr.shadowCastingMode=ShadowCastingMode.TwoSided;
                    GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.ContributeGI);
                    total+=Enumerable.Range(0,mesh.subMeshCount).Sum(i=>(int)mesh.GetIndexCount(i)/3);count++;
                }
            }
            finally {PrefabUtility.UnloadPrefabContents(source);}
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.white;
            RenderSettings.ambientIntensity=1;RenderSettings.skybox=null;
            var settings=new LightingSettings {name="Canyon ambient visibility study",bakedGI=true,realtimeGI=false,
                lightmapper=LightingSettings.Lightmapper.ProgressiveCPU,
                lightmapResolution=24,lightmapMaxSize=2048,lightmapPadding=4,
                ao=true,aoMaxDistance=.55f,aoExponentIndirect=1,aoExponentDirect=0,
                directSampleCount=32,indirectSampleCount=64,environmentSampleCount=64,maxBounces=1};
            LightmapSettings.lightmapsMode=LightmapsMode.NonDirectional;
            Lightmapping.lightingSettings=settings;Lightmapping.giWorkflowMode=Lightmapping.GIWorkflowMode.OnDemand;
            EditorSceneManager.SaveScene(scene,Folder+"/Canyon bake study.unity");
            Debug.Log($"CANYON_BAKE_BEGIN receivers={pairs.Count} casters={count} triangles={total}");
            if(!Lightmapping.Bake())throw new Exception("Canyon lightmapper did not complete");
            var maps=LightmapSettings.lightmaps;
            if(maps.Length==0)throw new Exception("Empty canyon lightmaps");
            var mapPaths=new List<string>();
            for(int i=0;i<maps.Length;i++)
            {
                var texture=maps[i].lightmapColor;
                var rt=RenderTexture.GetTemporary(texture.width,texture.height,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
                Graphics.Blit(texture,rt);var previous=RenderTexture.active;RenderTexture.active=rt;
                var readable=new Texture2D(texture.width,texture.height,TextureFormat.RGBAFloat,false,true);
                readable.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);readable.Apply();
                RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);
                var pixels=readable.GetPixels();var values=pixels.Select(p=>p.grayscale).Where(v=>v>.001f).OrderBy(v=>v).ToArray();
                float white=values[(int)((values.Length-1)*.98f)];
                if(white<=0 || float.IsNaN(white))throw new Exception("Invalid baked ambient exposure");
                for(int p=0;p<pixels.Length;p++){float v=Mathf.Clamp01(pixels[p].grayscale/white);pixels[p]=new Color(v,v,v,1);}
                var mask=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false,true);mask.SetPixels(pixels);mask.Apply();
                string file=Folder+"/Ambient visibility "+i+".png";File.WriteAllBytes(file,mask.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(mask);UnityEngine.Object.DestroyImmediate(readable);
                AssetDatabase.ImportAsset(file);var importer=(TextureImporter)AssetImporter.GetAtPath(file);
                importer.sRGBTexture=false;importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;
                importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.maxTextureSize=2048;
                var android=new TextureImporterPlatformSettings{name="Android",overridden=true,maxTextureSize=2048,format=TextureImporterFormat.ASTC_6x6};
                importer.SetPlatformTextureSettings(android);importer.SaveAndReimport();mapPaths.Add(file);
                Debug.Log($"CANYON_BAKE_MAP index={i} size={texture.width}x{texture.height} white={white} source={AssetDatabase.GetAssetPath(texture)}");
            }
            foreach(var p in pairs)
            {
                if(p.receiver.lightmapIndex<0 || p.receiver.lightmapIndex>=maps.Length)throw new Exception("Missing baked receiver "+p.binding.path);
                p.binding.index=p.receiver.lightmapIndex;p.binding.scaleOffset=p.receiver.lightmapScaleOffset;
            }
            var report=JsonUtility.ToJson(new BakeReport{bindings=pairs.Select(p=>p.binding).ToArray(),maps=mapPaths.ToArray(),triangles=total},true);
            File.WriteAllText(Folder+"/Bake bindings.json",report);File.WriteAllText(Output+"/bake.json",report);
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            Debug.Log("CANYON_BAKE_COMPLETE "+pairs.Count);
        }
        [Serializable] public class Entry
        {
            public string path, mesh, meshPath;
            public string[] materials, shaders;
            public Vector3 centre, size;
            public int vertices, triangles, uv2;
            public bool enabled, active;
        }
        [Serializable] public class Inventory { public Entry[] renderers; public Vector3 rootPosition, rootScale; }
        public static string Relative(Transform t, Transform root)
        {
            if(t==root)return "";
            return Relative(t.parent,root)+"/"+t.name;
        }
        public static void Inspect()
        {
            Directory.CreateDirectory(Output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var entries=root.GetComponentsInChildren<MeshRenderer>(true).Select(r=>
                {
                    var filter=r.GetComponent<MeshFilter>();
                    var mesh=filter?filter.sharedMesh:null;
                    return new Entry { path=Relative(r.transform,root.transform),mesh=mesh?mesh.name:"",meshPath=AssetDatabase.GetAssetPath(mesh),
                        materials=r.sharedMaterials.Select(m=>m?m.name:"").ToArray(),shaders=r.sharedMaterials.Select(m=>m?m.shader.name:"").ToArray(),
                        centre=root.transform.InverseTransformPoint(r.bounds.center),size=r.bounds.size,
                        vertices=mesh?mesh.vertexCount:0,triangles=mesh?Enumerable.Range(0,mesh.subMeshCount).Sum(i=>(int)mesh.GetIndexCount(i)/3):0,
                        uv2=mesh?mesh.uv2.Length:0,enabled=r.enabled,active=r.gameObject.activeInHierarchy};
                }).ToArray();
                File.WriteAllText(Output+"/inventory.json",JsonUtility.ToJson(new Inventory{renderers=entries,rootPosition=root.transform.position,rootScale=root.transform.lossyScale},true));
                Debug.Log($"CANYON_LIGHTING_INVENTORY renderers={entries.Length} triangles={entries.Sum(x=>x.triangles)} uv2={entries.Count(x=>x.uv2==x.vertices)}");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
