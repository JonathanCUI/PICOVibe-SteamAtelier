using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DefaultExecutionOrder(1000)]
public sealed class LocomotiveXRQuality : MonoBehaviour {
 public enum Detail {Automatic,Standalone,VisionPro}
 public Detail quality=Detail.Automatic;
 public UniversalRenderPipelineAsset standardPipeline,finePipeline;
 public bool adaptive=true;
 public bool forceNearForAudit;
 public int activeMeshes,activeTriangles,activeParticles;
 public string DisplayName=>High?"精细":"标准";
 public bool High=>quality==Detail.VisionPro||(quality==Detail.Automatic&&Application.platform!=RuntimePlatform.Android);
 public float SmokeRate(float rpm)=>High?65+rpm*.55f:45+rpm*.35f;
 static readonly HashSet<LocomotiveXRMesh> live=new HashSet<LocomotiveXRMesh>();
 static LocomotiveXRQuality current;
 LocomotiveController engine;LocomotiveFreeAssembly assembly;LocomotiveThermalEffects thermal;
 ParticleSystem[] particles;readonly Dictionary<ParticleSystem,int> originalCaps=new Dictionary<ParticleSystem,int>();
 float next;int shelfFrame=-1;bool tabletCaptured;
 RenderPipelineAsset previousPipeline;
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetStatics(){live.Clear();current=null;}
 public static void Register(LocomotiveXRMesh m){live.Add(m);if(current)current.Choose(m);}
 public static void Unregister(LocomotiveXRMesh m){live.Remove(m);}
 void Awake(){current=this;previousPipeline=QualitySettings.renderPipeline;engine=GetComponent<LocomotiveController>();assembly=GetComponent<LocomotiveFreeAssembly>();thermal=GetComponent<LocomotiveThermalEffects>();}
 void Start(){engine.adaptiveDetail=true;Configure();Refresh();MarkShelfDirty();}
 public void SetQuality(Detail value){quality=value;Configure();Refresh();MarkShelfDirty();tabletCaptured=false;}
 public void Toggle(){SetQuality(High?Detail.Standalone:Detail.VisionPro);}
 public static void MarkShelfDirty(){if(current)current.shelfFrame=Time.frameCount+1;}
 void Configure(){
  var pipeline=High?finePipeline:standardPipeline;if(pipeline&&QualitySettings.renderPipeline!=pipeline)QualitySettings.renderPipeline=pipeline;
  particles=engine.locomotive.GetComponentsInChildren<ParticleSystem>(true);
  foreach(var ps in particles){var main=ps.main;if(!originalCaps.ContainsKey(ps))originalCaps[ps]=main.maxParticles;
   int cap=ps==thermal.smoke?(High?320:200):(High?140:90);main.maxParticles=Mathf.Min(originalCaps[ps],cap);
   var r=ps.GetComponent<ParticleSystemRenderer>();if(r){r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;}
  }
  QualitySettings.shadows=UnityEngine.ShadowQuality.HardOnly;QualitySettings.shadowDistance=High?16:10;
 }
 public void Refresh(){foreach(var m in live)if(m)Choose(m);}
 void Choose(LocomotiveXRMesh m){if(!m.filter||m.levels==null||m.levels.Length==0)return;
  int level=High?0:1;
  bool auxiliary=m.gameObject.layer==29||m.gameObject.layer==30;
  bool hint=false;for(var t=m.transform;t!=null;t=t.parent)if(t.name=="同规格安装位置"||t.name=="整机参考轮廓"){hint=true;break;}
  if(auxiliary)level=3;else if(hint)level=2;
  else if(adaptive&&!forceNearForAudit&&engine.studioCamera&&m.visual){
   var cam=engine.studioCamera.transform.position;float d=Vector3.Distance(cam,m.visual.bounds.ClosestPoint(cam));
   float middle=m.track?7:7.5f,far=m.track?18:15;
   // Separate up/down thresholds prevent repeated mesh switching near a boundary.
   if(m.currentLevel>=3)far-=.6f;if(m.currentLevel>=2)middle-=.4f;
   if(d>far)level=3;else if(d>middle)level=2;
  }
  m.Select(level);
 }
 void LateUpdate(){
  if(Time.unscaledTime>=next){next=Time.unscaledTime+.20f;Refresh();}
  // Static UI previews need rendering only when their content changes.
  if(assembly.shelfCamera){assembly.shelfCamera.enabled=false;if(shelfFrame>=0&&Time.frameCount>shelfFrame){Refresh();RenderPreview(assembly.shelfCamera);shelfFrame=-1;}}
  if(!tabletCaptured&&Time.frameCount>5){foreach(var c in UnityEngine.Object.FindObjectsOfType<Camera>())if(c.name=="平板预览相机"){RenderPreview(c);c.enabled=false;tabletCaptured=true;break;}}
  if(thermal.smoke){var emission=thermal.smoke.emission;emission.rateOverTime=thermal.fireEnabled&&thermal.HasModule(5)?SmokeRate(engine.rpm):0;}
 }
 static void RenderPreview(Camera camera){var request=new UniversalRenderPipeline.SingleCameraRequest{destination=camera.targetTexture};RenderPipeline.SubmitRenderRequest(camera,request);}
 public void Sample(){activeMeshes=activeTriangles=activeParticles=0;foreach(var m in live)if(m&&m.visual&&m.visual.enabled&&m.gameObject.activeInHierarchy){activeMeshes++;var mesh=m.filter.sharedMesh;for(int i=0;i<mesh.subMeshCount;i++)activeTriangles+=(int)mesh.GetIndexCount(i)/3;}if(particles!=null)foreach(var ps in particles)if(ps&&ps.gameObject.activeInHierarchy)activeParticles+=ps.particleCount;}
 void OnDestroy(){if(current==this){QualitySettings.renderPipeline=previousPipeline;current=null;}}
}
