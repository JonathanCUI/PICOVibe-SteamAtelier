using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
[Serializable] public class LocomotiveSide {
 public int sign=1;public Transform[] wheels=new Transform[3],couplings=new Transform[2],outerPins=new Transform[2];
 public Transform mainRod,crosshead,guide,crankpin,wristpin,pistonRod,piston;
 public Transform returnCrank,eccentricRod,expansion,dieBlock,radiusRod,combination,union,crossheadArm,liftArm,liftLink,valve,stem,reachRod;
 public Transform admissionLeft,admissionRight,exhaustLeft,exhaustRight;
 [NonSerialized]public LocomotiveKinematics.Pose pose;
}
public class LocomotiveController : MonoBehaviour {
 public LocomotiveCylinderSteamV7 cylinderSteam; public LocomotiveFreeAssembly freeAssembly; public LocomotiveWorkshop workshop; public Transform locomotive,display;public GameObject environment,principleParts;public Camera studioCamera;
 public LocomotiveSide[] sides;public LocomotivePart[] parts;public Transform[] axles;public Transform gaugeNeedle;public Transform[] doors;
 public TMP_Text titleText,lessonText,metricsText,partText,statusText;public Slider phaseSlider,speedSlider;
 public float phase=25,rpm=8;public int direction=1,assembled;public bool running,exploded,assemblyLesson,principle,travel,doorsOpen;
 public float yaw=26,pitch=16,distance=12.8f;public Vector3 focus=new Vector3(.1f,1.5f,0);
 public bool inputEnabled=true,externalView,adaptiveDetail; bool changing;Vector2 lastPointer,downPointer;LocomotivePart selected;public float travelled;int lessonIndex;
 public readonly string[] chapterNames={"底架与承载","轮轴与联动","活塞与传动","配汽与换向","锅炉与热循环","车体与补给","操纵与附件"};
 public void Start(){Application.runInBackground=true;Application.targetFrameRate=60;foreach(var lod in locomotive.GetComponentsInChildren<LODGroup>(true))if(lod.gameObject.activeInHierarchy&&lod.enabled)lod.ForceLOD(0);assembled=parts.Length;if(phaseSlider){phaseSlider.minValue=0;phaseSlider.maxValue=360;phaseSlider.onValueChanged.AddListener(v=>{if(changing)return;phase=v;running=false;ApplyPose();});}if(speedSlider){speedSlider.minValue=2;speedSlider.maxValue=20;speedSlider.value=rpm;speedSlider.onValueChanged.AddListener(v=>rpm=v);}ApplyPose();UpdateLabels();}
 public void Advance(float dt){if(running&&!exploded&&!assemblyLesson){float delta=rpm*6*dt*direction;phase=Mathf.Repeat(phase-delta,360);if(travel){travelled+=delta*Mathf.Deg2Rad*.65f;if(Mathf.Abs(travelled)>42){travelled=Mathf.Clamp(travelled,-42,42);running=false;}}}}
 public void Update(){Advance(Time.deltaTime);ApplyPose();UpdateLabels();Orbit();}
 public static void Link(Transform tr,LocomotiveKinematics.D2 a,LocomotiveKinematics.D2 b,float z){if(!tr)return;tr.localPosition=a.AtZ(z);tr.localRotation=Quaternion.Euler(0,0,(float)(Math.Atan2(b.y-a.y,b.x-a.x)*180/Math.PI));}
 void SetFlow(Transform tr,bool active,float lo,float hi,int sign){if(!tr)return;float length=hi-lo-.01f;if(workshop&&workshop.Guided)active &= workshop.installed> (tr.name.Contains("进汽")?4:5);tr.gameObject.SetActive(principle&&active&&!exploded&&!assemblyLesson&&length>.005f);tr.localPosition=new Vector3((lo+hi)/2,.65f,sign*1.085f);tr.localScale=new Vector3(Mathf.Max(.001f,length),1,1);}
 public void ApplyPose(){
  if(parts==null||sides==null)return;foreach(var p in parts)if(p){p.Restore();}
  for(int k=0;k<sides.Length;k++){
   var s=sides[k];int sign=s.sign;double theta=(phase+(sign<0?90:0))*Math.PI/180;var p=LocomotiveKinematics.Solve(theta,-direction,s.pose.alpha,s.pose.beta);s.pose=p;float deg=(float)(theta*180/Math.PI),wz=sign*.775f,z=sign*1.085f;
   for(int i=0;i<3;i++){s.wheels[i].localPosition=new Vector3((i-1)*1.45f,.65f,wz);s.wheels[i].localRotation=Quaternion.Euler(0,0,deg);}
   var q=p.Q;for(int i=0;i<2;i++){var a=q+new LocomotiveKinematics.D2(i==0?-1.45:0,0);var b=a+new LocomotiveKinematics.D2(1.45,0);Link(s.couplings[i],a,b,sign*(.775f+(i==0?.125f:.165f)));int axle=i==0?-1:1;s.outerPins[i].localPosition=(q+new LocomotiveKinematics.D2(axle*1.45,0)).AtZ(sign*(.775f+(i==0?.125f:.165f)));}
   var slide=new LocomotiveKinematics.D2(p.slider,.65);Link(s.mainRod,q,slide,z);s.crosshead.localPosition=slide.AtZ(z);s.guide.localPosition=new Vector3(2.275f,.65f,z);s.crankpin.localPosition=q.AtZ(z);s.wristpin.localPosition=slide.AtZ(z);
   s.pistonRod.localPosition=new Vector3((float)p.slider+.595f,.65f,z);s.piston.localPosition=new Vector3((float)p.slider+1,.65f,z);
   Link(s.returnCrank,p.Q,p.E,sign*1.165f);Link(s.eccentricRod,p.E,p.D,sign*1.205f);
   s.expansion.localPosition=p.A.AtZ(sign*1.27f);s.expansion.localRotation=Quaternion.Euler(0,0,(float)(p.phi*180/Math.PI));
   s.dieBlock.localPosition=p.B.AtZ(sign*1.27f);s.dieBlock.localRotation=Quaternion.Euler(0,0,(float)((p.phi-p.alpha)*180/Math.PI));
   Link(s.radiusRod,p.B,p.R,sign*1.32f);Link(s.combination,p.F,p.V,sign*1.37f);Link(s.union,p.U,p.F,sign*1.42f);Link(s.crossheadArm,slide,p.U,sign*1.465f);Link(s.liftArm,p.W,p.T,sign*1.515f);Link(s.liftLink,p.T,p.H,sign*1.47f);
   s.valve.localPosition=new Vector3((float)p.V.x+.703f,1.03f,z);s.stem.localPosition=new Vector3((float)p.V.x+.31f,1.02f,z);Link(s.reachRod,new LocomotiveKinematics.D2(-1.90,1.56),p.T,sign*1.56f);
   if(!cylinderSteam){float valve=(float)p.ValveDisplacement;float pistonX=(float)p.slider+1;SetFlow(s.admissionLeft,p.LeftAdmission>.001f,2.82f,pistonX-.04f,sign);SetFlow(s.admissionRight,p.RightAdmission>.001f,pistonX+.04f,3.48f,sign);SetFlow(s.exhaustLeft,p.LeftExhaust>.001f,2.82f,pistonX-.04f,sign);SetFlow(s.exhaustRight,p.RightExhaust>.001f,pistonX+.04f,3.48f,sign);}
  }
  if(axles!=null)foreach(var axle in axles)axle.localRotation=Quaternion.Euler(0,0,phase);
  var doorsV11=GetComponent<LocomotiveDoorsV11>();if(doorsV11)doorsV11.Apply();else if(doors!=null)for(int i=0;i<doors.Length;i++)doors[i].localRotation=Quaternion.Euler(0,doorsOpen?(i==0?-60:60):0,0);
  if(gaugeNeedle)gaugeNeedle.localRotation=Quaternion.Euler(0,0,running?-55:-125);
  locomotive.localPosition=new Vector3(travelled,0,0);
  for(int i=0;i<parts.Length;i++){var p=parts[i];if(!p)continue;bool detach=exploded||(assemblyLesson&&i>=assembled);if(detach)p.transform.localPosition+=p.explodeOffset;bool visible=!(principle&&p.hideInPrinciple&&!exploded&&!assemblyLesson)&&!(assemblyLesson&&i>assembled);if(workshop&&workshop.Guided)visible &= workshop.IsPartVisible(p);if(freeAssembly)visible &= freeAssembly.IsPartVisible(p);if(p.gameObject.activeSelf!=visible){p.gameObject.SetActive(visible);if(visible&&!adaptiveDetail){var lod=p.GetComponent<LODGroup>();if(lod)lod.ForceLOD(0);}}}
  if(principleParts)principleParts.SetActive(principle&&!exploded&&!assemblyLesson);
  if(workshop&&workshop.Guided&&principleParts){foreach(Transform t in principleParts.transform){bool cylinder=t.name.Contains("汽缸")||t.name.Contains("进汽示意")||t.name.Contains("排汽示意");bool exhaust=t.name.Contains("烟箱");bool cab=t.name.Contains("驾驶室");int required=cylinder?2:exhaust?6:cab?7:1;if(workshop.installed<required)t.gameObject.SetActive(false);else if(!t.name.Contains("进汽示意")&&!t.name.Contains("排汽示意"))t.gameObject.SetActive(true);}}
  else if(principleParts){foreach(Transform t in principleParts.transform)if(!t.name.Contains("进汽示意")&&!t.name.Contains("排汽示意"))t.gameObject.SetActive(true);}
  if(phaseSlider){changing=true;phaseSlider.value=phase;changing=false;}
 }
 public void UpdateLabels(){if(lessonText){if(assemblyLesson){lessonText.text=assembled<parts.Length?("装配 "+(assembled+1)+" / "+parts.Length+"\n"+parts[assembled].title+"\n\n"+parts[assembled].explanation+"\n\n点击待装零件或「装入」完成此步。"):("装配完成\n从热源、配汽到传动，所有零件已就位。点击运行观察联动。");}
 else if(selected)lessonText.text=selected.partId+"  "+selected.title+"\n\n"+selected.explanation;
 else if(principle)lessonText.text="从蒸汽到转动\n\n橙色：进汽；蓝色：排汽。\n滑阀交替打开两侧通道，推动活塞往复。\n十字头承受侧向力，主连杆把推力传到动轮。\n\n透明区域与流向颜色是教学示意。";
 else lessonText.text="蒸汽机车 · 可拆装教具\n\n点击零件查看作用；拖动旋转，双指或滚轮缩放。\n\n选择逐件组装，按承载、传动、配汽、热循环的顺序理解机械结构。";}
  if(metricsText&&sides!=null&&sides.Length>0){var p=sides[0].pose;metricsText.text="曲柄角  "+phase.ToString("0")+"°\n活塞行程  "+((p.slider-1.87)*1000).ToString("0")+" / 560 mm\n"+(direction>0?"前进":"后退")+" · "+rpm.ToString("0")+" rpm";}
  if(partText)partText.text=assemblyLesson?(assembled+" / "+parts.Length+" 已装入"):(exploded?"爆炸图 · 独立零件":"点击零件查看编号与作用");
 }
 public void ToggleRun(){if(exploded||assemblyLesson){exploded=false;assemblyLesson=false;assembled=parts.Length;}running=!running;selected=null;statusText.text="";}
 public void ToggleExplode(){running=false;assemblyLesson=false;exploded=!exploded;selected=null;distance=exploded?17:12.8f;}
 public void StartLesson(){running=false;travel=false;travelled=0;exploded=false;principle=false;assemblyLesson=true;assembled=0;phase=25;selected=null;distance=13;}
 public void NextPart(){if(!assemblyLesson){StartLesson();return;}assembled=Mathf.Min(parts.Length,assembled+1);ApplyPose();}
 public void FinishAssembly(){assemblyLesson=false;exploded=false;assembled=parts.Length;ApplyPose();}
 public void TogglePrinciple(){assemblyLesson=false;exploded=false;principle=!principle;selected=null;distance=principle?10.8f:12.8f;}
 public void Reverse(){direction=-direction;}
 public void ToggleTravel(){travel=!travel;statusText.text=travel?"行驶模式 · 轮缘速度与轨道位移保持一致":"原地观察模式";}
 public void ToggleDoors(){var d=GetComponent<LocomotiveDoorsV11>();if(d)d.ToggleAll();else doorsOpen=!doorsOpen;}
 public void ResetView(){doorsOpen=false;var d=GetComponent<LocomotiveDoorsV11>();if(d)d.CloseAll();running=false;exploded=false;assemblyLesson=false;principle=false;travel=false;travelled=0;phase=25;direction=1;selected=null;distance=12.8f;pitch=16;yaw=26;focus=new Vector3(.1f,1.5f,0);statusText.text="";ApplyPose();}
 public void Select(LocomotivePart p){if(assemblyLesson&&assembled<parts.Length){if(p==parts[assembled])NextPart();return;}selected=p;if(workshop)workshop.feedback.text=p.title+" · "+p.explanation;UpdateLabels();}
 public void FocusNext(){lessonIndex=(lessonIndex+1)%4;selected=null;assemblyLesson=false;exploded=false;if(lessonIndex==1){principle=true;focus=new Vector3(2.05f,.95f,1.15f);yaw=8;pitch=20;distance=6.5f;statusText.text="配汽机构：观察滑阀如何交替打开气口。";}else if(lessonIndex==2){principle=true;focus=new Vector3(.55f,2.2f,0);yaw=25;pitch=25;distance=8.5f;statusText.text="热循环：烟气走管内，锅炉水在管外。";}else if(lessonIndex==3){principle=false;doorsOpen=true;focus=new Vector3(-1.6f,2.3f,0);yaw=-125;pitch=18;distance=6.8f;statusText.text="操纵区：压力表和调节阀位于驾驶室内。";}else{principle=false;focus=new Vector3(.1f,1.5f,0);yaw=26;pitch=16;distance=12.8f;statusText.text="整机视角";}ApplyPose();SetCamera();}
 void Orbit(){if(!inputEnabled)return;if(studioCamera==null||!studioCamera.enabled)return;
  bool over=EventSystem.current&&EventSystem.current.IsPointerOverGameObject();if(Input.touchCount>0&&EventSystem.current)over=EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
  if(Input.touchCount==2){var a=Input.GetTouch(0);var b=Input.GetTouch(1);distance=Mathf.Clamp(distance-(Vector2.Distance(a.position,b.position)-Vector2.Distance(a.position-a.deltaPosition,b.position-b.deltaPosition))*.012f,4,22);}
  if(Input.GetMouseButtonDown(0)){lastPointer=Input.mousePosition;downPointer=lastPointer;}
  if(!over&&Input.GetMouseButton(0)&&Input.touchCount<2){Vector2 p=Input.mousePosition,d=p-lastPointer;if(d.magnitude<200){yaw+=d.x*.16f;pitch=Mathf.Clamp(pitch-d.y*.12f,-4,65);}lastPointer=p;}
  if(!over&&Input.GetMouseButtonUp(0)&&Vector2.Distance(downPointer,Input.mousePosition)<10){RaycastHit hit;if(Physics.Raycast(studioCamera.ScreenPointToRay(Input.mousePosition),out hit,100)){var p=hit.collider.GetComponentInParent<LocomotivePart>();if(p)Select(p);}}
  if(!over)distance=Mathf.Clamp(distance-Input.mouseScrollDelta.y*.6f,4,22);SetCamera();
 }
 public void SetCamera(){if(externalView)return;Vector3 target=locomotive.position+focus;studioCamera.transform.position=target+Quaternion.Euler(-pitch,yaw,0)*new Vector3(0,0,distance);studioCamera.transform.LookAt(target);}
}





