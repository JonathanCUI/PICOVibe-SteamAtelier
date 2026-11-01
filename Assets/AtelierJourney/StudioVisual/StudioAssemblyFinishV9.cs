using System;using System.Linq;using System.Collections.Generic;using UnityEngine;
// A temporary gold sweep uses the existing surfaces; original materials are restored exactly.
public sealed class StudioAssemblyFinishV9:MonoBehaviour {
 public float Duration=2.1f;public Material pulseLineMaterial;public int RingCount=>rings.Count;public Shader finishShader;public Material sparkleMaterial;public int SparkCount{get;private set;}public Vector3 SweepAxis{get;private set;}public AudioClip servo,seat,pawl,sweep;
 public Action<Transform> nativeBegin;public Action<float> nativeProgress;public Action nativeRestore;bool nativeActive;
 public bool Active=>nativeActive||originals.Count>0;public float Progress{get;private set;}public int SoundEvents{get;private set;}
 readonly Dictionary<Renderer,Material[]> originals=new Dictionary<Renderer,Material[]>();
 readonly Dictionary<Material,Material[]> copies=new Dictionary<Material,Material[]>();AudioSource sound;Vector4 range;ParticleSystem sparks;readonly List<Vector3> seeds=new List<Vector3>();int emitted;readonly List<LineRenderer> rings=new List<LineRenderer>();
 public void Sound(int kind,float gain=1,float pitch=1){if(!sound){sound=gameObject.AddComponent<AudioSource>();sound.playOnAwake=false;sound.spatialBlend=0;sound.volume=.55f;}var clip=kind==0?servo:kind==1?seat:kind==2?pawl:sweep;if(!clip)return;sound.pitch=pitch;sound.PlayOneShot(clip,gain);SoundEvents++;}
 public void Begin(Transform train){Restore();if(nativeBegin!=null){nativeActive=true;nativeBegin(train);Sound(3,.65f,1.4f);return;}if(!finishShader)return;Bounds bounds=new Bounds();bool first=true;
  foreach(var r in train.GetComponentsInChildren<MeshRenderer>()){
   var mats=r.sharedMaterials;if(!r.enabled||mats.Length==0||mats.Any(m=>!m||!(m.shader.name=="Universal Render Pipeline/Lit"||m.shader.name=="SteamJourney/StudioMechanical")||m.GetFloat("_Surface")>.5f))continue;
   if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);originals[r]=mats;
   string label=(r.name+" "+r.transform.parent.name).ToLowerInvariant();
   int role=new[]{"rod","pipe","rail","wheel","tyre","pin","coupling","hub"}.Any(label.Contains)?1:0;
   var replacement=new Material[mats.Length];
   for(int k=0;k<mats.Length;k++){var m=mats[k];if(!copies.TryGetValue(m,out var pair)){pair=new Material[2];copies.Add(m,pair);}if(!pair[role]){
    var c=new Material(m){shader=finishShader,name=m.name+" · temporary champagne gold sweep"};c.shaderKeywords=Array.Empty<string>();
    c.SetFloat("_HasNormal",m.IsKeywordEnabled("_NORMALMAP")?1:0);c.SetFloat("_HasMetalMap",m.IsKeywordEnabled("_METALLICSPECGLOSSMAP")?1:0);c.SetFloat("_HasOcclusion",m.IsKeywordEnabled("_OCCLUSIONMAP")?1:0);c.SetFloat("_HasEmission",m.IsKeywordEnabled("_EMISSION")?1:0);
    c.SetFloat("_PulseRole",role==1?1:.16f);c.SetFloat("_CavityEnabled",m.shader.name=="SteamJourney/StudioMechanical"?1:0);pair[role]=c;
   }replacement[k]=pair[role];}r.sharedMaterials=replacement;

  }SweepAxis=(train.right-train.up*.35f).normalized;float min=float.PositiveInfinity,max=float.NegativeInfinity;
  for(int k=0;k<8;k++){Vector3 c=bounds.center+Vector3.Scale(bounds.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1));float d=Vector3.Dot(c,SweepAxis);min=Mathf.Min(min,d);max=Mathf.Max(max,d);}
  range=new Vector4(min,Mathf.Max(.1f,max-min),0,0);Shader.SetGlobalVector("_AssemblySweepAxis",SweepAxis);CreateSparks();CreateRings(train);SetProgress(0);Sound(3,.65f,1.4f);
 }
 public void SetProgress(float p){Progress=Mathf.Clamp01(p);if(nativeActive){nativeProgress?.Invoke(Progress);return;}range.z=Progress;UpdateRings(p);Shader.SetGlobalVector("_AssemblyGoldRange",range);
  if(sparks&&p>.18f&&p<.84f){float head=Mathf.Lerp(-.06f,1.06f,Mathf.InverseLerp(.18f,.78f,p));while(emitted<seeds.Count&&(Vector3.Dot(seeds[emitted],SweepAxis)-range.x)/range.y<=head){Vector3 position=seeds[emitted];float spin=emitted*2.39996f;var e=new ParticleSystem.EmitParams{position=position,velocity=new Vector3(Mathf.Cos(spin)*.23f,.3f+(emitted%3)*.07f,Mathf.Sin(spin)*.23f),startLifetime=.28f+(emitted%3)*.07f,startSize=.022f+(emitted%4)*.006f,startColor=new Color(1,.86f,.43f,1),rotation=emitted*47};sparks.Emit(e,1);emitted++;SparkCount++;}}
 }
 void CreateSparks(){emitted=SparkCount=0;seeds.Clear();if(!sparkleMaterial)return;
  var rs=originals.Keys.OfType<MeshRenderer>().Where(r=>r.GetComponent<MeshFilter>()&&r.bounds.size.magnitude>.08f).ToArray();
  for(int k=0;k<20&&rs.Length>0;k++){var r=rs[(k*71+13)%rs.Length];var mesh=r.GetComponent<MeshFilter>().sharedMesh;var vs=mesh.vertices;var ns=mesh.normals;int n=(k*37+7)%vs.Length;Vector3 pos=r.transform.TransformPoint(vs[n]);if(ns.Length==vs.Length)pos+=r.transform.TransformDirection(ns[n])*.014f;seeds.Add(pos);}seeds.Sort((x,y)=>Vector3.Dot(x,SweepAxis).CompareTo(Vector3.Dot(y,SweepAxis)));
  var o=new GameObject("Assembly completion glints");sparks=o.AddComponent<ParticleSystem>();sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=sparks.main;main.playOnAwake=false;main.loop=false;main.duration=2;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=48;main.gravityModifier=.06f;main.startSpeed=0;
  var emission=sparks.emission;emission.enabled=false;var shape=sparks.shape;shape.enabled=false;var color=sparks.colorOverLifetime;color.enabled=true;var g=new Gradient();g.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(1,.62f,.14f),1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(0,1)});color.color=g;var size=sparks.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,1,1,0));var renderer=o.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=sparkleMaterial;renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;sparks.Play();
 }
 void CreateRings(Transform train){
  if(!pulseLineMaterial)return;var j=GetComponent<SteamJourney>();if(!j)return;
  foreach(var side in j.engine.sides)foreach(var wheel in side.wheels){
   // Small concentric locking rings sit outside the real wheel face, not over the body.
   for(int ring=0;ring<2;ring++){var go=new GameObject("Temporary cyan hub lock");go.transform.SetParent(train,false);
    var lr=go.AddComponent<LineRenderer>();lr.sharedMaterial=pulseLineMaterial;lr.useWorldSpace=false;lr.loop=true;lr.positionCount=64;lr.widthMultiplier=ring==0?.014f:.009f;lr.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
    Vector3 c=train.InverseTransformPoint(wheel.position);c.z=side.sign*1.014f;float radius=ring==0?.16f:.26f;
    for(int n=0;n<64;n++){float a=n*Mathf.PI*2/64;lr.SetPosition(n,c+new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0));}rings.Add(lr);
   }
  }
 }
 void UpdateRings(float p){foreach(var r in rings){if(!r)continue;Vector3 wp=r.transform.TransformPoint(r.GetPosition(0));float u=(Vector3.Dot(wp,SweepAxis)-range.x)/range.y;
  float age=p-(.10f+u*.52f);float alpha=Mathf.SmoothStep(0,1,age/.045f)*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.36f,age)))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.82f,1,p)));
  r.startColor=r.endColor=new Color(.025f,2.5f,4.2f,alpha);r.enabled=alpha>.001f;
 }}
 public void Restore(){if(nativeActive){nativeActive=false;nativeRestore?.Invoke();}if(sparks){sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);Destroy(sparks.gameObject);sparks=null;}seeds.Clear();foreach(var e in originals)if(e.Key)e.Key.sharedMaterials=e.Value;originals.Clear();foreach(var pair in copies.Values)foreach(var m in pair)if(m)Destroy(m);foreach(var r in rings)if(r)Destroy(r.gameObject);rings.Clear();copies.Clear();if(finishShader)Shader.SetGlobalVector("_AssemblyGoldRange",new Vector4(0,1,1,0));}
 public void Cancel(){Restore();if(sound)sound.Stop();}
 void OnDisable(){Cancel();}void OnDestroy(){Cancel();}
}
