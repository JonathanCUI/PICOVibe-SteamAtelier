using UnityEngine;

// A compressed teaching model. Dependencies, not a shared animation clock, transmit energy.
// It intentionally does not claim to be a boiler pressure/temperature engineering simulation.
public sealed partial class SteamJourney {
    public Renderer flameVolume;
    public bool bakedFirebox;
    public ParticleSystem furnaceParticles;
    public Renderer pausedFurnace;
    public bool directionalFlow;
    Vector3 flameRestScale;
    Vector3 furnaceRestScale;
    ParticleSystem[] furnaceParticleLayers;
    bool furnaceEmitting, furnaceStateSet;
    float nextFlameUpdate;

    void AnimateBakedFire(float power)
    {
        if (furnaceParticles)
        {
            var source = furnaceParticles;
            bool visible = !inSandbox && modules[0].installed && power > .012f;
            bool emit = visible && !paused;
            // Keep the group alive so the native adapter receives the stopped
            // state of every child emitter before any ancestor is hidden.
            if (!source.gameObject.activeSelf) source.gameObject.SetActive(true);
            if (furnaceParticleLayers == null) furnaceParticleLayers = source.GetComponentsInChildren<ParticleSystem>(true);
            bool changed = !furnaceStateSet || furnaceEmitting != emit;
            if (changed)
            {
                furnaceStateSet = true; furnaceEmitting = emit;
                foreach (var layer in furnaceParticleLayers) { var emission=layer.emission; emission.enabled=emit; }
                if (emit) source.Play(true);
                else source.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (pausedFurnace && pausedFurnace.gameObject.activeSelf != (visible && paused))
                pausedFurnace.gameObject.SetActive(visible && paused);
            if (!emit) return;
            float rate = slow ? .25f : 1f;
            foreach (var layer in furnaceParticleLayers)
            {
                var main = layer.main;
                if (!Mathf.Approximately(main.simulationSpeed, rate)) main.simulationSpeed = rate;
            }
            if (!changed && Time.unscaledTime < nextFlameUpdate) return;
            nextFlameUpdate = Time.unscaledTime + 1f / 12f;
            if (furnaceRestScale == Vector3.zero) furnaceRestScale = source.transform.localScale;
            source.transform.localScale = furnaceRestScale * Mathf.Lerp(.7f, 1f, power);
            if (pausedFurnace) pausedFurnace.transform.localScale = source.transform.localScale;
            return;
        }
        if (!flameVolume) return;
        var t = flameVolume.transform;
        bool show = !inSandbox && modules[0].installed && power > .012f;
        bool wasShowing = t.gameObject.activeSelf;
        if (show != wasShowing) t.gameObject.SetActive(show);
        if (!show || (wasShowing && (paused || Time.unscaledTime < nextFlameUpdate))) return;
        nextFlameUpdate = Time.unscaledTime + 1f / 12f;
        if (flameRestScale == Vector3.zero) flameRestScale = t.localScale;
        float flicker = 1f + .095f * Mathf.Sin(FlowTime * 8.7f) + .055f * Mathf.Sin(FlowTime * 15.1f);
        float height = Mathf.SmoothStep(.18f, 1f, power) * flicker;
        t.localScale = new Vector3(flameRestScale.x * (1f + .035f * Mathf.Sin(FlowTime * 6.5f)),
            flameRestScale.y * height, flameRestScale.z);
    }
    public Renderer fireJacket;
    public Transform[] rockingBars;
    public Transform cutLever;
    float openExhaustAccumulator;int openExhaustHole;
    public Renderer[] emberRenderers;
    public Material waterSkin,waterBody,emberMaterial,flameMaterial;
    public JourneyFluidField[] fluidFields;
    public Light furnaceLight;
    public float FireLevel {get;private set;}
    public float GasLevel {get;private set;}
    public float WaterHeat {get;private set;}
    public float SteamPressure {get;private set;}
    public float Delivery {get;private set;}
    public float CylinderPower {get;private set;}
    public float WheelPower {get;private set;}
    public float ProcessTime {get;private set;}
    // Combustion and working steam are separate paths. A removed heat exchanger breaks both.
    public bool BoilerGasConnected=>modules[0].installed&&modules[1].installed;
    public bool ChimneyConnected=>BoilerGasConnected&&modules[2].installed;
    // A chimney is an outlet of the smoke path, not an independent particle emitter.
    public float ChimneyExhaustPower { get {if(!ChimneyConnected||(!running&&!ResidualEnergy))return 0;bool found=false;foreach(var f in fluidFields){if(!f||f.kind!=JourneyFluidKind.Smoke||f.stage!=2)continue;found=true;if(!f.Arrived)return 0;}return found?GasLevel:0;} }
    void InvalidateDetachedFlow(JourneyModule module){int stage=System.Array.IndexOf(modules,module);if(stage<0)return;foreach(var f in fluidFields){if(!f)continue;bool affected=f.stage==stage;for(var u=f.upstream;u;u=u.upstream)if(u.stage==stage)affected=true;if(stage<=2&&f.kind==JourneyFluidKind.Smoke&&f.stage>=stage)affected=true;if(affected)f.ResetField();}if(stage<=2){if(smoke)smoke.Clear();GetComponent<WorkshopSteamV20>()?.Clear();}}
    public float TeachingRpm=>12*WheelPower*(1-brakeApplied);
    float motorSettled,brakeApplied;
    bool guideCamera;
    MaterialPropertyBlock fxBlock;
    public void ResetProcess(){chamberCharge[0]=chamberCharge[1]=leftMist=rightMist=leftVent=rightVent=0;System.Array.Clear(valveDensity,0,valveDensity.Length);System.Array.Clear(valveVent,0,valveVent.Length);FireLevel=GasLevel=WaterHeat=SteamPressure=Delivery=CylinderPower=WheelPower=ProcessTime=0;motorSettled=brakeApplied=benchStroke=0;FlowTime=0;MotionTime=0;if(fluidFields!=null)foreach(var f in fluidFields)if(f)f.ResetField();if(smoke)smoke.Clear();if(steam)steam.Clear();}
    static float Approach(float value,float target,float dt,float seconds)=>Mathf.MoveTowards(value,target,dt/seconds);
    public void StepProcess(float dt){
        ProcessTime+=dt;
        FireLevel=Approach(FireLevel,running&&modules[0].installed?1:0,dt,2.3f);
        GasLevel=Approach(GasLevel,FireLevel>.12f?FireLevel:0,dt,2.0f);
        WaterHeat=Approach(WaterHeat,BoilerGasConnected&&GasLevel>.6f?GasLevel:0,dt,5.2f);
        SteamPressure=Approach(SteamPressure,modules[1].installed&&WaterHeat>.68f?WaterHeat:0,dt,modules[1].installed?3.8f:2.8f);
        Delivery=Approach(Delivery,modules[1].installed&&modules[2].installed&&modules[3].installed&&SteamPressure>.45f?SteamPressure:0,dt,2.5f);
        bool charged=true;if(fluidFields!=null)foreach(var field in fluidFields)if(field.stage==3&&!field.Arrived)charged=false;
        CylinderPower=Approach(CylinderPower,modules[4].installed&&charged&&(Delivery>.67f||CylinderPower>.01f)?Delivery:0,dt,2.0f);
        WheelPower=Approach(WheelPower,modules[4].installed&&modules[5].installed&&(CylinderPower>.38f||WheelPower>.01f)?CylinderPower:0,dt,3.5f);
        brakeApplied=Approach(brakeApplied,modules[6].installed?Mathf.Clamp01(BrakeCylinderPressure/3.5f):0,dt,.7f);
        if(!modules[5].installed&&CylinderPower>.45f)benchStroke=Mathf.MoveTowards(benchStroke,1,dt*.45f*CylinderPower);
        float drive=modules[5].installed?WheelPower:0;
        float rate=12*drive*(1-brakeApplied); // Five seconds per revolution at full teaching power.
        if(rate>.001f){MotionTime+=dt;engine.phase=Mathf.Repeat(engine.phase-rate*6*dt,360);}
        pressure=SteamPressure*8.4f;
    }
    public bool ProcessReached(int s){switch(s){case 0:return GasLevel>.85f;case 1:return SteamPressure>.75f;case 2:return SteamPressure>.75f;case 3:bool reached=Delivery>.85f;foreach(var f in fluidFields)if(f.stage==3&&!f.Arrived)reached=false;return reached;case 4:return CylinderPower>.85f&&(modules[5].installed||benchStroke>.99f);case 5:return WheelPower>.85f;default:return brakeApplied>.99f;}}
    public string ProcessCaption(){
        if(!running)return Current.observation;
        if(FireLevel<.6f)return SwanInputActive?"点火中 · 炉火逐渐形成，开始加热锅炉。":"点火中 · 煤层先出现红热，火焰逐渐形成。";
        if(GasLevel<.7f)return "燃烧形成烟气 · 观察烟气离开煤层，沿火箱上部流向烟管入口。";
        if(!modules[1].installed)return "煤层持续燃烧 · 尚未接上锅炉，烟气从敞开的火箱逸出。";
        if(WaterHeat<.68f)return "烟气已进入烟管 · 金属管壁正在把热量传给周围的水，动力机构仍静止。";
        if(SteamPressure<.45f)return SwanInputActive?"水受热产生蒸汽 · 蓝色箭头表示水的流向，蒸汽逐渐积聚。":"水逐渐沸腾 · 水面起伏、气泡上升，上部空间开始积聚蒸汽。";
        if(!modules[2].installed)return "烟箱通道已断开 · 炉火与锅炉继续换热，下游供汽逐渐减弱。";
        if(!modules[3].installed)return "蒸汽在水面上方汇集 · 供汽通路尚未装入，汽缸与车轮不工作。";
        if(Delivery<.67f||!ProcessReached(3))return "开启供汽 · 蒸汽沿汽包取汽管、过热器和主汽管逐段向前流动。";
        if(!modules[4].installed)return "主汽管已充汽 · 等待装入汽缸配汽机构。";
        if(CylinderPower<.8f)return "蒸汽到达阀室 · 先充满开启的通道，再推动活塞。";
        if(!modules[5].installed)return "单次做功完成 · 蒸汽推动活塞到行程末端；装上轮组与配汽机构后才能连续往复。";
        if(WheelPower<.85f)return "活塞建立推力 · 连杆开始带动车轮，轮组逐渐加速。";
        if(modules[6].installed&&reservoirPressure<4.5f&&CompressorRate>0)return "蒸汽带动气泵 · 空气活塞给总风缸充气，列车管与副风缸逐渐建立压力。";
        if(modules[6].installed&&brakeApplied>.01f)return brakeApplied>.99f?"制动完成 · 闸瓦贴合，轮组停下；按 B 松闸。":"正在制动 · 拉杆推动闸瓦，轮组逐渐减速。";
        return "动力链已贯通 · 烟气走烟管，蒸汽走独立汽管；乏汽排入喷管，增强烟囱抽风。";
    }
    void GuideProcessCamera(float dt){
        if(!guideCamera||inSandbox||!running||paused)return;
        Vector3 target=new Vector3(-1.3f,1.95f,0);float range=6.2f,tilt=22;
        if(modules[1].installed&&GasLevel>.65f){target=new Vector3(.8f,2.25f,0);range=8.6f;tilt=25;}
        if(ActiveStage==2&&SteamPressure>.25f){target=new Vector3(2.65f,2.3f,0);range=7.2f;}
        if(modules[3].installed&&WaterHeat>.68f){target=new Vector3(.7f,2.55f,.1f);range=7.3f;tilt=30;}
        if(modules[3].installed&&Delivery>.05f){target=new Vector3(1.7f,2.25f,.35f);range=7.4f;tilt=23;}
        if(modules[4].installed&&ProcessReached(3)){target=new Vector3(3.0f,.85f,1.08f);range=5.1f;tilt=16;}
        if(modules[5].installed&&WheelPower>.65f){target=new Vector3(.25f,1.5f,0);range=11.3f;tilt=23;}
        float ease=1-Mathf.Exp(-dt*1.8f);focus=Vector3.Lerp(focus,target,ease);distance=Mathf.Lerp(distance,range,ease);pitch=Mathf.Lerp(pitch,tilt,ease);yaw=Mathf.LerpAngle(yaw,22,ease);
    }
    float FluidStrength(JourneyFluidField field){
        if(field.stage==6)return CompressorRate;
        if(field.kind==JourneyFluidKind.Bubble)return Mathf.InverseLerp(.40f,.95f,WaterHeat);
        if(field.kind==JourneyFluidKind.Air)return FireLevel;
        if(field.stage==0)return GasLevel;
        if(field.stage==1||field.stage==2)return (field.stage==1?BoilerGasConnected:ChimneyConnected)?GasLevel:0;
        if(field.stage==3)return Delivery;
        if(field.stage==4)return CylinderPower;
        return WheelPower;
    }
    float nextFieldPublication;
    void AnimateRefinedEffects(float dt){
        if(cutLever)cutLever.localRotation=Quaternion.Euler(0,0,inspectionMode==0?25:inspectionMode==1?0:-25);
        bool active=inSandbox||running||ResidualEnergy;
        float firePower=inSandbox?.85f:FireLevel;
        float effectDt=(!active||paused&&!inSandbox)?0:dt*(slow&&!inSandbox?.25f:1);
        if(fxBlock==null)fxBlock=new MaterialPropertyBlock();if(!SwanInputActive)Shader.SetGlobalFloat("_JourneyTime",FlowTime);
        if(bakedFirebox) AnimateBakedFire(active ? firePower : 0);
        else if(flameVolume){flameVolume.enabled=!inSandbox&&modules[0].installed;fxBlock.Clear();fxBlock.SetFloat("_Burn",active?firePower:0);fxBlock.SetFloat("_FlowTime",FlowTime);flameVolume.SetPropertyBlock(fxBlock);}
        if(fireJacket)fireJacket.enabled=!inSandbox&&cutaway&&modules[1].installed;
        if(!bakedFirebox&&emberRenderers!=null){fxBlock.Clear();fxBlock.SetColor("_EmissionColor",new Color(2.5f,.34f,.018f)*(active?firePower:0));foreach(var r in emberRenderers)if(r)r.SetPropertyBlock(fxBlock);}
        if(!SwanInputActive){
            if(waterSkin){waterSkin.SetFloat("_Heat",WaterHeat);waterSkin.SetFloat("_FlowTime",FlowTime);}
            if(waterBody)waterBody.SetFloat("_FlowTime",FlowTime);
        }
        if(!bakedFirebox&&furnaceLight){
            furnaceLight.enabled=!inSandbox&&modules[0].installed&&active&&FireLevel>.01f;
            // Local light follows combustion, including residual heat and a frozen teaching pause.
            float flutter=.91f+.06f*Mathf.Sin(FlowTime*7.3f)+.03f*Mathf.Sin(FlowTime*13.7f+1.8f);
            furnaceLight.intensity=FireLevel*4.6f*flutter;
            furnaceLight.color=Color.Lerp(new Color(1,.25f,.045f),new Color(1,.44f,.115f),FireLevel);
        }
        bool publishFields=!SwanInputActive || (!inSandbox && cutaway && Time.unscaledTime>=nextFieldPublication);
        if(SwanInputActive&&publishFields)nextFieldPublication=Time.unscaledTime+1f/24f;
        if(fluidFields!=null)foreach(var field in fluidFields){if(!field)continue;
            if(!SwanInputActive)field.system.GetComponent<Renderer>().enabled=!inSandbox&&cutaway;
            if(SwanInputActive && (effectDt<=0 || (!modules[field.stage].installed && !field.HasWisps)))continue;
            float strength=active&&modules[field.stage].installed?FluidStrength(field):0;
            if(field.port>0){bool open=field.port==5?(ValvePortOpen(0,3)||ValvePortOpen(0,4)):ValvePortOpen(0,field.port);strength*=open?1:0;}
            if(directionalFlow && SwanInputActive) field.AdvanceArrow(effectDt,strength);
            else field.Advance(effectDt,strength,publishFields);}
        if(!bakedFirebox) SetEmission(fire,0); // Replaced by a depth-tested volumetric furnace, not billboard blobs.
        bool exhaustArrived=true;int lastGas=ChimneyConnected?2:BoilerGasConnected?1:0;if(fluidFields!=null)foreach(var f in fluidFields)if(f.stage==lastGas&&f.kind==JourneyFluidKind.Smoke&&!f.Arrived)exhaustArrived=false;
        float smokePower=active?(inSandbox?.8f:modules[0].installed&&exhaustArrived?GasLevel:0):0;
        float exhaustPulse=.8f+.2f*Mathf.Sin(engine.phase*Mathf.Deg2Rad*4);
        bool chimney=ChimneyConnected;
        bool bakedOutlet=chimney&&bakedChimney;
        if(bakedOutlet){if(smoke.isPlaying||smoke.isPaused)smoke.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);}
        else SetEmission(smoke,chimney?smokePower*(inSandbox?92:36)*exhaustPulse:0);
        SetEmission(steam,active&&!inSandbox&&cutaway&&modules[1].installed?SteamPressure*12:0);
        if(smoke&&!bakedOutlet){var main=smoke.main;main.startColor=Color.Lerp(new Color(.31f,.33f,.34f,.76f),new Color(.56f,.58f,.59f,.65f),inSandbox?.35f:WheelPower*.45f);main.startSize=new ParticleSystem.MinMaxCurve(inSandbox?.88f:.50f,inSandbox?1.30f:.80f);}
        if(smoke&&!bakedOutlet){var v=smoke.velocityOverLifetime;v.enabled=true;v.space=ParticleSystemSimulationSpace.World;Vector3 drift=inSandbox?-trainAnchor.right*speed*.13f:Vector3.right*.04f;v.x=drift.x;v.y=inSandbox?.22f:(.12f+smokePower*.12f);v.z=drift.z;var sm=smoke.main;sm.startSpeed=new ParticleSystem.MinMaxCurve(inSandbox?.20f:.28f,inSandbox?.34f:.46f);var sn=smoke.noise;sn.strength=inSandbox?.055f:.11f;}
        // With no smokebox, each tube mouth vents separately. Never emit through the closed firebox crown.
        if(smoke&&!chimney&&!inSandbox&&smokePower>.01f&&effectDt>0){openExhaustAccumulator+=effectDt*60*smokePower;
            if(!smoke.isPlaying)smoke.Play();while(openExhaustAccumulator>=1){openExhaustAccumulator--;int h=(openExhaustHole++*13)%33;float y=h<30?1.72f+(h/6)*.14f:2.45f,z=h<30?-.41f+(h%6)*.145f:-.22f+(h-30)*.22f;float variation=Mathf.PerlinNoise(openExhaustHole*.719f,1.3f);
                var e=new ParticleSystem.EmitParams{position=engine.locomotive.TransformPoint(new Vector3(BoilerGasConnected?2.32f:-.64f,y,z)),velocity=engine.locomotive.TransformDirection(new Vector3(.17f+variation*.12f,.025f+variation*.04f,0)),startSize=(h<30?.09f:.15f)*(1+variation*.7f),startLifetime=1.7f+variation*.8f,startColor=new Color(.39f,.40f,.41f,.43f)};smoke.Emit(e,1);}}
        if(paused&&!inSandbox){if(!bakedOutlet)smoke.Pause();steam.Pause();}
    }
    void PositionExhaust(){if(!smoke)return;smoke.transform.localPosition=ChimneyConnected?new Vector3(2.7f,3.7f,0):new Vector3(BoilerGasConnected?2.32f:-.64f,2.1f,0);}
}
