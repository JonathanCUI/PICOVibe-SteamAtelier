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
public sealed partial class SteamXrTrial : MonoBehaviour
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
    public Material originalSolarHalo,originalLunarHalo;
    public Material[] finishOriginals, finishVariants;
    public Material[] detailOriginals, detailVariants;
    bool detailReview;
    bool valleyFinishReview;
    public Transform frontGuard;
    bool valleyReview,cloudControl,cycleReview,lampReview,surfaceReview,celestialReview;
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
        celestialReview=Requested("xr_celestial_review");
        valleyFinishReview=Requested("xr_valley_finish_review");
        valleyReview=valleyFinishReview || celestialReview || lampReview || cycleReview || Requested("valley_cloud_review");
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
        if(detailReview)return; // Keep both material-review exposures at the authored camera pose.
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
        var blockArrival=journey.GetComponent<SwanBlockArrival>();
        if(blockArrival)
        {
            FrameTrain();
            if(Requested("xr_block_review")){yield return ReviewBlocks(blockArrival);yield break;}
            blockArrival.arrival.manual=false;
            if(Requested("xr_paper_performance_review"))
                yield return BlockWindow("workshop-entry",()=>blockArrival.arrival.Active);
            while(blockArrival.arrival.Active)yield return null;
        }
        bool artReview=Requested("steam_art_views");
        if(Requested("xr_integration_review")){yield return ReviewVisualIntegration();yield break;}
        if(Requested("xr_geometry_review")){yield return ReviewGeometry();yield break;}
        if(Requested("xr_completion_review") || Requested("xr_completion_demo"))
        {yield return ReviewCompletion(Requested("xr_completion_demo"));yield break;}
        if(Requested("xr_paper_motion_demo")){yield return DemoPaperMotion();yield break;}
        if(Requested("xr_sketch_drag_demo")){yield return DemoSketchDrag();yield break;}
        if(Requested("xr_sketch_page_review")){yield return ReviewSketchPage();yield break;}
        if(Requested("xr_paper_performance_review")){yield return ReviewPaperPerformance();yield break;}
        surfaceReview=Requested("xr_surface_review");
        bool exhaustReview=Requested("xr_open_exhaust_review");
        bool guardReview=Requested("xr_guard_review");
        detailReview=Requested("xr_detail_review");
        bool finishReview=guardReview || detailReview || Requested("xr_finish_review");
        if(surfaceReview || exhaustReview || finishReview || celestialReview || valleyFinishReview || Requested("xr_presentation"))
        {
            var hud=FindObjectOfType<SwanFrameRateHud>(true);
            if(hud){hud.enabled=false;hud.VisualTransform.gameObject.SetActive(false);}
        }
        if(exhaustReview){yield return ReviewOpenExhaust();yield break;}
        if(finishReview){yield return ReviewFinish(guardReview);yield break;}
        if(surfaceReview)
        {
            yield return ReviewWorkshopSurfaces();
            yield return ReviewValley();
            yield break;
        }
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
        if(valleyFinishReview){
            var desk=journey.GetComponent<StudioSpatialConsoleV18>();
            var root=desk.rotary.parent.parent;
            var mid=root.InverseTransformPoint((desk.rotary.position+desk.introLever.position)*.5f);
            PlaceView(root,mid+new Vector3(.18f,1.4f,1.25f),mid);
            yield return SurfaceShot("workshop-controls");
        }
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
    IEnumerator DemoSketchDrag()
    {
        var cards=journey.GetComponent<StudioPartCards>();
        var sheet=cards.sketchSheet;
        var hud=FindObjectOfType<SwanFrameRateHud>(true);
        if(hud){hud.enabled=false;hud.VisualTransform.gameObject.SetActive(false);}
        CancelHeld();replay=true;cards.Select(0);
        yield return new WaitForSecondsRealtime(2);
        Debug.Log("XR_DRAG_READY");
        yield return new WaitForSecondsRealtime(4);
        PlaceView(sheet.transform,new Vector3(.50f,.04f,-2.45f),Vector3.zero);
        yield return new WaitForSecondsRealtime(2);
        Vector3 point=sheet.transform.TransformPoint(new Vector3(0,(SwanSketchSheet.SupportHeight-.5f)*1.5f,0));
        var hit=focus.Pick(new Ray(view.transform.position,(point-view.transform.position).normalized));
        if(!hit.collider || hit.collider.gameObject!=sheet.gameObject){
            replay=false;Debug.LogError("XR_DRAG_FAIL pick");yield break;
        }
        float width=sheet.transform.TransformVector(Vector3.right).magnitude;
        Vector3 start=sheet.transform.position,offset=Vector3.zero,peak=Vector3.zero;
        var cue=new GameObject("Demo pinch cursor").AddComponent<LineRenderer>();
        cue.sharedMaterial=cards.spatialLeaderMaterial;
        cue.useWorldSpace=false;cue.loop=true;cue.positionCount=32;
        cue.startWidth=cue.endWidth=width*.007f;
        cue.transform.rotation=view.transform.rotation;
        for(int i=0;i<32;i++){
            float a=i*Mathf.PI/16;
            cue.SetPosition(i,new Vector3(Mathf.Cos(a),Mathf.Sin(a))*width*.027f);
        }
        cue.enabled=false;
        yield return new WaitForSecondsRealtime(1);
        Send(0,Phase.Began,point,hit.collider.gameObject,hit.point);
        cue.transform.position=point-view.transform.forward*(width*.14f);
        cue.enabled=true;Debug.Log("XR_DRAG_BEGIN");
        yield return new WaitForSecondsRealtime(1.3f);
        Vector3 pullDirection=sheet.transform.InverseTransformVector(view.transform.right).normalized;
        float[] stopSamples={0,.1f,.2f,.33f,.5f,.72f,.94f,1.3f,1.8f};
        int stopSample=0;
        for(float t=0;t<18.7f;){
            t=Mathf.Min(18.7f,t+Time.unscaledDeltaTime);
            float x=0,y=0,z=0;
            if(t<4){x=.58f*Mathf.Sin(2*Mathf.PI*t/4);}
            else if(t>=6.2f && t<10.7f){
                float u=(t-6.2f)/4.5f;
                z=.55f*Mathf.Sin(u*2*Mathf.PI);
            }else if(t>=10.7f && t<14.2f){
                y=.36f*Mathf.Sin((t-10.7f)*2*Mathf.PI/3.5f);
            }else if(t>=14.2f){
                float u=(t-14.2f)/4.5f;
                float wave=Mathf.Sin(6*Mathf.PI*u)*Mathf.SmoothStep(0,1,u/.18f);
                x=.48f*wave-.13f*Mathf.SmoothStep(0,1,u);
                y=.16f*wave;
                z=.18f*wave+.29f*Mathf.SmoothStep(0,1,u);
            }
            offset=width*(view.transform.right*x+view.transform.up*y-view.transform.forward*z);
            Send(0,Phase.Moved,point+offset,null,Vector3.zero);
            cue.transform.position=point+offset-view.transform.forward*(width*.14f);
            yield return null;
            var b=sheet.Bend;
            peak=Vector3.Max(peak,new Vector3(Mathf.Abs(b.x),Mathf.Abs(b.y),Mathf.Abs(b.z)));
            if(stopSample<stopSamples.Length && t>=4+stopSamples[stopSample] && t<6.2f){
                Debug.Log($"XR_DRAG_HOLD elapsed={t-4:F2} signedLag={Vector3.Dot(sheet.EdgeBend,pullDirection):F3} travelError={Vector3.Distance(start,sheet.transform.position):F4}");
                stopSample++;
            }
        }
        Send(0,Phase.Ended,point+offset,null,Vector3.zero);
        cue.enabled=false;Debug.Log("XR_DRAG_RELEASE");
        double releasedAt=Time.realtimeSinceStartupAsDouble;
        var releaseDirection=sheet.transform.InverseTransformVector(.48f*view.transform.right+.16f*view.transform.up-.18f*view.transform.forward).normalized;
        foreach(float delay in new[]{0f,.1f,.2f,.35f,.5f,.75f,1f,1.4f,2.7f}){
            while(Time.realtimeSinceStartupAsDouble-releasedAt<delay)yield return null;
            Debug.Log($"XR_DRAG_SETTLE elapsed={Time.realtimeSinceStartupAsDouble-releasedAt:F2} signedLag={Vector3.Dot(sheet.EdgeBend,releaseDirection):F3}");
        }
        float error=Vector3.Distance(start+offset,sheet.transform.position);
        // Pick it up again, put it in a different place, then turn pages there.
        var relocated=sheet.transform.position;
        point=sheet.transform.TransformPoint(new Vector3(0,(SwanSketchSheet.SupportHeight-.5f)*1.5f,0));
        Send(0,Phase.Began,point,sheet.gameObject,point);cue.enabled=true;
        yield return new WaitForSecondsRealtime(.5f);
        Vector3 move=width*(view.transform.right*.82f+view.transform.up*.18f+view.transform.forward*.18f);
        for(float t=0;t<2.5f;){
            t=Mathf.Min(2.5f,t+Time.unscaledDeltaTime);
            var shift=move*Mathf.SmoothStep(0,1,t/2.5f);
            Send(0,Phase.Moved,point+shift,null,Vector3.zero);
            cue.transform.position=point+shift-view.transform.forward*(width*.14f);yield return null;
        }
        Send(0,Phase.Ended,point+move,null,Vector3.zero);Destroy(cue.gameObject);
        yield return new WaitForSecondsRealtime(2.5f);
        var placed=sheet.transform.position;
        int turns=sheet.TurnsCompleted;
        foreach(int page in new[]{1,2,1,0}){
            cards.Select(page);yield return new WaitForSecondsRealtime(2.1f);
        }
        bool retained=Vector3.Distance(placed,sheet.transform.position)<.002f;
        Debug.Log($"XR_DRAG_DONE travelError={error:F4} relocateError={Vector3.Distance(relocated+move,placed):F4} peak={peak:F3} rest={sheet.Bend.magnitude:F4} edgeRest={sheet.EdgeBend.magnitude:F4} retained={retained} turns={sheet.TurnsCompleted-turns}");
        replay=false;
    }

    IEnumerator DemoPaperMotion()
    {
        var cards=journey.GetComponent<StudioPartCards>();var sheet=cards.sketchSheet;
        var hud=FindObjectOfType<SwanFrameRateHud>(true);
        if(hud){hud.enabled=false;hud.VisualTransform.gameObject.SetActive(false);}
        CancelHeld();replay=true;cards.Select(0);
        PlaceView(sheet.transform,new Vector3(.16f,.02f,-2.3f),Vector3.zero);
        yield return new WaitForSecondsRealtime(2);
        Vector3 placed=sheet.transform.position;
        int turns=sheet.TurnsCompleted,sounds=sheet.RustlesPlayed;
        Debug.Log($"XR_MOTION_READY time={Time.realtimeSinceStartupAsDouble:F3}");
        yield return new WaitForSecondsRealtime(2);
        journey.Act(JourneyAction.Introduction);yield return new WaitForSecondsRealtime(.40f);
        bool rolling=sheet.gameObject.activeInHierarchy && sheet.Unrolled>0 && sheet.Unrolled<1 && !sheet.GetComponent<Collider>().enabled;
        Debug.Log($"XR_MOTION_CLOSE rolling={rolling} amount={sheet.Unrolled:F3}");
        yield return new WaitForSecondsRealtime(1.2f);
        bool closed=!sheet.gameObject.activeSelf && !sheet.GetComponent<Collider>().enabled;
        yield return new WaitForSecondsRealtime(.6f);
        journey.Act(JourneyAction.Introduction);yield return new WaitForSecondsRealtime(1.8f);
        foreach(int page in new[]{1,2,1,0}){
            cards.Select(page);yield return new WaitForSecondsRealtime(2.2f);
        }
        PlaceView(sheet.transform,new Vector3(.95f,.08f,-2.35f),Vector3.zero);
        yield return new WaitForSecondsRealtime(1.4f);
        journey.Act(JourneyAction.Introduction);yield return new WaitForSecondsRealtime(1.6f);
        journey.Act(JourneyAction.Introduction);yield return new WaitForSecondsRealtime(1.8f);
        cards.Select(1);yield return new WaitForSecondsRealtime(2.2f);
        // Reverse a close in flight, then request close during a page turn.
        journey.Act(JourneyAction.Introduction);yield return new WaitForSecondsRealtime(.3f);
        journey.Act(JourneyAction.Introduction);yield return new WaitForSecondsRealtime(1.4f);
        cards.Select(0);yield return new WaitForSecondsRealtime(.3f);
        journey.Act(JourneyAction.Introduction);yield return new WaitForSecondsRealtime(2.6f);
        closed&=!sheet.gameObject.activeSelf;
        journey.Act(JourneyAction.Introduction);yield return new WaitForSecondsRealtime(1.7f);
        bool retained=Vector3.Distance(placed,sheet.transform.position)<.001f;
        bool ready=sheet.Unrolled==1 && !sheet.IsTurning && sheet.GetComponent<Collider>().enabled;
        int turned=sheet.TurnsCompleted-turns,played=sheet.RustlesPlayed-sounds;
        Debug.Log($"XR_MOTION_CHECK closed={closed} ready={ready} retained={retained} turns={turned} sounds={played} triangles={sheet.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3}");
        if(!rolling || !closed || !ready || !retained || turned!=6 || played!=turned)Debug.LogError("XR_MOTION_FAIL state or sound");
        replay=false;Debug.Log("XR_MOTION_DONE");
    }

    IEnumerator ReviewSketchPage()
    {
        var cards=journey.GetComponent<StudioPartCards>();var sheet=cards.sketchSheet;
        var hud=FindObjectOfType<SwanFrameRateHud>(true);
        if(hud){hud.enabled=false;hud.VisualTransform.gameObject.SetActive(false);}
        replay=true;CancelHeld();cards.Select(0);
        yield return new WaitForSecondsRealtime(4);
        Debug.Log("XR_PAPER_SHOT workshop");yield return new WaitForSecondsRealtime(5);
        var start=sheet.transform.position;
        float width=sheet.transform.TransformVector(Vector3.right).magnitude;
        PlaceView(sheet.transform,new Vector3(.08f,.025f,-2.35f),Vector3.zero);
        yield return new WaitForSecondsRealtime(1);
        var point=sheet.transform.TransformPoint(new Vector3(.26f,-.12f,0));
        var hit=focus.Pick(new Ray(view.transform.position,(point-view.transform.position).normalized));
        if(!hit.collider || hit.collider.gameObject!=sheet.gameObject){Debug.LogError("XR_PAPER_FAIL pick");yield break;}
        Send(0,Phase.Began,point,hit.collider.gameObject,hit.point);
        yield return new WaitForSecondsRealtime(1);
        bool flat=sheet.Bend.sqrMagnitude<1e-6f && Vector3.Distance(start,sheet.transform.position)<.001f;
        // First pull sideways and towards the viewer, then reverse the sideways motion.
        var side=sheet.transform.right*width;var towards=-sheet.transform.forward*width;
        float positive=0,negative=0,depth=0;
        Vector3 travel=Vector3.zero;
        for(int direction=0;direction<2;direction++)
        {
            var from=travel;var to=(direction==0?.75f:-.28f)*side+(direction==0?.35f:.70f)*towards;
            float elapsed=0;
            do{
                elapsed+=Mathf.Min(Time.unscaledDeltaTime,.1f);
                travel=Vector3.Lerp(from,to,elapsed/1.1f);
                Send(0,Phase.Moved,point+travel,null,Vector3.zero);
                yield return null;
                positive=Mathf.Max(positive,sheet.Bend.x);negative=Mathf.Min(negative,sheet.Bend.x);
                depth=Mathf.Max(depth,Mathf.Abs(sheet.Bend.z));
            }while(elapsed<1.1f);
        }
        Send(0,Phase.Ended,point+travel,null,Vector3.zero);
        yield return new WaitForSecondsRealtime(2);
        float error=Vector3.Distance(start+travel,sheet.transform.position);
        bool settled=sheet.Bend.sqrMagnitude<1e-6f;
        Debug.Log("XR_PAPER_SHOT reading");yield return new WaitForSecondsRealtime(5);
        bool pages=true;
        var block=new MaterialPropertyBlock();
        var surface=sheet.GetComponent<MeshRenderer>();
        int inkId=Shader.PropertyToID("_MainTex");
        for(int index=1;index<=16;index++){
            cards.Step(1);
            double timeout=Time.realtimeSinceStartupAsDouble+5;
            while((sheet.DisplayedPage!=index || sheet.IsTurning) && Time.realtimeSinceStartupAsDouble<timeout)yield return null;
            yield return new WaitForSecondsRealtime(.5f);
            surface.GetPropertyBlock(block);
            var ink=block.GetTexture(inkId);
            var ray=new Ray(view.transform.position,(sheet.transform.position-view.transform.position).normalized);
            bool correct=ink && ink.name=="Step"+(index+1).ToString("D2") && sheet.DisplayedPage==index
                && cards.SelectedIndex==index && sheet.gameObject.activeInHierarchy && !cards.PreparedSpatialCanvas.gameObject.activeInHierarchy
                && focus.Pick(ray).collider==sheet.GetComponent<Collider>() && Vector3.Distance(sheet.transform.position,start+travel)<.002f;
            pages&=correct;
            Debug.Log($"XR_FOLIO_PAGE {index+1:D2} correct={correct} art={(ink?ink.name:"missing")}");
            Debug.Log($"XR_PAPER_SHOT page-{index+1:D2}");yield return new WaitForSecondsRealtime(2.8f);
        }
        cards.Step(1);yield return new WaitForSecondsRealtime(1.2f);
        pages&=cards.SelectedIndex==0 && sheet.DisplayedPage==0;
        // Send navigation requests on consecutive frames, including a wrap back to page one.
        cards.Step(-1);yield return null;cards.Step(-1);yield return null;cards.Select(0);yield return null;
        yield return new WaitForSecondsRealtime(2.7f);
        pages&=sheet.DisplayedPage==0 && cards.SelectedIndex==0;
        bool restored=sheet.gameObject.activeInHierarchy && !cards.PreparedSpatialCanvas.gameObject.activeInHierarchy && !sheet.originalFrame.activeInHierarchy;
        cards.Toggle();yield return new WaitForSecondsRealtime(1.6f);bool hidden=!sheet.gameObject.activeInHierarchy;
        cards.Toggle();yield return new WaitForSecondsRealtime(1.6f);
        restored&=sheet.gameObject.activeInHierarchy && Vector3.Distance(sheet.transform.position,start+travel)<.002f;
        var oldSuppress=journey.suppressPersistence;journey.suppressPersistence=true;
        bool automatic=journey.InstallCurrent();
        double autoTimeout=Time.realtimeSinceStartupAsDouble+5;
        while((sheet.DisplayedPage!=1 || sheet.IsTurning) && Time.realtimeSinceStartupAsDouble<autoTimeout)yield return null;
        automatic&=cards.SelectedIndex==1 && sheet.DisplayedPage==1;
        journey.ResetJourney();journey.suppressPersistence=oldSuppress;
        yield return new WaitForSecondsRealtime(1.2f);
        Debug.Log($"XR_PAPER_CHECK flat={flat} bendPos={positive:F3} bendNeg={negative:F3} depth={depth:F3} placedError={error:F4} settled={settled} pages={pages} auto={automatic} restored={restored} toggle={hidden}");
        if(!flat || positive<=.005f || negative>=-.005f || depth<=.005f || error>.002f || !settled || !pages || !automatic || !restored || !hidden)
            Debug.LogError("XR_PAPER_FAIL interaction");
        replay=false;Debug.Log("XR_PAPER_DONE");
    }
    IEnumerator ReviewPaperPerformance()
    {
        CancelHeld();replay=true;
        var cards=journey.GetComponent<StudioPartCards>();var sheet=cards.sketchSheet;
        yield return new WaitForSecondsRealtime(3);
        yield return Sample("assembly-start",false);
        var overviewPosition=transform.position;var overviewRotation=transform.rotation;
        PlaceView(sheet.transform,new Vector3(.35f,0,-2.45f),Vector3.zero);
        yield return new WaitForSecondsRealtime(2);
        yield return Sample("paper-reading",false);
        Vector3 original=sheet.transform.position;
        float width=sheet.transform.TransformVector(Vector3.right).magnitude;
        Vector3 point=sheet.transform.TransformPoint(new Vector3(0,-.15f,0));
        Send(0,Phase.Began,point,sheet.gameObject,point);yield return null;
        int selected=-1,turnCount=sheet.TurnsCompleted;
        yield return Sample("paper-drag-and-turn",false,t=>{
            Send(0,Phase.Moved,point+width*(view.transform.right*(.42f*Mathf.Sin(t*2.2f))
                +view.transform.forward*(.25f*Mathf.Sin(t*1.6f))),null,Vector3.zero);
            int index=Mathf.FloorToInt(t/2.4f)%5;
            if(index!=selected){cards.Select(index);selected=index;}
        });
        Send(0,Phase.Ended,point,null,Vector3.zero);yield return new WaitForSecondsRealtime(2);
        sheet.transform.position=original;
        Debug.Log($"XR_AUDIT_PAPER turns={sheet.TurnsCompleted-turnCount} triangles={sheet.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3} renderers={sheet.GetComponents<Renderer>().Length}");
        transform.SetPositionAndRotation(overviewPosition,overviewRotation);
        cards.Select(0);
        for(int i=0;i<7;i++){if(!journey.InstallCurrent()){Debug.LogError("XR_AUDIT_FAIL partial install");yield break;}yield return null;}
        yield return new WaitForSecondsRealtime(3);
        yield return Sample("assembly-seven",false);
        while(!journey.Complete && journey.InstallCurrent())yield return null;
        while(journey.CompletionActive)yield return null;
        if(!journey.Complete){Debug.LogError("XR_AUDIT_FAIL incomplete");yield break;}
        journey.inspectionMode=0;journey.cutaway=false;journey.ApplyVisibility();FrameTrain();
        yield return new WaitForSecondsRealtime(3);yield return Sample("assembly-complete",false);
        journey.inspectionMode=1;journey.cutaway=true;journey.ApplyVisibility();
        yield return new WaitForSecondsRealtime(2);yield return Sample("xray",false);
        journey.inspectionMode=2;journey.ApplyVisibility();journey.Act(JourneyAction.Verify);
        double timeout=Time.realtimeSinceStartupAsDouble+75;
        while(journey.WheelPower<.05f && Time.realtimeSinceStartupAsDouble<timeout)yield return null;
        yield return Sample("cutaway-hot",false);
        journey.Act(JourneyAction.Drive);
        yield return BlockWindow("valley-transition",()=>!journey.inSandbox || journey.SwanSceneLoading || journey.SwanTransitionActive);
        if(!journey.inSandbox || journey.SwanSceneLoading){Debug.LogError("XR_AUDIT_FAIL valley load");yield break;}
        var sky=journey.GetComponent<MesaAtmosV16>().cloudTransforms[0].parent;
        PlaceView(sky,new Vector3(1,20.5f,24),new Vector3(0,7,0));
        var cycle=journey.GetComponent<MesaDayNightV13>();cycle.automatic=false;cycle.SetTime(.46f);
        journey.SwanSetControl(JourneyAction.Throttle,0);journey.SwanSetControl(JourneyAction.Brake,1);
        yield return new WaitForSecondsRealtime(4);yield return Sample("valley-day-parked",false);
        journey.SwanSetControl(JourneyAction.Brake,0);journey.SwanSetControl(JourneyAction.Throttle,.75f);
        yield return new WaitForSecondsRealtime(8);float distance=journey.routeDistance;
        yield return Sample("valley-day-moving",false);
        Debug.Log($"XR_AUDIT_DRIVE speed={journey.speed:F3} travelled={journey.routeDistance-distance:F3}");
        cycle.SetTime(.93f);yield return new WaitForSecondsRealtime(6);yield return Sample("valley-night-moving",false);
        cycle.automatic=true;journey.SetMode(false);
        yield return BlockWindow("workshop-return",()=>journey.SwanTransitionActive);
        FrameTrain();yield return new WaitForSecondsRealtime(3);yield return Sample("assembly-return",false);
        replay=false;Debug.Log("XR_AUDIT_DONE");
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
    IEnumerator ReviewBlocks(SwanBlockArrival blocks)
    {
        var hud=FindObjectOfType<SwanFrameRateHud>(true);
        if(hud){hud.enabled=false;hud.VisualTransform.gameObject.SetActive(false);}
        replay=true;detailReview=true;
        var root=journey.furnaceParticles.transform.parent;
        float side=root.InverseTransformPoint(view.transform.position).z>=0?1:-1;
        PlaceView(root,new Vector3(2.6f,7f,12.4f*side),new Vector3(0,.8f,.8f*side));
        var arrival=blocks.arrival;
        if(!arrival.Active || blocks.ExhibitGroups<2){Debug.LogError("XR_BLOCK_FAIL startup");yield break;}
        arrival.manual=false;
        yield return BlockWindow("workshop-scan",()=>arrival.Active);
        yield return Sample("workshop-settled",false);
        yield return BlockProof(arrival,"workshop");
        if(!journey.InstallCurrent() || arrival.Active || blocks.ExhibitGroups!=0){Debug.LogError("XR_BLOCK_FAIL placement");yield break;}
        while(!journey.Complete && journey.InstallCurrent())yield return null;
        while(journey.CompletionActive)yield return null;
        journey.Act(JourneyAction.Drive);
        float until=Time.realtimeSinceStartup+100;
        while(!journey.inSandbox && Time.realtimeSinceStartup<until)yield return null;
        if(!journey.inSandbox || !arrival.Active){Debug.LogError("XR_BLOCK_FAIL valley entry");yield break;}
        var sky=journey.GetComponent<MesaAtmosV16>().cloudTransforms[0].parent;
        PlaceView(sky,new Vector3(1,20.5f,24),new Vector3(0,7,0));
        yield return BlockWindow("valley-scan",()=>arrival.Active);
        yield return Sample("valley-settled",false);
        yield return BlockProof(arrival,"valley");
        journey.SetMode(false);yield return null;
        PlaceView(root,new Vector3(2.6f,6.7f,10.3f*side),new Vector3(0,1.25f,0));
        if(!arrival.Active){Debug.LogError("XR_BLOCK_FAIL workshop return");yield break;}
        yield return BlockWindow("workshop-return",()=>arrival.Active);
        yield return Sample("full-train-settled",false);
        yield return BlockProof(arrival,"full-train");
        PlaceView(root,new Vector3(1.6f,5.2f,7.1f*side),new Vector3(0,2f,0));
        yield return SurfaceShot("tank-A");
        replay=detailReview=false;FrameTrain();Debug.Log("XR_BLOCK_DONE");
    }
    IEnumerator BlockWindow(string label,System.Func<bool> active)
    {
        var frames=new List<float>(160);double start=Time.realtimeSinceStartupAsDouble,last=start;
        while(active() && last-start<60)
        {
            yield return null;double now=Time.realtimeSinceStartupAsDouble;
            frames.Add((float)((now-last)*1000));last=now;
        }
        if(active() || frames.Count==0){Debug.LogError("XR_BLOCK_FAIL window "+label);yield break;}
        frames.Sort();Debug.Log($"XR_BLOCK_RESULT {label} fps={frames.Count/(last-start):F2} frames={frames.Count} seconds={last-start:F2} p95ms={frames[(frames.Count-1)*95/100]:F2}");
    }
    IEnumerator BlockProof(JourneySceneArrival arrival,string label)
    {
        arrival.manual=true;arrival.Request();yield return null;yield return null;
        arrival.SetProgress(.50f);yield return SurfaceShot(label+"-reveal");
        arrival.SetProgress(1);arrival.Cancel();arrival.manual=false;
        yield return SurfaceShot(label+"-whole");
    }
    IEnumerator ReviewFinish(bool guardReview)
    {
        if(detailReview){CancelHeld();replay=true;}
        while (!journey.Complete && journey.InstallCurrent()) yield return null;
        if (!journey.Complete) { Debug.LogError("XR_FINISH_FAIL installation"); yield break; }
        while (journey.CompletionActive) yield return null;
        journey.cutaway=false; journey.inspectionMode=0; journey.ApplyVisibility();
        var loco=guardReview ? frontGuard : journey.engine.locomotive;
        var variants=detailReview ? detailVariants : finishVariants;
        var originals=detailReview ? detailOriginals : finishOriginals;
        var targets=new Dictionary<Renderer,Material[]>();
        foreach (var r in loco.GetComponentsInChildren<MeshRenderer>(false))
        {
            var slots=r.sharedMaterials;
            if (r.enabled && System.Array.Exists(slots,m=>System.Array.IndexOf(variants,m)>=0))
                targets.Add(r,slots);
        }
        if(targets.Count==0){Debug.LogError("XR_FINISH_FAIL no visible receivers");yield break;}
        Debug.Log("XR_FINISH_VISIBLE "+targets.Count);
        var root=journey.furnaceParticles.transform.parent;
        float side=root.InverseTransformPoint(view.transform.position).z>=0 ? 1 : -1;
        for(int angle=0;angle<2;angle++)
        {
            var eye=angle==0 ? new Vector3(1.6f,5.2f,7.1f*side) : new Vector3(4.3f,4.5f,7.3f*side);
            var aim=new Vector3(0,2.0f,0);
            if(guardReview)
            {
                aim=new Vector3(3.45f,1.36f,0);
                eye=aim+(angle==0 ? new Vector3(3.9f,2.7f,3.1f*side) : new Vector3(3.7f,1.3f,1.8f*side));
            }
            else if(detailReview && angle==1)
            {
                aim=new Vector3(2.8f,2.1f,0);
                eye=aim+new Vector3(4.8f,1.5f,2.8f*side);
            }
            PlaceView(root,eye,aim);
            foreach(bool after in new[]{false,true})
            {
                foreach(var pair in targets)
                {
                    var slots=(Material[])pair.Value.Clone();
                    if(!after) for(int i=0;i<slots.Length;i++)
                    {
                        int index=System.Array.IndexOf(variants,slots[i]);
                        if(index>=0)slots[i]=originals[index];
                    }
                    pair.Key.sharedMaterials=slots;
                }
                string label=(angle==0?"side":"turn")+(after?"-after":"-before");
                if(angle==0 && !after) yield return new WaitForSecondsRealtime(14);
                if(detailReview) yield return new WaitForSecondsRealtime(4);
                yield return SurfaceShot(label);
                if(angle==0 || detailReview)yield return Sample(label,false);
            }
        }
        if(detailReview)
        {
            yield return ReviewValley();
            detailReview=false;replay=false;
        }
        else Debug.Log("XR_FINISH_DONE");
    }
    IEnumerator ReviewWorkshopSurfaces()
    {
        for(int i=0;i<9;i++)
        {
            if(!journey.InstallCurrent())throw new System.InvalidOperationException("Missing cab assembly step");
            yield return null;
        }
        FrameTrain();yield return SurfaceShot("assembly");
        while(!journey.Complete && journey.InstallCurrent())yield return null;
        while(journey.CompletionActive)yield return null;
        journey.cutaway=true;journey.inspectionMode=1;journey.ApplyVisibility();
        journey.Act(JourneyAction.Verify);
        yield return new WaitForSecondsRealtime(4);
        var loco=journey.furnaceParticles.transform.parent;
        float side=loco.InverseTransformPoint(view.transform.position).z>=0?1:-1;
        PlaceView(loco,new Vector3(-.65f,3.5f,4.5f*side),new Vector3(-.3f,2.35f,0));
        yield return SurfaceShot("boiler");yield return Sample("art-inspection",false);
    }
    IEnumerator ReviewOpenExhaust()
    {
        var loco=journey.furnaceParticles.transform.parent;
        var stack=journey.bakedChimney.rollingPlume;
        var layers=journey.furnaceParticles.GetComponentsInChildren<ParticleSystem>(true);
        for(int step=0;step<3;step++)
        {
            if(!journey.InstallCurrent()){Debug.LogError("XR_EXHAUST_FAIL install");yield break;}
            if(!journey.running)journey.Act(JourneyAction.Verify);
            float side=loco.InverseTransformPoint(view.transform.position).z>=0?1:-1;
            PlaceView(loco,new Vector3(2.6f,5.8f,6.4f*side),new Vector3(.45f,2.7f,0));
            int smokeMax=0,fireMax=0,vapourMax=0,stackMax=0;
            float until=Time.realtimeSinceStartup+85,settled=0;
            while(Time.realtimeSinceStartup<until && settled<2)
            {
                int flames=0;foreach(var layer in layers)flames+=layer.particleCount;
                smokeMax=Mathf.Max(smokeMax,journey.smoke.particleCount);
                fireMax=Mathf.Max(fireMax,flames);
                vapourMax=Mathf.Max(vapourMax,journey.steam.particleCount);
                stackMax=Mathf.Max(stackMax,stack.particleCount);
                // Wait until combustion has reached every open tube. A cold sample would pass spuriously.
                bool gasArrived=true;int paths=0;
                foreach(var field in journey.fluidFields)
                    if(field && field.kind==JourneyFluidKind.Smoke && field.stage==step){paths++;gasArrived &= field.Arrived;}
                bool hot=journey.GasLevel>.85f && gasArrived && paths>0;
                if(step>=1)hot &= journey.WaterHeat>.82f && vapourMax>0;
                if(step==2)hot &= stackMax>0;
                settled=hot ? settled+Time.unscaledDeltaTime : 0;
                yield return null;
            }
            Debug.Log($"XR_EXHAUST_STATE step={step+1} legacyMax={smokeMax} fireMax={fireMax} vapourMax={vapourMax} stackMax={stackMax} hotWindow={settled:F2}");
            if(settled<2 || smokeMax!=0 || fireMax==0 || (step==0 && vapourMax!=0) || (step<2 && stackMax!=0))
            {Debug.LogError("XR_EXHAUST_FAIL effects");yield break;}
            yield return SurfaceShot("outlet-"+(step+1));
        }
        for(int i=3;i<9;i++)journey.InstallCurrent();
        FrameTrain();yield return SurfaceShot("cab-outline");
        Debug.Log("XR_EXHAUST_DONE");
    }
    IEnumerator SurfaceShot(string label)
    {
        yield return new WaitForSecondsRealtime(valleyFinishReview?4f:1.5f);
        Debug.Log("XR_SURFACE_SHOT "+label);
        yield return new WaitForSecondsRealtime(valleyFinishReview?7f:3f);
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
        if(valleyFinishReview){yield return ReviewValleyFinish(sky);yield break;}
        if(celestialReview){yield return ReviewCelestials(sky);yield break;}
        if(detailReview)
        {
            var cycle=journey.GetComponent<MesaDayNightV13>();
            cycle.automatic=false;cycle.SetTime(.46f);
            yield return SurfaceShot("canyon-day");
            float start=journey.routeDistance;
            journey.SwanSetControl(JourneyAction.Brake,0);journey.SwanSetControl(JourneyAction.Throttle,.75f);
            yield return new WaitForSecondsRealtime(6);
            yield return SurfaceShot("canyon-driving");
            yield return Sample("detail-valley-moving",false);
            Debug.Log($"XR_DETAIL_DRIVE speed={journey.speed} distance={journey.routeDistance-start}");
            cycle.automatic=true;
            if(Mathf.Abs(journey.routeDistance-start)<.01f)Debug.LogError("XR_FINISH_FAIL valley stationary");
            else Debug.Log("XR_FINISH_DONE");
            yield break;
        }
        if(surfaceReview)
        {
            var daylight=journey.GetComponent<MesaDayNightV13>();daylight.automatic=false;
            daylight.SetTime(.46f);yield return SurfaceShot("day");
            journey.SwanSetControl(JourneyAction.Brake,0);journey.SwanSetControl(JourneyAction.Throttle,.75f);
            yield return new WaitForSecondsRealtime(8);yield return Sample("art-valley-day",false);
            daylight.SetTime(.93f);
            var smoke=journey.GetComponent<MesaAtmosV16>();
            float end=Time.realtimeSinceStartup+35;
            while((smoke.TrailReach()<5 || sky.InverseTransformPoint(journey.valleyHeadlamps.transform.position).z<2) && Time.realtimeSinceStartup<end)yield return null;
            yield return SurfaceShot("night");yield return Sample("art-valley-night",false);
            daylight.automatic=true;Debug.Log("XR_SURFACE_DONE");yield break;
        }
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
        if(System.Array.Exists(panel,t=>t.action==JourneyAction.Lights && t.useActionOverride) ||
            System.Array.Exists(journey.GetComponentsInParent<SwanJourneyPresentation>(true),p=>p.moveHandle)){
            Debug.LogError("XR_LAMP_FAIL retired control");yield break;}
        daylight.automatic=false;daylight.SetTime(.46f);
        while(lights.Lit)yield return null;
        var left=lights.lenses[0].transform.parent;var right=lights.lenses[1].transform.parent;
        var centre=(left.position+right.position)*.5f;float scale=Mathf.Abs(left.lossyScale.z);
        var cameraPoint=centre+(left.forward*10f+left.right*2.2f+left.up*1.8f)*scale;
        PlaceView(skyRoot,skyRoot.InverseTransformPoint(cameraPoint),skyRoot.InverseTransformPoint(centre+left.forward*.8f*scale));
        yield return new WaitForSecondsRealtime(1);Debug.Log("XR_LAMP_SHOT lamps-off visible="+lights.Lit);
        yield return new WaitForSecondsRealtime(3);
        yield return CheckLampFade(daylight,lights,true);
        Debug.Log("XR_LAMP_SHOT lamps-on visible="+lights.Lit+" scale="+scale+" range="+lights.spots[0].range);
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
        yield return CheckLampFade(daylight,lights,false);
        yield return new WaitForSecondsRealtime(1.4f);Debug.Log("XR_LAMP_SHOT track-off");
        yield return new WaitForSecondsRealtime(3);
        yield return CheckLampFade(daylight,lights,true);
        foreach(var beam in lights.beams)beam.enabled=false;
        yield return new WaitForSecondsRealtime(1.4f);
        Debug.Log("XR_LAMP_SHOT track-lit range="+lights.spots[0].range+" intensity="+lights.spots[0].intensity);
        yield return new WaitForSecondsRealtime(3);
        foreach(var beam in lights.beams)beam.enabled=true;
        yield return new WaitForSecondsRealtime(1.4f);Debug.Log("XR_LAMP_SHOT track-beams");
        yield return new WaitForSecondsRealtime(3);
        PlaceView(skyRoot,eye,aim);journey.SwanSetControl(JourneyAction.Brake,0);journey.SwanSetControl(JourneyAction.Throttle,.75f);
        yield return new WaitForSecondsRealtime(8);
        yield return CheckLampFade(daylight,lights,false);float start=journey.routeDistance;
        yield return Sample("valley-lamps-off",false);
        Debug.Log("XR_LAMP_MOTION off lit="+lights.Lit+" speed="+journey.speed+" delta="+(journey.routeDistance-start)+" puffs="+atmosphere.ActivePuffs);
        yield return CheckLampFade(daylight,lights,true);start=journey.routeDistance;
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
    IEnumerator CheckLampFade(MesaDayNightV13 sky,SwanValleyHeadlamps lamps,bool night)
    {
        sky.SetTime(night?.93f:.46f);
        float targetValue=night?1:0,previous=lamps.Brightness;
        int intermediate=0;float until=Time.realtimeSinceStartup+40;
        while(lamps.Brightness!=targetValue && Time.realtimeSinceStartup<until)
        {
            yield return null;
            float value=lamps.Brightness;
            if((night && value<previous) || (!night && value>previous) || Mathf.Abs(value-previous)>.045f){
                Debug.LogError("XR_LAMP_FAIL discontinuous fade");yield break;}
            if(value>0 && value<1 && value!=previous)intermediate++;
            previous=value;
        }
        if(intermediate<2 || lamps.Lit!=night || lamps.Brightness!=targetValue)Debug.LogError("XR_LAMP_FAIL incomplete fade");
        else Debug.Log($"XR_LAMP_FADE night={night} intermediate={intermediate} end={lamps.Brightness}");
    }
    IEnumerator ReviewValleyFinish(Transform root)
    {
        var sky=journey.GetComponent<MesaDayNightV13>();sky.automatic=false;sky.SetTime(.40f);
        var panel=journey.GetComponent<StudioSpatialRefineV19>();
        PlaceView(panel.face,new Vector3(-3.2f,3.8f,4.9f),new Vector3(-.5f,.50f,0));
        yield return SurfaceShot("driving-closed");panel.ToggleReturn();
        yield return SurfaceShot("driving-open");panel.ToggleReturn();
        var p0=sky.clouds[0].localPosition;
        var mid=Vector3.Lerp(new Vector3(0,7,0),root.InverseTransformPoint(sky.sun.position),.47f);
        PlaceView(root,new Vector3(1,23,40),mid);
        yield return SurfaceShot("wind-day-a");
        yield return SurfaceShot("wind-day-b");
        Debug.Log("XR_FINISH_WIND travel="+Vector3.Distance(p0,sky.clouds[0].localPosition));
        var coach=journey.valleyTrain.vehicles[2];
        var panes=System.Array.Find(coach.GetComponentsInChildren<MeshRenderer>(),r=>r.sharedMaterial.shader.name=="SwanJourney/XR Coach Window");
        var center=panes.bounds.center;float size=panes.bounds.extents.magnitude;
        foreach(int side in new[]{1,-1}){
            var cameraAt=center+(coach.forward*side*2.65f+coach.up*.85f)*size;
            PlaceView(root,root.InverseTransformPoint(cameraAt),root.InverseTransformPoint(center));
            sky.SetTime(.40f);yield return SurfaceShot("windows-day-"+side);
            sky.SetTime(.90f);yield return SurfaceShot("windows-night-"+side);
        }
        PlaceView(root,new Vector3(1,23,40),new Vector3(0,7,0));
        journey.SwanSetControl(JourneyAction.Brake,0);journey.SwanSetControl(JourneyAction.Throttle,.75f);
        yield return Sample("valley-finish-night-moving",false);
        journey.SetMode(false);
        yield return new WaitForSecondsRealtime(5);Debug.Log("XR_VALLEY_FINISH_DONE");
        sky.automatic=true;
    }

    IEnumerator ReviewCelestials(Transform root)
    {
        var sky=journey.GetComponent<MesaDayNightV13>();
        var sun=sky.sun.GetComponent<Renderer>();var moon=sky.moon.GetComponent<Renderer>();
        var solar=sky.sunHalo.GetComponent<Renderer>();var lunar=sky.moonHalo.GetComponent<Renderer>();
        var opticalSun=solar.sharedMaterial;var opticalMoon=lunar.sharedMaterial;
        bool automatic=sky.automatic;float entry=sky.timeOfDay;
        sky.automatic=false;
        try
        {
            foreach(var phase in new[]{.90f,.40f,.262f})
            {
                string label=phase<.3f?"dawn":phase<.5f?"day":"night";
                sky.SetTime(phase);
                var subject=phase<.5f?sky.sun:sky.moon;
                var aim=Vector3.Lerp(new Vector3(0,7,0),root.InverseTransformPoint(subject.position),.47f);
                PlaceView(root,new Vector3(1,23,40),aim);
                for(int take=0;take<4;take++)
                {
                    // ABBA keeps the viewpoint and time identical and counterbalances warmup.
                    bool optical=take==1 || take==2;
                    sky.useOpticalCards=optical;
                    sun.forceRenderingOff=moon.forceRenderingOff=optical;
                    solar.sharedMaterial=optical?opticalSun:originalSolarHalo;
                    lunar.sharedMaterial=optical?opticalMoon:originalLunarHalo;
                    sky.SetTime(phase);
                    yield return new WaitForSecondsRealtime(1);
                    string name=label+"-"+(optical?"optics":"original")+"-"+take;
                    var uv=view.WorldToViewportPoint(subject.position);
                    var activeBody=phase<.5f?sun:moon;var activeHalo=phase<.5f?solar:lunar;
                    int surfaces=(activeBody.isVisible && !activeBody.forceRenderingOff?1:0)+(activeHalo.isVisible?1:0);
                    Debug.Log($"XR_CELESTIAL {name} centre={uv:F2} alpha={view.backgroundColor.a} surfaces={surfaces} shader={activeHalo.sharedMaterial.shader.name}");
                    yield return Sample(name,false);
                }
            }
        }
        finally
        {
            solar.sharedMaterial=opticalSun;lunar.sharedMaterial=opticalMoon;
            sun.forceRenderingOff=moon.forceRenderingOff=sky.useOpticalCards=true;
            sky.SetTime(entry);sky.automatic=automatic;
        }
        Debug.Log("XR_CELESTIAL_DONE");
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
    IEnumerator Sample(string label,bool move,System.Action<float> update=null)
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
            update?.Invoke((float)(last-start));
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
