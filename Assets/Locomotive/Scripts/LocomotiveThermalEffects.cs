using System;
using UnityEngine;
[Serializable] public class SteamFlowPath {
 public string name; public Vector3[] points; public Transform[] arrows; public int module; public int side=-1; public int gate; public float speed=1.4f; public float offset;
}
public class LocomotiveThermalEffects : MonoBehaviour {
 public LocomotiveFreeAssembly freeAssembly; public LocomotiveController engine; public LocomotiveWorkshop workshop;
 public ParticleSystem flames,embers,smoke,exhaust,bubbles,vapour; public Light fireLight;
 public SteamFlowPath[] flows; public Transform fireDoor; public Vector3 fireDoorHome; public Quaternion fireDoorRotation;
 public bool fireEnabled=true,fireDoorOpen; public int exhaustPulses; public float flowClock;
 bool[,] previous=new bool[2,2]; bool initialised; float lastPhase;
 public bool HasModule(int index)=>freeAssembly?freeAssembly.HasSystem(index):!workshop||!workshop.Guided||workshop.installed>index;
 void Emission(ParticleSystem p,bool enabled){if(!p)return;var em=p.emission;em.enabled=enabled;}
 public void ResetPulseTracking(){initialised=false;exhaustPulses=0;}
 public void TickValveEvents(){
  bool moving=engine.running&&HasModule(5)&&HasModule(3)&&!engine.exploded&&!engine.assemblyLesson;
  float delta=Mathf.Abs(Mathf.DeltaAngle(lastPhase,engine.phase));bool continuous=delta<45;lastPhase=engine.phase;
  for(int i=0;i<engine.sides.Length;i++){var p=engine.sides[i].pose;bool[] open={p.LeftExhaust>.001,p.RightExhaust>.001};for(int j=0;j<2;j++){if(initialised&&moving&&continuous&&open[j]&&!previous[i,j]){exhaust.Emit(9);exhaustPulses++;}previous[i,j]=open[j];}}
  initialised=true;
 }
 void LateUpdate(){bool fire=fireEnabled&&HasModule(0)&&!engine.exploded&&!engine.assemblyLesson;
  Emission(flames,fire);Emission(embers,fire);Emission(smoke,fire&&HasModule(5));Emission(bubbles,fire&&engine.principle);Emission(vapour,fire&&engine.principle);
  if(fireLight){fireLight.enabled=fire;fireLight.intensity=1.2f+Mathf.PerlinNoise(Time.time*6,1)*.9f;}
  if(fireDoor&&fireDoorOpen&&!engine.exploded&&!GetComponent<LocomotiveDoorsV11>()){fireDoor.localPosition=fireDoorHome+new Vector3(-.13f,0,.37f);fireDoor.localRotation=Quaternion.Euler(0,65,0)*fireDoorRotation;}
  if(engine.running||!initialised)TickValveEvents();else {initialised=false;}
  bool animate=engine.running||workshop&&workshop.Guided; if(animate)flowClock+=Time.deltaTime;
  if(flows==null)return;foreach(var f in flows){bool on=fire&&engine.principle&&HasModule(f.module);if(f.gate!=0&&f.side>=0){var p=engine.sides[f.side].pose;on &= f.gate==1?p.LeftAdmission>.001:f.gate==2?p.RightAdmission>.001:f.gate==3?p.LeftExhaust>.001:f.gate==4?p.RightExhaust>.001:(p.LeftExhaust>.001||p.RightExhaust>.001);}
   float total=0;for(int i=1;i<f.points.Length;i++)total+=Vector3.Distance(f.points[i-1],f.points[i]);
   for(int k=0;k<f.arrows.Length;k++){var a=f.arrows[k];if(a.gameObject.activeSelf!=on)a.gameObject.SetActive(on);if(!on)continue;float dist=Mathf.Repeat(flowClock*f.speed+total*k/f.arrows.Length+f.offset,total);for(int i=1;i<f.points.Length;i++){float len=Vector3.Distance(f.points[i-1],f.points[i]);if(dist<=len){a.localPosition=Vector3.Lerp(f.points[i-1],f.points[i],dist/len);a.localRotation=Quaternion.LookRotation((f.points[i]-f.points[i-1]).normalized);break;}dist-=len;}}
  }
 }
}

