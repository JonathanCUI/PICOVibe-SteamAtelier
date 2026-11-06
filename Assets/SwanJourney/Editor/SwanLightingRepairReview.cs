using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SwanJourney.Editor
{
    public static class SwanLightingRepairReview
    {
        public const string Output="Builds/Evidence/LightingRepair";
        [Serializable] class Entry { public string path,mesh,materials;public Vector3 centre,size;public int vertices,triangles; }
        [Serializable] class Inventory { public Entry[] workshop,train; }
        public static void Inspect()
        {
            Directory.CreateDirectory(Output);
            EditorSceneManager.OpenScene("Assets/SwanJourney/Scenes/SteamJourneyXRTrial.unity");
            var j=UnityEngine.Object.FindObjectOfType<SteamJourney>(true);
            Entry Describe(MeshRenderer r)
            {
                var m=r.GetComponent<MeshFilter>().sharedMesh;
                return new Entry{path=SwanCanyonLightingStudy.Relative(r.transform,j.workshopRoot.transform),mesh=AssetDatabase.GetAssetPath(m),
                    materials=string.Join(";",r.sharedMaterials.Select(x=>x?x.name:"null")),centre=r.bounds.center,size=r.bounds.size,vertices=m.vertexCount,triangles=m.triangles.Length/3};
            }
            bool HasMesh(MeshRenderer r)=>r.GetComponent<MeshFilter>() && r.GetComponent<MeshFilter>().sharedMesh;
            File.WriteAllText(Output+"/inventory.json",JsonUtility.ToJson(new Inventory{
                workshop=j.workshopRoot.GetComponentsInChildren<MeshRenderer>(true).Where(HasMesh).Where(r=>!r.transform.IsChildOf(j.engine.locomotive)).Select(Describe).ToArray(),
                train=j.engine.locomotive.GetComponentsInChildren<MeshRenderer>(true).Where(HasMesh).Select(Describe).ToArray()},true));
            Canyon();
        }
        public static void Canyon()
        {
            Directory.CreateDirectory(Output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SwanCanyonLightingStudy.PrefabPath));
            var b=root.GetComponent<SwanCanyonBakedLighting>();
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.skybox=null;RenderSettings.fog=false;
            RenderSettings.ambientSkyColor=new Color(.30f,.38f,.47f);RenderSettings.ambientEquatorColor=new Color(.19f,.20f,.22f);RenderSettings.ambientGroundColor=new Color(.12f,.095f,.07f);
            Shader.SetGlobalColor("_SwanCanyonAmbientSky",RenderSettings.ambientSkyColor.linear);
            Shader.SetGlobalColor("_SwanCanyonAmbientHorizon",RenderSettings.ambientEquatorColor.linear);
            Shader.SetGlobalColor("_SwanCanyonAmbientGround",RenderSettings.ambientGroundColor.linear);
            var light=new GameObject("Review daylight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.color=new Color(1,.93f,.82f);
            float a=(.46f-.25f)*Mathf.PI*2;var orbit=new Vector3(Mathf.Cos(a)*18,Mathf.Sin(a)*20,Mathf.Sin(a)*8);
            light.transform.rotation=Quaternion.LookRotation(-(new Vector3(0,5.5f,4)+orbit-new Vector3(0,1,-3)).normalized);RenderSettings.sun=light;
            var fill=new GameObject("Review sky fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.12f;fill.color=new Color(.69f,.81f,1);fill.transform.rotation=Quaternion.LookRotation(-(new Vector3(0,5.5f,4)-orbit-new Vector3(0,1,-3)).normalized);
            var rim=new GameObject("Review rim").AddComponent<Light>();rim.type=LightType.Directional;rim.intensity=.18f;rim.color=new Color(.52f,.68f,1);rim.transform.rotation=Quaternion.Euler(38,-130,0);
            var ambient=new SphericalHarmonicsL2();ambient.AddAmbientLight(new Color(.08f,.09f,.10f));RenderSettings.ambientProbe=ambient;
            var cam=new GameObject("Review").AddComponent<Camera>();cam.fieldOfView=42;cam.nearClipPlane=.005f;cam.farClipPlane=10;
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.20f,.22f,.24f);cam.allowHDR=false;
            cam.transform.position=new Vector3(.60f,.66f,-1.05f);cam.transform.LookAt(new Vector3(0,.065f,0));
            for(int mode=0;mode<4;mode++)
            {
                b.SetReview(mode>0);
                Shader.SetGlobalFloat("_SwanCanyonLightEnabled",mode>1?1:0);
                Shader.SetGlobalTexture("_SwanCanyonLightA",b.lightStates[1]);Shader.SetGlobalTexture("_SwanCanyonLightB",b.lightStates[1]);Shader.SetGlobalFloat("_SwanCanyonLightBlend",0);
                if(mode==3)foreach(var s in b.surfaces)s.renderer.sharedMaterials=s.bakedMaterials.Select(m=>{var n=new Material(m);n.SetTexture("_CanyonBakedMap",Texture2D.whiteTexture);return n;}).ToArray();
                Capture(cam,"canyon-"+mode);
            }
            b.SetReview(true);cam.transform.position=new Vector3(-.50f,.65f,1.05f);cam.transform.LookAt(new Vector3(0,.07f,0));
            Capture(cam,"canyon-front");
            File.WriteAllLines(Output+"/canyon-surfaces.txt",root.GetComponentsInChildren<MeshRenderer>().Select(r=>r.name+" | "+r.bounds.size+" | "+string.Join(";",r.sharedMaterials.Select(m=>m.name+":"+m.shader.name))));
            Debug.Log("LIGHTING_REPAIR_REVIEW_COMPLETE");
        }
        public static void CanyonAfter()
        {
            SwanCanyonFoliage.PreparePrefab();SwanLightingFinishBake.Bind();Canyon();
        }
        public static void All(){Workshop();CanyonAfter();}
        public static void Workshop()
        {
            Directory.CreateDirectory(Output);EditorSceneManager.OpenScene("Assets/SwanJourney/Scenes/SteamJourneyXRTrial.unity");
            var j=UnityEngine.Object.FindObjectOfType<SteamJourney>(true);
            foreach(var p in j.modules.Concat(j.finishing))p.installed=true;
            j.inSandbox=false;j.cutaway=false;j.inspectionMode=0;j.ApplyVisibility();
            foreach(var r in UnityEngine.Object.FindObjectsOfType<Renderer>(true))if(!r.transform.IsChildOf(j.workshopRoot.transform))r.enabled=false;
            foreach(var cam in UnityEngine.Object.FindObjectsOfType<Camera>(true))cam.enabled=false;
            var camera=new GameObject("Workshop review").AddComponent<Camera>();camera.nearClipPlane=.01f;camera.farClipPlane=20;camera.fieldOfView=42;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.18f,.20f,.22f);camera.allowHDR=false;j.view=camera;
            var mood=UnityEngine.Object.FindObjectOfType<StudioWorkshopMood>(true);mood.SendMessage("Awake");
            RenderSettings.ambientMode=AmbientMode.Trilight;var ambient=new SphericalHarmonicsL2();ambient.AddAmbientLight(new Color(.06f,.07f,.08f));RenderSettings.ambientProbe=ambient;
            var train=j.engine.locomotive;
            void Place(Vector3 eye,Vector3 target){camera.transform.position=train.TransformPoint(eye);camera.transform.LookAt(train.TransformPoint(target));}
            var shadows=j.GetComponent<SwanDisplayShadows>();shadows.SendMessage("Awake");shadows.SendMessage("LateUpdate");
            Place(new Vector3(-5.4f,4.5f,5.2f),new Vector3(-.1f,1.8f,0));shadows.SendMessage("LateUpdate");Capture(camera,"workshop-shadow-before");
            SwanDisplayShadowBuild.Apply(j);shadows=j.GetComponent<SwanDisplayShadows>();shadows.SendMessage("Awake");shadows.SendMessage("LateUpdate");Capture(camera,"workshop-shadow-after");
            Debug.Log($"REPAIR_SHADOW_PROOF active={shadows.Active} casters={shadows.ActiveCasters} triangles={shadows.ActiveTriangles} complete={j.Complete} transition={j.SwanTransitionActive} map={Shader.GetGlobalVector("_MainLightShadowParams")} sun={RenderSettings.sun.shadows}");
            Place(new Vector3(-6.8f,2.2f,3.1f),new Vector3(-3.3f,.20f,0));shadows.SendMessage("LateUpdate");Capture(camera,"track-before");
            SwanTrackContactBake.Apply(j);Capture(camera,"track-after");
            foreach(int mode in new[]{1,2}){j.cutaway=true;j.inspectionMode=mode;j.ApplyVisibility();shadows.SendMessage("LateUpdate");Capture(camera,"workshop-mode-"+mode);}
            if(ShaderUtil.ShaderHasError(Shader.Find("SwanJourney/XR Structure Surface")))throw new Exception("Track shader error");
            Debug.Log("LIGHTING_REPAIR_WORKSHOP_COMPLETE");
        }
        public static void Capture(Camera camera,string name)
        {
            var rt=new RenderTexture(1400,1050,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
            File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
            RenderTexture.active=null;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}
