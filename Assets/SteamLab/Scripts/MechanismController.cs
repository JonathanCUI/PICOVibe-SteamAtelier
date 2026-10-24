using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class MechanismController : MonoBehaviour {
 public Transform mechanism,wheel,rod,crosshead,guide,crankpin,wristpin;
 public Camera studioCamera;
 public GameObject environment;
 public TMP_Text titleText,lessonText,metricsText,partText;
 public Slider phaseSlider;
 public Button runButton;
 public float phase=25, rpm=10;
 public bool running, exploded, assemblyLesson;
 public int assembled=5;
 public const float Radius=.28f, RodLength=2.15f, AxisHeight=.8f, RodZ=.2f;
 readonly string[] names={"导轨","动轮","十字头","主连杆","曲柄销"};
 readonly string[] lessons={
  "① 安装导轨 P16\n导轨限制十字头只能沿直线移动。",
  "② 安装动轮 P09\n轮轴的位置固定，动轮只能绕轴旋转。",
  "③ 放入十字头 P14\n它沿导轨往复滑动，为活塞杆承受侧向力。",
  "④ 装入主连杆 P12\n连杆两孔中心距固定，连接十字头与轮上偏心销。",
  "⑤ 插入两处销轴 P15\n一处连接轮上偏心孔，一处连接十字头叉孔；两端都可转动。",
  "组装完成\n转动动轮，观察十字头的 560 mm 行程。\n这里演示运动关系，动力来自蒸汽活塞。"
 };
 bool updatingSlider; Vector2 lastPointer; float yaw=12,pitch=14,distance=5.4f;
 int qaFrame; string qaPath; bool qaDone; float[] qaAngles={0,90,180,270};
 public void Start(){
  Application.targetFrameRate=60;
  var args=Environment.GetCommandLineArgs();
  var idx=Array.IndexOf(args,"--qa");if(idx>=0&&idx+1<args.Length){qaPath=args[idx+1];Directory.CreateDirectory(qaPath);running=false;}
  if(phaseSlider){phaseSlider.minValue=0;phaseSlider.maxValue=360;phaseSlider.onValueChanged.AddListener(v=>{if(!updatingSlider){phase=v;running=false;}});}
  ApplyPose();UpdateLabels();
 }
 public static Vector3 Crank(float degrees){float a=degrees*Mathf.Deg2Rad;return new Vector3(Radius*Mathf.Cos(a),AxisHeight+Radius*Mathf.Sin(a),RodZ);}
 public static Vector3 Slider(float degrees){float a=degrees*Mathf.Deg2Rad,s=Radius*Mathf.Sin(a);return new Vector3(Radius*Mathf.Cos(a)+Mathf.Sqrt(RodLength*RodLength-s*s),AxisHeight,RodZ);}
 public void Update(){
  if(running&&!exploded&&!assemblyLesson)phase=Mathf.Repeat(phase+rpm*6*Time.deltaTime,360);
  ApplyPose();UpdateLabels();
  if(string.IsNullOrEmpty(qaPath))Orbit();
  else RunQa();
 }
 void Orbit(){
  if(studioCamera==null||!studioCamera.enabled)return;
  if(Input.touchCount==2){var a=Input.GetTouch(0);var b=Input.GetTouch(1);float before=Vector2.Distance(a.position-a.deltaPosition,b.position-b.deltaPosition);float now=Vector2.Distance(a.position,b.position);distance=Mathf.Clamp(distance-(now-before)*.006f,3.5f,8);}
  if(Input.touchCount>0&&EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId))return;
  if(Input.GetMouseButtonDown(0))lastPointer=Input.mousePosition;
  if(Input.GetMouseButton(0)&&Input.touchCount<2&&!EventSystem.current.IsPointerOverGameObject()){
   Vector2 p=Input.mousePosition,d=p-lastPointer;
   if(d.magnitude<200){yaw+=d.x*.15f;pitch=Mathf.Clamp(pitch-d.y*.12f,-5,45);}lastPointer=p;
  }
  distance=Mathf.Clamp(distance-Input.mouseScrollDelta.y*.3f,3.5f,8);
  Quaternion q=Quaternion.Euler(pitch,yaw,0);
  Vector3 target=new Vector3(1.1f,.78f,0);
  studioCamera.transform.position=target+q*new Vector3(0,0,distance);
  studioCamera.transform.LookAt(target);
 }
 public void ApplyPose(){
  var a=Crank(phase);var b=Slider(phase);var d=b-a;
  wheel.localPosition=new Vector3(0,AxisHeight,0);wheel.localRotation=Quaternion.Euler(0,0,phase);
  rod.localPosition=a;rod.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);
  crosshead.localPosition=b;crosshead.localRotation=Quaternion.identity;
  crankpin.localPosition=a;crankpin.localRotation=Quaternion.identity;
  wristpin.localPosition=b+Vector3.forward*.029f;wristpin.localRotation=Quaternion.identity;
  guide.localPosition=new Vector3(2.275f,AxisHeight,RodZ);guide.localRotation=Quaternion.identity;
  Transform[] parts={guide,wheel,crosshead,rod,crankpin};
  for(int i=0;i<parts.Length;i++){
   bool detached=exploded||(assemblyLesson&&i>=assembled);
   if(detached){Vector3[] offsets={new Vector3(0,.65f,.05f),new Vector3(-.1f,.1f,.35f),new Vector3(.25f,.2f,.95f),new Vector3(0,-.18f,1),new Vector3(-.15f,.7f,1.35f)};parts[i].localPosition+=offsets[i];}
   parts[i].gameObject.SetActive(true);
  }
  if(exploded||(assemblyLesson&&assembled<5))wristpin.localPosition+=new Vector3(.35f,.55f,1.4f);
  if(phaseSlider){updatingSlider=true;phaseSlider.value=phase;updatingSlider=false;}
 }
 void UpdateLabels(){
  if(lessonText)lessonText.text=assemblyLesson?lessons[Mathf.Clamp(assembled,0,5)]:
   (exploded?"拆开观察\n每个零件独立移动；销孔、导向面与转轴决定装配位置。":"运动原理\n十字头作直线往复运动，主连杆推动偏心曲柄销，动轮绕轴旋转。\n本试验不包含蒸汽供给和配汽机构。");
  if(metricsText)metricsText.text="曲柄角  "+phase.ToString("0")+"°\n位移  "+((Slider(phase).x-(RodLength-Radius))*1000).ToString("0")+" / 560 mm\n连杆孔距  2150 mm";
  if(partText)partText.text=assemblyLesson?("装配进度  "+assembled+" / 5"):(exploded?"5 类独立组件 · 2 处销轴":"尺寸校正后 · 固定长度运动");
 }
 public void ToggleRun(){if(exploded||assemblyLesson){exploded=false;assemblyLesson=false;assembled=5;}running=!running;}
 public void ToggleExplode(){running=false;assemblyLesson=false;exploded=!exploded;}
 public void StartLesson(){running=false;exploded=false;assemblyLesson=true;assembled=0;phase=0;}
 public void NextPart(){if(!assemblyLesson){StartLesson();return;}assembled=Mathf.Min(5,assembled+1);}
 public void ResetView(){running=false;exploded=false;assemblyLesson=false;assembled=5;phase=25;yaw=12;pitch=14;distance=5.4f;}
 void RunQa(){
  qaFrame++;
  if(qaFrame==20){Validate();phase=0;}
  if(qaFrame==40)ScreenCapture.CaptureScreenshot(Path.Combine(qaPath,"assembled.png"));
  for(int i=0;i<4;i++){if(qaFrame==70+i*35)phase=qaAngles[i];if(qaFrame==85+i*35)ScreenCapture.CaptureScreenshot(Path.Combine(qaPath,"phase-"+qaAngles[i]+".png"));}
  if(qaFrame==220){GameObject.Find("拆开 / 合上").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();phase=35;}
  if(qaFrame==240)ScreenCapture.CaptureScreenshot(Path.Combine(qaPath,"exploded.png"));
  if(qaFrame==270){GameObject.Find("逐件组装").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();var b=GameObject.Find("装下一件").GetComponent<UnityEngine.UI.Button>();b.onClick.Invoke();b.onClick.Invoke();b.onClick.Invoke();File.WriteAllText(Path.Combine(qaPath,"ui-qa.json"),"{\"assemblyButtonWorked\":"+(assemblyLesson&&assembled==3?"true":"false")+",\"persistentNextListeners\":"+b.onClick.GetPersistentEventCount()+"}");}
  if(qaFrame==290)ScreenCapture.CaptureScreenshot(Path.Combine(qaPath,"assembly-lesson.png"));
  if(qaFrame>330&&!qaDone){qaDone=true;File.WriteAllText(Path.Combine(qaPath,"complete.txt"),"QA complete");Application.Quit();}
 }
 public void Validate(){
  double maxDistanceError=0,minTravel=9,maxTravel=-9,minEndGap=9;float savedPhase=phase;
  for(int i=0;i<=720;i++){
   var a=Crank(i*.5f);var b=Slider(i*.5f);
   phase=i*.5f;ApplyPose();
   maxDistanceError=Math.Max(maxDistanceError,Math.Max(Vector3.Distance(rod.position,crankpin.position),Vector3.Distance(rod.TransformPoint(Vector3.right*RodLength),crosshead.position)));
   minTravel=Math.Min(minTravel,b.x);maxTravel=Math.Max(maxTravel,b.x);
   minEndGap=Math.Min(minEndGap,Math.Min(b.x-.09-1.72,2.83-b.x-.19));
  }
  phase=savedPhase;ApplyPose();string report="{\"samples\":721,\"maxJointClosureErrorMetres\":"+maxDistanceError.ToString("G9",System.Globalization.CultureInfo.InvariantCulture)+
  ",\"sliderMin\":"+minTravel.ToString("G9",System.Globalization.CultureInfo.InvariantCulture)+",\"sliderMax\":"+maxTravel.ToString("G9",System.Globalization.CultureInfo.InvariantCulture)+
  ",\"minGuideEndGapMetres\":"+minEndGap.ToString("G9",System.Globalization.CultureInfo.InvariantCulture)+",\"note\":\"Kinematic and envelope checks; mesh interference checked separately\"}";
  if(!string.IsNullOrEmpty(qaPath))File.WriteAllText(Path.Combine(qaPath,"motion-qa.json"),report);
  Debug.Log(report);
 }
}




