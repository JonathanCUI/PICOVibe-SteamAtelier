using System.Collections;
using SwanJourney;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.IO;

public sealed partial class SteamXrTrial
{
    IEnumerator ReviewDisplayLighting()
    {
        CancelHeld();replay=true;detailReview=true;
        var fx=journey.GetComponent<SwanDisplayShadows>();
        var cards=journey.GetComponent<StudioPartCards>();if(cards && cards.visible)cards.Toggle();
        if(!fx){Debug.LogError("XR_LIGHTING_FAIL missing-controller");yield break;}
        Debug.Log("XR_LIGHTING_READY");
        yield return new WaitForSecondsRealtime(3);
        yield return CompletionWindow("assembly-shadow-free",6);
        if(fx.Active){Debug.LogError("XR_LIGHTING_FAIL assembly-gate");yield break;}
        while(!journey.Complete && journey.InstallCurrent())yield return null;
        while(journey.CompletionActive)yield return null;
        for(int i=0;i<3 && journey.cutaway;i++)journey.Act(JourneyAction.Cutaway);
        var train=journey.engine.locomotive;
        float side=train.InverseTransformPoint(view.transform.position).z>=0?1:-1;
        PlaceView(train,new Vector3(2.8f,4.0f,7.5f*side),new Vector3(0,1.35f,0));
        fx.ReviewDisabled=true;fx.ReviewAoDisabled=true;yield return LightingShot("workshop-before");
        yield return CompletionWindow("complete-no-shadow",7);
        fx.ReviewDisabled=false;fx.ReviewAoDisabled=false;yield return LightingShot("workshop-after");
        yield return CompletionWindow("complete-one-shadow",7);
        Debug.Log($"XR_LIGHTING_CHECK workshop active={fx.Active} casters={fx.ActiveCasters} triangles={fx.ActiveTriangles}");
        if(!ShadowMapProof("workshop")){Debug.LogError("XR_LIGHTING_FAIL workshop-shadow-map");yield break;}
        if(!fx.Active){Debug.LogError("XR_LIGHTING_FAIL complete-gate");yield break;}
        var track=journey.workshopRoot.GetComponentsInChildren<MeshRenderer>(true);
        int contactReceivers=0;foreach(var r in track)foreach(var m in r.sharedMaterials)
            if(m && m.IsKeywordEnabled("_TRACK_CONTACT")){contactReceivers++;break;}
        if(contactReceivers!=29){Debug.LogError("XR_LIGHTING_FAIL track-contact-receivers "+contactReceivers);yield break;}
        int assemblyContact=0;
        foreach(var r in journey.workshopRoot.GetComponentsInChildren<MeshRenderer>(true))foreach(var m in r.sharedMaterials)
            if(m && m.IsKeywordEnabled("_DETAIL_CONTACT"))
            {
                var mesh=r.GetComponent<MeshFilter>().sharedMesh;
                if(!m.GetTexture("_TrackContactMap") || mesh.uv2.Length!=mesh.vertexCount){Debug.LogError("XR_LIGHTING_FAIL assembly-contact-binding "+r.name);yield break;}
                assemblyContact++;break;
            }
        if(assemblyContact<10){Debug.LogError("XR_LIGHTING_FAIL assembly-contact-count "+assemblyContact);yield break;}
        Debug.Log("XR_LIGHTING_CONTACT receivers="+assemblyContact+" track="+contactReceivers);
        var controlRoot=journey.GetComponent<StudioSpatialConsoleV18>().console;
        PlaceView(controlRoot,new Vector3(-2.5f,3.4f,4.3f),new Vector3(-1.1f,.05f,.65f));
        fx.ReviewAoDisabled=true;yield return LightingShot("console-contact-before");
        fx.ReviewAoDisabled=false;yield return LightingShot("console-contact-after");
        PlaceView(train,new Vector3(5.1f,4.2f,5.6f*side),new Vector3(.5f,1.6f,0));
        yield return LightingShot("handrail-shadow-close");
        PlaceView(train,new Vector3(6.4f,2.1f,3.0f*side),new Vector3(3.1f,.15f,0));
        yield return LightingShot("track-contact-close");
        for(int mode=1;mode<=2;mode++)
        {
            journey.Act(JourneyAction.Cutaway);yield return new WaitForSecondsRealtime(.5f);
            if(!fx.Active || fx.ActiveCasters!=1 || !ShadowMapProof("inspection-"+mode))
            {Debug.LogError("XR_LIGHTING_FAIL inspection-shadow-"+mode);yield break;}
            yield return LightingShot(mode==1?"workshop-xray":"workshop-section");
            yield return CompletionWindow("inspection-shadow-"+mode,7);
            Debug.Log($"XR_LIGHTING_CHECK inspection={mode} active={fx.Active} casters={fx.ActiveCasters} triangles={fx.ActiveTriangles}");
        }
        journey.Act(JourneyAction.Drive);
        double deadline=Time.realtimeSinceStartupAsDouble+90;
        while((!journey.inSandbox || journey.SwanSceneLoading || journey.SwanTransitionActive) && Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(!journey.inSandbox || journey.SwanTransitionActive){Debug.LogError("XR_LIGHTING_FAIL canyon-load");yield break;}
        var drivingCaption=journey.GetComponent<StudioSpatialRefineV19>().driveMessage;
        if(drivingCaption.gameObject.activeSelf || !string.IsNullOrEmpty(drivingCaption.text)){Debug.LogError("XR_LIGHTING_FAIL driving-caption-returned");yield break;}
        var sky=journey.GetComponent<SwanValleyIntegration>().sky;sky.automatic=false;sky.SetTime(.46f);
        journey.SwanSetControl(JourneyAction.Brake,1);journey.SwanSetControl(JourneyAction.Throttle,0);journey.speed=0;
        journey.routeDistance=5;journey.SendMessage("PlaceTrain");
        PlaceView(journey.spatialRoot,new Vector3(1,20.5f,24),new Vector3(0,7,0));
        for(int pass=0;pass<2;pass++)
        {
            fx.ReviewDisabled=true;yield return LightingShot("day-before-"+pass);
            yield return CompletionWindow("valley-no-shadow-"+pass,7,null,age=>{journey.routeDistance=4+age;journey.SendMessage("PlaceTrain");});
            fx.ReviewDisabled=false;yield return LightingShot("day-after-"+pass);
            yield return CompletionWindow("valley-one-shadow-"+pass,7,null,age=>{journey.routeDistance=4+age;journey.SendMessage("PlaceTrain");});
        }
        Debug.Log($"XR_LIGHTING_CHECK valley active={fx.Active} casters={fx.ActiveCasters} triangles={fx.ActiveTriangles}");
        if(!ShadowMapProof("valley")){Debug.LogError("XR_LIGHTING_FAIL valley-shadow-map");yield break;}
        var path=new JourneyPath();
        PlaceView(journey.spatialRoot,path.At(16)+Vector3.up*1.2f,path.At(20)+Vector3.up*.5f);
        yield return LightingShot("portal-entry");
        var bake=Object.FindObjectOfType<SwanCanyonBakedLighting>();
        if(!bake || bake.lightStates==null || bake.lightStates.Length!=4)
        {Debug.LogError("XR_LIGHTING_FAIL baked-states");yield break;}
        CaptureLightingMaps(bake);
        foreach(var surface in bake.surfaces)foreach(var material in surface.bakedMaterials)
            if(!material.GetTexture("_CanyonBakedMap")){Debug.LogError("XR_LIGHTING_FAIL missing-visibility-map");yield break;}
        PlaceView(journey.spatialRoot,new Vector3(1,20.5f,24),new Vector3(0,7,0));
        foreach(float time in new[]{.27f,.46f,.73f,.93f})
        {
            sky.SetTime(time);bake.ReviewLightingDisabled=true;yield return LightingShot("bake-before-"+time.ToString("F2"));
            bake.ReviewLightingDisabled=false;yield return LightingShot("bake-after-"+time.ToString("F2"));
            if(Shader.GetGlobalFloat("_SwanCanyonLightEnabled")<.5f){Debug.LogError("XR_LIGHTING_FAIL irradiance-inactive");yield break;}
            if(time==.73f)yield return CompletionWindow("valley-dusk-baked",7);
            if(time==.46f || time==.93f)
            {
                PlaceView(bake.transform,new Vector3(-.35f,.30f,-.58f),new Vector3(-.03f,.10f,.01f));
                yield return LightingShot("cave-contact-"+time.ToString("F2"));
                PlaceView(bake.transform,new Vector3(.60f,.30f,-.60f),new Vector3(.40f,.045f,-.25f));
                yield return LightingShot("stones-contact-"+time.ToString("F2"));
                PlaceView(journey.spatialRoot,new Vector3(1,20.5f,24),new Vector3(0,7,0));
            }
        }
        sky.SetTime(.46f);journey.routeDistance=5;journey.SendMessage("PlaceTrain");
        PlaceView(journey.consoleRoot.transform,new Vector3(2.6f,3.5f,4.5f),new Vector3(0,.1f,0));
        yield return LightingShot("driving-console-contact");
        var coach=journey.valleyTrain.vehicles[2];
        PlaceView(coach,new Vector3(0,5,7.5f),new Vector3(0,.6f,0));
        fx.ReviewDisabled=true;yield return LightingShot("coach-no-shadow-close");
        fx.ReviewDisabled=false;yield return LightingShot("coach-filtered-shadow-close");
        sky.SetTime(.69f);yield return LightingShot("coach-dusk-shadow-close");
        sky.SetTime(.46f);
        PlaceView(bake.transform,new Vector3(-.32f,.115f,.028f),new Vector3(-.12f,.09f,.028f));
        yield return LightingShot("portal-exit");
        sky.SetTime(.9f);PlaceView(journey.spatialRoot,new Vector3(1,20.5f,24),new Vector3(0,7,0));
        fx.ReviewDisabled=true;yield return LightingShot("night-before");yield return CompletionWindow("night-no-shadow",7);
        fx.ReviewDisabled=false;yield return LightingShot("night-after");yield return CompletionWindow("night-one-shadow",7);
        if(!ShadowMapProof("night")){Debug.LogError("XR_LIGHTING_FAIL night-shadow-map");yield break;}
        yield return ReviewTunnelHeadlights(bake);
        var table=journey.spatialRoot;var p=table.position;var q=table.rotation;var scale=table.localScale;
        table.position+=Vector3.right*.15f;table.rotation=Quaternion.AngleAxis(18,Vector3.up)*q;table.localScale=scale*1.15f;
        yield return LightingShot("moved-resized");table.SetPositionAndRotation(p,q);table.localScale=scale;
        journey.SetMode(false);deadline=Time.realtimeSinceStartupAsDouble+40;
        while((journey.SwanTransitionActive || journey.SwanSceneLoading) && Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        PlaceView(train,new Vector3(5.1f,4.2f,5.6f*side),new Vector3(.5f,1.6f,0));
        yield return LightingShot("contact-after-return");
        journey.ResetJourney();yield return new WaitForSecondsRealtime(2);
        if(fx.Active){Debug.LogError("XR_LIGHTING_FAIL reset-gate");yield break;}
        sky.automatic=true;fx.ReviewDisabled=false;replay=false;detailReview=false;
        Debug.Log("XR_LIGHTING_DONE assemblyGate=true inspectionShadows=true dayNightBakes=4 resetGate=true");
    }

    IEnumerator ReviewTunnelHeadlights(SwanCanyonBakedLighting bake)
    {
        var lamps=journey.valleyHeadlamps;
        var integration=journey.GetComponent<SwanValleyIntegration>();
        integration.sky.SetTime(.93f);
        yield return new WaitForSecondsRealtime(3);
        int masks=0;foreach(var surface in bake.surfaces)foreach(var m in surface.bakedMaterials)
            if(m.IsKeywordEnabled("_CANYON_TUNNEL"))masks++;
        if(masks<5 || lamps.spots.Length!=2){Debug.LogError("XR_LIGHTING_FAIL tunnel setup");yield break;}
        Debug.Log("XR_LIGHTING_TUNNEL receivers="+masks+" spots="+lamps.spots.Length);
        foreach(float route in new[]{19.8f,23.0f})
        {
            journey.routeDistance=route;journey.speed=0;journey.SendMessage("PlaceTrain");
            var path=new JourneyPath();var point=path.At(route);
            var uv=point-integration.fieldMin;uv.Scale(new Vector3(1/integration.fieldSize.x,1/integration.fieldSize.y,1/integration.fieldSize.z));
            var field=integration.tunnelField;
            float visibility=field.GetPixel(Mathf.Clamp((int)(uv.x*field.width),0,field.width-1),
                Mathf.Clamp((int)(uv.y*field.height),0,field.height-1),Mathf.Clamp((int)(uv.z*field.depth),0,field.depth-1)).r;
            Debug.Log($"XR_LIGHTING_TUNNEL route={route} position={point} visibility={visibility:F3} spot={lamps.spots[0].intensity:F3} range={lamps.spots[0].range:F3}");
            PlaceView(journey.spatialRoot,new Vector3(-4.0f,3.5f,-1.6f),new Vector3(.8f,2.0f,-5.25f));
            if(route>22)PlaceView(journey.spatialRoot,new Vector3(10.8f,3.2f,-5.0f),new Vector3(3.9f,2.0f,-5.25f));
            foreach(var b in lamps.beams)b.enabled=false;
            foreach(var light in lamps.spots)light.enabled=false;
            yield return LightingShot("tunnel-dark-"+route.ToString("F1"));
            foreach(var light in lamps.spots)light.enabled=true;
            yield return LightingShot("tunnel-lit-"+route.ToString("F1"));
            foreach(var b in lamps.beams)b.enabled=true;
            yield return LightingShot("tunnel-beam-"+route.ToString("F1"));
        }
        PlaceView(journey.spatialRoot,new Vector3(1,18,21.5f),new Vector3(0,6.5f,0));
        yield return CompletionWindow("night-tunnel-lighting",9,null,age=>{journey.routeDistance=18+age;journey.SendMessage("PlaceTrain");});
    }
    IEnumerator LightingShot(string label)
    {
        // Wait for rendered frames, not only elapsed time: a stalled emulator can
        // return an older compositor frame after the state has already changed.
        for(int i=0;i<8;i++)yield return new WaitForEndOfFrame();
        var caption=journey.GetComponent<StudioSpatialConsoleV18>().message;
        if(caption.gameObject.activeSelf || !string.IsNullOrEmpty(caption.text))Debug.LogError("XR_LIGHTING_FAIL workshop-caption-returned");
        CaptureLightingDetail(label);
        Debug.Log("XR_LIGHTING_SHOT "+label);yield return new WaitForSecondsRealtime(2);
    }
    void CaptureLightingDetail(string label)
    {
        // Diagnostic single frame only, outside every FPS measurement window.
        // This does not change the XR camera or the normal player render path.
        var go=new GameObject("Lighting review capture");var camera=go.AddComponent<Camera>();
        camera.CopyFrom(view);camera.enabled=false;camera.stereoTargetEye=StereoTargetEyeMask.None;
        camera.transform.SetPositionAndRotation(view.transform.position,view.transform.rotation);
        camera.fieldOfView=48;camera.aspect=1.4f;camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.18f,.20f,.22f,1);
        var data=camera.GetUniversalAdditionalCameraData();data.allowXRRendering=false;
        var target=new RenderTexture(1400,1000,24,RenderTextureFormat.ARGB32);
        var image=new Texture2D(1400,1000,TextureFormat.RGB24,false);
        var active=RenderTexture.active;
        try
        {
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1400,1000),0,0);image.Apply();
            var folder=Path.Combine(Application.persistentDataPath,"LightingReview");Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder,label+".png"),image.EncodeToPNG());
        }
        finally {RenderTexture.active=active;Destroy(image);target.Release();Destroy(target);Destroy(go);}
        Debug.Log("XR_LIGHTING_DETAIL "+label);
    }
    void CaptureLightingMaps(SwanCanyonBakedLighting bake)
    {
        string folder=Path.Combine(Application.persistentDataPath,"LightingReview");Directory.CreateDirectory(folder);
        var previous=RenderTexture.active;
        for(int i=0;i<bake.lightStates.Length;i++)
        {
            var map=bake.lightStates[i];var rt=RenderTexture.GetTemporary(1024,1024,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            Graphics.Blit(map,rt);RenderTexture.active=rt;
            var image=new Texture2D(1024,1024,TextureFormat.RGB24,false,true);image.ReadPixels(new Rect(0,0,1024,1024),0,0);image.Apply();
            string label="gpu-light-map-"+i;File.WriteAllBytes(Path.Combine(folder,label+".png"),image.EncodeToPNG());
            Destroy(image);RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Debug.Log("XR_LIGHTING_DETAIL "+label);
            Debug.Log($"XR_LIGHTING_TEXTURE state={i} format={map.format} size={map.width}x{map.height}");
        }
    }
    bool ShadowMapProof(string label)
    {
        var parameters=Shader.GetGlobalVector("_MainLightShadowParams");
        var size=Shader.GetGlobalVector("_MainLightShadowmapSize");
        bool keyword=Shader.IsKeywordEnabled("_MAIN_LIGHT_SHADOWS") || Shader.IsKeywordEnabled("_MAIN_LIGHT_SHADOWS_CASCADE");
        var sun=RenderSettings.sun;
        bool filtered=Shader.IsKeywordEnabled("_SHADOWS_SOFT") || Shader.IsKeywordEnabled("_SHADOWS_SOFT_LOW") || Shader.IsKeywordEnabled("_SHADOWS_SOFT_MEDIUM");
        Debug.Log($"XR_LIGHTING_MAP {label} keyword={keyword} filtered={filtered} strength={parameters.x:F3} size={size} sun={(sun?sun.type.ToString():"none")} shadows={(sun?sun.shadows.ToString():"none")}");
        return keyword && filtered && parameters.x>.1f && sun && sun.type==LightType.Directional && sun.shadows!=LightShadows.None;
    }
}
