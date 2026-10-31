using UnityEngine;
using UnityEngine.Rendering;

// Shares the canyon's density/light shader and volume mesh, with a short bench-scale plume.
[DefaultExecutionOrder(3500)]
public sealed class WorkshopSteamV20 : MonoBehaviour {
 public SteamJourney journey;public MesaAtmosV16 source;public Material material;
 public int ActivePuffs{get;private set;}public float MaxDiameter{get;private set;}
 sealed class Puff{public Transform t;public Renderer r;public float age,life,seed;public Vector3 position;public bool alive;}
 readonly Puff[] pool=new Puff[32];MaterialPropertyBlock block;float credit;int serial;bool lastSandbox;
 void Awake(){block=new MaterialPropertyBlock();var root=new GameObject("Bench rolling white steam V20");root.transform.SetParent(journey.workshopRoot.transform,false);
  for(int i=0;i<pool.Length;i++){var o=new GameObject("Bench steam "+i);o.transform.SetParent(root.transform,false);o.AddComponent<MeshFilter>().sharedMesh=source.volumeMesh;var r=o.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.enabled=false;pool[i]=new Puff{t=o.transform,r=r};}
 }
 public void Clear(){foreach(var p in pool)if(p!=null){p.alive=false;if(p.r)p.r.enabled=false;}credit=0;ActivePuffs=0;}
 void LateUpdate() { using (SwanJourney.SwanJourneyCpuTimings.Measure(SwanJourney.SwanJourneyCpuTimings.Stage.WorkshopSteam)) CpuMeasuredLateUpdate(); }
    void CpuMeasuredLateUpdate(){
  if(lastSandbox!=journey.inSandbox){Clear();lastSandbox=journey.inSandbox;}if(journey.inSandbox)return;
  bool chimney=journey.ChimneyConnected;var legacy=journey.smoke.GetComponent<Renderer>();legacy.enabled=!chimney;
  if(!chimney){Clear();return;}if(journey.paused)return;float power=journey.ChimneyExhaustPower;
  float dt=Mathf.Min(Time.deltaTime,.05f);credit+=dt*6.4f*Mathf.Clamp01(power);
  while(credit>=1){credit--;var p=pool[serial%pool.Length];p.seed=serial++*2.399963f;p.age=0;p.life=4.0f+.32f*Mathf.Sin(p.seed);p.alive=true;p.r.enabled=true;p.position=journey.WorldPointToScene(journey.smoke.transform.position)+Vector3.up*.02f;p.t.rotation=journey.SceneRotationToWorld(Quaternion.Euler(7*Mathf.Sin(p.seed),p.seed*57.3f,8*Mathf.Cos(p.seed)));}
  ActivePuffs=0;MaxDiameter=0;
  foreach(var p in pool){if(!p.alive)continue;p.age+=dt;if(p.age>p.life){p.alive=false;if(p.r)p.r.enabled=false;continue;}float u=p.age/p.life;
   p.position+=(Vector3.up*Mathf.Lerp(.98f,.22f,Mathf.SmoothStep(0,1,p.age/2.0f))+journey.WorldDirectionToScene(journey.trainAnchor.right)*(-Mathf.Lerp(.10f,1.05f,Mathf.SmoothStep(0,1,p.age/1.6f))+.07f*Mathf.Sin(p.seed+p.age*2))+journey.WorldDirectionToScene(journey.trainAnchor.forward)*(.06f*Mathf.Sin(p.age+p.seed)))*dt;
   p.t.position=journey.ScenePointToWorld(p.position);float size=Mathf.Lerp(.28f,1.38f,Mathf.Pow(u,.66f));p.t.localScale=new Vector3(size,size*1.12f,size*.94f);MaxDiameter=Mathf.Max(MaxDiameter,size);
   float fade=Mathf.SmoothStep(0,1,u/.07f)*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.62f,1,u)));block.Clear();block.SetFloat("_Seed",p.seed);block.SetFloat("_Phase",p.seed+p.age*.85f);block.SetFloat("_Opacity",fade*1.1f);p.r.SetPropertyBlock(block);ActivePuffs++;
  }
 }
 void OnDisable(){Clear();}
}
