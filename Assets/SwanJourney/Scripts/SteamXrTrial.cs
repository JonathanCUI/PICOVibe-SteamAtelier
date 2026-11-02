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
    public SteamXrFocus focus;
    public SkinnedMeshRenderer[] handMasks;
    public Shader handMaskShader;
    Material maskMaterial;
    readonly PXR_Hand[] trackedHands = new PXR_Hand[2];
    float nextHandFit;
    readonly bool[] touchingButton = new bool[2];
    public Mesh[] cloudControlMeshes;
    public Material[] cloudControlMaterials;
    bool valleyReview,cloudControl,cycleReview,lampReview;
    readonly bool[] held = new bool[2];
    readonly PartPinchMotion[] partPinches = new PartPinchMotion[2];
    readonly HandJointLocations[] joints = new HandJointLocations[2];
    readonly bool[] usingHand = new bool[2];
    readonly float[] lastHandPose = new float[2];
    readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
    Vector3 target;
    bool replay;
    int activeGrabFrames;

    void Awake()
    {
        maskMaterial=new Material(handMaskShader);
        foreach (var skin in handMasks)
        {
            var slots=new Material[skin.sharedMesh.subMeshCount];
            for(int j=0;j<slots.Length;j++) slots[j]=maskMaterial;
            skin.sharedMaterials=slots;
        }
        for(int i=0;i<2;i++) trackedHands[i]=handMasks[i].GetComponentInParent<PXR_Hand>();
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
    void OnEnable() { Application.onBeforeRender += BeforeRender; }
    void OnDisable() { Application.onBeforeRender -= BeforeRender; CancelHeld(); }
    void OnDestroy() { if(maskMaterial) Destroy(maskMaterial); }
    void OnApplicationPause(bool paused) { if (paused) CancelHeld(); }
    void OnApplicationFocus(bool focused) { if (!focused) CancelHeld(); }
    void CancelHeld()
    {
        for (int i=0; i<held.Length; i++)
        {
            CancelHand(i); touchingButton[i]=false;
            if(handMasks!=null && handMasks.Length>i && handMasks[i]) handMasks[i].enabled=false;
        }
    }
    void CancelHand(int i)
    {
        partPinches[i]=default;
        if (!held[i]) return;
        held[i]=false;
        if (interaction) Send(i,Phase.Canceled,Vector3.zero,null,Vector3.zero);
    }
    void TrackHead()
    {
        var head = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
        if (head.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 p)) view.transform.localPosition = p;
        if (head.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion q)) view.transform.localRotation = q;
    }
    bool ReadJoints(int i)
    {
        return PXR_HandTracking.GetJointLocations(i==0 ? HandType.HandLeft : HandType.HandRight,ref joints[i])
            && joints[i].jointLocations!=null && joints[i].jointCount>(int)HandJoint.JointIndexTip
            && joints[i].jointLocations.Length>(int)HandJoint.JointIndexTip;
    }
    void BeforeRender()
    {
        TrackHead();
        if (replay || !Application.isFocused || !interaction.Ready || interaction.TwoHandActive) return;
        for (int i=0;i<2;i++)
        {
            if (!held[i] || held[1-i] || !usingHand[i] || !interaction.WaitingForPartRelease(101+i) || !ReadJoints(i)) continue;
            // Catch a brief opening between Updates. Only enqueue the edge here;
            // gameplay, gaze and physics still run once in the regular Update.
            var thumb=joints[i].jointLocations[(int)HandJoint.JointThumbTip];
            var index=joints[i].jointLocations[(int)HandJoint.JointIndexTip];
            if (!TipGeometry(thumb,index,out var centre,out float gap)) continue;
            var hand=transform.TransformPoint(centre);
            if (!ReadFingerPinch(i,gap,interaction.WithinPartTapTravel(101+i,hand)))
            {
                Send(i,Phase.Ended,hand,null,Vector3.zero);
                held[i]=false;
            }
        }
    }
    static bool TipGeometry(HandJointLocation thumb, HandJointLocation index, out Vector3 centre, out float gap)
    {
        centre=default; gap=-1;
        if ((thumb.locationStatus & index.locationStatus & HandLocationStatus.PositionValid)==0) return false;
        var t=thumb.pose.Position.ToVector3(); var f=index.pose.Position.ToVector3();
        centre=(t+f)*.5f; gap=Vector3.Distance(t,f);
        return true;
    }
    public struct PartPinchMotion
    {
        const float PartOpenTravel = .0045f, PartCloseTravel = .0035f;
        float closest, reopenedTo;
        bool measured, compactRelease;
        public float OpeningTravel => reopenedTo-closest;
        public bool Read(float gap,bool down,bool allowCompactRelease,bool twoHands)
        {
            if (twoHands) { this=default; return gap<(down ? .035f : .022f); }
            if (down)
            {
                closest=measured ? Mathf.Min(closest,gap) : gap;
                measured=true;
                // Fingertip joint centers vary with the hand and pinch pose.
                // Measure the lift from this pinch's closest sample.
                float release=allowCompactRelease ? Mathf.Min(closest+PartOpenTravel,.028f) : .028f;
                if (gap<release) return true;
                measured=false;
                reopenedTo=gap;
                compactRelease=gap<.028f;
                return false;
            }
            // After a small opening, a stationary hand must not press again just
            // because both samples are below the ordinary pinch-down threshold.
            if (compactRelease)
            {
                reopenedTo=Mathf.Max(reopenedTo,gap);
                if (gap>=.028f) compactRelease=false;
                else if (reopenedTo-gap<PartCloseTravel) return false;
            }
            if (gap>=.022f) return false;
            closest=gap; measured=true; compactRelease=false;
            return true;
        }
    }
    bool ReadFingerPinch(int i,float gap,bool compact)
    {
        bool pressed=partPinches[i].Read(gap,held[i],compact,interaction.TwoHandActive);
        if (compact && !pressed)
            Debug.LogFormat("XR_PART_RELEASE hand={0} gapMm={1:F1} openingMm={2:F1}",101+i,gap*1000f,partPinches[i].OpeningTravel*1000f);
        return pressed;
    }
    void Update()
    {
        TrackHead();
        if (replay || !interaction.Ready || !Application.isFocused) return;
        // The shared-space scene disables the physics tick. XR ray and touch queries
        // need collider poses from the current workbench transform on each frame.
        Physics.SyncTransforms();
        focus.UpdateFocus(view);
        bool fit=Time.unscaledTime>=nextHandFit;
        if (fit) nextHandFit=Time.unscaledTime+1;
        for (int i=0; i<2; i++)
        {
            var device = InputDevices.GetDeviceAtXRNode(i==0 ? XRNode.LeftHand : XRNode.RightHand);
            bool pressed;
            Vector3 p;
            Quaternion q;
            bool jointPose=ReadJoints(i);
            bool hands=TryReadHand(i,jointPose,out p,out q,out pressed,out float gap);
            if (hands) lastHandPose[i]=Time.unscaledTime;
            // A brief occlusion of one hand must not release a two-hand grab.
            if (!hands && usingHand[i] && held[i] && Time.unscaledTime-lastHandPose[i]<.10f) continue;
            if (usingHand[i]!=hands) { CancelHand(i); touchingButton[i]=false; usingHand[i]=hands; }
            if (gap>=0)
            {
                bool compact=held[i] && !held[1-i] && interaction.WaitingForPartRelease(101+i)
                    && interaction.WithinPartTapTravel(101+i,transform.TransformPoint(p));
                pressed=ReadFingerPinch(i,gap,compact);
            }
            else partPinches[i]=default;
            bool wrist=jointPose && (joints[i].jointLocations[(int)HandJoint.JointWrist].locationStatus & HandLocationStatus.PositionValid)!=0;
            if (handMasks[i].enabled!=wrist)
            {
                handMasks[i].enabled=wrist;
                Debug.Log("XR_PASSTHROUGH_HAND side="+i+" tracked="+wrist);
            }
            if (wrist && fit && trackedHands[i] && trackedHands[i].Basemesh && joints[i].handScale>.65f && joints[i].handScale<1.4f)
                trackedHands[i].Basemesh.localScale=Vector3.one*joints[i].handScale;
            // A near index touch only needs the index tip, not a pointing ray or thumb.
            if (jointPose && !held[i])
            {
                var index=joints[i].jointLocations[(int)HandJoint.JointIndexTip];
                if ((index.locationStatus & HandLocationStatus.PositionValid)!=0)
                {
                    var tip=transform.TransformPoint(index.pose.Position.ToVector3());
                    var button=focus.TouchButton(tip,touchingButton[i] ? .018f : .009f);
                    if (button && (touchingButton[i] || !pressed))
                    {
                        if (!touchingButton[i])
                        {
                            Send(i,Phase.Began,tip,button.gameObject,tip);
                            Send(i,Phase.Ended,tip,button.gameObject,tip);
                            Debug.Log("XR_BUTTON_TOUCH "+button.name);
                        }
                        touchingButton[i]=true;
                        continue;
                    }
                }
            }
            touchingButton[i]=false;
            if (!hands)
            {
                bool positioned = device.TryGetFeatureValue(CommonUsages.devicePosition,out p);
                bool oriented = device.TryGetFeatureValue(CommonUsages.deviceRotation,out q);
                if (!positioned || !oriented || !device.TryGetFeatureValue(CommonUsages.triggerButton,out pressed))
                { CancelHand(i); continue; }
            }
            if (!pressed && !held[i]) continue;
            var hand=transform.TransformPoint(p);
            var dir=transform.rotation*q*Vector3.forward;
            // In V23 the highlighted gaze target owns the whole pinch. A miss must not
            // silently select something else along the hand ray. Controllers still point.
            var h=hands ? focus.Hit : focus.Pick(new Ray(hand,dir));
            if (hands && pressed && !held[i]) Debug.Log(i==0 ? "XR_HAND_PINCH left" : "XR_HAND_PINCH right");
            Send(i,pressed&&!held[i]?Phase.Began:pressed?Phase.Moved:Phase.Ended,hand,
                 focus.GestureTarget(h),h.collider?h.point:hand+dir*.5f);
            held[i]=pressed;
        }
    }
    bool TryReadHand(int i,bool jointPose,out Vector3 position,out Quaternion rotation,out bool pressed,out float gap)
    {
        var side=i==0 ? HandType.HandLeft : HandType.HandRight;
        HandAimState aim=default;
        if (!PXR_HandTracking.GetAimState(side,ref aim)) aim=default;
        var thumb=jointPose ? joints[i].jointLocations[(int)HandJoint.JointThumbTip] : default;
        var index=jointPose ? joints[i].jointLocations[(int)HandJoint.JointIndexTip] : default;
        return ReadPinchPose(aim,thumb,index,view.transform.localPosition,out position,out rotation,out pressed,out gap);
    }
    public static bool ResolvePinch(HandAimState aim,HandJointLocation thumb,HandJointLocation index,
        bool held,Vector3 eye,out Vector3 position,out Quaternion rotation,out bool pressed,bool twoHands=false)
    {
        bool tracked=ReadPinchPose(aim,thumb,index,eye,out position,out rotation,out pressed,out float gap);
        if (gap>=0) pressed=new PartPinchMotion().Read(gap,held,false,twoHands);
        return tracked;
    }
    static bool ReadPinchPose(HandAimState aim,HandJointLocation thumb,HandJointLocation index,
        Vector3 eye,out Vector3 position,out Quaternion rotation,out bool pressed,out float gap)
    {
        bool ray=(aim.aimStatus & HandAimStatus.AimRayValid)!=0;
        bool tips=TipGeometry(thumb,index,out position,out gap);
        if (!tips) position=aim.aimRayPose.Position.ToVector3();
        var direction=position-eye;
        rotation=ray ? aim.aimRayPose.Orientation.ToQuat() :
            direction.sqrMagnitude>.000001f ? Quaternion.LookRotation(direction) : Quaternion.identity;
        pressed=(aim.aimStatus & (HandAimStatus.AimIndexPinching|HandAimStatus.AimRayTouched))!=0;
        return ray || tips;
    }
    void Send(int i,Phase phase,Vector3 hand,GameObject obj,Vector3 hit)
    {
        interaction.QueueXrGesture(new SwanGestureSample { id=101+i,phase=phase,pinch=true,capturedAt=Time.realtimeSinceStartupAsDouble,
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
        float distance=found?Mathf.Clamp(b.size.magnitude*1.25f,1.05f,2.4f):1.3f;
        var eye=target+Vector3.back*distance+Vector3.up*.24f;
        var facing=Vector3.ProjectOnPlane(view.transform.localRotation*Vector3.forward,Vector3.up);
        transform.rotation=facing.sqrMagnitude>.0001f ? Quaternion.Inverse(Quaternion.LookRotation(facing,Vector3.up)) : Quaternion.identity;
        transform.position=eye-transform.rotation*view.transform.localPosition;
        Debug.Log("XR_TRIAL_VIEW target="+target+" eye="+view.transform.position+" bounds="+b);
    }
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(2);
        PXR_Manager.EnableVideoSeeThrough = true;
        Debug.Log("XR_TRIAL_ROOM requested="+PXR_Manager.EnableVideoSeeThrough+" clear="+view.backgroundColor);
        Application.targetFrameRate=90;
        bool artReview=Requested("steam_art_views");
        bool handReview=Requested("xr_two_hand_review");
        if (!valleyReview && !artReview && !handReview && !Requested("xr_performance_review"))
        {
            FrameTrain();
            if (Requested("xr_panel_review")) yield return ReviewPanel();
            yield break;
        }
        int total=journey.modules.Length+journey.finishing.Length, installed=0;
        while (installed<total && journey.InstallCurrent()) { installed++; yield return null; }
        for (int i=0;i<600 && journey.CompletionActive;i++) yield return null;
        if (!journey.Complete) { Debug.LogError("XR_TRIAL_INCOMPLETE "+installed+"/"+total); yield break; }
        if(valleyReview){yield return ReviewValley();yield break;}
        journey.inspectionMode=1; journey.cutaway=true; journey.ApplyVisibility();
        FrameTrain();
        if (handReview)
        {
            CancelHeld();
            replay=true;
            yield return Sample("two-hand-replay",true);
            replay=false;
            yield break;
        }
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
        CancelHeld();
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
    IEnumerator ReviewPanel()
    {
        replay=true;
        CancelHeld();
        var controls=FindObjectsOfType<JourneyControl>();
        foreach (var action in new[]{JourneyAction.Introduction,JourneyAction.Verify,JourneyAction.FinishAll})
        {
            yield return null;
            Physics.SyncTransforms();
            bool matched=false;
            foreach (var control in controls)
            {
                if (!control || !control.gameObject.activeInHierarchy || control.action!=action) continue;
                var box=control.GetComponent<Collider>();
                if (!box || !box.enabled) continue;
                Vector3 point=box.bounds.center;
                var hit=focus.Pick(new Ray(view.transform.position,(point-view.transform.position).normalized));
                var touch=focus.TouchButton(point,.006f);
                var selected=hit.collider ? hit.collider.GetComponentInParent<JourneyControl>() : null;
                Debug.Log("XR_PANEL_PROBE expected="+action+" ray="+(hit.collider?hit.collider.name:"none")+
                    " selected="+(selected?selected.action.ToString():"none")+" touch="+(touch?touch.name:"none"));
                if(!selected || selected.action!=action) continue;
                Send(0,Phase.Began,point,hit.collider.gameObject,hit.point);
                yield return null;
                Send(0,Phase.Ended,point,hit.collider.gameObject,hit.point);
                matched=true;
                break;
            }
            Debug.Log("XR_PANEL_REVIEW "+action+" matched="+matched);
        }
        replay=false;
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
        float minScale=interaction.SceneScale, maxScale=minScale;
        while (Time.realtimeSinceStartupAsDouble-start<12)
        {
            if(move)
            {
                float wave=.025f*Mathf.Sin((float)(last-start)*1.4f);
                for(int i=0;i<2;i++){
                    var h=target+view.transform.right*((i==0?-1:1)*(.10f+wave))+Vector3.forward*wave;
                    Send(i,times.Count==0?Phase.Began:Phase.Moved,h,null,h);
                }
            }
            yield return null;
            double now=Time.realtimeSinceStartupAsDouble;
            times.Add((float)((now-last)*1000));last=now;
            if(interaction.TwoHandActive) activeGrabFrames++;
            if(move) { minScale=Mathf.Min(minScale,interaction.SceneScale); maxScale=Mathf.Max(maxScale,interaction.SceneScale); }
        }
        if(move) for(int i=0;i<2;i++) Send(i,Phase.Ended,target,interaction.workbenchRoot.gameObject,target);
        if(move) Debug.Log("XR_REPLAY_SCALE min="+minScale.ToString("F3")+" max="+maxScale.ToString("F3"));
        times.Sort();
        Debug.LogFormat("XR_TRIAL_RESULT {0} fps={1:F2} frames={2} p95ms={3:F2} grabFrames={4} moved={5:F4} focused={6}",
            label,times.Count/(last-start),times.Count,times[(times.Count-1)*95/100],activeGrabFrames,
            Vector3.Distance(interaction.workbenchRoot.position,startTarget),Application.isFocused);
    }
}
