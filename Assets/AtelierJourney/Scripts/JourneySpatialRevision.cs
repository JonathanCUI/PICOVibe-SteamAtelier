using System;
using TMPro;
using System.Linq;
using UnityEngine;

[Serializable] public sealed class JourneyGhostPair { public Renderer source,hull,edges; }
public sealed partial class SteamJourney {
 // Optional common placement root. With no root the authored desktop coordinates are unchanged.
 public Transform spatialRoot;
 public Matrix4x4 SpatialWorldToLocal=>spatialRoot?spatialRoot.worldToLocalMatrix:Matrix4x4.identity;
 public float SpatialScale=>spatialRoot?Mathf.Max(.0001f,Mathf.Abs(spatialRoot.lossyScale.x)):1f;
 public Vector3 ScenePointToWorld(Vector3 point)=>spatialRoot?spatialRoot.TransformPoint(point):point;
 public Vector3 WorldPointToScene(Vector3 point)=>spatialRoot?spatialRoot.InverseTransformPoint(point):point;
 public Vector3 SceneVectorToWorld(Vector3 vector)=>spatialRoot?spatialRoot.TransformVector(vector):vector;
 public Vector3 SceneDirectionToWorld(Vector3 direction)=>spatialRoot?spatialRoot.TransformDirection(direction):direction;
 public Vector3 WorldDirectionToScene(Vector3 direction)=>spatialRoot?spatialRoot.InverseTransformDirection(direction):direction;
 public Quaternion SceneRotationToWorld(Quaternion rotation)=>spatialRoot?spatialRoot.rotation*rotation:rotation;
 public int inspectionMode=1; // 0 opaque, 1 ghost/structural edges, 2 open section.
 public JourneyGhostPair[] inspectionGhosts;
 public Renderer[] inspectionBores;
 public Renderer[] valveMist;
 MaterialPropertyBlock valveFxBlock;
 public Transform[] valveRockers,valveInputLinks,valveOutputLinks,compressorPistons;
 public Transform airNeedle,pipeNeedle; public TMP_Text airReadout;
 public float reservoirPressure=0,brakePipePressure=0,auxiliaryPressure=0;
 public float BrakeCylinderPressure {get;private set;}
 public float CompressorRate {get;private set;}
 float compressorTime; bool compressorDemand=true;
 readonly float[] pistonValveShift=new float[2];
 JourneyModule pendingDetach; Vector2 detachPointer; bool snapReady;
 public bool SnapReady=>snapReady;
 public static float SolvePistonValve(float input,out Vector3 inputPin,out Vector3 outputPin){
  // Equal-arm reversing rocker converts the previous outside-admission lever to inside admission.
  // Both connecting rods retain fixed length; the upper valve never uses an unrelated sine clock.
  const float pivot=2.85f,r=.24f,length=.403f; float a=Mathf.Asin(Mathf.Clamp((input-2.447f)/r,-.9f,.9f));
  for(int n=0;n<14;n++){float x=pivot+r*Mathf.Sin(a)-input,z=r*(Mathf.Cos(a)-1);float f=x*x+z*z-length*length;float derivative=2*x*r*Mathf.Cos(a)-2*z*r*Mathf.Sin(a);a-=f/(Mathf.Abs(derivative)<1e-5f?1e-5f:derivative);}
  inputPin=new Vector3(pivot+r*Mathf.Sin(a),1.08f,1.325f+r*Mathf.Cos(a));
  outputPin=new Vector3(pivot-r*Mathf.Sin(a),1.08f,1.325f-r*Mathf.Cos(a));
  return outputPin.x+Mathf.Sqrt(.30f*.30f-Mathf.Pow(outputPin.z-1.085f,2))-3.15f;
 }
 public static float ValveOpening(float shift,int port){
  // Sleeve ports occupy [3.02,3.06] and [3.24,3.28]. Two .08-wide heads sit at +/- .13.
  // Inner edges admit live steam from the middle; outer edges release to the end exhaust cavities.
  return Mathf.Clamp(port==1?-shift-.04f:port==2?shift-.04f:port==3?shift:-shift,0,.04f);
 }
 public bool ValvePortOpen(int side,int port)=>ValveOpening(pistonValveShift[Mathf.Clamp(side,0,1)],port)>.001f;
 void SpatialLink(Transform t,Vector3 a,Vector3 b){if(!t)return;t.localPosition=a;t.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);t.localScale=new Vector3(1,Vector3.Distance(a,b),1);}
 void AnimatePistonValves(){
  for(int i=0;i<engine.sides.Length;i++){var s=engine.sides[i];Vector3 a,b;float d=SolvePistonValve((float)s.pose.V.x,out a,out b);pistonValveShift[i]=d;a.z*=s.sign;b.z*=s.sign;
   s.valve.localPosition=new Vector3(3.15f+d,1.08f,s.sign*1.085f);
   if(valveRockers!=null&&i<valveRockers.Length){SpatialLink(valveRockers[i],a,b);SpatialLink(valveInputLinks[i],new Vector3((float)s.pose.V.x,1.08f,s.sign*1.565f),a);SpatialLink(valveOutputLinks[i],b,s.valve.localPosition);}
  }
  if(valveMist!=null&&valveMist.Length==3){float x=3.15f+pistonValveShift[0];ValveMist(valveMist[0],x-.09f,x+.09f,Delivery*.40f,false);ValveMist(valveMist[1],2.81f,x-.17f,ValvePortOpen(0,3)?CylinderPower*.18f:0,true);ValveMist(valveMist[2],x+.17f,3.49f,ValvePortOpen(0,4)?CylinderPower*.18f:0,true);}
 }
 readonly float[] valveDensity=new float[3],valveVent=new float[3];
 void ValveMist(Renderer r,float a,float b,float density,bool exhaust){
  int index=Array.IndexOf(valveMist,r);float dt=paused?0:Mathf.Min(Time.deltaTime,.05f)*(slow?.25f:1);
  valveDensity[index]=Mathf.Lerp(valveDensity[index],density,1-Mathf.Exp(-dt*2));valveVent[index]=Mathf.Lerp(valveVent[index],exhaust?1:0,1-Mathf.Exp(-dt*2));
  r.enabled=!inSandbox&&cutaway&&modules[4].installed&&b>a;r.transform.localPosition=new Vector3((a+b)*.5f,1.08f,1.085f);r.transform.localScale=new Vector3(Mathf.Max(.001f,b-a),.225f,.225f);
  var block=valveFxBlock??(valveFxBlock=new MaterialPropertyBlock());block.Clear();block.SetFloat("_Density",valveDensity[index]);block.SetFloat("_Front",Mathf.Clamp01(valveDensity[index]/.3f));block.SetFloat("_FlowTime",FlowTime);block.SetFloat("_Direction",index==2?-1:1);block.SetFloat("_Exhaust",valveVent[index]);r.SetPropertyBlock(block);
 }
 void Pneumatics(float dt){
  bool supplied=modules[6].installed&&(inSandbox?pressure>1:Delivery>.12f);
  if(reservoirPressure<6.6f)compressorDemand=true;if(reservoirPressure>8.0f)compressorDemand=false;
  CompressorRate=Mathf.MoveTowards(CompressorRate,supplied&&compressorDemand?Mathf.Clamp01(pressure/5):0,dt*.8f);
  if(!paused||inSandbox){compressorTime+=dt*CompressorRate;reservoirPressure=Mathf.Clamp(reservoirPressure+dt*(CompressorRate*.62f-.003f),0,8.1f);
   float pipeTarget=Mathf.Min(reservoirPressure,5*(1-brake));float old=brakePipePressure;brakePipePressure=Mathf.MoveTowards(old,pipeTarget,dt*(brake>.01f?2.6f:.75f));
   reservoirPressure=Mathf.Max(0,reservoirPressure-Mathf.Max(0,brakePipePressure-old)*.15f);
   if(brake<.05f&&brakePipePressure>auxiliaryPressure)auxiliaryPressure=Mathf.MoveTowards(auxiliaryPressure,brakePipePressure,dt*.65f);
   float before=BrakeCylinderPressure;float target=brake<.01f?0:Mathf.Clamp((auxiliaryPressure-brakePipePressure)*2.5f,0,3.5f);
   BrakeCylinderPressure=Mathf.MoveTowards(before,target,dt*2.5f);auxiliaryPressure=Mathf.Max(0,auxiliaryPressure-Mathf.Max(0,BrakeCylinderPressure-before)*.08f);}
  if(compressorPistons!=null)for(int i=0;i<compressorPistons.Length;i++)if(compressorPistons[i])compressorPistons[i].localPosition=new Vector3(0,Mathf.Sin(compressorTime*5.2f+i*Mathf.PI)*.10f,0);
  if(airNeedle)airNeedle.localRotation=Quaternion.Euler(0,0,125-reservoirPressure/10*250);
  if(pipeNeedle)pipeNeedle.localRotation=Quaternion.Euler(0,0,125-brakePipePressure/10*250);
  if(airReadout)airReadout.text="总风 "+reservoirPressure.ToString("0.0")+"  /  列车管 "+brakePipePressure.ToString("0.0")+" bar";
 }
 void UpdateInspection(){
  bool ghost=!inSandbox&&cutaway&&inspectionMode==1;
  if(inspectionGhosts!=null)foreach(var pair in inspectionGhosts){bool show=ghost&&pair.source&&pair.source.gameObject.activeInHierarchy;if(pair.hull)pair.hull.enabled=show;if(pair.edges)pair.edges.enabled=show;}
  if(inspectionBores!=null)foreach(var r in inspectionBores)if(r)r.enabled=!ghost;
 }
 public bool DetachModule(JourneyModule m){
  if(inSandbox||AutoAssembling||m==null||!m.installed||!m.preview)return false;
  CancelCompletion();
  ImpactWater(m,.35f);m.installed=false;InvalidateDetachedFlow(m);m.parked=true;m.verified=false;m.observed=0;
  var tr=m.preview.transform;tr.position=trainAnchor.TransformPoint(m.previewCentre);tr.rotation=trainAnchor.rotation;tr.localScale=Vector3.one;
  stage=modules.TakeWhile(q=>q.installed).Count();if(!Teaching)finishIndex=Mathf.Max(0,Array.IndexOf(finishing,m));
  // The verification latch belongs to the player. Stored heat, steam and wheel inertia decay locally.
  ApplyVisibility();RefreshPreview();SaveProgress();Notify("已拆下 "+m.title+" · 可暂放、旋转，靠近接口松手装回。");return true;
 }
 JourneyModule HitInstalled(Vector2 pointer){
  if(inSandbox)return null;
  foreach(var hit in Physics.RaycastAll(view.ScreenPointToRay(pointer),200).OrderBy(h=>h.distance)){
   if(hit.collider.GetComponentInParent<JourneyControl>() is JourneyControl c&&c.caption!="Inspection shell"&&c.caption!="Ember")continue;
   foreach(var m in modules.Concat(finishing).Where(q=>q.installed))if(m.objects.Any(o=>o&&hit.transform.IsChildOf(o.transform)))return m;
  }return null;
 }
 void MagneticPreview(Vector3 raw){
  if(!held||heldModule==null)return;var target=trainAnchor.TransformPoint(heldModule.previewCentre);
  var localRaw=trainAnchor.InverseTransformPoint(raw);
  float range=Mathf.Max(.62f,heldModule.snapRadius);float dist=Vector3.Distance(localRaw,heldModule.previewCentre);
  snapReady=(dist<range||Vector3.Distance(localRaw,heldModule.socket)<.65f)&&(heldModule==Current||Array.IndexOf(finishing,heldModule)>=0&&!Teaching);
  held.position=snapReady?Vector3.Lerp(raw,target,.68f):raw;
  held.localScale=Vector3.Lerp(held.localScale,Vector3.one,Mathf.Clamp01(Time.unscaledDeltaTime*14));
  if(snapReady){held.rotation=Quaternion.Slerp(held.rotation,trainAnchor.rotation,.22f);if(trayState)trayState.text="接口已对齐 · 松手装入";}
  if(aimRing)aimRing.localScale=Vector3.one*(snapReady?1.18f:1);
 }
}
