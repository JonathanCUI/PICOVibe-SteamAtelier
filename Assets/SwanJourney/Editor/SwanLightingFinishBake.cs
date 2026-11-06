using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SwanJourney.Editor
{
    // Offline irradiance only; no lightmapper or realtime capture is used by the player.
    public static class SwanLightingFinishBake
    {
        public const string Folder="Assets/SwanJourney/Branding/CanyonLighting/TimeOfDay";
        public static readonly float[] Times={.27f,.46f,.73f,.93f};
        public static readonly string[] Names={"Dawn","Day","Dusk","Night"};
        [Serializable] public class Report {public float[] times;public string[] maps;public int receivers;public int size;public float decodeScale=4;}
        public static void PrepareAll(){Bake();SwanStructureOcclusionBuild.Prepare();}
        public static void Bake()
        {
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder(SwanCanyonLightingStudy.Folder,"TimeOfDay");
            var bindings=JsonUtility.FromJson<SwanCanyonLightingStudy.BakeReport>(File.ReadAllText(SwanCanyonLightingStudy.Folder+"/Bake bindings.json"));
            if(bindings.maps.Length!=1)throw new Exception("Expected one shared canyon atlas");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=PrefabUtility.LoadPrefabContents(SwanCanyonLightingStudy.PrefabPath);
            var receivers=new Dictionary<string,MeshRenderer>();var materials=new Dictionary<Material,Material>();
            try
            {
                int count=0;
                foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var f=r.GetComponent<MeshFilter>();
                    if(!r.enabled || !r.gameObject.activeInHierarchy || !f || !f.sharedMesh)continue;
                    string relative=SwanCanyonLightingStudy.Relative(r.transform,root.transform);
                    var binding=bindings.bindings.FirstOrDefault(x=>x.path==relative);
                    var go=new GameObject("Bake "+count++ +" "+r.name,typeof(MeshFilter),typeof(MeshRenderer));
                    go.transform.SetPositionAndRotation(r.transform.position*20,r.transform.rotation);go.transform.localScale=r.transform.lossyScale*20;
                    go.GetComponent<MeshFilter>().sharedMesh=binding==null?f.sharedMesh:AssetDatabase.LoadAssetAtPath<Mesh>(binding.meshPath);
                    var mr=go.GetComponent<MeshRenderer>();
                    mr.sharedMaterials=r.sharedMaterials.Select(src=>
                    {
                        if(!src)throw new Exception("Missing canyon material");
                        if(!materials.TryGetValue(src,out var m))
                        {
                            m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.CopyPropertiesFromMaterial(src);
                            m.SetColor("_EmissionColor",Color.black);m.DisableKeyword("_EMISSION");
                            // Material albedo contributes to indirect bounce; receiver albedo is not baked into irradiance.
                            materials.Add(src,m);
                        }
                        return m;
                    }).ToArray();
                    mr.receiveGI=ReceiveGI.Lightmaps;mr.scaleInLightmap=binding==null?0:1;
                    mr.stitchLightmapSeams=true;mr.shadowCastingMode=ShadowCastingMode.TwoSided;
                    GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.ContributeGI);
                    if(binding!=null)receivers.Add(relative,mr);
                }
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            if(receivers.Count!=bindings.bindings.Length)throw new Exception("Missing time-of-day receiver");
            var settings=new LightingSettings {name="Canyon four-state irradiance",bakedGI=true,realtimeGI=false,
                lightmapper=LightingSettings.Lightmapper.ProgressiveCPU,lightmapResolution=24,lightmapMaxSize=1024,lightmapPadding=4,
                ao=false,directSampleCount=64,indirectSampleCount=64,environmentSampleCount=64,maxBounces=2};
            LightmapSettings.lightmapsMode=LightmapsMode.NonDirectional;Lightmapping.lightingSettings=settings;
            Lightmapping.giWorkflowMode=Lightmapping.GIWorkflowMode.OnDemand;
            Light Make(string name){var l=new GameObject(name).AddComponent<Light>();l.type=LightType.Directional;l.lightmapBakeType=LightmapBakeType.Baked;l.shadows=LightShadows.Soft;l.shadowAngle=2;return l;}
            var sun=Make("Baked sun");var moon=Make("Baked moon");var rim=Make("Baked sky fill");
            RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.fog=false;
            EditorSceneManager.SaveScene(scene,Folder+"/Time of day bake.unity");
            var outputs=new List<string>();
            for(int state=0;state<Times.Length;state++)
            {
                float a=(Times[state]-.25f)*Mathf.PI*2,e=Mathf.Sin(a);
                float day=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.28f,.42f,e));
                float sunPower=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.16f,.32f,e));
                float moonPower=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,-.4f,e));
                var orbit=new Vector3(Mathf.Cos(a)*18,Mathf.Sin(a)*20,Mathf.Sin(a)*8);
                var skyCentre=new Vector3(0,5.5f,4);var target=new Vector3(0,1,-3);
                sun.transform.rotation=Quaternion.LookRotation(-(skyCentre+orbit-target).normalized);
                moon.transform.rotation=Quaternion.LookRotation(-(skyCentre-orbit-target).normalized);
                sun.color=Color.Lerp(new Color(1,.57f,.33f),new Color(1,.93f,.82f),Mathf.Clamp01(e*1.6f));sun.intensity=.001f+1.5f*sunPower;
                moon.color=Color.Lerp(new Color(.57f,.67f,.84f),new Color(.69f,.81f,1),day);moon.intensity=.12f+1.03f*moonPower;
                rim.transform.rotation=Quaternion.Euler(38,-130,0);rim.intensity=Mathf.Lerp(.10f,.18f,day);rim.color=new Color(.52f,.68f,1);
                RenderSettings.sun=sunPower>=moonPower?sun:moon;
                RenderSettings.ambientSkyColor=Color.Lerp(new Color(.27f,.33f,.43f),new Color(.30f,.38f,.47f),day);
                RenderSettings.ambientEquatorColor=Color.Lerp(new Color(.19f,.21f,.25f),new Color(.19f,.20f,.22f),day);
                RenderSettings.ambientGroundColor=Color.Lerp(new Color(.14f,.12f,.115f),new Color(.12f,.095f,.07f),day);
                EditorSceneManager.SaveScene(scene);Debug.Log("LIGHTING_FINISH_BAKE_BEGIN "+Names[state]);
                if(!Lightmapping.Bake())throw new Exception("Failed bake "+Names[state]);
                var maps=LightmapSettings.lightmaps.Select(x=>ReadLinear(x.lightmapColor)).ToArray();
                const int size=1024;var pixels=new Color[size*size];
                var distance=Enumerable.Repeat(float.PositiveInfinity,size*size).ToArray();
                foreach(var binding in bindings.bindings)
                {
                    var r=receivers[binding.path];if(r.lightmapIndex<0 || r.lightmapIndex>=maps.Length)throw new Exception("Unbaked receiver "+r.name);
                    var source=maps[r.lightmapIndex];var dst=binding.scaleOffset;var src=r.lightmapScaleOffset;
                    // Unity packs charts into other renderers' empty UV regions. Renderer
                    // rectangles overlap, so transfer only actual triangles and their gutters.
                    Rasterize(r.GetComponent<MeshFilter>().sharedMesh,source,src,dst,pixels,distance,size);
                }
                // Store perceptual RGB so compressed 8-bit maps retain precision in shadows.
                // The sRGB sampler decodes back to linear irradiance before shader blending.
                var encoded=pixels.Select(c=>new Color(Mathf.LinearToGammaSpace(c.r),Mathf.LinearToGammaSpace(c.g),Mathf.LinearToGammaSpace(c.b),1)).ToArray();
                var result=new Texture2D(size,size,TextureFormat.RGB24,false);result.SetPixels(encoded);result.Apply();
                string path=Folder+"/"+Names[state]+" irradiance.png";File.WriteAllBytes(path,result.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(result);foreach(var t in maps)UnityEngine.Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.sRGBTexture=true;importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;
                importer.maxTextureSize=1024;importer.textureCompression=TextureImporterCompression.CompressedHQ;
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name="Android",overridden=true,maxTextureSize=1024,format=TextureImporterFormat.ASTC_6x6});
                importer.SaveAndReimport();outputs.Add(path);
                Debug.Log($"LIGHTING_FINISH_BAKE_DONE state={Names[state]} receivers={receivers.Count} maximum={pixels.Max(c=>Mathf.Max(c.r,Mathf.Max(c.g,c.b)))*4:F4}");
            }
            File.WriteAllText(Folder+"/Lighting states.json",JsonUtility.ToJson(new Report {times=Times,maps=outputs.ToArray(),receivers=receivers.Count,size=1024},true));
            AssetDatabase.Refresh();AssetDatabase.SaveAssets();Bind();Debug.Log("LIGHTING_FINISH_ALL_BAKED");
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        static Vector2 NearestEdge(Vector2 p,Vector2 a,Vector2 b)
        {
            var d=b-a;return a+d*Mathf.Clamp01(Vector2.Dot(p-a,d)/Mathf.Max(d.sqrMagnitude,1e-12f));
        }
        static void Rasterize(Mesh mesh,Texture2D source,Vector4 src,Vector4 dst,Color[] pixels,float[] distance,int size)
        {
            var uv=mesh.uv2;var triangles=mesh.triangles;
            Vector2 Pixel(Vector2 v)=>new Vector2((v.x*dst.x+dst.z)*size-.5f,(v.y*dst.y+dst.w)*size-.5f);
            for(int i=0;i<triangles.Length;i+=3)
            {
                var a=Pixel(uv[triangles[i]]);var b=Pixel(uv[triangles[i+1]]);var c=Pixel(uv[triangles[i+2]]);
                float area=Cross(b-a,c-a);if(Mathf.Abs(area)<1e-9f)continue;
                int x0=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.x,Mathf.Min(b.x,c.x))-3));
                int x1=Mathf.Min(size-1,Mathf.CeilToInt(Mathf.Max(a.x,Mathf.Max(b.x,c.x))+3));
                int y0=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.y,Mathf.Min(b.y,c.y))-3));
                int y1=Mathf.Min(size-1,Mathf.CeilToInt(Mathf.Max(a.y,Mathf.Max(b.y,c.y))+3));
                for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                {
                    var p=new Vector2(x,y);var nearest=p;
                    bool inside=Cross(b-a,p-a)*area>=0 && Cross(c-b,p-b)*area>=0 && Cross(a-c,p-c)*area>=0;
                    if(!inside)
                    {
                        nearest=NearestEdge(p,a,b);var bc=NearestEdge(p,b,c);var ca=NearestEdge(p,c,a);
                        if((p-bc).sqrMagnitude<(p-nearest).sqrMagnitude)nearest=bc;
                        if((p-ca).sqrMagnitude<(p-nearest).sqrMagnitude)nearest=ca;
                    }
                    float d=(p-nearest).sqrMagnitude;int index=y*size+x;
                    if(d>9 || d>=distance[index])continue;
                    float u=((nearest.x+.5f)/size-dst.z)/dst.x,v=((nearest.y+.5f)/size-dst.w)/dst.y;
                    var value=source.GetPixelBilinear(u*src.x+src.z,v*src.y+src.w)/4;
                    value.a=1;pixels[index]=value;distance[index]=d;
                }
            }
        }
        static Texture2D ReadLinear(Texture2D texture)
        {
            // Reimport the original EXR as an ordinary linear HDR texture to avoid platform lightmap RGBM encoding.
            string path=AssetDatabase.GetAssetPath(texture);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            var backup=new TextureImporterSettings();importer.ReadTextureSettings(backup);
            var android=importer.GetPlatformTextureSettings("Android");
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=false;importer.isReadable=true;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name="Android",overridden=true,maxTextureSize=1024,format=TextureImporterFormat.RGBAHalf});
            importer.SaveAndReimport();var hdr=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var result=new Texture2D(hdr.width,hdr.height,TextureFormat.RGBAFloat,false,true);result.SetPixels(hdr.GetPixels());result.Apply();
            importer.SetTextureSettings(backup);importer.SetPlatformTextureSettings(android);importer.SaveAndReimport();return result;
        }
        public static void Bind()
        {
            var report=JsonUtility.FromJson<Report>(File.ReadAllText(Folder+"/Lighting states.json"));
            var atlas=JsonUtility.FromJson<SwanCanyonLightingStudy.BakeReport>(File.ReadAllText(SwanCanyonLightingStudy.Folder+"/Bake bindings.json"));
            var root=PrefabUtility.LoadPrefabContents(SwanCanyonLightingStudy.PrefabPath);
            try
            {
                var b=root.GetComponent<SwanCanyonBakedLighting>();if(!b)throw new Exception("Missing canyon AO");
                b.lightTimes=report.times;b.lightStates=report.maps.Select(AssetDatabase.LoadAssetAtPath<Texture2D>).ToArray();
                if(b.lightStates.Any(x=>!x))throw new Exception("Missing baked light state");
                var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/SwanJourney/XrCanyonBaked.shader");
                foreach(var surface in b.surfaces)
                {
                    var path=SwanCanyonLightingStudy.Relative(surface.renderer.transform,root.transform);
                    var binding=atlas.bindings.Single(x=>x.path==path);
                    var visibility=AssetDatabase.LoadAssetAtPath<Texture2D>(SwanCanyonLightingStudy.Folder+"/Local contact.png");
                    if(!visibility)visibility=AssetDatabase.LoadAssetAtPath<Texture2D>(atlas.maps[binding.index]);
                    if(!visibility || surface.originalMaterials.Length!=surface.bakedMaterials.Length)
                        throw new Exception("Incomplete canyon binding "+path);
                    for(int i=0;i<surface.bakedMaterials.Length;i++)
                    {
                        var material=surface.bakedMaterials[i];var source=surface.originalMaterials[i];
                        if(!material || !source || material==source)throw new Exception("Invalid canyon material pair "+path);
                        material.CopyPropertiesFromMaterial(source);material.shader=shader;material.renderQueue=-1;
                        bool tunnelReceiver=path.Contains("railway") || path.Contains("bore") ||
                            path.Contains("tunnel approaches") || source.name.Contains("Ochre sand");
                        if(tunnelReceiver)material.EnableKeyword("_CANYON_TUNNEL");
                        else material.DisableKeyword("_CANYON_TUNNEL");
                        if(source.name.Contains("Ochre sand"))material.EnableKeyword("_CANYON_NIGHT_SAND");
                        else material.DisableKeyword("_CANYON_NIGHT_SAND");
                        if(path.Contains("railway"))SwanTrackContactBake.ApplyMetalFinish(source,material);
                        material.SetTexture("_CanyonBakedMap",visibility);EditorUtility.SetDirty(material);
                        if(material.GetTexture("_CanyonBakedMap")!=visibility)throw new Exception("Canyon visibility binding failed "+path);
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root,SwanCanyonLightingStudy.PrefabPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();
        }
        public static void PreviewFinish()
        {
            Bind();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SwanCanyonLightingStudy.PrefabPath));
            var baked=root.GetComponent<SwanCanyonBakedLighting>();baked.SetReview(true);
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.30f,.38f,.47f);
            RenderSettings.ambientEquatorColor=new Color(.19f,.20f,.22f);RenderSettings.ambientGroundColor=new Color(.12f,.095f,.07f);
            RenderSettings.fog=false;RenderSettings.skybox=null;
            var sun=new GameObject("Preview daylight").AddComponent<Light>();sun.type=LightType.Directional;
            sun.intensity=1.5f;sun.color=new Color(1,.93f,.82f);sun.transform.rotation=Quaternion.Euler(58,-32,0);RenderSettings.sun=sun;
            var camera=new GameObject("Preview").AddComponent<Camera>();camera.fieldOfView=42;
            camera.nearClipPlane=.005f;camera.farClipPlane=10;camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.18f,.20f,.22f);camera.allowHDR=false;
            camera.transform.position=new Vector3(.6f,.75f,-1.05f);camera.transform.LookAt(new Vector3(0,.065f,0));
            string folder=Path.GetFullPath("Builds/Evidence/LightingFinish");Directory.CreateDirectory(folder);
            var rt=new RenderTexture(1400,1050,24);camera.targetTexture=rt;
            for(int i=0;i<4;i++)
            {
                Shader.SetGlobalTexture("_SwanCanyonLightA",baked.lightStates[i]);Shader.SetGlobalTexture("_SwanCanyonLightB",baked.lightStates[i]);
                Shader.SetGlobalFloat("_SwanCanyonLightBlend",0);Shader.SetGlobalFloat("_SwanCanyonLightEnabled",1);
                camera.Render();RenderTexture.active=rt;
                var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
                File.WriteAllBytes(folder+"/editor-"+Names[i]+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
                var mapRt=RenderTexture.GetTemporary(1024,1024,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
                Graphics.Blit(baked.lightStates[i],mapRt);RenderTexture.active=mapRt;
                image=new Texture2D(1024,1024,TextureFormat.RGB24,false,true);image.ReadPixels(new Rect(0,0,1024,1024),0,0);image.Apply();
                File.WriteAllBytes(folder+"/gpu-"+Names[i]+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.ReleaseTemporary(mapRt);
            }
            RenderTexture.active=null;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);
            Debug.Log("LIGHTING_FINISH_PREVIEW_COMPLETE");
        }
    }
}
