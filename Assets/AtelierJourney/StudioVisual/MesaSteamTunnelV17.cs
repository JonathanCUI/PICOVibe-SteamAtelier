using UnityEngine;

// Shared CPU/GPU free-air field baked from the actual canyon geometry.
// It applies to locomotive exhaust only; the three sky clouds are unaffected.
[DefaultExecutionOrder(3300)]
public sealed class MesaSteamTunnelV17 : MonoBehaviour {
 public SteamJourney journey; public Texture3D freeAir; public Vector3 fieldMin,fieldSize;
 public float start,end; public bool protectionEnabled=true;
 JourneyPath path; byte[] pixels; int nx,ny,nz;
 Vector3[] points; float sampleStart,spacing;
 void Awake(){if(!journey)journey=GetComponent<SteamJourney>();if(!journey)journey=FindObjectOfType<SteamJourney>();Initialize();}
 void LateUpdate() { using (SwanJourney.SwanJourneyCpuTimings.Measure(SwanJourney.SwanJourneyCpuTimings.Stage.SteamTunnel)) CpuMeasuredLateUpdate(); }
    void CpuMeasuredLateUpdate(){Bind();}
 public void Initialize(){
  path=new JourneyPath();sampleStart=start-5;spacing=(end-start+10)/128;
  points=new Vector3[129];for(int i=0;i<points.Length;i++)points[i]=path.At(sampleStart+i*spacing);
  if(freeAir){nx=freeAir.width;ny=freeAir.height;nz=freeAir.depth;pixels=freeAir.GetPixelData<byte>(0).ToArray();}
  Bind();
 }
 public void Bind(){if(journey&&journey.SwanInputActive)return;Shader.SetGlobalMatrix("_MesaWorldToLocal",journey?journey.SpatialWorldToLocal:Matrix4x4.identity);Shader.SetGlobalTexture("_MesaFreeAir",freeAir);Shader.SetGlobalVector("_MesaFieldMin",fieldMin);Shader.SetGlobalVector("_MesaFieldInvSize",new Vector3(1/fieldSize.x,1/fieldSize.y,1/fieldSize.z));Shader.SetGlobalFloat("_MesaFieldEnabled",protectionEnabled&&freeAir&&(!journey||journey.inSandbox)?1:0);}
 void OnDisable(){if(!journey||!journey.SwanInputActive)Shader.SetGlobalFloat("_MesaFieldEnabled",0);}
 void OnDestroy(){if(!journey||!journey.SwanInputActive)Shader.SetGlobalFloat("_MesaFieldEnabled",0);}
 // Path and field queries use authored scene coordinates; convert external world hits explicitly.
 public Vector3 WorldToField(Vector3 point)=>journey?journey.WorldPointToScene(point):point;
 public Vector3 AtWorld(float s)=>journey?journey.ScenePointToWorld(At(s)):At(s);
 public float AirWorld(Vector3 point)=>Air(WorldToField(point));
 public bool InsideWorld(Vector3 point,out float s,out float lateral)=>Inside(WorldToField(point),out s,out lateral);
 public Vector3 At(float s){return path.At(s);}
 public Vector3 Tangent(float s){return (path.At(s+.03f)-path.At(s-.03f)).normalized;}
 public void Project(Vector3 p,out float s,out float lateral){
  float best=float.MaxValue;s=start;lateral=0;
  for(int i=0;i<points.Length-1;i++){
   Vector3 d=points[i+1]-points[i],v=p-points[i];d.y=v.y=0;
   float f=Mathf.Clamp01(Vector3.Dot(v,d)/d.sqrMagnitude);Vector3 q=v-d*f;
   if(q.sqrMagnitude<best){best=q.sqrMagnitude;s=sampleStart+(i+f)*spacing;lateral=Vector3.Dot(q,Vector3.Cross(Vector3.up,d.normalized));}
  }
 }
 public bool Inside(Vector3 p,out float s,out float lateral){Project(p,out s,out lateral);return protectionEnabled&&s>start&&s<end&&Mathf.Abs(lateral)<1.25f;}
 public float Air(Vector3 p){
  if(!protectionEnabled||pixels==null)return 1;
  Vector3 u=p-fieldMin;u=new Vector3(u.x/fieldSize.x,u.y/fieldSize.y,u.z/fieldSize.z);
  if(u.x<0||u.y<0||u.z<0||u.x>1||u.y>1||u.z>1)return 1;
  float x=Mathf.Clamp(u.x*nx-.5f,0,nx-1),y=Mathf.Clamp(u.y*ny-.5f,0,ny-1),z=Mathf.Clamp(u.z*nz-.5f,0,nz-1);
  int a=(int)x,b=(int)y,c=(int)z,a1=Mathf.Min(a+1,nx-1),b1=Mathf.Min(b+1,ny-1),c1=Mathf.Min(c+1,nz-1);x-=a;y-=b;z-=c;
  float lo=Mathf.Lerp(Mathf.Lerp(V(a,b,c),V(a1,b,c),x),Mathf.Lerp(V(a,b1,c),V(a1,b1,c),x),y);
  float hi=Mathf.Lerp(Mathf.Lerp(V(a,b,c1),V(a1,b,c1),x),Mathf.Lerp(V(a,b1,c1),V(a1,b1,c1),x),y);
  return Mathf.Lerp(lo,hi,z)/255f;
 }
 float V(int x,int y,int z){return pixels[x+nx*(y+ny*z)];}
}
