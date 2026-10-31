using UnityEngine;using TMPro;
[DefaultExecutionOrder(3600)]
public sealed class StudioSpatialRefineV19:MonoBehaviour {
 public SteamJourney journey;public MesaDayNightV13 sky;
 public Transform face,throttleSlide,directionPivot,brakePivot,returnCover,brakeNeedle;
 public TMP_Text throttleReadout,driveMessage,returnLabel;
 public Renderer[] passengerWindows;public Light cabLight;public GameObject[] scenery;
 public bool ReturnOpen{get;private set;}public float WindowGlow{get;private set;}
 public readonly int[] CoachSparkCounts=new int[3];
 MaterialPropertyBlock windowBlock;float sparkCredit,cabRange;int sparkContact;bool lastMode;ParticleSystem coachSparks;
 bool swanDisplayHidden,displayTextReady;int textThrottle,textDirection,textLap;bool textBrake,textReturn;
 void Awake(){if(cabLight)cabRange=cabLight.range;if(!sky)sky=FindObjectOfType<MesaDayNightV13>(true);}
 public void ToggleReturn(){ReturnOpen=!ReturnOpen;}
 public bool TryReturn(){if(!ReturnOpen)return false;ReturnOpen=false;return true;}
 public void DragThrottle(Vector2 pointer){
  var ray=journey.view.ScreenPointToRay(pointer);var plane=new Plane(face.up,face.TransformPoint(new Vector3(0,.45f,0)));
  if(plane.Raycast(ray,out float d)){var p=face.InverseTransformPoint(ray.GetPoint(d));journey.throttle=Mathf.Clamp01((.56f-p.z)/1.04f);}
 }
 void LateUpdate() { using (SwanJourney.SwanJourneyCpuTimings.Measure(SwanJourney.SwanJourneyCpuTimings.Stage.Refine)) CpuMeasuredLateUpdate(); }
    void CpuMeasuredLateUpdate(){
  if(!journey)return;
  // The workbench does not display the driving desk or passenger windows.
  // Keep this Swan-only: desktop continues to execute its original display path.
  if(journey.SwanInputActive&&!journey.inSandbox){
   if(!swanDisplayHidden){HideDrivingDisplay();swanDisplayHidden=true;}
   return;
  }
  bool refresh=swanDisplayHidden;swanDisplayHidden=false;
  foreach(var o in scenery)if(o&&o.activeSelf)o.SetActive(false);
  if(lastMode!=journey.inSandbox){ReturnOpen=false;lastMode=journey.inSandbox;}
  throttleSlide.localPosition=new Vector3(2.05f,.09f,Mathf.Lerp(.56f,-.48f,journey.throttle));throttleSlide.localRotation=Quaternion.identity;
  directionPivot.localRotation=Quaternion.Euler(journey.direction>0?-23:23,0,0);
  brakePivot.localRotation=Quaternion.Euler(journey.brake>.5f?25:-25,0,0);
  returnCover.localRotation=Quaternion.Slerp(returnCover.localRotation,Quaternion.Euler(ReturnOpen?-108:0,0,0),1-Mathf.Exp(-Time.unscaledDeltaTime*14));
  if(brakeNeedle)brakeNeedle.localRotation=Quaternion.Euler(0,0,125-Mathf.Clamp01(journey.BrakeCylinderPressure/3.5f)*250);
  UpdateDisplayText(refresh);
  float night=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.10f,.68f,sky.Daylight));WindowGlow=journey.inSandbox?Mathf.Lerp(.08f,2.6f,night):0;
  if(windowBlock==null)windowBlock=new MaterialPropertyBlock();windowBlock.Clear();windowBlock.SetColor("_EmissionColor",new Color(1,.60f,.25f)*WindowGlow);
  foreach(var r in passengerWindows)if(r)r.SetPropertyBlock(windowBlock);
  if(cabLight){cabLight.enabled=journey.inSandbox;cabLight.intensity=night*1.25f;cabLight.range=cabRange*journey.SpatialScale;}
  EmitCoachSparks(Mathf.Min(Time.deltaTime,.05f));
 }
 void HideDrivingDisplay(){
  foreach(var o in scenery)if(o&&o.activeSelf)o.SetActive(false);
  ReturnOpen=false;lastMode=false;WindowGlow=0;sparkCredit=0;displayTextReady=false;
  if(returnCover)returnCover.localRotation=Quaternion.identity;
  if(cabLight){cabLight.enabled=false;cabLight.intensity=0;}
  if(coachSparks)coachSparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
  if(windowBlock==null)windowBlock=new MaterialPropertyBlock();windowBlock.Clear();windowBlock.SetColor("_EmissionColor",Color.black);
  foreach(var r in passengerWindows)if(r)r.SetPropertyBlock(windowBlock);
  if(driveMessage&&driveMessage.text.Length>0)driveMessage.text="";
 }
 void UpdateDisplayText(bool force){
  if(!journey.SwanInputActive){
   throttleReadout.text="汽门  "+Mathf.RoundToInt(journey.throttle*100)+" %";returnLabel.text=ReturnOpen?"按下返回工坊":"掀盖 · 返回工坊";
   driveMessage.text=journey.inSandbox?(journey.direction>0?"前进":"后退")+"   ·   "+(journey.brake>.5f?"制动":"松开")+"   ·   第 "+(journey.laps+1)+" 圈":"";
   displayTextReady=false;return;
  }
  int throttle=Mathf.RoundToInt(journey.throttle*100),direction=journey.direction>0?1:-1,lap=journey.laps;
  bool braking=journey.brake>.5f;force|=!displayTextReady;
  if(force||textThrottle!=throttle)throttleReadout.text="汽门  "+throttle+" %";
  if(force||textReturn!=ReturnOpen)returnLabel.text=ReturnOpen?"按下返回工坊":"掀盖 · 返回工坊";
  if(force||textDirection!=direction||textBrake!=braking||textLap!=lap)
   driveMessage.text=(direction>0?"前进":"后退")+"   ·   "+(braking?"制动":"松开")+"   ·   第 "+(lap+1)+" 圈";
  textThrottle=throttle;textDirection=direction;textBrake=braking;textLap=lap;textReturn=ReturnOpen;displayTextReady=true;
 }
 void EmitCoachSparks(float dt){
  if(!journey.inSandbox||journey.HardBrakeIntensity<.01f||journey.speed<.035f||!journey.sparks){sparkCredit=0;return;}
  if(!coachSparks){
   // A separate world-space pool avoids the locomotive's inspection/visibility ownership.
   var o=new GameObject("Coach wheel rail contact sparks V19");o.transform.SetParent(journey.sandboxRoot.transform,false);coachSparks=o.AddComponent<ParticleSystem>();coachSparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
   var main=coachSparks.main;main.playOnAwake=false;main.loop=true;main.simulationSpace=ParticleSystemSimulationSpace.World;main.scalingMode=ParticleSystemScalingMode.Shape;main.maxParticles=256;main.startSpeed=0;main.gravityModifier=journey.spatialRoot?0:.10f;if(journey.spatialRoot){main.simulationSpace=ParticleSystemSimulationSpace.Local;main.scalingMode=ParticleSystemScalingMode.Hierarchy;var force=coachSparks.forceOverLifetime;force.enabled=true;force.space=ParticleSystemSimulationSpace.Local;force.y=-.981f;}
   var em=coachSparks.emission;em.enabled=false;var sh=coachSparks.shape;sh.enabled=false;
   var co=coachSparks.colorOverLifetime;co.enabled=true;var g=new Gradient();g.SetKeys(new[]{new GradientColorKey(new Color(1,.95f,.70f),0),new GradientColorKey(new Color(1,.42f,.05f),.65f)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(.9f,.55f),new GradientAlphaKey(0,1)});co.color=g;
   var sz=coachSparks.sizeOverLifetime;sz.enabled=true;sz.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,1,1,.1f));
   var rr=o.GetComponent<ParticleSystemRenderer>();rr.sharedMaterial=journey.sparks.GetComponent<ParticleSystemRenderer>().sharedMaterial;rr.renderMode=ParticleSystemRenderMode.Stretch;rr.lengthScale=3;rr.velocityScale=.12f;rr.minParticleSize=.002f;rr.maxParticleSize=.018f;rr.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;coachSparks.Play();
  }
  if(!coachSparks.isPlaying)coachSparks.Play();sparkCredit+=dt*300*journey.HardBrakeIntensity;
  while(sparkCredit>=1){sparkCredit--;int k=sparkContact++%12,coach=k/4;var tr=journey.coaches[coach];float side=k%2==0?-1:1;
   float u=Mathf.Repeat(sparkContact*.6180339f,1),v=Mathf.Repeat(sparkContact*.4142135f,1);
   var p=new Vector3(k%4<2?-.572f:.572f,.035f,side*.198f);
   var e=new ParticleSystem.EmitParams{position=tr.TransformPoint(p),velocity=tr.TransformDirection(new Vector3(-journey.direction*(.40f+u*.32f),.36f+v*.28f,side*.25f)),startLifetime=.18f+v*.20f,startSize=.018f+u*.014f,startColor=new Color(1,.65f+u*.25f,.18f,1)};
   if(journey.spatialRoot){e.position=coachSparks.transform.InverseTransformPoint(e.position);e.velocity=coachSparks.transform.InverseTransformVector(e.velocity*journey.SpatialScale);}
   coachSparks.Emit(e,1);CoachSparkCounts[coach]++;
  }
 }
}
