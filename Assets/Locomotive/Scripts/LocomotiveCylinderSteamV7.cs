using UnityEngine;
// Teaching visualisation: valve timing changes the chamber density; closing a port never destroys retained steam.
[DefaultExecutionOrder(150)]
public class LocomotiveCylinderSteamV7 : MonoBehaviour {
 public LocomotiveController engine;
 public float[] density=new float[4],temperature=new float[4],lastVolume=new float[4];
 public float maxDensityStep; public int samples;
 MaterialPropertyBlock block;
 static readonly int Density=Shader.PropertyToID("_Density"),Tint=Shader.PropertyToID("_BaseColor");
 void LateUpdate(){Tick(Mathf.Min(Time.deltaTime,.05f));}
 public void Tick(float dt){
  if(!engine||engine.sides==null)return;if(block==null)block=new MaterialPropertyBlock();
  bool visible=engine.principle&&!engine.exploded&&!engine.assemblyLesson;
  for(int s=0;s<engine.sides.Length;s++){
   var side=engine.sides[s];var p=side.pose;float piston=(float)p.slider+1;
   side.exhaustLeft.GetComponent<Renderer>().enabled=false;side.exhaustRight.GetComponent<Renderer>().enabled=false;
   for(int end=0;end<2;end++){
    int i=s*2+end;var tr=end==0?side.admissionLeft:side.admissionRight;
    float lo=end==0?2.82f:piston+.04f,hi=end==0?piston-.04f:3.48f;
    float length=Mathf.Max(.006f,hi-lo);
    float admission=(float)(end==0?p.LeftAdmission:p.RightAdmission),exhaust=(float)(end==0?p.LeftExhaust:p.RightExhaust);
    float target,temp;
    if(admission>.001f){target=5.2f;temp=1;}
    else if(exhaust>.001f){target=.65f;temp=0;}
    else{target=Mathf.Clamp(density[i]*Mathf.Pow(Mathf.Clamp(lastVolume[i]/length,.98f,1.02f),.45f),1.15f,4.8f);temp=temperature[i];}
    if(!visible){density[i]=0;temperature[i]=temp;lastVolume[i]=length;tr.gameObject.SetActive(false);continue;}
    float old=density[i];density[i]=Mathf.Lerp(old,target,1-Mathf.Exp(-dt/(exhaust>.001f?.35f:.22f)));
    temperature[i]=Mathf.Lerp(temperature[i],temp,1-Mathf.Exp(-dt/.28f));
    maxDensityStep=Mathf.Max(maxDensityStep,Mathf.Abs(density[i]-old));samples++;lastVolume[i]=length;
    tr.gameObject.SetActive(true);tr.localPosition=new Vector3((lo+hi)*.5f,.65f,side.sign*1.085f);tr.localScale=new Vector3(length,1,1);
    var r=tr.GetComponent<Renderer>();r.enabled=true;block.Clear();block.SetFloat(Density,density[i]);
    block.SetColor(Tint,Color.Lerp(new Color(.77f,.88f,.97f,.88f),new Color(.96f,.93f,.84f,.88f),temperature[i]));r.SetPropertyBlock(block);
   }
  }
 }
}
