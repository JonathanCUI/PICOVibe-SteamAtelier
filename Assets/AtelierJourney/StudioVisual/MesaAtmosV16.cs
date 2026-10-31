using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(3400)]
public sealed class MesaAtmosV16 : MonoBehaviour {
 public SteamJourney journey; public MesaDayNightV13 sky; public MesaEnhanceV15 previous;
 public Material steamMaterial; public Mesh volumeMesh; public MesaSteamTunnelV17 tunnel;
 public int ConfinedPuffs {get;private set;} public int BlockedPuffs {get;private set;} public int InvalidCenters {get;private set;}
 public bool FreezeCloudTransforms; public float CloudTimeOverride=-1;
 public int ActivePuffs {get;private set;} public float Evolution {get;private set;}
 public bool CloudUpdatesReady=>block!=null&&isActiveAndEnabled;
 public int SwanHiddenSkipCount{get;private set;}public int SwanHiddenCleanupCount{get;private set;}public int CloudWriteCount{get;private set;}
 const int Capacity=42;
 sealed class Puff {public Transform t;public Renderer r;public float age,life,size,seed;public Vector3 drift,position;public bool alive,confined;public float route,lateral,height,diameter,blocked;public int direction;}
 Puff[] puffs=new Puff[Capacity];Renderer[] clouds;Vector3[] starts,scales;MaterialPropertyBlock block;
 float clock,emit;int serial;bool wasActive,swanHiddenCleaned;
 void OnEnable(){swanHiddenCleaned=false;}
 void Awake(){
  block=new MaterialPropertyBlock();clouds=new Renderer[3];starts=new Vector3[3];scales=new Vector3[3];
  for(int i=0;i<3;i++){clouds[i]=sky.clouds[i].GetComponent<Renderer>();starts[i]=sky.clouds[i].localPosition;scales[i]=sky.clouds[i].localScale;}
  var root=new GameObject("Rolling volumetric exhaust V16");root.transform.SetParent(journey.sandboxRoot.transform,false);
  for(int i=0;i<Capacity;i++){
   var go=new GameObject("Steam puff "+i);go.transform.SetParent(root.transform,false);go.AddComponent<MeshFilter>().sharedMesh=volumeMesh;
   var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=steamMaterial;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;r.enabled=false;
   puffs[i]=new Puff{t=go.transform,r=r};
  }
 }
 void Emit(){
  var p=puffs[serial%Capacity];float a=serial*2.399963f;serial++;p.alive=true;p.age=0;p.life=6.3f+Mathf.Sin(a*1.31f)*.7f;p.size=4.1f+Mathf.Sin(a*1.71f)*.65f;p.seed=Mathf.Repeat(serial*.618034f,1)*7;
  p.position=journey.WorldPointToScene(journey.smoke.transform.position)+new Vector3(Mathf.Cos(a)*.06f,.06f,Mathf.Sin(a)*.06f);
  p.t.rotation=journey.SceneRotationToWorld(Quaternion.Euler(Mathf.Sin(a)*12,a*Mathf.Rad2Deg,Mathf.Cos(a)*10));
  p.drift=-journey.WorldDirectionToScene(journey.trainAnchor.right)*journey.direction*(.20f+Mathf.Min(journey.speed,5)*.12f)+new Vector3(.18f,1.02f,.06f);p.r.enabled=true;p.blocked=0;p.diameter=.24f;p.confined=false;
  bool inBore=tunnel&&tunnel.Inside(p.position,out p.route,out p.lateral);
  bool atCoveredPortal=tunnel&&p.route>tunnel.start-1.2f&&p.route<tunnel.end+1.2f&&Mathf.Abs(p.lateral)<1.25f&&tunnel.Air(p.position)<.96f;
  if(inBore||atCoveredPortal){
   p.confined=true;p.direction=journey.direction==0?1:journey.direction;p.life=3.6f;
   p.height=Mathf.Clamp(p.position.y-tunnel.At(p.route).y,.5f,1.42f);p.lateral=Mathf.Clamp(p.lateral,-.22f,.22f);
   p.position=tunnel.At(p.route)+Vector3.up*p.height+Vector3.Cross(Vector3.up,tunnel.Tangent(p.route))*p.lateral;
   // A portal's rock overhang extends beyond the route markers. Find the
   // actual free space below it instead of spawning inside the rock lip.
   while(tunnel.Air(p.position)<.95f&&p.height>.55f){p.height-=.04f;p.position-=Vector3.up*.04f;}
  }
  if(tunnel&&tunnel.Air(p.position)<.90f){p.alive=false;p.r.enabled=false;BlockedPuffs++;}
 }
 public void ClearSteam(){foreach(var p in puffs){p.alive=false;p.r.enabled=false;}emit=0;}

 void LateUpdate() { using (SwanJourney.SwanJourneyCpuTimings.Measure(SwanJourney.SwanJourneyCpuTimings.Stage.Atmosphere)) CpuMeasuredLateUpdate(); }
    void CpuMeasuredLateUpdate(){
  float dt=Mathf.Min(Time.deltaTime,.06f);clock+=dt;Evolution=CloudTimeOverride>=0?CloudTimeOverride:clock*.40f;
  bool active=journey.inSandbox;
  if(journey.SwanInputActive&&!active){
   if(!swanHiddenCleaned){ClearSteam();ActivePuffs=ConfinedPuffs=InvalidCenters=0;wasActive=false;swanHiddenCleaned=true;SwanHiddenCleanupCount++;}
   SwanHiddenSkipCount++;return;
  }
  swanHiddenCleaned=false;
  for(int i=0;i<clouds.Length;i++){
   var t=sky.clouds[i];float seed=i*2.37f;
   t.localPosition=starts[i]+(FreezeCloudTransforms?Vector3.zero:new Vector3(Mathf.Sin(clock*.025f+seed)*.55f,Mathf.Sin(clock*.14f+seed)*.07f,Mathf.Cos(clock*.023f+seed)*.25f));
   t.localScale=scales[i];block.Clear();block.SetFloat("_Phase",Evolution+seed);clouds[i].SetPropertyBlock(block);CloudWriteCount++;
  }
  if(active){emit+=dt*(5.0f+Mathf.Clamp01(journey.speed/4)*.8f);while(emit>=1){Emit();emit--;}}
  else {emit=0;if(wasActive)foreach(var p in puffs){p.alive=false;p.r.enabled=false;}}
  wasActive=active;ActivePuffs=0;ConfinedPuffs=0;InvalidCenters=0;
  foreach(var p in puffs){if(!p.alive)continue;p.age+=dt;if(p.age>=p.life){p.alive=false;p.r.enabled=false;continue;}
   float u=p.age/p.life;float curl=clock*.8f+p.seed;
   Vector3 next=p.position;
   if(p.confined){
    p.route+=p.direction*(.65f+Mathf.Min(journey.speed,2.8f)*.3f)*dt;
    p.height=Mathf.Min(1.48f,p.height+dt*.16f);
    next=tunnel.At(p.route)+Vector3.up*p.height+Vector3.Cross(Vector3.up,tunnel.Tangent(p.route))*(p.lateral+Mathf.Sin(curl)*.055f);
    if(p.route<tunnel.start-.10f||p.route>tunnel.end+.10f){p.confined=false;p.drift=tunnel.Tangent(p.route)*p.direction*.55f+Vector3.up*.95f;}
   }else next+=(p.drift+new Vector3(Mathf.Sin(curl)*.20f,Mathf.Sin(curl*.71f)*.10f,Mathf.Cos(curl*.91f)*.16f))*dt;
   // Stop and dissipate an outdoor puff at rock rather than letting its centre
   // climb through the mountain and reappear above it. The shader clips its edges.
   if(tunnel&&tunnel.Air(next)<.90f){if(p.blocked==0){BlockedPuffs++;p.blocked=.0001f;}}
   else if(p.blocked==0)p.position=next;
   if(p.blocked>0){p.blocked+=dt;if(p.blocked>.50f){p.alive=false;p.r.enabled=false;continue;}}
   float growth=Mathf.Lerp(.24f,p.size,Mathf.Pow(u,.65f));
   if(p.confined){growth=Mathf.Min(growth,.72f);ConfinedPuffs++;}
   p.diameter=Mathf.MoveTowards(p.diameter,growth,dt*1.6f);
   p.t.position=journey.ScenePointToWorld(p.position);p.t.localScale=new Vector3(p.diameter*1.05f,p.diameter,p.diameter*.94f);
   if(tunnel&&tunnel.Air(p.position)<.5f)InvalidCenters++;
   float fade=Mathf.SmoothStep(0,1,u/.065f)*(1-Mathf.SmoothStep(.62f,1,u));
   fade*=1-Mathf.Clamp01(p.blocked/.5f);if(p.confined)fade*=.68f;
   block.Clear();block.SetFloat("_Seed",p.seed);block.SetFloat("_Phase",p.age*.64f+p.seed);block.SetFloat("_Opacity",fade*1.18f);p.r.SetPropertyBlock(block);ActivePuffs++;
  }
 }
}
