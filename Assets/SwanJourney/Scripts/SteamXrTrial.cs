using System.Collections;
using System.Collections.Generic;
using ByteDance.PICO.XR;
using SwanAtelier;
using SwanJourney;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using Phase = UnityEngine.InputSystem.TouchPhase;

[DefaultExecutionOrder(-500)]
public sealed class SteamXrTrial : MonoBehaviour
{
    public Camera view;
    public SteamJourney journey;
    public SwanJourneyInteraction interaction;
    public Mesh[] cloudControlMeshes;
    public Material[] cloudControlMaterials;
    bool valleyReview,cloudControl,cycleReview,lampReview;
    readonly bool[] held = new bool[2];
    readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
    Vector3 target;
    bool replay;
    int activeGrabFrames;

    void Awake()
    {
        journey.suppressPersistence=true;journey.SwanInputActive=true;
        cycleReview=Requested("valley_daynight_review");
        lampReview=Requested("valley_lamp_review");
        valleyReview=lampReview || cycleReview || Requested("valley_cloud_review");
        cloudControl=valleyReview && Requested("valley_cloud_control");
        if(cloudControl)
        {
            var clouds=journey.GetComponent<MesaAtmosV16>().cloudTransforms;
            for(int i=0;i<clouds.Length;i++)
            {
                clouds[i].GetComponent<MeshFilter>().sharedMesh=cloudControlMeshes[i];
                clouds[i].GetComponent<MeshRenderer>().sharedMaterial=cloudControlMaterials[i];
            }
        }
    }
    void OnEnable() { Application.onBeforeRender += TrackHead; }
    void OnDisable() { Application.onBeforeRender -= TrackHead; }
    void TrackHead()
    {
        var head = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
        if (head.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 p)) view.transform.localPosition = p;
        if (head.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion q)) view.transform.localRotation = q;
    }
    void Update()
    {
        TrackHead();
        if (replay || !interaction.Ready) return;
        for (int i=0; i<2; i++)
        {
            var device = InputDevices.GetDeviceAtXRNode(i==0 ? XRNode.LeftHand : XRNode.RightHand);
            bool pressed;
            Vector3 p;
            Quaternion q;
            HandAimState aim = default;
            if (PXR_HandTracking.GetAimState(i==0 ? HandType.HandLeft : HandType.HandRight,ref aim)
                && (aim.aimStatus & HandAimStatus.AimRayValid)!=0)
            {
                p=aim.aimRayPose.Position.ToVector3(); q=aim.aimRayPose.Orientation.ToQuat();
                pressed=(aim.aimStatus & HandAimStatus.AimRayTouched)!=0;
            }
            else
            {
                bool positioned = device.TryGetFeatureValue(CommonUsages.devicePosition,out p);
                bool oriented = device.TryGetFeatureValue(CommonUsages.deviceRotation,out q);
                pressed=positioned && oriented && device.TryGetFeatureValue(CommonUsages.triggerButton,out bool down) && down;
            }
            if (!pressed && !held[i]) continue;
            var hand=transform.TransformPoint(p);
            var dir=transform.rotation*q*Vector3.forward;
            bool hit=Physics.Raycast(hand,dir,out RaycastHit h,8f,~0,QueryTriggerInteraction.Collide);
            Send(i,pressed&&!held[i]?Phase.Began:pressed?Phase.Moved:Phase.Ended,hand,
                 hit?h.collider.gameObject:null,hit?h.point:hand+dir*.5f);
            held[i]=pressed;
        }
    }
    void Send(int i,Phase phase,Vector3 hand,GameObject obj,Vector3 hit)
    {
        interaction.QueueXrGesture(new SwanGestureSample { id=101+i,phase=phase,pinch=true,
            handPosition=hand,target=obj,hitPosition=hit });
    }
    void FrameTrain()
    {
        bool found=false;
        Bounds b=default;
        foreach (var r in journey.shellRenderers)
        {
            if (!r || !r.gameObject.activeInHierarchy) continue;
            if (!found) { b=r.bounds; found=true; } else b.Encapsulate(r.bounds);
        }
        target=found?b.center:journey.trainAnchor.TransformPoint(Vector3.up*1.3f);
        float distance=found?Mathf.Clamp(b.size.magnitude*1.1f,.65f,2.4f):1.3f;
        var eye=target+new Vector3(.20f,.25f,-1f).normalized*distance;
        transform.rotation=Quaternion.LookRotation(target-eye,Vector3.up)*Quaternion.Inverse(view.transform.localRotation);
        transform.position=eye-transform.rotation*view.transform.localPosition;
        Debug.Log("XR_TRIAL_VIEW target="+target+" eye="+view.transform.position+" bounds="+b);
    }
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(2);
        PXR_Manager.EnableVideoSeeThrough = true;
        Debug.Log("XR_TRIAL_ROOM requested="+PXR_Manager.EnableVideoSeeThrough+" clear="+view.backgroundColor);
        Application.targetFrameRate=90;
        int total=journey.modules.Length+journey.finishing.Length, installed=0;
        while (installed<total && journey.InstallCurrent()) { installed++; yield return null; }
        for (int i=0;i<600 && journey.CompletionActive;i++) yield return null;
        if (!journey.Complete) { Debug.LogError("XR_TRIAL_INCOMPLETE "+installed+"/"+total); yield break; }
        if(valleyReview){yield return ReviewValley();yield break;}
        journey.inspectionMode=1; journey.cutaway=true; journey.ApplyVisibility();
        FrameTrain();
        bool artReview=Requested("steam_art_views");
        float side=journey.furnaceParticles.transform.parent.InverseTransformPoint(view.transform.position).z>=0?1:-1;
        if(artReview)
        {
            yield return FurnaceView("coal-cold",side);
            FrameTrain();
        }
        yield return new WaitForSecondsRealtime(3);
        yield return Sample("xray",false);
        journey.Act(JourneyAction.Cutaway);
        journey.Act(JourneyAction.Verify);
        double timeout=Time.realtimeSinceStartupAsDouble+90;
        while (journey.WheelPower<.05f && Time.realtimeSinceStartupAsDouble<timeout) yield return null;
        yield return Sample("cutaway-hot",false);
        replay=true;
        yield return Sample("two-hand-replay",true);
        replay=false;
        Debug.Log("XR_TRIAL_DONE");
        if(artReview)
        {
            yield return FurnaceView("coal-hot",side);
            yield return ShowArt("fire-window",new Vector3(-1.40f,1.80f,0),new Vector3(-2.18f,2.17f,1.09f*side));
            yield return ShowArt("boiler-waterline",new Vector3(.69f,2.79f,0),new Vector3(.37f,3.40f,1.54f*side));
            yield return ShowArt("chimney",new Vector3(2.55f,4.40f,0),new Vector3(3.15f,4.65f,2.55f*side));
            journey.Act(JourneyAction.Verify);
            float waitUntil=Time.realtimeSinceStartup+18f;
            while(journey.FireLevel>.001f && Time.realtimeSinceStartup<waitUntil) yield return null;
            yield return FurnaceView("coal-ash",side);
            journey.Act(JourneyAction.Verify);
            FrameTrain();
            Debug.Log("XR_ART_DONE");
        }
    }
    static bool Requested(string key)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
        using(var intent=activity.Call<AndroidJavaObject>("getIntent"))
            return intent.Call<bool>("getBooleanExtra",key,false);
#else
        return false;
#endif
    }
    void PlaceView(Transform root,Vector3 localEye,Vector3 localTarget)
    {
        Vector3 eye=root.TransformPoint(localEye),at=root.TransformPoint(localTarget);
        transform.rotation=Quaternion.LookRotation(at-eye,root.up)*Quaternion.Inverse(view.transform.localRotation);
        transform.position=eye-transform.rotation*view.transform.localPosition;
    }
    IEnumerator ReviewValley()
    {
        yield return new WaitForSecondsRealtime(3);
        journey.Act(JourneyAction.Drive);
        float deadline=Time.realtimeSinceStartup+90;
        while((!journey.inSandbox || journey.SwanSceneLoading || journey.SwanTransitionActive) && Time.realtimeSinceStartup<deadline)
            yield return null;
        if(!journey.inSandbox || journey.SwanSceneLoading || journey.SwanTransitionActive){Debug.LogError("XR_CLOUD_FAIL valley load");yield break;}
        var clouds=journey.GetComponent<MesaAtmosV16>().cloudTransforms;
        var sky=clouds[0].parent;
        var eye=new Vector3(1,20.5f,24);var aim=new Vector3(0,7,0);
        PlaceView(sky,eye,aim);
        yield return new WaitForSecondsRealtime(4);
        if(cycleReview){yield return ReviewCycle(sky,eye,aim);yield break;}
        if(lampReview){yield return ReviewHeadlamps(sky,eye,aim);yield break;}
        Debug.Log("XR_CLOUD_STATE control="+cloudControl+" shader="+clouds[1].GetComponent<Renderer>().sharedMaterial.shader.name+
            " parkedSpeed="+journey.speed+" eyes="+view.stereoEnabled);
        Debug.Log("XR_CLOUD_SHOT overview");
        yield return new WaitForSecondsRealtime(3);
        yield return Sample("valley-parked",false);
        journey.SwanSetControl(JourneyAction.Brake,0);
        journey.SwanSetControl(JourneyAction.Throttle,.75f);
        deadline=Time.realtimeSinceStartup+25;
        while(journey.speed<.5f && Time.realtimeSinceStartup<deadline) yield return null;
        float startDistance=journey.routeDistance;
        yield return Sample("valley-moving",false);
        Debug.Log("XR_CLOUD_DRIVE speed="+journey.speed+" distanceDelta="+(journey.routeDistance-startDistance));
        Vector3 center=sky.InverseTransformPoint(clouds[1].position);
        PlaceView(sky,center+new Vector3(3,2.0f,9.0f),center);
        yield return new WaitForSecondsRealtime(.7f);Debug.Log("XR_CLOUD_SHOT close");
        yield return new WaitForSecondsRealtime(4);
        PlaceView(sky,center+new Vector3(8.0f,2.5f,5.5f),center);
        yield return new WaitForSecondsRealtime(.7f);Debug.Log("XR_CLOUD_SHOT side");
        yield return new WaitForSecondsRealtime(3);
        PlaceView(sky,eye,aim);
        Debug.Log("XR_CLOUD_DONE");
    }
    IEnumerator ReviewHeadlamps(Transform skyRoot,Vector3 eye,Vector3 aim)
    {
        var daylight=journey.GetComponent<MesaDayNightV13>();var lights=journey.valleyHeadlamps;
        var atmosphere=journey.GetComponent<MesaAtmosV16>();
        var panel=journey.sandboxRoot.GetComponentsInChildren<SwanJourneyTarget>(true);
        var button=System.Array.Find(panel,t=>t.action==JourneyAction.Lights && t.useActionOverride && t.GetComponent<Collider>());
        if(!button || !button.gameObject.activeInHierarchy){Debug.LogError("XR_LAMP_FAIL missing switch");yield break;}
        daylight.automatic=false;daylight.SetTime(.93f);
        if(journey.HeadlampsSelected)journey.SwanAct(button.action);
        var left=lights.lenses[0].transform.parent;var right=lights.lenses[1].transform.parent;
        var centre=(left.position+right.position)*.5f;float scale=Mathf.Abs(left.lossyScale.z);
        var cameraPoint=centre+(left.forward*10f+left.right*2.2f+left.up*1.8f)*scale;
        PlaceView(skyRoot,skyRoot.InverseTransformPoint(cameraPoint),skyRoot.InverseTransformPoint(centre+left.forward*.8f*scale));
        yield return new WaitForSecondsRealtime(1);Debug.Log("XR_LAMP_SHOT lamps-off visible="+lights.Lit);
        yield return new WaitForSecondsRealtime(3);
        bool toggled=journey.SwanAct(button.action);
        yield return new WaitForSecondsRealtime(.6f);Debug.Log("XR_LAMP_SHOT lamps-on visible="+lights.Lit+" action="+toggled+" scale="+scale+" range="+lights.spots[0].range);
        yield return new WaitForSecondsRealtime(3);
        cameraPoint=centre+(left.forward*25+left.right*3+left.up*3.5f)*scale;
        PlaceView(skyRoot,skyRoot.InverseTransformPoint(cameraPoint),skyRoot.InverseTransformPoint(centre));
        yield return new WaitForSecondsRealtime(1);Debug.Log("XR_LAMP_SHOT lamps-distant");
        yield return new WaitForSecondsRealtime(3);
        // Fixed three-quarter view: isolate the actual URP-lit surfaces from the translucent shafts.
        var up=lights.transform.up;
        var forward=Vector3.ProjectOnPlane(left.forward,up).normalized;
        cameraPoint=centre+(forward*6.6f+left.right*12f+up*7.2f)*scale;
        var trackAim=centre+(forward*4.6f-up*.9f)*scale;
        PlaceView(skyRoot,skyRoot.InverseTransformPoint(cameraPoint),skyRoot.InverseTransformPoint(trackAim));
        journey.SwanAct(button.action);
        yield return new WaitForSecondsRealtime(1.4f);Debug.Log("XR_LAMP_SHOT track-off");
        yield return new WaitForSecondsRealtime(3);
        journey.SwanAct(button.action);
        foreach(var beam in lights.beams)beam.enabled=false;
        yield return new WaitForSecondsRealtime(1.4f);
        Debug.Log("XR_LAMP_SHOT track-lit range="+lights.spots[0].range+" intensity="+lights.spots[0].intensity);
        yield return new WaitForSecondsRealtime(3);
        foreach(var beam in lights.beams)beam.enabled=true;
        yield return new WaitForSecondsRealtime(1.4f);Debug.Log("XR_LAMP_SHOT track-beams");
        yield return new WaitForSecondsRealtime(3);
        PlaceView(skyRoot,eye,aim);journey.SwanSetControl(JourneyAction.Brake,0);journey.SwanSetControl(JourneyAction.Throttle,.75f);
        yield return new WaitForSecondsRealtime(8);
        journey.SwanAct(button.action);yield return new WaitForSecondsRealtime(.6f);float start=journey.routeDistance;
        yield return Sample("valley-lamps-off",false);
        Debug.Log("XR_LAMP_MOTION off lit="+lights.Lit+" speed="+journey.speed+" delta="+(journey.routeDistance-start)+" puffs="+atmosphere.ActivePuffs);
        journey.SwanAct(button.action);yield return new WaitForSecondsRealtime(.6f);start=journey.routeDistance;
        yield return Sample("valley-lamps-on",false);
        Debug.Log("XR_LAMP_MOTION on lit="+lights.Lit+" speed="+journey.speed+" delta="+(journey.routeDistance-start)+" puffs="+atmosphere.ActivePuffs+" invalid="+atmosphere.InvalidCenters);
        daylight.SetTime(.46f);
        float deadline=Time.realtimeSinceStartup+38;
        // Show the plume on the open, near side of the track.
        while((atmosphere.TrailReach()<5f || skyRoot.InverseTransformPoint(lights.transform.position).z<2) && Time.realtimeSinceStartup<deadline)
            yield return null;
        Debug.Log("XR_LAMP_SHOT valley-trail reach="+atmosphere.TrailReach()+" puffs="+atmosphere.ActivePuffs+" speed="+journey.speed+" invalid="+atmosphere.InvalidCenters);
        yield return new WaitForSecondsRealtime(4);yield return Sample("valley-trail-day",false);
        var environment=FindObjectOfType<SwanJourneyEnvironment>();
        MeshRenderer cactus=null; float best=float.MaxValue;
        foreach(var r in environment.GetComponentsInChildren<MeshRenderer>())
        {
            if(r.name!="Senita1_LOD0")continue;
            var local=skyRoot.InverseTransformPoint(r.bounds.center);
            float d=(new Vector2(local.x,local.z)-new Vector2(0,6)).sqrMagnitude;
            if(d<best){cactus=r;best=d;}
        }
        if(!cactus){Debug.LogError("XR_LAMP_FAIL missing green cactus");yield break;}
        var patch=skyRoot.InverseTransformPoint(cactus.bounds.center);
        var closeEye=patch+new Vector3(.85f,1.2f,3.5f);
        PlaceView(skyRoot,closeEye,patch);
        yield return new WaitForSecondsRealtime(1.5f);Debug.Log("XR_LAMP_SHOT clean-day");
        yield return new WaitForSecondsRealtime(3);
        daylight.SetTime(.93f);
        yield return new WaitForSecondsRealtime(1.5f);Debug.Log("XR_LAMP_SHOT clean-night");
        yield return new WaitForSecondsRealtime(3);
        daylight.SetTime(.46f);PlaceView(skyRoot,eye,aim);
        daylight.automatic=true;Debug.Log("XR_LAMP_DONE");
    }
    IEnumerator ReviewCycle(Transform skyRoot,Vector3 eye,Vector3 aim)
    {
        var sky=journey.GetComponent<MesaDayNightV13>();
        float began=Time.realtimeSinceStartup;
        float entry=sky.timeOfDay;
        Debug.Log("XR_DN_ENTER time="+entry+" seconds="+sky.cycleSeconds+" automatic="+sky.automatic);
        yield return Sample("cycle-parked",false);
        journey.SwanSetControl(JourneyAction.Brake,0);journey.SwanSetControl(JourneyAction.Throttle,.75f);
        while(journey.speed<.5f && Time.realtimeSinceStartup-began<30) yield return null;
        yield return CyclePhase(sky,"day");
        yield return WaitSolar(sky,.742f);
        yield return CyclePhase(sky,"sunset");
        yield return WaitSolar(sky,.94f);
        yield return CyclePhase(sky,"night");
        yield return WaitSolar(sky,.221f);
        yield return CyclePhase(sky,"sunrise");
        yield return WaitSolar(sky,entry);
        Debug.Log("XR_DN_ORBIT elapsed="+(Time.realtimeSinceStartup-began)+" speed="+journey.speed);
        journey.SwanSetControl(JourneyAction.Throttle,0);journey.SwanSetControl(JourneyAction.Brake,1);
        journey.SetMode(false);
        yield return new WaitForSecondsRealtime(2);
        Debug.Log("XR_DN_RETURN workshop="+(!journey.inSandbox)+" roomAlpha="+view.backgroundColor.a+
            " nightTint="+Shader.GetGlobalFloat("_SwanSkyNight")+" complete="+journey.Complete);
        journey.SetMode(true);
        float deadline=Time.realtimeSinceStartup+65;
        while((!journey.inSandbox || journey.SwanSceneLoading || journey.SwanTransitionActive) && Time.realtimeSinceStartup<deadline)yield return null;
        if(!journey.inSandbox || journey.SwanSceneLoading){Debug.LogError("XR_DN_FAIL return to valley");yield break;}
        PlaceView(skyRoot,eye,aim);journey.SwanSetControl(JourneyAction.Brake,0);journey.SwanSetControl(JourneyAction.Throttle,.75f);
        Debug.Log("XR_DN_DONE");
    }
    IEnumerator WaitSolar(MesaDayNightV13 sky,float phase)
    {
        float advance=Mathf.Repeat(phase-sky.timeOfDay,1),previous=sky.timeOfDay,total=0;
        float deadline=Time.realtimeSinceStartup+sky.cycleSeconds*advance+10;
        while(total<advance && Time.realtimeSinceStartup<deadline)
        {
            yield return null;total+=Mathf.Repeat(sky.timeOfDay-previous,1);previous=sky.timeOfDay;
        }
        if(total<advance)Debug.LogError("XR_DN_FAIL clock stopped");
    }
    IEnumerator CyclePhase(MesaDayNightV13 sky,string phase)
    {
        Debug.Log("XR_DN_SHOT "+phase+" time="+sky.timeOfDay+" daylight="+sky.Daylight+
            " sun="+sky.sun.gameObject.activeInHierarchy+" moon="+sky.moon.gameObject.activeInHierarchy+
            " cloudNight="+Shader.GetGlobalFloat("_SwanSkyNight")+" roomAlpha="+view.backgroundColor.a);
        yield return new WaitForSecondsRealtime(2.5f);
        float route=journey.routeDistance;
        yield return Sample("cycle-"+phase,false);
        Debug.Log("XR_DN_MOTION "+phase+" speed="+journey.speed+" distance="+(journey.routeDistance-route));
    }
    IEnumerator FurnaceView(string label,float side)
    {
        return ShowArt(label,new Vector3(-1.33f,1.72f,0),new Vector3(-1.95f,2.12f,.82f*side));
    }
    IEnumerator ShowArt(string label,Vector3 focus,Vector3 cameraPoint)
    {
        var loco=journey.furnaceParticles.transform.parent;
        Vector3 eye=loco.TransformPoint(cameraPoint);
        transform.rotation=Quaternion.LookRotation(loco.TransformPoint(focus)-eye,loco.up)*Quaternion.Inverse(view.transform.localRotation);
        transform.position=eye-transform.rotation*view.transform.localPosition;
        yield return new WaitForSecondsRealtime(.7f);
        int flames=0,coals=0;
        foreach(var s in journey.furnaceParticles.GetComponentsInChildren<ParticleSystem>(true)) flames+=s.particleCount;
        foreach(var r in journey.emberRenderers) if(r && r.enabled && r.gameObject.activeInHierarchy) coals++;
        var plume=journey.bakedChimney.rollingPlume;
        Debug.Log("XR_ART_SHOT "+label+" fire="+flames+" coal="+coals+" heat="+journey.FireLevel+
            " vapour="+journey.steam.particleCount+" chimney="+(plume?plume.particleCount:0));
        yield return new WaitForSecondsRealtime(4);
    }
    IEnumerator Sample(string label,bool move)
    {
        SubsystemManager.GetInstances(displays);
        var d=displays.Find(x=>x.running);
        var loader=XRGeneralSettings.Instance.Manager.activeLoader;
        int passes=d!=null?d.GetRenderPassCount():0, eyes=0;
        if (d!=null) for(int p=0;p<passes;p++){d.GetRenderPass(p,out var pass);eyes+=pass.GetRenderParameterCount();}
        Debug.Log("XR_TRIAL_BEGIN "+label+" loader="+(loader?loader.name:"none")+" display="+(d!=null)+" eyes="+eyes+
            " resolution="+XRSettings.eyeTextureWidth+"x"+XRSettings.eyeTextureHeight+" installed="+journey.Complete+
            " mode="+journey.inspectionMode+" fire="+journey.FireLevel+" wheel="+journey.WheelPower);
        var times=new List<float>(2048);
        double start=Time.realtimeSinceStartupAsDouble,last=start;
        activeGrabFrames=0;
        var startTarget=interaction.workbenchRoot.position;
        while (Time.realtimeSinceStartupAsDouble-start<12)
        {
            if(move)
            {
                float wave=.025f*Mathf.Sin((float)(last-start)*1.4f);
                for(int i=0;i<2;i++){
                    var h=target+view.transform.right*((i==0?-1:1)*(.10f+wave))+Vector3.forward*wave;
                    Send(i,times.Count==0?Phase.Began:Phase.Moved,h,interaction.workbenchRoot.gameObject,h);
                }
            }
            yield return null;
            double now=Time.realtimeSinceStartupAsDouble;
            times.Add((float)((now-last)*1000));last=now;
            if(interaction.TwoHandActive) activeGrabFrames++;
        }
        if(move) for(int i=0;i<2;i++) Send(i,Phase.Ended,target,interaction.workbenchRoot.gameObject,target);
        times.Sort();
        Debug.LogFormat("XR_TRIAL_RESULT {0} fps={1:F2} frames={2} p95ms={3:F2} grabFrames={4} moved={5:F4} focused={6}",
            label,times.Count/(last-start),times.Count,times[(times.Count-1)*95/100],activeGrabFrames,
            Vector3.Distance(interaction.workbenchRoot.position,startTarget),Application.isFocused);
    }
}
