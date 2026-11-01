using System.Linq;using UnityEngine;
// Sparse contact-driven waves in authored scene metres. No extra physics objects or simulation allocations per frame.
public sealed class StudioWaterImpulseV10:MonoBehaviour {
 public SteamJourney owner;public int ImpactCount{get;private set;}public float LastStrength{get;private set;}public Vector3 LastOrigin{get;private set;}
 readonly Vector4[] impacts=new Vector4[8];int cursor;static readonly int Waves=Shader.PropertyToID("_WaterImpulses"),Clock=Shader.PropertyToID("_WaterImpulseTime");
 void Awake(){if(!owner)owner=GetComponent<SteamJourney>();Clear();}
 public void Clear(){for(int i=0;i<8;i++)impacts[i]=new Vector4(0,0,-1000,0);cursor=0;Shader.SetGlobalVectorArray(Waves,impacts);}
 public void Impact(JourneyModule m,float multiplier=1){if(!owner||owner.inSandbox||m==null)return;var bounds=new Bounds();bool any=false;
  // Inactive Renderer.bounds can be stale or zero before the first rendered frame. Transform mesh bounds explicitly.
  var filters=m.objects.Where(o=>o).SelectMany(o=>o.GetComponentsInChildren<MeshFilter>(true)).Where(f=>f.sharedMesh).Distinct();
  foreach(var f in filters){var b=f.sharedMesh.bounds;for(int k=0;k<8;k++){Vector3 q=owner.WorldPointToScene(f.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1))));if(!any){bounds=new Bounds(q,Vector3.zero);any=true;}else bounds.Encapsulate(q);}}
  Vector3 local=m.previewCentre;if(!any)bounds=new Bounds(owner.WorldPointToScene(owner.trainAnchor.TransformPoint(local)),Vector3.one*.15f);
  Vector3 contact=new Vector3(Mathf.Clamp(local.x,-1.9f,2.22f),2.72f,Mathf.Clamp(local.z,-.43f,.43f));float distance=Vector3.Distance(local,contact);
  float size=Mathf.Pow(Mathf.Max(.001f,bounds.size.x*bounds.size.y*bounds.size.z),1f/3);float strength=Mathf.Clamp(.007f+size*.009f,.007f,.024f)*Mathf.Exp(-distance*.22f)*multiplier;
  Vector3 origin=owner.trainAnchor.TransformPoint(contact);Vector3 sceneOrigin=owner.WorldPointToScene(origin);impacts[cursor]=new Vector4(sceneOrigin.x,sceneOrigin.z,Time.time,strength);cursor=(cursor+1)%8;ImpactCount++;LastStrength=strength;LastOrigin=origin;Shader.SetGlobalVectorArray(Waves,impacts);
 }
 void LateUpdate() { using (SwanJourney.SwanJourneyCpuTimings.Measure(SwanJourney.SwanJourneyCpuTimings.Stage.Water)) CpuMeasuredLateUpdate(); }
    void CpuMeasuredLateUpdate(){Shader.SetGlobalFloat(Clock,Time.time);Shader.SetGlobalMatrix("_WaterWorldToLocal",owner?owner.SpatialWorldToLocal:Matrix4x4.identity);Shader.SetGlobalFloat("_WaterWorldScale",owner?owner.SpatialScale:1);}
 void OnDisable(){Clear();}
}
public sealed partial class SteamJourney {void ImpactWater(JourneyModule part,float scale=1){if(SwanInputActive)return;var fx=GetComponent<StudioWaterImpulseV10>();if(fx)fx.Impact(part,scale);}}
