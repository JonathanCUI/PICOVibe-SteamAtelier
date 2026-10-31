using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed partial class SteamJourney {
 public GameObject[] socketGhosts;
 public JourneyButtonFeedback[] buttonFeedback;
 public AudioClip buttonSound,assemblySweepSound,switchSound;public AudioClip[] assemblyClips;public int LastAssemblyClip{get;private set;}=-1;
 public bool AutoAssembling {get;private set;}
 Coroutine assemblyRoutine;
 Coroutine completionRoutine;public bool CompletionActive=>completionRoutine!=null;public int CompletionFeedbackCount{get;private set;}
 void BeginManualCompletion(){if(!Complete||AutoAssembling||completionRoutine!=null)return;completionRoutine=StartCoroutine(ManualCompletion());}
 IEnumerator ManualCompletion(){cutaway=false;inspectionMode=0;ApplyVisibility();yield return CompletionFeedback();completionRoutine=null;}
 IEnumerator CompletionFeedback(){var fx=GetComponent<StudioAssemblyFinishV9>();CompletionFeedbackCount++;if(fx){fx.Begin(engine.locomotive);Notify("装配完成 · 全车联检。");float t=0;while(t<fx.Duration){t+=Time.deltaTime;fx.SetProgress(t/fx.Duration);yield return null;}fx.Restore();}Notify("机车已装配完成 · 按「出发」进入山谷铁路。");}
 void CancelCompletion(){if(completionRoutine!=null)StopCoroutine(completionRoutine);completionRoutine=null;var fx=GetComponent<StudioAssemblyFinishV9>();if(fx)fx.Cancel();}
 readonly List<GameObject> assemblyFlights=new List<GameObject>();
 public bool ResidualEnergy=>FireLevel+GasLevel+WaterHeat+SteamPressure+Delivery+CylinderPower+WheelPower>.003f;
 int activeScanIndex=-1;float scanStartTime;
 void UpdateSocketGhost(){
  int index=Array.IndexOf(modules,heldModule??Current);if(index<0)index=modules.Length+Array.IndexOf(finishing,heldModule??Current);
  if(socketGhosts==null)return;
  if(activeScanIndex!=index){activeScanIndex=index;scanStartTime=Time.time;}
  Shader.SetGlobalFloat("_SocketAge",Time.time-scanStartTime);
  for(int i=0;i<socketGhosts.Length;i++)if(socketGhosts[i])socketGhosts[i].SetActive(!inSandbox&&!AutoAssembling&&!(GetComponent<JourneySceneArrival>()?.Active??false)&&i==index&&!(heldModule??Current).installed);
  if(aimRing)aimRing.gameObject.SetActive(false);
  Shader.SetGlobalFloat("_SocketLock",snapReady?1:0);
 }
 void Feedback(JourneyAction action){
  if(buttonFeedback!=null)foreach(var f in buttonFeedback)if(f&&f.action==action&&f.gameObject.activeInHierarchy)f.Press();
  if(action!=JourneyAction.Whistle&&action!=JourneyAction.Assemble)Play(action==JourneyAction.Reverse||action==JourneyAction.Brake||action==JourneyAction.Throttle||action==JourneyAction.Cutaway?switchSound:buttonSound);
 }
 void PlayAssemblyLatch(){if(!audioSource)return;var clip=clickSound;if(assemblyClips!=null&&assemblyClips.Length>0){int index=UnityEngine.Random.Range(0,assemblyClips.Length);if(assemblyClips.Length>1&&index==LastAssemblyClip)index=(index+UnityEngine.Random.Range(1,assemblyClips.Length))%assemblyClips.Length;LastAssemblyClip=index;clip=assemblyClips[index];}if(clip){GetComponent<JourneyBackgroundMusic>()?.Duck(clip.length);audioSource.pitch=1;audioSource.PlayOneShot(clip,.66f);}}
 void CancelAssembly(){
  CancelCompletion();
  if(assemblyRoutine!=null)StopCoroutine(assemblyRoutine);assemblyRoutine=null;AutoAssembling=false;var finish=GetComponent<StudioAssemblyFinishV9>();if(finish)finish.Cancel();
  foreach(var o in assemblyFlights)if(o)Destroy(o);assemblyFlights.Clear();
 }
 public bool StartAssemblySequence(){
  if(inSandbox||AutoAssembling||Complete)return false;
  held=null;heldModule=null;snapReady=false;pendingDetach=null;cutaway=false;inspectionMode=0;ApplyVisibility();AutoAssembling=true;UpdateSocketGhost();
  assemblyRoutine=StartCoroutine(AssembleSequence());return true;
 }
 sealed class DockFlight {public Transform root;public Vector3 offset;public int module;}
 IEnumerator AssembleSequence(){
  int[] order={0,1,2,4,6,7,8,5,3,9,10};var remaining=modules.Where(m=>!m.installed).Concat(order.Where(k=>k<finishing.Length&&!finishing[k].installed).Select(k=>finishing[k])).ToArray();
  var fx=GetComponent<StudioAssemblyFinishV9>();var flights=new List<DockFlight>();
  Notify("自动装配 · 对准安装轴，分组锁定。");
  for(int i=0;i<remaining.Length;i++){
   var m=remaining[i];int type=Array.IndexOf(finishing,m);m.parked=false;if(m.preview)m.preview.SetActive(false);
   // Read the installed geometry at its real pose. Disabled superseded geometry stays excluded.
   foreach(var o in m.objects)if(o)o.SetActive(true);
   var sources=m.objects.Where(o=>o).SelectMany(o=>o.GetComponentsInChildren<MeshRenderer>()).Distinct().Where(r=>r.enabled&&r.gameObject.activeInHierarchy&&r.GetComponent<MeshFilter>()&&r.GetComponent<MeshFilter>().sharedMesh).ToArray();
   int mechanism=Array.IndexOf(modules,m);bool paired=type==1||type==4||type==5||mechanism==4;
   foreach(var group in sources.GroupBy(r=>paired?(trainAnchor.InverseTransformPoint(r.bounds.center).z>=0?1:-1):0)){
    var go=new GameObject("Assembly flight · "+m.title+" · "+group.Key);go.transform.SetParent(trainAnchor,false);
    foreach(var source in group){var o=new GameObject(source.name,typeof(MeshFilter),typeof(MeshRenderer));o.transform.SetParent(go.transform,false);
     o.transform.localPosition=trainAnchor.InverseTransformPoint(source.transform.position);o.transform.localRotation=Quaternion.Inverse(trainAnchor.rotation)*source.transform.rotation;
     Vector3 a=source.transform.lossyScale,b=trainAnchor.lossyScale;o.transform.localScale=new Vector3(a.x/b.x,a.y/b.y,a.z/b.z);
     o.GetComponent<MeshFilter>().sharedMesh=source.GetComponent<MeshFilter>().sharedMesh;o.GetComponent<MeshRenderer>().sharedMaterials=source.sharedMaterials;
    }
    Vector3 offset=type==0?new Vector3(0,.72f,0):type==1?new Vector3(0,.18f,group.Key*1.35f):type==2?new Vector3(-1.05f,1.15f,0):type==3?new Vector3(0,2.05f,0):type==4?new Vector3(0,.28f,group.Key*1.7f):type==5?new Vector3(0,-.05f,group.Key*1.45f):type==6?new Vector3(-1.75f,.35f,0):type==7?new Vector3(1.8f,0,0):type==8?new Vector3(-1.8f,0,0):new Vector3(.9f,.65f,type==9?.25f:-.25f);
    if(type<0)offset=mechanism==4?new Vector3(.35f,.12f,group.Key*1.55f):mechanism==5?new Vector3(0,-.18f,1.65f):mechanism==2?new Vector3(1.7f,.2f,0):new Vector3(0,1.25f+mechanism*.10f,0);
    go.transform.localPosition=offset;assemblyFlights.Add(go);flights.Add(new DockFlight{root=go.transform,offset=offset,module=i});
   }
   foreach(var o in m.objects)if(o)o.SetActive(false);
  }
  RefreshPreview();const float interval=.34f,duration=.78f;float elapsed=0;bool[] landed=new bool[remaining.Length],started=new bool[remaining.Length];int count=0;
  while(count<remaining.Length){elapsed+=Time.deltaTime;
   for(int i=0;i<remaining.Length;i++){
    if(landed[i])continue;float local=elapsed-.40f-i*interval;float t=Mathf.Clamp01(local/duration);
    if(local>=0&&!started[i])started[i]=true;
    // Continuous acceleration into the socket; the sound marks contact without a hovering pause.
    float travel=t*t*t;
    foreach(var f in flights)if(f.module==i)f.root.localPosition=f.offset*(1-travel);
    if(t>=1){landed[i]=true;count++;remaining[i].installed=true;stage=modules.TakeWhile(m=>m.installed).Count();ImpactWater(remaining[i]);foreach(var f in flights)if(f.module==i)f.root.gameObject.SetActive(false);PlayAssemblyLatch();ApplyVisibility();Notify("机械锁定 "+count+" / "+remaining.Length+" · "+remaining[i].title);}
   }yield return null;
  }
  foreach(var o in assemblyFlights)if(o)Destroy(o);assemblyFlights.Clear();ApplyVisibility();SaveProgress();
  yield return CompletionFeedback();
  AutoAssembling=false;assemblyRoutine=null;RefreshPreview();ApplyVisibility();Notify("机车已装配完成 · 按「出发」进入山谷铁路。");
 }
}
