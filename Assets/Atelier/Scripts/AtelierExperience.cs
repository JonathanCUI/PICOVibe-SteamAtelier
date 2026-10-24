using System;using System.Collections.Generic;using System.Linq;using UnityEngine;using TMPro;

[DefaultExecutionOrder(1200)]public class AtelierExperience:MonoBehaviour {
 public LocomotiveController engine;public LocomotiveFreeAssembly assembly;public LocomotiveDriveV9 drive;public LocomotivePresentationV4 presentation;
 public Transform throttlePivot,brakePivot,reversePivot,pressureNeedle,speedNeedle,trayRoot,floatingStatus;public TMP_Text status,speedText,pressureText,assemblyText,helpText,hoverText;public TMP_Text[] trayLabels;
 public bool Ready {get;private set;}
 public bool precisionDesk;public Transform statusDock;
 public Transform categoryPivot,resetGuard,lightToggle,sectionToggle,modeToggle,whistleCap;
 public Collider resetButton;public TMP_Text pageCounter;public TMP_Text[] categoryLabels;public GameObject[] trayOutlines;
 public int trayCategory;public bool guardOpen;public float resetProgress;float secondBlend,whistleAt;bool resetHolding;
 public int[] FilteredParts()=>Enumerable.Range(0,assembly.pieces.Length).Where(i=>trayCategory==0||assembly.pieces[i].category==trayCategory-1).ToArray();
 public int PageCount=>Mathf.Max(1,Mathf.CeilToInt(FilteredParts().Length/8f));
 public Transform[] traySlots;public Light secondLamp;public Renderer secondLens;public bool travelling;public float throttle,brake,pressure;public int direction=1,trayPage;public GameObject[] trayModels;
 public Vector3 viewTarget=new Vector3(0,1.2f,.5f);public float viewDistance=18.5f,viewYaw=32,viewPitch=21;
 AtelierControl held;Vector2 down,last;float heldValue;Material lens;float displayTime;bool manipulatingPart;int trayRevision=-1;public int operations;
 void Start(){engine.externalView=true;engine.inputEnabled=false;assembly.desktopInput=false;assembly.canvas.enabled=false;engine.studioCamera.rect=new Rect(0,0,1,1);if(presentation.tabletCamera)presentation.tabletCamera.enabled=false;
  assembly.AssembleForDisplay();drive.ResetDrive();travelling=false;throttle=0;brake=0;direction=1;engine.running=false;assembly.effects.fireEnabled=false;drive.limit=4.4f;assembly.showHint=true;presentation.lightOn=true;
  if(secondLens)lens=secondLens.material;RefreshTray();SetView(0);ApplyCamera();Ready=true;if(Environment.GetCommandLineArgs().Contains("-atelier-smoke"))StartCoroutine(PlayerSmokeCheck());}
 System.Collections.IEnumerator PlayerSmokeCheck(){for(int i=0;i<90;i++)yield return null;assembly.effects.fireEnabled=true;yield return new WaitForSeconds(3);ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.persistentDataPath,"08-player-workbench.png"));yield return new WaitForSeconds(2);System.IO.File.WriteAllText(System.IO.Path.Combine(Application.persistentDataPath,"player-smoke.txt"),(assembly.Complete&&floatingStatus?"PASS":"FAIL")+" standalone launch; "+Screen.width+"x"+Screen.height+"; "+SystemInfo.graphicsDeviceName);Application.Quit();}
 public void SetView(int view){if(view==0){viewTarget=new Vector3(0,1.1f,1.0f);viewDistance=18.8f;viewYaw=32;viewPitch=21;}else if(view==1){viewTarget=new Vector3(2.8f,1.5f,0);viewDistance=6.8f;viewYaw=52;viewPitch=16;}else if(view==2){viewTarget=new Vector3(-1.65f,2.3f,0);viewDistance=5.3f;viewYaw=-112;viewPitch=18;}else if(view==3){viewTarget=new Vector3(.1f,1.9f,0);viewDistance=10.5f;viewYaw=17;viewPitch=17;}ApplyCamera();}
 void ApplyCamera(){var cam=engine.studioCamera;cam.transform.position=viewTarget+Quaternion.Euler(-viewPitch,viewYaw,0)*Vector3.forward*viewDistance;cam.transform.LookAt(viewTarget);}
 public void SetThrottle(float value){throttle=Mathf.Clamp01(value);brake=0;assembly.SetSpeed(6+54*throttle);if(!assembly.Complete||direction==0){throttle=0;drive.Brake();return;}assembly.effects.fireEnabled=true;if(throttle<.035f){drive.Brake();if(!travelling)engine.running=false;}else if(travelling)drive.Move(direction);else{drive.Observation();engine.running=true;engine.direction=direction;}}
 public void Activate(AtelierAction action,int index=0){operations++;switch(action){
  case AtelierAction.Reverse:SetDirection(direction==1?0:direction==0?-1:1);break;
  case AtelierAction.Brake:brake=1;throttle=0;drive.Brake();break;
  case AtelierAction.Whistle:presentation.Whistle();whistleAt=Time.unscaledTime+.85f;break;
  case AtelierAction.Lights:presentation.ToggleHeadlight();break;
  case AtelierAction.Cutaway:assembly.ToggleCutaway();if(engine.principle)SetView(3);break;
  case AtelierAction.Observation:if(Mathf.Abs(drive.signedRpm)>.1f){Activate(AtelierAction.Brake);return;}travelling=!travelling;drive.Observation();if(travelling&&throttle>.03f)drive.Move(direction);break;
  case AtelierAction.Assembly:resetHolding=guardOpen;resetProgress=0;break;
  case AtelierAction.ResetGuard:guardOpen=!guardOpen;resetHolding=false;resetProgress=0;if(resetButton)resetButton.enabled=guardOpen;break;
  case AtelierAction.Category:trayCategory=index>0?Mathf.Clamp(index-1,0,3):(trayCategory+1)%4;trayPage=0;RefreshTray();break;
  case AtelierAction.TrayPrevious:trayPage=(trayPage+PageCount-1)%PageCount;RefreshTray();break;
  case AtelierAction.TrayNext:trayPage=(trayPage+1)%PageCount;RefreshTray();break;
  case AtelierAction.TrayPart:if(index>=0&&index<assembly.pieces.Length&&!assembly.pieces[index].installed){assembly.SelectPiece(index);assembly.BeginPointerDrag();manipulatingPart=true;RefreshTray();}break;
  case AtelierAction.Home:SetView(0);break;
  case AtelierAction.Interior:SetView(2);engine.GetComponent<LocomotiveDoorsV11>().OpenAll();break;
 }if(action!=AtelierAction.Whistle)presentation.PlayPick();}
 public void SetDirection(int value){Activate(AtelierAction.Brake);direction=Mathf.Clamp(value,-1,1);if(direction!=0)engine.direction=direction;}
 public bool HoldReset(float seconds){if(!guardOpen||!resetHolding)return false;resetProgress+=Mathf.Max(0,seconds);if(resetProgress<1.2f)return false;resetHolding=false;resetProgress=0;guardOpen=false;if(resetButton)resetButton.enabled=false;Activate(AtelierAction.Brake);engine.principle=false;drive.ResetDrive();assembly.ResetPuzzle();assembly.canvas.enabled=false;assembly.desktopInput=false;assembly.showHint=true;trayPage=0;trayCategory=0;direction=1;SetView(0);RefreshTray();return true;}
 public void CancelReset(){resetHolding=false;resetProgress=0;}
 void Update(){if(!assembly.canvas)return;var cam=engine.studioCamera;Vector2 mouse=Input.mousePosition;RaycastHit hit;AtelierControl target=null;if(Physics.Raycast(cam.ScreenPointToRay(mouse),out hit,150,~((1<<29)|(1<<30)|(1<<2))))target=hit.collider.GetComponentInParent<AtelierControl>();
  if(hoverText)hoverText.text=resetHolding?"重新装配 · 按住 "+Mathf.Clamp01(resetProgress/1.2f).ToString("P0"):target?target.caption:"";
  if(Input.GetMouseButtonDown(0)){down=last=mouse;if(target){held=target;target.PressFeedback();heldValue=target.action==AtelierAction.Throttle?throttle:brake;if(target.action!=AtelierAction.Throttle&&target.action!=AtelierAction.Brake)Activate(target.action,target.index);else if(target.action==AtelierAction.Brake)Activate(AtelierAction.Brake);}else{assembly.BeginWorldPointer(mouse);manipulatingPart=true;}}
  if(Input.GetMouseButton(0)){if(held&&held.action==AtelierAction.Assembly)HoldReset(Time.unscaledDeltaTime);if(held&&held.action==AtelierAction.Whistle&&Time.unscaledTime>whistleAt){presentation.Whistle();whistleAt=Time.unscaledTime+.85f;}if(held&&held.action==AtelierAction.Throttle)SetThrottle(heldValue+(mouse.y-down.y)*.006f);else if(manipulatingPart){assembly.DragWorldPointer(mouse);if(assembly.dragging&&Vector2.Distance(mouse,down)>6)assembly.TabletopDrag(mouse);}}
  if(Input.GetMouseButtonUp(0)){if(manipulatingPart){assembly.EndWorldPointer(mouse);if(assembly.dragging)assembly.ReleaseGrab();}held=null;manipulatingPart=false;CancelReset();RefreshTray();}
  if(Input.GetMouseButtonDown(1)||Input.GetMouseButtonDown(2))last=mouse;
  if(Input.GetMouseButton(1)){var delta=mouse-last;viewYaw+=delta.x*.20f;viewPitch=Mathf.Clamp(viewPitch-delta.y*.16f,4,70);}
  if(Input.GetMouseButton(2)){var delta=mouse-last;viewTarget-=(cam.transform.right*delta.x+cam.transform.up*delta.y)*viewDistance*.0011f;}
  last=mouse;if(!assembly.dragging)viewDistance=Mathf.Clamp(viewDistance-Input.mouseScrollDelta.y*.6f,2.8f,26);
  if(Input.GetKeyDown(KeyCode.Space))Activate(AtelierAction.Brake);if(Input.GetKeyDown(KeyCode.F))SetView(0);if(Input.GetKeyDown(KeyCode.C))Activate(AtelierAction.Cutaway);if(Input.GetKeyDown(KeyCode.Alpha1))SetView(1);if(Input.GetKeyDown(KeyCode.Alpha2))SetView(2);if(Input.GetKeyDown(KeyCode.Escape))assembly.LeaveInSpace();
  if(Input.GetKey(KeyCode.W))SetThrottle(throttle+Time.deltaTime*.3f);if(Input.GetKey(KeyCode.S))SetThrottle(throttle-Time.deltaTime*.3f);if(Input.GetKeyDown(KeyCode.R))Activate(AtelierAction.Reverse);
 }
 void LateUpdate(){ApplyCamera();if(floatingStatus&&!precisionDesk)floatingStatus.rotation=engine.studioCamera.transform.rotation;if(throttlePivot)throttlePivot.localRotation=Quaternion.Euler(precisionDesk?6-52*throttle:32-64*throttle,0,0);if(brakePivot)brakePivot.localRotation=Quaternion.Euler(-28+56*brake,0,0);if(reversePivot)reversePivot.localRotation=Quaternion.Euler(-direction*25,0,0);
  if(resetGuard)resetGuard.localRotation=Quaternion.RotateTowards(resetGuard.localRotation,Quaternion.Euler(guardOpen?-110:0,0,0),Time.unscaledDeltaTime*260);
  if(lightToggle)lightToggle.localRotation=Quaternion.Euler(presentation.lightOn?-22:22,0,0);if(sectionToggle)sectionToggle.localRotation=Quaternion.Euler(engine.principle?-22:22,0,0);if(modeToggle)modeToggle.localRotation=precisionDesk?Quaternion.Euler(0,travelling?-40:40,0):Quaternion.Euler(travelling?-22:22,0,0);if(whistleCap)whistleCap.localPosition=new Vector3(0,held&&held.action==AtelierAction.Whistle?-.035f:0,0);if(categoryPivot)categoryPivot.localRotation=Quaternion.Euler(0,trayCategory*90,0);
  pressure=Mathf.MoveTowards(pressure,assembly.Complete&&assembly.effects.fireEnabled?9.2f+(engine.running?.65f:0):0,Time.deltaTime*1.8f);if(pressureNeedle)pressureNeedle.localRotation=Quaternion.Euler(0,0,-130+pressure*26);if(speedNeedle)speedNeedle.localRotation=Quaternion.Euler(0,0,-130+drive.SpeedKmh*13);
  secondBlend=Mathf.MoveTowards(secondBlend,presentation.lightOn&&assembly.HasPiece("lheadlamp")?1:0,Time.deltaTime*(presentation.lightOn?2.2f:1.6f));if(secondLamp){secondLamp.enabled=secondBlend>.01f;secondLamp.intensity=5*secondBlend*secondBlend;}if(presentation.headlight)presentation.headlight.intensity=5*presentation.lightBlend*presentation.lightBlend;if(lens){lens.EnableKeyword("_EMISSION");lens.SetColor("_EmissionColor",new Color(1,.73f,.34f)*3.5f*secondBlend);}
  if(trayRevision!=assembly.InstalledCount){RefreshTray();trayRevision=assembly.InstalledCount;}
  if(Time.unscaledTime<displayTime)return;displayTime=Time.unscaledTime+.1f;
  if(status)status.text=!assembly.Complete?"装配工作台":drive.state=="到达终端"?"轨道终端 · 已停车":engine.running?(travelling?"轨道行驶":"原地演示"):"整备就绪";
  if(speedText)speedText.text=drive.SpeedKmh.ToString("00.0")+"  km/h";if(pressureText)pressureText.text=pressure.ToString("0.0")+"  bar";
  if(assemblyText)assemblyText.text=assembly.InstalledCount+" / "+assembly.pieces.Length+(assembly.Complete?"  装配完成":"  已安装");
  if(helpText)helpText.text=assembly.selected>=0?assembly.pieces[assembly.selected].title+"  ·  "+assembly.pieces[assembly.selected].description:"拖动金色操纵杆启动 · 红色手柄制动 · 右键环视 / 滚轮靠近";
 }
 public void RefreshTray(){
  if(trayModels!=null)foreach(var go in trayModels)if(go){go.SetActive(false);Destroy(go);}trayModels=new GameObject[8];if(traySlots==null)return;
  int[] ids=FilteredParts();trayPage=Mathf.Clamp(trayPage,0,PageCount-1);if(pageCounter)pageCounter.text=(trayPage+1).ToString("00")+" / "+PageCount.ToString("00");
  if(categoryLabels!=null)for(int i=0;i<categoryLabels.Length;i++)categoryLabels[i].color=i==trayCategory?new Color(1,.72f,.32f):new Color(.62f,.64f,.63f);
  for(int slot=0;slot<traySlots.Length;slot++){int at=trayPage*8+slot,index=at<ids.Length?ids[at]:-1;var control=traySlots[slot].GetComponent<AtelierControl>();if(control)control.index=index;if(trayOutlines!=null&&slot<trayOutlines.Length)trayOutlines[slot].SetActive(index>=0&&index==assembly.selected);
   if(index<0){if(trayLabels[slot])trayLabels[slot].text="";continue;}var p=assembly.pieces[index];if(trayLabels[slot])trayLabels[slot].text=p.title+(p.installed?" · 已装":"");if(p.installed)continue;
   if(assembly.looseObjects!=null&&assembly.looseObjects[index]){if(trayLabels[slot])trayLabels[slot].text=p.title+" · 台面";continue;}
   var model=Instantiate(p.prefab,traySlots[slot]);model.name="TrayPreview_"+p.key;float scale=.70f/Mathf.Max(p.size.x,p.size.y,p.size.z);model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.Euler(0,20,0);model.transform.localScale=Vector3.one*scale;
   // Bounds in the inclined bay's own frame keep every item seated on the felt.
   var filters=model.GetComponentsInChildren<MeshFilter>().Where(m=>m.sharedMesh&&m.GetComponent<MeshRenderer>()&&m.GetComponent<MeshRenderer>().enabled).ToArray();Bounds b=new Bounds();bool first=true;
   foreach(var mf in filters){var bounds=mf.sharedMesh.bounds;var matrix=traySlots[slot].worldToLocalMatrix*mf.transform.localToWorldMatrix;for(int mask=0;mask<8;mask++){var corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((mask&1)==0?-1:1,(mask&2)==0?-1:1,(mask&4)==0?-1:1));var q=matrix.MultiplyPoint3x4(corner);if(first){b=new Bounds(q,Vector3.zero);first=false;}else b.Encapsulate(q);}}
   if(!first)model.transform.localPosition+=new Vector3(-b.center.x,.037f-b.min.y,-.075f-b.center.z);
   foreach(var col in model.GetComponentsInChildren<Collider>(true)){col.enabled=false;Destroy(col);}foreach(var t in model.GetComponentsInChildren<Transform>(true))t.gameObject.layer=2;trayModels[slot]=model;
  }
 }
 void OnDestroy(){if(lens)Destroy(lens);}
}

