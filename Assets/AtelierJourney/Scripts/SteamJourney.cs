using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

// Independent lesson state. The original free-assembly scene is retained as an editable source.
[DefaultExecutionOrder(2000)]
public sealed partial class SteamJourney : MonoBehaviour {
    public LocomotiveController engine;
    public Camera view;
    public TMP_FontAsset font;
    public JourneyModule[] modules, finishing;
    public GameObject workshopRoot,sandboxRoot,previewRoot,cutRoot,consoleRoot;
    public Renderer[] shellRenderers;
    public Transform trainAnchor, previewStand, aimRing,verifyLever;
    public Transform[] coaches,coachWheels,brakeShoes;
    public GameObject[] supportStands;
    public Transform throttleLever,brakeLever,directionLever,pressureNeedle,speedNeedle;
    public TMP_Text consoleSpeed,consolePressure,consoleStatus;
    public ParticleSystem fire,smoke,steam,sparks;
    public SwanJourney.SwanJourneyChimneySmoke bakedChimney;
    public JourneyFlow[] flows;
    public JourneyDetail[] detailMeshes;
    public GameObject[] lessonModels;
    public Transform waterSurface,grate,brakeRod;
    public Renderer leftChamber,rightChamber;
    public Material liveSteam,spentSteam,emptyChamber;
    public AudioClip igniteSound,chuffSound,whistleSound,clickSound;
    public int stage,finishIndex;
    public bool running,paused,slow,cutaway=true,inSandbox;
    [NonSerialized] public bool suppressPersistence;
    [NonSerialized] public bool verificationInputLock;
    [NonSerialized] public Func<bool> celestialInputCapture;
    public float throttle,brake,speed,pressure,routeDistance;
    public int direction=1,laps;
    public bool Complete => finishing.All(m=>m.installed) && modules.All(m=>m.installed);
    public bool Teaching => stage<modules.Length;
    public JourneyModule Current => Teaching?modules[stage]:finishing[Mathf.Clamp(finishIndex,0,finishing.Length-1)];
    public TMP_Text trayTitle,trayState,verifyLabel;
    public Renderer[] sectionOnly; public GameObject trayDrive; TMP_Text processStatus;float leftMist,rightMist,leftVent,rightVent;
    public GameObject trayFinish; public TMP_Text trayCounter;
    public Renderer verifyLens; public GameObject[] trayNavigation;
    public int ActiveStage => Mathf.Max(0,modules.Count(m=>m.installed)-1);
    public float MotionTime {get;private set;}
    public float FlowTime {get;private set;}
    TMP_Text heading,body,caption,stateLabel,progress,speedLabel,noticeText,footer;
    UnityEngine.UI.Button assembleButton,verifyButton,nextButton,driveButton,slowButton,cutButton;
    GameObject lessonPanel,drivePanel,partNavigation;
    readonly List<UnityEngine.UI.Image> stepDots=new List<UnityEngine.UI.Image>();
    JourneyPath path;AudioSource audioSource;float chuffAt,noticeUntil,demoTime,autoSaveAt;
    Transform held;JourneyControl heldControl;Vector3 previewHome;Vector2 previousPointer,downPointer;float dragDepth,controlValue; Transform rotatingPart; JourneyModule heldModule;
    Vector3 focus=new Vector3(1.5f,1.2f,0);float yaw=25,pitch=30,distance=14.8f;
    int cameraMode;bool lamps=true;Quaternion throttleRest,brakeRest,directionRest;Vector3[] shoeHome;
    bool swanDrivingDisplayHidden,swanDrivingTextReady;int drivingSpeedTenths,drivingPressureTenths;bool drivingTextMoving;
    Camera instrumentCamera;RenderTexture instrumentTexture;float instrumentAt;
    const string SaveKey="SteamJourney.Progress.v1";
    [Serializable] class Saved {public int stage,finishIndex;public bool[] installed,verified,finished;}
    void Start(){
        path=new JourneyPath(); engine.enabled=false;engine.freeAssembly=null;engine.workshop=null;engine.cylinderSteam=null;
        engine.principle=false;engine.exploded=false;engine.assemblyLesson=false;engine.travel=false;engine.travelled=0;engine.adaptiveDetail=true;
        engine.inputEnabled=false;engine.externalView=true;engine.running=false;
        if(engine.principleParts)engine.principleParts.SetActive(false);
        audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.volume=.42f;
        if(throttleLever)throttleRest=throttleLever.localRotation;if(brakeLever)brakeRest=brakeLever.localRotation;if(directionLever)directionRest=directionLever.localRotation;
        shoeHome=brakeShoes.Select(t=>t.localPosition).ToArray();
        if(!SwanInputActive)LoadProgress();
        BuildUI();
        // Each new Swan application session starts at the first assembly step.
        // Start runs once; headset sleep, focus changes and canyon returns must
        // not clear the assembly that is in progress during this session.
        if(SwanInputActive){ResetJourney();Debug.Log("SWAN_JOURNEY_FRESH_SESSION step=1 installed=0");}
        else{SetMode(false);RefreshPreview();}
        ApplyVisibility();UpdateText();
        Application.targetFrameRate=72;
    }
    void Update() { using (SwanJourney.SwanJourneyCpuTimings.Measure(SwanJourney.SwanJourneyCpuTimings.Stage.Simulation)) CpuMeasuredUpdate(); }
    void CpuMeasuredUpdate(){
        float dt=Mathf.Min(Time.deltaTime,.05f);ReadInput();
        if(inSandbox)Drive(dt);else if((running||ResidualEnergy)&&!paused){
            float step=dt*(slow?.25f:1);demoTime+=step;FlowTime+=step;
            StepProcess(step);
            for(int i=0;i<modules.Length;i++){
                var m=modules[i];
                if(!m.installed)continue;
                if(ProcessReached(i))m.observed+=step;
                if(m.observed>=RequiredObservation(i))m.verified=true;
            }
        }
        if(!inSandbox)UpdateBrakeEffects(dt,0);
        UpdateMechanicalPose();Pneumatics(dt);UpdateRailAudio(dt);RefreshFrameVisibility();Animate(dt);UpdateText();UpdateSocketGhost();ApplyCamera(dt);
        if(Time.unscaledTime>autoSaveAt){autoSaveAt=Time.unscaledTime+10;SaveProgress();}
        if(inSandbox&&instrumentCamera&&Time.unscaledTime>=instrumentAt){instrumentAt=Time.unscaledTime+.1f;instrumentCamera.Render();}
    }
    public static float RequiredObservation(int index)=>index==6?2f:3f;
    public void Act(JourneyAction action){
        if(GetComponent<JourneySceneArrival>() is JourneySceneArrival arrival&&arrival.Active&&action!=JourneyAction.Reset)return;
        Feedback(action);
        if(AutoAssembling&&action!=JourneyAction.Reset&&action!=JourneyAction.Verify&&action!=JourneyAction.Pause)return;
        switch(action){
            case JourneyAction.Assemble:InstallCurrent();break;
            case JourneyAction.FinishAll:FinishRemaining();break;
            case JourneyAction.ReturnPart:Current.parked=false;RefreshPreview();break;
            case JourneyAction.Verify:
                if(inSandbox)break;if(!modules[0].installed){Notify("先从展示台拿起火箱，装入底盘。");break;}
                running=!running;paused=false;guideCamera=false;if(running){demoTime=0;Play(igniteSound);Notify("验证已开启 · 已安装的部件将按热量与蒸汽的传递顺序工作。");}else{Notify("验证已关闭 · 停止加热，蒸汽与机构逐渐平息。");}break;
            case JourneyAction.Next:break; // Installation advances automatically; testing is optional.
            case JourneyAction.Pause:paused=!paused;break;
            case JourneyAction.Slow:slow=!slow;break;
            case JourneyAction.Cutaway:inspectionMode=(inspectionMode+1)%3;cutaway=inspectionMode!=0;break;
            case JourneyAction.Drive:if(Complete)SetMode(true);else Notify("完成所有组件装配后，就能出发。");break;
            case JourneyAction.Workshop:var refined=GetComponent<StudioSpatialRefineV19>();if(!refined||refined.TryReturn())SetMode(false);break;
            case JourneyAction.WorkshopGuard:GetComponent<StudioSpatialRefineV19>()?.ToggleReturn();break;
            case JourneyAction.Throttle:throttle=Mathf.Repeat(throttle+.25f,1.25f);break;
            case JourneyAction.Brake:brake=brake>.5f?0:1;break;
            case JourneyAction.Reverse:if(speed<.03f)direction=direction>0?-1:1;else Notify("停稳后再换向。");break;
            case JourneyAction.Whistle:Play(whistleSound);break;
            case JourneyAction.Lights:lamps=!lamps;break;
            case JourneyAction.View:break;
            case JourneyAction.PreviousPart:SelectFinishing(-1);break;
            case JourneyAction.NextPart:SelectFinishing(1);break;
            case JourneyAction.Introduction:GetComponent<StudioSpatialConsoleV18>()?.ToggleIntroduction();break;
            case JourneyAction.GuidePrevious:GetComponent<StudioSpatialConsoleV18>()?.StepPart(-1);break;
            case JourneyAction.GuideNext:GetComponent<StudioSpatialConsoleV18>()?.StepPart(1);break;
            case JourneyAction.ResetGuard:GetComponent<StudioSpatialConsoleV18>()?.ToggleGuard();break;
            case JourneyAction.Reset:var spatial=GetComponent<StudioSpatialConsoleV18>();if(inSandbox||!spatial||spatial.TryReset())ResetJourney();break;
        }
        UpdateText();
    }
    public bool InstallCurrent(){
        if(inSandbox||AutoAssembling||Current.installed)return false;var installed=Current;installed.installed=true;
        installed.parked=false;PlayAssemblyLatch();ImpactWater(installed);
        if(Teaching){stage=modules.TakeWhile(m=>m.installed).Count();finishIndex=0;}else if(!Complete){for(int n=1;n<=finishing.Length;n++){int k=(finishIndex+n)%finishing.Length;if(!finishing[k].installed){finishIndex=k;break;}}}
        RefreshPreview();ApplyVisibility();SaveProgress();
        Notify(Complete?"机车装配完成 · 按展示台上的「出发」按钮，去山谷铁路跑一圈。":installed.title+" 已装好 · 下一个组件已放上展示台。");
        if(Complete)BeginManualCompletion();
        return true;
    }
    public bool FinishRemaining()=>StartAssemblySequence();
    public void SelectFinishing(int delta){if(Teaching)return;finishIndex=(finishIndex+delta+finishing.Length)%finishing.Length;RefreshPreview();SaveProgress();}
    public void ReviewView(Vector3 target,float range,float angle,float tilt){guideCamera=false;focus=target;distance=range;yaw=angle;pitch=tilt;}
    public void FocusMechanism(){if(!Teaching)return;var points=new[]{new Vector3(-1.3f,1.75f,0),new Vector3(.7f,2.1f,0),new Vector3(2.6f,2.15f,0),new Vector3(.9f,2.4f,0),new Vector3(2.7f,.85f,1),new Vector3(0,1.2f,0),new Vector3(.1f,.6f,.3f)};var distances=new[]{6.2f,9f,7.6f,8.2f,4.9f,11.5f,8.5f};focus=points[stage];distance=distances[stage];yaw=stage==4?12:22;pitch=stage==4?16:22;}
    public void ResetJourney(){CancelAssembly();stage=finishIndex=0;foreach(var m in modules.Concat(finishing)){m.installed=false;m.verified=false;m.observed=0;m.parked=false;}running=paused=false;ResetProcess();reservoirPressure=brakePipePressure=auxiliaryPressure=BrakeCylinderPressure=CompressorRate=0;compressorDemand=true;cutaway=true;inspectionMode=1;SetMode(false);RefreshPreview();SaveProgress();Notify("从展示台拿起第一个组件，装入底盘。");}
    public void SetMode(bool sandbox){
        if(sandbox&&!Complete)return;
        if(SwanInputActive&&SwanModeRequest!=null&&!SwanModeRequest(sandbox))return;
        var arrival=GetComponent<JourneySceneArrival>();if(arrival)arrival.Cancel();
        bool changed=inSandbox!=sandbox;CancelAssembly();bool returning=inSandbox&&!sandbox;held=null;heldControl=null;inSandbox=sandbox;speed=0;throttle=0;brake=0;running=false;paused=false;ResetProcess();
        workshopRoot.SetActive(!sandbox);sandboxRoot.SetActive(sandbox);consoleRoot.SetActive(sandbox);
        trainAnchor.SetParent(sandbox?sandboxRoot.transform:workshopRoot.transform,false);
        lastPosePhase = float.NaN;
        trainAnchor.localScale=Vector3.one*(sandbox?.255f:1);trainAnchor.localPosition=Vector3.zero;trainAnchor.localRotation=Quaternion.identity;
        if(detailMeshes!=null)foreach(var detail in detailMeshes)if(detail.filter)detail.filter.sharedMesh=sandbox?detail.miniature:detail.inspection;
        cameraMode=0;if(!sandbox){trainAnchor.localPosition=Vector3.up*.09f;focus=new Vector3(1.6f,.15f,1.4f);yaw=25;pitch=32;distance=16.2f;}else{pressure=7.8f;reservoirPressure=8;brakePipePressure=auxiliaryPressure=5;BrakeCylinderPressure=0;fire.Clear();smoke.Clear();focus=new Vector3(0,6f,-3f);yaw=12;pitch=30;distance=54;routeDistance=0;PlaceTrain();}
        if(lessonPanel)lessonPanel.SetActive(!sandbox);if(drivePanel)drivePanel.SetActive(sandbox);
        ApplyVisibility();RefreshPreview();if(sandbox)Notify("向前推汽门出发，拨动制动手柄停车；停稳后可前进或后退。");else if(returning)Notify("已回到装配工坊 · 机车装配进度已保留。");
        ApplyCamera(1f);var impulses=GetComponent<StudioWaterImpulseV10>();if(impulses)impulses.Clear();var mood=FindObjectOfType<StudioWorkshopMood>();if(mood)mood.ApplyImmediate();if(changed&&arrival)arrival.Request();
    }
    void Drive(float dt){
        float prior=speed;float target=direction==0?0:throttle*2.6f;float grade=(path.At(routeDistance+.3f).y-path.At(routeDistance-.3f).y)/.6f;
        float acceleration=(target-speed)*.45f-Mathf.Clamp01(BrakeCylinderPressure/3.5f)*1.9f-grade*.45f*direction;
        speed=Mathf.Clamp(speed+acceleration*dt,0,2.8f);float delta=speed*dt*direction;
        float old=routeDistance;routeDistance=Mathf.Repeat(routeDistance+delta,path.Length);
        if(direction>0&&routeDistance<old&&speed>.03f){laps++;Notify("完成第 "+laps+" 圈 · 峡谷环线行驶完成。");}
        if(direction<0&&routeDistance>old&&speed>.03f){laps++;Notify("完成第 "+laps+" 圈。");}
        engine.phase=Mathf.Repeat(engine.phase-delta/.255f/.65f*Mathf.Rad2Deg,360);
        FlowTime+=dt;MotionTime+=dt;pressure=Mathf.MoveTowards(pressure,7.8f-throttle*1.3f,dt*.3f);
        if(delta!=0)PlaceTrain();
        UpdateBrakeEffects(dt,Mathf.Max(0,(prior-speed)/Mathf.Max(.001f,dt)));
    }
    void PlaceTrain(){
        trainAnchor.SetLocalPositionAndRotation(path.At(routeDistance),path.Rotation(routeDistance));
        for(int i=0;i<coaches.Length;i++){float s=routeDistance-(2.06f+i*2.10f);coaches[i].SetLocalPositionAndRotation(path.At(s),path.Rotation(s));}
        var wheelRotation=Quaternion.Euler(0,0,-routeDistance/.11f*Mathf.Rad2Deg);
        foreach(var wheel in coachWheels)wheel.localRotation=wheelRotation;
    }
    float lastPosePhase = float.NaN;
    int lastPoseDirection;
    bool lastPoseWasBench;

    void UpdateMechanicalPose(){
        engine.direction = direction;
        bool benchOnly = modules[4].installed && !modules[5].installed;
        // A bench piston can move without wheel rotation. Re-solve when entering or
        // leaving that mode, and whenever the crank or reverser actually moves.
        if(!SwanInputActive || engine.phase != lastPosePhase || direction != lastPoseDirection || benchOnly != lastPoseWasBench){
            JourneyMotion.Apply(engine);
            lastPosePhase = engine.phase;
            lastPoseDirection = direction;
            lastPoseWasBench = benchOnly;
        }
        if(benchOnly) ApplyCylinderBench();
        AnimatePistonValves();
    }

    ulong visibleInstallation;
    int visibleInspection = -1;
    bool visibleCutaway, visibleSandbox;
    JourneyModule visibleSelection;

    ulong InstallationBits(){
        ulong bits=0;
        for(int i=0;i<modules.Length;i++)if(modules[i].installed)bits|=1UL<<i;
        for(int i=0;i<finishing.Length;i++)if(finishing[i].installed)bits|=1UL<<(modules.Length+i);
        return bits;
    }
    void RefreshFrameVisibility(){
        // These 18 flags live in managed memory. Only cross to Unity renderers when the layout changes.
        if(!SwanInputActive || modules.Length+finishing.Length>64 || visibleInspection!=inspectionMode ||
            visibleCutaway!=cutaway || visibleSandbox!=inSandbox || visibleSelection!=Current ||
            visibleInstallation!=InstallationBits())ApplyVisibility();
    }
    public void ApplyVisibility(){
        if(SwanInputActive){
            visibleInstallation=InstallationBits();visibleInspection=inspectionMode;
            visibleCutaway=cutaway;visibleSandbox=inSandbox;visibleSelection=Current;
        }
        foreach(var m in modules.Concat(finishing))foreach(var o in m.objects)if(o&&o.activeSelf!=m.installed)o.SetActive(m.installed);
        bool section=!inSandbox&&cutaway;
        if(sectionOnly!=null)foreach(var r in sectionOnly)if(r && r.enabled!=section)r.enabled=section;
        if(cutRoot.activeSelf!=section)cutRoot.SetActive(section);
        for(int i=0;i<lessonModels.Length;i++)if(lessonModels[i].activeSelf!=modules[i].installed)lessonModels[i].SetActive(modules[i].installed);
        foreach(var r in shellRenderers)if(r && r.enabled==section)r.enabled=!section;
        if(supportStands!=null)foreach(var stand in supportStands){bool show=!inSandbox&&!modules[5].installed;if(stand.activeSelf!=show)stand.SetActive(show);}
        if(aimRing && socketGhosts==null)aimRing.gameObject.SetActive(!inSandbox&&!Current.installed);UpdateInspection();
    }
    void Animate(float dt){
        bool active=inSandbox||running||ResidualEnergy;bool burn=active&&modules[0].installed;
        PositionExhaust();
        AnimateRefinedEffects(dt);
        if(grate)grate.localRotation=Quaternion.identity;
        if(rockingBars!=null)foreach(var bar in rockingBars)if(bar)bar.localRotation=Quaternion.Euler(0,0,running&&!inSandbox?Mathf.Sin(FlowTime*1.15f)*4.5f:0);
        if(waterSurface)waterSurface.localPosition=new Vector3(.7f,2.72f,0);
        foreach(var f in flows){bool show=active&&!inSandbox&&cutaway&&modules[f.stage].installed;
            if(show&&f.port>0){var p=engine.sides[0].pose;show=f.port==1?p.LeftAdmission>.001:f.port==2?p.RightAdmission>.001:f.port==3?p.LeftExhaust>.001:p.RightExhaust>.001;}
            if(f.marker.gameObject.activeSelf!=show)f.marker.gameObject.SetActive(show);if(!show)continue;
            float u=Mathf.Repeat(FlowTime*f.speed+f.offset,1)*(f.points.Length-1);int k=Mathf.Min((int)u,f.points.Length-2);
            f.marker.localPosition=Vector3.Lerp(f.points[k],f.points[k+1],u-k);f.marker.localRotation=Quaternion.FromToRotation(Vector3.up,(f.points[k+1]-f.points[k]).normalized);
        }
        if(modules[4].installed){var p=engine.sides[0].pose;float px=(float)p.slider+1;Chamber(leftChamber,2.83f,px-.045f,ValvePortOpen(0,1),ValvePortOpen(0,3),active);Chamber(rightChamber,px+.045f,3.47f,ValvePortOpen(0,2),ValvePortOpen(0,4),active);}
        float braking=inSandbox?Mathf.Clamp01(BrakeCylinderPressure/3.5f):brakeApplied;
        for(int i=0;i<brakeShoes.Length;i++)brakeShoes[i].localPosition=shoeHome[i]+new Vector3(-.045f*braking,0,0);
        if(brakeRod)brakeRod.localScale=new Vector3(1-.12f*braking,1,1);
        if(verifyLever)verifyLever.localPosition=new Vector3(0,active?.055f:.085f,0);
        UpdateDrivingDisplay();
    }
    void UpdateDrivingDisplay(){
        // Only the hidden Swan driving presentation is gated. Thermal, pneumatic,
        // valve, wheel and workshop interaction updates remain in Animate/Update.
        if(SwanInputActive&&!inSandbox){
            if(!swanDrivingDisplayHidden){UpdateDrivingLights();swanDrivingDisplayHidden=true;swanDrivingTextReady=false;}
            return;
        }
        bool refresh=swanDrivingDisplayHidden;swanDrivingDisplayHidden=false;
        if(throttleLever)throttleLever.localRotation=throttleRest*Quaternion.Euler(throttle*-45,0,0);
        if(brakeLever)brakeLever.localRotation=brakeRest*Quaternion.Euler(0,brake*-65,0);
        if(directionLever)directionLever.localRotation=directionRest*Quaternion.Euler(direction*24,0,0);
        if(pressureNeedle)pressureNeedle.localRotation=Quaternion.Euler(0,0,125-pressure/10*250);
        if(speedNeedle)speedNeedle.localRotation=Quaternion.Euler(0,0,125-speed/2.8f*200);
        if(!SwanInputActive){
            if(consoleSpeed)consoleSpeed.text=(speed*12).ToString("00.0")+" km/h";if(consolePressure)consolePressure.text=pressure.ToString("0.0")+" bar";
            if(consoleStatus)consoleStatus.text=speed>.03f?"山谷铁路 · 行驶中":"山谷铁路 · 停车";
            swanDrivingTextReady=false;
        }else{
            int speedTenths=Mathf.RoundToInt(speed*120),pressureTenths=Mathf.RoundToInt(pressure*10);bool moving=speed>.03f;
            refresh|=!swanDrivingTextReady;
            // Format the same quantized value used as the cache key, including
            // half-value boundaries; raw floats can round differently in ToString.
            if(consoleSpeed&&(refresh||drivingSpeedTenths!=speedTenths))consoleSpeed.text=(speedTenths*.1f).ToString("00.0")+" km/h";
            if(consolePressure&&(refresh||drivingPressureTenths!=pressureTenths))consolePressure.text=(pressureTenths*.1f).ToString("0.0")+" bar";
            if(consoleStatus&&(refresh||drivingTextMoving!=moving))consoleStatus.text=moving?"山谷铁路 · 行驶中":"山谷铁路 · 停车";
            drivingSpeedTenths=speedTenths;drivingPressureTenths=pressureTenths;drivingTextMoving=moving;swanDrivingTextReady=true;
        }
        UpdateDrivingLights();
    }
    void UpdateDrivingLights(){
        var spatialLighting=GetComponent<StudioSpatialRefineV19>();
        foreach(var l in trainAnchor.GetComponentsInChildren<Light>(true)){if(l==furnaceLight||(spatialLighting&&l==spatialLighting.cabLight))continue;var part=l.GetComponentInParent<LocomotivePart>();bool isLamp=part&&(part.partId=="P25-R"||part.partId=="P25-L");l.enabled=lamps&&inSandbox&&isLamp;}
    }
    void Chamber(Renderer r,float a,float b,bool admission,bool exhaust,bool active){if(!r)return;r.enabled=cutaway&&!inSandbox&&b>a;
        r.transform.localPosition=new Vector3((a+b)*.5f,.65f,1.085f);r.transform.localScale=new Vector3(Mathf.Max(.001f,b-a),.46f,.46f);
        r.sharedMaterial=liveSteam;if(fxBlock==null)fxBlock=new MaterialPropertyBlock();fxBlock.Clear();
        bool left=r==leftChamber;int side=left?0:1;float volume=Mathf.Max(.055f,(b-a)/.64f);
        float inlet=ValveOpening(pistonValveShift[0],left?1:2)/.04f,outlet=ValveOpening(pistonValveShift[0],left?3:4)/.04f;
        float dt=paused?0:Mathf.Min(Time.deltaTime,.05f)*(slow?.25f:1);
        // A bounded teaching mass balance: admission adds steam, closed expansion preserves mass,
        // exhaust removes it through the open port, then shrinking clearance compresses the remainder.
        float mass=chamberCharge[side];if(active&&inlet>0)mass=Mathf.Lerp(mass,CylinderPower*volume,1-Mathf.Exp(-dt*3.2f*inlet));
        if(outlet>0)mass=Mathf.Lerp(mass,.035f*volume,1-Mathf.Exp(-dt*2.3f*outlet));if(!active)mass*=Mathf.Exp(-dt*.85f);
        chamberCharge[side]=Mathf.Clamp(mass,0,1.1f);float target=1-Mathf.Exp(-Mathf.Min(1.6f,mass/volume)*1.15f);
        float last=left?leftMist:rightMist,vent=left?leftVent:rightVent;
        last=Mathf.Lerp(last,target,1-Mathf.Exp(-dt*2.2f));vent=Mathf.Lerp(vent,outlet,1-Mathf.Exp(-dt*2));
        if(left){leftMist=last;leftVent=vent;}else{rightMist=last;rightVent=vent;}
        fxBlock.SetFloat("_Density",last);fxBlock.SetFloat("_Front",1);fxBlock.SetFloat("_FlowTime",FlowTime);fxBlock.SetFloat("_Direction",left?1:-1);fxBlock.SetFloat("_Exhaust",vent);r.SetPropertyBlock(fxBlock);}
    readonly float[] chamberCharge=new float[2];
    public static void SetEmission(ParticleSystem ps,float rate){if(!ps)return;var e=ps.emission;e.rateOverTime=rate;if(!ps.isPlaying)ps.Play();}
    public void RefreshPreview(){if(!previewRoot)return;
        if(AutoAssembling){foreach(var q in modules.Concat(finishing))if(q.preview)q.preview.SetActive(false);return;}
        foreach(var item in modules.Concat(finishing)){if(!item.preview)continue;item.preview.SetActive(!inSandbox&&!item.installed&&(item==Current||item.parked));}
        var m=Current;if(m.preview&&!m.parked){m.preview.transform.position=previewStand.position+SceneVectorToWorld(m.previewOffset);m.preview.transform.rotation=SceneRotationToWorld(Quaternion.identity);m.preview.transform.localScale=Vector3.one*m.presentationScale;previewHome=m.preview.transform.position;}
        if(aimRing)aimRing.position=trainAnchor.TransformPoint(m.socket);
    }
    // Unsnapped components retain their full world pose. Tray is only the default docking location.
    public bool BeginGrab(){return BeginGrabModule(Current);}
    bool BeginGrabModule(JourneyModule m){if(inSandbox||AutoAssembling||m.installed||!m.preview)return false;heldModule=m;held=m.preview.transform;snapReady=false;dragDepth=Mathf.Max(1,Vector3.Dot(held.position-view.transform.position,view.transform.forward));return true;}
    public void MoveGrab(Vector3 worldPoint){MagneticPreview(worldPoint);}
    public void AdjustGrabDepth(float depthChange){dragDepth=Mathf.Clamp(dragDepth+depthChange,1,40);}
    public void MoveGrabScreen(Vector2 screenPoint){if(!held)return;var ray=view.ScreenPointToRay(screenPoint);var raw=ray.GetPoint(dragDepth/Mathf.Max(.2f,Vector3.Dot(ray.direction,view.transform.forward)));var target=trainAnchor.TransformPoint(heldModule.previewCentre);var projected=view.WorldToScreenPoint(target);float radius=Mathf.Clamp(Screen.height*.06f,45,100);if(projected.z>0&&Vector2.Distance(screenPoint,projected)<radius&&Vector3.Distance(raw,target)<4.5f){raw=Vector3.Lerp(raw,target,.82f);dragDepth=Mathf.Lerp(dragDepth,projected.z,.35f);}MoveGrab(raw);}
    public void RotateLoose(Vector2 delta){var t=held?held:rotatingPart;if(!t)return;t.Rotate(view.transform.up,-delta.x*.45f,Space.World);t.Rotate(view.transform.right,delta.y*.45f,Space.World);var m=modules.Concat(finishing).FirstOrDefault(q=>q.preview&&q.preview.transform==t);if(m!=null)m.parked=true;}
    public bool EndGrab(){if(!held)return false;var m=heldModule;bool fits=snapReady||Vector3.Distance(trainAnchor.InverseTransformPoint(held.position),m.previewCentre)<.72f;held=null;heldModule=null;snapReady=false;
        if(fits&&m==Current){m.parked=false;return InstallCurrent();}if(fits&&!Teaching&&Array.IndexOf(finishing,m)>=0){finishIndex=Array.IndexOf(finishing,m);m.parked=false;return InstallCurrent();}
        m.parked=true;RefreshPreview();Notify(SwanInputActive?"已暂放在空间 · 再次捏合拿起，双击捏合快速归位。":"已暂放在空间 · 可再次拿取，右键拖动旋转零件；R 放回展示台。");return false;}
    JourneyModule HitLoose(Vector2 pointer){RaycastHit hit;if(!Physics.Raycast(view.ScreenPointToRay(pointer),out hit,200))return null;return modules.Concat(finishing).FirstOrDefault(m=>m.preview&&!m.installed&&hit.transform.IsChildOf(m.preview.transform));}
    void ReadInput(){
        if(SwanInputActive)return;
        if(verificationInputLock)return;
        if(celestialInputCapture!=null && celestialInputCapture()){previousPointer=Input.mousePosition;return;}
        if(GetComponent<JourneySceneArrival>() is JourneySceneArrival arrival&&arrival.Active){previousPointer=Input.mousePosition;return;}
        bool over=EventSystem.current&&EventSystem.current.IsPointerOverGameObject();
        if(Input.touchCount>0&&EventSystem.current)over|=EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        if(Input.GetKeyDown(KeyCode.Space))Act(inSandbox?JourneyAction.Brake:JourneyAction.Verify);
        if(Input.GetKeyDown(KeyCode.B)&&modules[6].installed)Act(JourneyAction.Brake);
        if(Input.GetKeyDown(KeyCode.P))Act(JourneyAction.Pause);if(Input.GetKeyDown(KeyCode.C))Act(JourneyAction.Cutaway);if(Input.GetKeyDown(KeyCode.R)&&!held)Act(JourneyAction.ReturnPart);
        if(inSandbox){if(Input.GetKey(KeyCode.W))throttle=Mathf.Clamp01(throttle+Time.deltaTime*.5f);if(Input.GetKey(KeyCode.S))throttle=Mathf.Clamp01(throttle-Time.deltaTime*.5f);if(Input.GetKeyDown(KeyCode.H))Act(JourneyAction.Whistle);}
        Vector2 pointer=Input.mousePosition;Vector2 d=pointer-previousPointer;
        if(Input.GetMouseButtonDown(1)&&!over){var m=HitLoose(pointer);rotatingPart=m!=null?m.preview.transform:null;previousPointer=pointer;}
        if(Input.GetMouseButton(1)&&!over){if(held||rotatingPart)RotateLoose(d);else{guideCamera=false;yaw+=d.x*.18f;pitch=Mathf.Clamp(pitch-d.y*.14f,5,85);}}
        if(Input.GetMouseButtonUp(1))rotatingPart=null;
        if(Input.GetMouseButtonDown(0)&&!over){downPointer=previousPointer=pointer;RaycastHit hit;
            if(Physics.Raycast(view.ScreenPointToRay(pointer),out hit,200)){var c=hit.collider.GetComponentInParent<JourneyControl>();if(c&&c.caption!="Inspection shell"&&c.caption!="Ember"){heldControl=c;controlValue=c.action==JourneyAction.Throttle?throttle:brake;}else{var m=HitLoose(pointer);if(m!=null)BeginGrabModule(m);else{pendingDetach=HitInstalled(pointer);detachPointer=pointer;}}}}
        if(Input.GetMouseButton(0)&&pendingDetach!=null&&Vector2.Distance(pointer,detachPointer)>16){var m=pendingDetach;pendingDetach=null;if(DetachModule(m))BeginGrabModule(m);}
        if(Input.GetMouseButton(0)){
            if(held&&!Input.GetMouseButton(1))MoveGrabScreen(pointer);
            else if(heldControl){if(heldControl.action==JourneyAction.Throttle&&Vector2.Distance(downPointer,pointer)>8){var refined=GetComponent<StudioSpatialRefineV19>();if(refined)refined.DragThrottle(pointer);else throttle=Mathf.Clamp01(controlValue+(downPointer.y-pointer.y)/180);}}
            else if(!held&&pendingDetach==null&&!over&&!Input.GetMouseButton(1)&&d.magnitude<200){if(d.sqrMagnitude>.5f)guideCamera=false;yaw+=d.x*.18f;pitch=Mathf.Clamp(pitch-d.y*.14f,5,85);}}
        if(Input.GetMouseButtonUp(0)){pendingDetach=null;if(held)EndGrab();if(heldControl){if(Vector2.Distance(downPointer,pointer)<8&&!(heldControl.action==JourneyAction.Throttle&&GetComponent<StudioSpatialRefineV19>()))Act(heldControl.action);heldControl=null;}}
        if(!over){float scroll=Input.mouseScrollDelta.y;if(held){AdjustGrabDepth(scroll*.65f);if(Mathf.Abs(scroll)>.01f)MoveGrabScreen(pointer);}else{if(Mathf.Abs(scroll)>.01f)guideCamera=false;distance=Mathf.Clamp(distance-scroll*(inSandbox?1.5f:.7f),inSandbox?8:3,inSandbox?50:30);}}
        if(Input.touchCount==2){var t0=Input.GetTouch(0);var t1=Input.GetTouch(1);distance=Mathf.Clamp(distance-(Vector2.Distance(t0.position,t1.position)-Vector2.Distance(t0.position-t0.deltaPosition,t1.position-t1.deltaPosition))*.02f,3,50);}
        previousPointer=pointer;
    }
    void ApplyCamera(float dt){
        if(SwanInputActive)return;
        GuideProcessCamera(dt);
        Vector3 target=focus;float d=distance;float p=pitch;
        if(inSandbox&&cameraMode==1){target=trainAnchor.position+Vector3.up*.5f;d=7;p=26;}
        if(inSandbox&&cameraMode==2){target=consoleRoot.transform.position+Vector3.up;d=11;p=42;}
        var desired=target+Quaternion.Euler(-p,yaw,0)*new Vector3(0,0,d);
        view.transform.position=Vector3.Lerp(view.transform.position,desired,1-Mathf.Exp(-dt*8));var right=Vector3.Cross(Vector3.up,target-view.transform.position).normalized;view.transform.LookAt(target);
    }
    void Play(AudioClip clip){if(clip&&audioSource){GetComponent<JourneyBackgroundMusic>()?.Duck(clip.length);audioSource.PlayOneShot(clip);}}
    void Notify(string text){if(noticeText)noticeText.text=text;noticeUntil=Time.unscaledTime+7;}
    void SaveProgress(){if(suppressPersistence)return;var s=new Saved{stage=stage,finishIndex=finishIndex,installed=modules.Select(m=>m.installed).ToArray(),verified=modules.Select(m=>m.verified).ToArray(),finished=finishing.Select(m=>m.installed).ToArray()};PlayerPrefs.SetString(SaveKey,JsonUtility.ToJson(s));PlayerPrefs.Save();}
    public static bool[] UpgradeFinishedProgress(bool[] finished,int targetCount){
        if(finished==null)return null;
        if(finished.Length==13&&(targetCount==11||targetCount==10)){
            var old=finished;
            finished=new[]{old[0],old[1],old[2],old[3],old[4]&&old[6],old[5]&&old[7],old[8],old[9],old[10],old[11],old[12]};
        }
        if(finished.Length==11&&targetCount==10)
            return finished.Take(9).Append(finished[9]&&finished[10]).ToArray();
        // The editable desktop source still has two individual lamp steps.
        if(finished.Length==10&&targetCount==11)return finished.Append(finished[9]).ToArray();
        return finished;
    }
    void LoadProgress(){
        if(!PlayerPrefs.HasKey(SaveKey))return;
        try{
            var s=JsonUtility.FromJson<Saved>(PlayerPrefs.GetString(SaveKey));
            if(s==null)return;
            var finished=UpgradeFinishedProgress(s.finished,finishing.Length);
            if(s.installed==null||s.verified==null||finished==null||s.installed.Length!=modules.Length||s.verified.Length!=modules.Length||finished.Length!=finishing.Length)return;
            finishIndex=Mathf.Clamp(s.finishIndex,0,finishing.Length-1);
            for(int i=0;i<modules.Length;i++){
                modules[i].installed=s.installed[i];modules[i].verified=s.verified[i];
                modules[i].observed=s.verified[i]?RequiredObservation(i):0;
            }
            for(int i=0;i<finishing.Length;i++)finishing[i].installed=finished[i];
            stage=modules.TakeWhile(m=>m.installed).Count();
        }catch{stage=0;}
    }
    void OnApplicationPause(bool pause){if(pause)SaveProgress();}void OnApplicationQuit(){SaveProgress();}
    void UpdateText(){if(!progress)return;
        if(!SwanInputActive)progress.text=inSandbox?"山谷铁路   /   第 "+(laps+1)+" 圈":"已装配  "+(modules.Count(m=>m.installed)+finishing.Count(m=>m.installed))+" / "+(modules.Length+finishing.Length);
        if(trayTitle)trayTitle.text=Complete?"你的机车已完成":AutoAssembling?"自动装配 · 依次归位":Current.title;
        if(trayState)trayState.text=AutoAssembling?"零件正在依次装入":held&&snapReady?"接口已对齐 · 松手装入":Complete?"按「出发」进入微缩铁路":Teaching?"拿起组件 · 装入高亮接口":"车体与附件 · 可切换选择";
        if(verifyLabel)verifyLabel.text=running?"验证  开":"验证  关";
        if(verifyLens){fxBlock=fxBlock??new MaterialPropertyBlock();fxBlock.Clear();fxBlock.SetColor("_BaseColor",running?new Color(.12f,.75f,.49f):new Color(.16f,.25f,.22f));fxBlock.SetColor("_EmissionColor",running?new Color(.06f,.48f,.23f):Color.black);verifyLens.SetPropertyBlock(fxBlock);}
        if(trayNavigation!=null)foreach(var o in trayNavigation)o.SetActive(!inSandbox);
        if(trayDrive)trayDrive.SetActive(!inSandbox);
        if(trayFinish)trayFinish.SetActive(!inSandbox);if(trayCounter)trayCounter.text=(Complete||AutoAssembling?modules.Count(m=>m.installed)+finishing.Count(m=>m.installed):Teaching?stage+1:modules.Length+finishIndex+1).ToString("00")+" / "+(modules.Length+finishing.Length).ToString("00");
        // Spatial controls above remain live. The desktop overlay below has no
        // visible consumer on Swan; SpatialMessage reads noticeText directly.
        if(SwanInputActive){if(Time.unscaledTime>noticeUntil)noticeText.text="";return;}
        if(processStatus)processStatus.text=running&&!inSandbox?ProcessCaption():"";
        cutButton.gameObject.SetActive(!inSandbox);cutButton.GetComponentInChildren<TMP_Text>().text=!cutaway?"外观  实体":inspectionMode==1?"观察  透视":"观察  剖面";
        speedLabel.text=(speed*12).ToString("00.0")+"  km/h\n"+(direction>0?"前进":direction<0?"后退":"中位")+"   ·   锅压 "+pressure.ToString("0.0")+" bar\n汽门 "+Mathf.RoundToInt(throttle*100)+"%   制动 "+Mathf.RoundToInt(brake*100)+"%";
        footer.text=inSandbox?"W / S 调汽门    空格 制动    H 汽笛    拖动旋转 · 滚轮缩放":"拿起展示台组件装配    空格 验证开关    C 观察模式    "+(modules[6].installed?"B 制动 / 松闸    ":"")+"左拖拿取/拆卸/暂放 · 右拖转零件/视角 · 持件滚轮调远近 · R 归位";
        if(Time.unscaledTime>noticeUntil)noticeText.text="";
    }
    RectTransform Rect(string name,Transform parent,Vector2 a,Vector2 b){var o=new GameObject(name,typeof(RectTransform));var r=(RectTransform)o.transform;r.SetParent(parent,false);r.anchorMin=a;r.anchorMax=b;r.offsetMin=r.offsetMax=Vector2.zero;return r;}
    TMP_Text Text(Transform parent,string value,Vector2 a,Vector2 b,float size,Color color){var r=Rect(value,parent,a,b);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=value;t.fontSize=size;t.color=color;t.enableWordWrapping=true;t.raycastTarget=false;return t;}
    UnityEngine.UI.Button Button(Transform parent,string label,Vector2 a,Vector2 b,JourneyAction action,bool accent=false){var r=Rect(label,parent,a,b);var img=r.gameObject.AddComponent<UnityEngine.UI.Image>();img.color=accent?new Color(.69f,.46f,.17f):new Color(.13f,.20f,.22f);var bt=r.gameObject.AddComponent<UnityEngine.UI.Button>();bt.targetGraphic=img;bt.onClick.AddListener(()=>Act(action));var tx=Text(r,label,new Vector2(.04f,.08f),new Vector2(.96f,.92f),19,Color.white);tx.alignment=TextAlignmentOptions.Center;return bt;}
    void BuildUI(){
        var root=new GameObject("Journey · State and contextual controls",typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=20;var scaler=root.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,1000);scaler.matchWidthOrHeight=.5f;
        if(!SwanInputActive&&!EventSystem.current)new GameObject("Journey EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
        var top=Rect("Header",root.transform,new Vector2(0,.94f),Vector2.one);top.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(.035f,.07f,.08f,.95f);
        Text(top,"STEAM ATELIER   /   蒸汽原理工坊",new Vector2(.025f,.29f),new Vector2(.49f,.82f),24,new Color(.92f,.88f,.77f));progress=Text(top,"",new Vector2(.70f,.3f),new Vector2(.98f,.8f),21,Color.white);progress.alignment=TextAlignmentOptions.Right;
        Button(top,"重新体验",new Vector2(.59f,.22f),new Vector2(.68f,.74f),JourneyAction.Reset);
        // Context is on the physical one-piece tray. No lesson card, Next gate or auto-install menu.
        cutButton=Button(root.transform,"剖视  开",new Vector2(.025f,.10f),new Vector2(.11f,.15f),JourneyAction.Cutaway);
        processStatus=Text(root.transform,"",new Vector2(.16f,.10f),new Vector2(.94f,.15f),19,new Color(.72f,.87f,.82f));
        noticeText=Text(root.transform,"",new Vector2(.025f,.065f),new Vector2(.74f,.11f),21,new Color(1,.84f,.49f));
        footer=Text(root.transform,"",new Vector2(.025f,.015f),new Vector2(.98f,.047f),16,new Color(.73f,.79f,.77f));
        var dr=Rect("Driving status only",root.transform,new Vector2(.74f,.80f),new Vector2(.985f,.93f));dr.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(.035f,.07f,.08f,.75f);drivePanel=dr.gameObject;
        speedLabel=Text(dr,"",new Vector2(.05f,.22f),new Vector2(.95f,.96f),19,new Color(.87f,.83f,.71f));
        Button(dr,"返回工坊",new Vector2(.02f,.01f),new Vector2(.49f,.25f),JourneyAction.Workshop);Button(dr,"观察视角",new Vector2(.51f,.01f),new Vector2(.98f,.25f),JourneyAction.View);
        foreach(Transform t in consoleRoot.GetComponentsInChildren<Transform>(true))t.gameObject.layer=0;
        // SpatialAdapter already renders this scene natively. Restoring layer 0
        // here after its Awake would start a second Unity scene render.
        if(!SwanInputActive)view.cullingMask|=1;
        if(SwanInputActive){canvas.enabled=false;root.SetActive(false);ApplySpatialHud();}
    }
    void OnDestroy(){if(instrumentTexture){instrumentTexture.Release();Destroy(instrumentTexture);}if(instrumentCamera)Destroy(instrumentCamera.gameObject);}
}


