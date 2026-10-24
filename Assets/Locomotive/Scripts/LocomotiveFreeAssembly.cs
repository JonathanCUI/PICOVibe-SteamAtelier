using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

[Serializable] public class SteamPuzzlePiece {
 public string key,title,description,fitKey; public int category; public GameObject prefab;
 public LocomotivePart[] members; public Vector3 centre,size; public bool installed;
}
[DefaultExecutionOrder(100)]
public class LocomotiveFreeAssembly : MonoBehaviour {
 public bool tabletop;
 public LocomotiveController engine; public LocomotiveThermalEffects effects;
 public SteamPuzzlePiece[] pieces; public TMP_FontAsset font; public Material ghostMaterial;
 public GameObject infoCard; public GameObject[] slotBorders; public TMP_Text[] categoryLabels; public Canvas canvas; public TMP_Text infoTitle,infoDescription,infoMeta,progress,toast,pageLabel,runLabel,hintLabel,referenceLabel;
 public UnityEngine.UI.Button removeButton,returnButton,runButton,cutawayButton; public UnityEngine.UI.Image[] slotBackgrounds; public TMP_Text[] slotLabels;
 public Camera shelfCamera; public Transform shelfRoot; public RenderTexture shelfTexture;
 public int selected=-1,category,page,acceptedDrops,rejectedDrops; public bool dragging,desktopInput=true,showHint,showReference;
 public GameObject token,ghost,reference; public GameObject consolePrefab; public GameObject[] looseObjects; public Vector3[] safePositions; public Quaternion[] safeRotations; public float[] safeScales; public LocomotivePresentationV4 presentation; public string lastDrop; public int looseDrops; bool orbitCaptured,panCaptured; float dragDepth; public int[] visibleSlots=new int[8]; public float snapRadius=.46f;
 public UnityEngine.UI.Slider speedControl; public TMP_Text speedReadout; public float requestedRpm=24;
 public void SetSpeed(float value){requestedRpm=Mathf.Clamp(value,6,60);if(speedControl&&Mathf.Abs(speedControl.value-requestedRpm)>.01f)speedControl.SetValueWithoutNotify(requestedRpm);if(speedReadout)speedReadout.text=requestedRpm.ToString("0")+" 转/分";if(!engine.running)engine.rpm=requestedRpm;}
 public bool Complete => pieces!=null&&pieces.All(p=>p.installed);
 public int InstalledCount => pieces.Count(p=>p.installed);
 public int[] slotOccupants,pieceSlots; public int activeSocket=-1; int pendingPull=-1,pulledFrom=-1; bool pulledClear=true;
 public Vector3 Socket => selected<0||activeSocket<0?Vector3.positiveInfinity:SlotPosition(activeSocket);
 public Vector3 SlotPosition(int slot)=>engine.locomotive.TransformPoint(pieces[slot].centre);
 public bool SlotInstalled(int slot)=>slotOccupants!=null&&slotOccupants[slot]>=0;
 public bool Compatible(int piece,int slot)=>piece>=0&&slot>=0&&pieces[piece].fitKey==pieces[slot].fitKey;
 public int OccupantOf(int slot)=>slotOccupants[slot];
 public bool CanFit(int piece,int slot)=>Compatible(piece,slot)&&!SlotInstalled(slot);
 int ClosestSocket(Vector3 world){int best=-1;float distance=float.MaxValue;for(int i=0;i<pieces.Length;i++)if(CanFit(selected,i)){float d=Vector3.SqrMagnitude(world-SlotPosition(i));if(d<distance){distance=d;best=i;}}return best;}
 void AimSocket(int slot){if(activeSocket==slot)return;activeSocket=slot;if(ghost)Destroy(ghost);ghost=null;if(slot<0)return;ghost=Instantiate(pieces[slot].prefab,SlotPosition(slot),engine.locomotive.rotation);Ghostify(ghost);ghost.name="同规格安装位置";}

 public bool HasPiece(string key)=>Enumerable.Range(0,pieces.Length).Any(i=>pieces[i].key==key&&SlotInstalled(i));
 public bool HasSystem(int module){if(module==0)return HasPiece("boiler")&&HasPiece("firebox");return Complete;}
 readonly Dictionary<LocomotivePart,int> membership=new Dictionary<LocomotivePart,int>();
 readonly List<GameObject> shelfModels=new List<GameObject>(); readonly Stack<int> history=new Stack<int>();
 LocomotivePart pendingInteraction; Vector3 grabLocal,pendingGrabLocal; Vector2 pointerDown,lastPointer; bool pointerMoved,leftOrbit; float toastUntil,resetUntil; int hoverSlot=-1;
 static readonly Color Paper=new Color(.92f,.94f,.89f),Muted=new Color(.59f,.70f,.66f),Gold=new Color(.93f,.73f,.41f),Panel=new Color(.035f,.075f,.067f,.96f);
 public bool IsPartVisible(LocomotivePart part){int i;return !membership.TryGetValue(part,out i)||SlotInstalled(i);}
 void Awake(){slotOccupants=Enumerable.Repeat(-1,pieces.Length).ToArray();pieceSlots=Enumerable.Repeat(-1,pieces.Length).ToArray();looseObjects=new GameObject[pieces.Length];safePositions=new Vector3[pieces.Length];safeRotations=new Quaternion[pieces.Length];safeScales=new float[pieces.Length];presentation=GetComponent<LocomotivePresentationV4>();engine.freeAssembly=this;effects.freeAssembly=this;for(int i=0;i<pieces.Length;i++)foreach(var p in pieces[i].members)membership[p]=i;}
 void Start(){BuildUI();if(!tabletop)BuildShelf();ResetPuzzle();if(tabletop)canvas.enabled=false;}
 void Update(){if(!engine.travel)engine.rpm=Mathf.MoveTowards(engine.rpm,requestedRpm,40*Time.deltaTime);
  if(desktopInput){bool over=EventSystem.current&&EventSystem.current.IsPointerOverGameObject();bool inMain=engine.studioCamera.pixelRect.Contains(Input.mousePosition);
   if(Input.GetMouseButtonDown(1)){orbitCaptured=CanStartOrbit(Input.mousePosition,over);lastPointer=Input.mousePosition;}
   if(Input.GetMouseButtonDown(2)){panCaptured=CanStartOrbit(Input.mousePosition,over);lastPointer=Input.mousePosition;}
   if(orbitCaptured&&Input.GetMouseButton(1)){Vector2 now=Input.mousePosition,d=now-lastPointer;if(d.magnitude<200)OrbitBy(d);lastPointer=now;}
   if(panCaptured&&Input.GetMouseButton(2)){Vector2 now=Input.mousePosition,d=now-lastPointer;engine.focus-= (engine.studioCamera.transform.right*d.x+engine.studioCamera.transform.up*d.y)*engine.distance*.0013f;engine.SetCamera();lastPointer=now;}
   if(Input.GetMouseButtonUp(1))orbitCaptured=false;if(Input.GetMouseButtonUp(2))panCaptured=false;
   if(Input.GetMouseButtonDown(0)&&!over&&CanStartOrbit(Input.mousePosition,false))BeginWorldPointer(Input.mousePosition);
   if(pendingPull>=0&&Input.GetMouseButton(0))DragWorldPointer(Input.mousePosition);
   if(leftOrbit&&Input.GetMouseButton(0)){Vector2 now=Input.mousePosition,d=now-lastPointer;if(d.magnitude<200)OrbitBy(d);lastPointer=now;}
   if(!over&&inMain&&Input.mouseScrollDelta.y!=0){if(dragging){dragDepth=Mathf.Clamp(dragDepth+Input.mouseScrollDelta.y*.55f,2,35);}else{engine.distance=Mathf.Clamp(engine.distance-Input.mouseScrollDelta.y*.6f,5,25);engine.SetCamera();}}
   if(dragging&&Input.GetMouseButton(0)&&!Input.GetMouseButton(1)){if(Vector2.Distance(pointerDown,Input.mousePosition)>6)pointerMoved=true;if(pointerMoved&&inMain)MovePointer(Input.mousePosition);}
   if(Input.GetMouseButtonUp(0))EndWorldPointer(Input.mousePosition);
   if(Input.GetKeyDown(KeyCode.Escape))LeaveInSpace();if(Input.GetKeyDown(KeyCode.H))ToggleHint();if(Input.GetKeyDown(KeyCode.F))ResetCamera();
   if(Input.GetKeyDown(KeyCode.Q)&&token)token.transform.Rotate(Vector3.up,30,Space.World);if(Input.GetKeyDown(KeyCode.E)&&token)token.transform.Rotate(Vector3.up,-30,Space.World);
   if(Input.GetKey(KeyCode.LeftControl)&&Input.GetKeyDown(KeyCode.Z))Undo();
  }
  UpdatePlacementHints();
  if(runLabel)runLabel.text=engine.running?"暂停运动":"启动机车";
  if(toast&&Time.unscaledTime>toastUntil)toast.text=Complete?"装配完成 · 可在操作台控制灯光、汽笛与剖视":"自由选件 · 可将零件暂放在周围空间";
 }
 public bool BeginWorldPointer(Vector2 screen){pendingInteraction=null;pendingPull=-1;pointerDown=screen;lastPointer=screen;pointerMoved=false;bool found=false;RaycastHit hit;
 if(Physics.Raycast(engine.studioCamera.ScreenPointToRay(screen),out hit,250,~((1<<30)|(1<<29)|(1<<2)))){var loose=hit.transform.GetComponentInParent<SteamLoosePiece>();if(loose){SelectPiece(loose.index);BeginPointerDrag();grabLocal=token.transform.InverseTransformPoint(hit.point);pointerDown=screen;found=true;}else{var part=hit.transform.GetComponentInParent<LocomotivePart>();int i;if(part&&membership.TryGetValue(part,out i)&&SlotInstalled(i)){int occupant=slotOccupants[i];SelectPiece(occupant);pendingGrabLocal=Quaternion.Inverse(engine.locomotive.rotation)*(hit.point-SlotPosition(i));pendingPull=occupant;pendingInteraction=part;found=true;}}}leftOrbit=!found;return found;}
 public void DragWorldPointer(Vector2 screen){if(pendingPull>=0&&Vector2.Distance(pointerDown,screen)>9){Vector2 down=pointerDown;pendingInteraction=null;TakeInstalled(pendingPull);pendingPull=-1;BeginPointerDrag();grabLocal=pendingGrabLocal;pointerDown=down;pointerMoved=true;}if(dragging&&pointerMoved)MovePointer(screen);}
 public void EndWorldPointer(Vector2 screen){
 if(pendingPull>=0&&pendingInteraction&&Vector2.Distance(pointerDown,screen)<=9){var d=GetComponent<LocomotiveDoorsV11>();if(d&&d.Toggle(pendingInteraction)){if(presentation)presentation.PlayPick();Notify(pendingInteraction.partId=="M02-D"?"炉门已切换 · 再次点击关闭或打开":"此侧车门已切换 · 拖动仍可拆卸驾驶室",3);}}
 pendingPull=-1;pendingInteraction=null;leftOrbit=false;if(dragging){if(pointerMoved&&engine.studioCamera.pixelRect.Contains(screen))ReleaseGrab();else dragging=false;}
 }
 static Bounds VisualBounds(GameObject go){var rr=go.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled&&!(r is ParticleSystemRenderer)).ToArray();var b=new Bounds(go.transform.position,Vector3.zero);if(rr.Length>0){b=rr[0].bounds;for(int i=1;i<rr.Length;i++)b.Encapsulate(rr[i].bounds);}return b;}
 public void UpdatePlacementHints(){bool overlap=token&&ghost&&VisualBounds(token).Intersects(VisualBounds(ghost));if(ghost)ghost.SetActive(!overlap&&(showHint||dragging&&token&&Vector3.Distance(token.transform.position,Socket)<snapRadius*1.8f));if(reference)foreach(Transform t in reference.transform){bool intersects=token&&VisualBounds(token).Intersects(VisualBounds(t.gameObject));bool selectedHint=ghost&&t.name=="参考_"+pieces[activeSocket].key;t.gameObject.SetActive(!intersects&&!selectedHint);}}
 public bool CanStartOrbit(Vector2 screen,bool uiBlocked){return !uiBlocked&&engine.studioCamera.pixelRect.Contains(screen)&&screen.y<Screen.height*.91f&&screen.y>Screen.height*.04f;}

 public void OrbitBy(Vector2 delta){engine.yaw+=delta.x*.22f;engine.pitch=Mathf.Clamp(engine.pitch-delta.y*.16f,-10,70);engine.SetCamera();}
 public void ResetCamera(){engine.yaw=26;engine.pitch=18;engine.distance=11.5f;engine.focus=new Vector3(.25f,1.0f,0);engine.SetCamera();}
 public void ResetPuzzle(){var drive=GetComponent<LocomotiveDriveV9>();if(drive)drive.ResetDrive();ClearSelection();for(int i=0;i<looseObjects.Length;i++){if(looseObjects[i])Destroy(looseObjects[i]);looseObjects[i]=null;}foreach(var p in pieces)p.installed=false;for(int i=0;i<pieces.Length;i++){slotOccupants[i]=pieceSlots[i]=-1;}pendingPull=pulledFrom=-1;pulledClear=true;acceptedDrops=rejectedDrops=looseDrops=0;history.Clear();engine.ResetView();engine.inputEnabled=false;engine.rpm=24;SetSpeed(24);effects.fireEnabled=false;effects.fireDoorOpen=false;showReference=false;referenceLabel.text="整机参考";if(reference)Destroy(reference);reference=null;category=page=0;ResetCamera();engine.ApplyPose();RefreshShelf();RefreshInfo();UpdateProgress();Notify("只留底盘骨架，选一个喜欢的零件开始吧。",6);}

 bool SettleOnWorkbench(){
  var bounds=VisualBounds(token);var size=bounds.size;Vector3 delta=bounds.center-token.transform.position;
  var preferred=bounds.center;preferred.y=-.465f+size.y*.5f;
  var candidates=new[]{preferred,new Vector3(0,preferred.y,4.9f),new Vector3(-1.5f,preferred.y,4.9f),new Vector3(1.5f,preferred.y,4.9f),new Vector3(-5.5f,preferred.y,-3.6f),new Vector3(-1.8f,preferred.y,-3.6f),new Vector3(2.2f,preferred.y,-3.6f),new Vector3(6.1f,preferred.y,-3.6f)};
  foreach(var center in candidates){var b=new Bounds(center,size+Vector3.one*.08f);if(b.min.x< -10.7f||b.max.x>10.7f||b.min.z< -4.9f||b.max.z>6.9f)continue;
   if(b.Intersects(new Bounds(new Vector3(0,1.5f,0),new Vector3(9,4,3.7f))))continue;
   if(b.Intersects(new Bounds(new Vector3(-6.25f,.5f,3.45f),new Vector3(4.6f,3,2.2f))))continue;
   if(b.Intersects(new Bounds(new Vector3(5.25f,.3f,3.25f),new Vector3(5.8f,2,2.9f))))continue;
   bool occupied=false;for(int i=0;i<looseObjects.Length;i++)if(i!=selected&&looseObjects[i]&&b.Intersects(VisualBounds(looseObjects[i]))){occupied=true;break;}if(occupied)continue;
   if(!SafeToLeave(selected,center-delta,token.transform.rotation,token.transform.localScale.x))continue;token.transform.position=center-delta;return true;
  }return false;
 }
 public void TabletopDrag(Vector2 screen){pointerMoved=true;MovePointer(screen);}
 public void AssembleForDisplay(){ClearSelection();for(int i=0;i<pieces.Length;i++){slotOccupants[i]=pieceSlots[i]=i;pieces[i].installed=true;if(looseObjects[i])Destroy(looseObjects[i]);looseObjects[i]=null;}engine.ApplyPose();RefreshInfo();UpdateProgress();}
 public void RequestReset(){if(Time.unscaledTime<resetUntil){ResetPuzzle();resetUntil=0;}else{resetUntil=Time.unscaledTime+4;Notify("再点一次「重新开始」清空本次装配。",4);}}
 public void SelectPiece(int index){if(index<0||index>=pieces.Length)return;ClearSelection();pulledFrom=-1;pulledClear=true;grabLocal=Vector3.zero;selected=index;var p=pieces[index];if(!p.installed){engine.running=false;engine.travel=false;engine.phase=25;engine.ApplyPose();token=looseObjects[index];if(!token){token=Instantiate(p.prefab);token.name="空间零件_"+p.title;var loose=token.AddComponent<SteamLoosePiece>();loose.index=index;loose.templateSlot=index;looseObjects[index]=token;ParkToken();safePositions[index]=token.transform.position;safeRotations[index]=token.transform.rotation;safeScales[index]=token.transform.localScale.x;}AimSocket(ClosestSocket(token.transform.position));}RefreshInfo();RefreshShelf();if(presentation)presentation.PlayPick();Notify(p.installed?"按住零件向外拖，即可拆下。":"松手可留在空间 · 右键旋转 · 拖动时滚轮调远近",4);}
 void ClearSelection(){if(token&&selected>=0&&!SafeToLeave(selected,token.transform.position,token.transform.rotation,token.transform.localScale.x)){token.transform.SetPositionAndRotation(safePositions[selected],safeRotations[selected]);token.transform.localScale=Vector3.one*safeScales[selected];}if(ghost)Destroy(ghost);token=ghost=null;selected=-1;activeSocket=-1;dragging=false;}

 public void BeginPointerDrag(){if(selected<0||pieces[selected].installed||!token)return;dragging=true;grabLocal=Vector3.zero;pointerMoved=false;pointerDown=Input.mousePosition;dragDepth=Mathf.Max(2,Vector3.Dot(token.transform.position-engine.studioCamera.transform.position,engine.studioCamera.transform.forward));}
 public bool BeginGrab(int index){if(pieces[index].installed)TakeInstalled(index);else SelectPiece(index);if(!token)return false;dragging=true;dragDepth=Mathf.Max(2,Vector3.Dot(token.transform.position-engine.studioCamera.transform.position,engine.studioCamera.transform.forward));return true;}
 public Vector3[] SocketAnchors(int slot){
  string key=pieces[slot].key;
  Vector3[] points=key=="supply"?new[]{new Vector3(.35f,2.90f,0),new Vector3(-2.02f,2.65f,-.30f),new Vector3(2.88f,1.19f,1.085f),new Vector3(2.88f,1.19f,-1.085f),new Vector3(1.0f,2.64f,-.35f)}:
   key=="exhaust"?new[]{new Vector3(2.70f,2.60f,0),new Vector3(3.15f,.94f,1.085f),new Vector3(3.15f,.94f,-1.085f)}:
   key=="feed"?new[]{new Vector3(-.96f,1.70f,-1.005f),new Vector3(-.55f,1.85f,-.61f),new Vector3(-.96f,1.70f,1.005f),new Vector3(-.55f,1.85f,.61f)}:new Vector3[0];
  return points.Select(p=>engine.locomotive.TransformPoint(p)).Concat(new[]{SlotPosition(slot)+engine.locomotive.rotation*grabLocal}).ToArray();
 }
 public void MovePointer(Vector2 screenPoint){
  if(selected<0||!token)return;var cam=engine.studioCamera;
  var plane=new Plane(-cam.transform.forward,cam.transform.position+cam.transform.forward*dragDepth);
  float d;var ray=cam.ScreenPointToRay(screenPoint);if(!plane.Raycast(ray,out d))return;
  Vector3 position=ray.GetPoint(d)-token.transform.rotation*grabLocal;int best=-1;float score=float.MaxValue;
  float pixelRadius=Mathf.Clamp(Screen.height*.065f,42,72);
  if(pulledFrom>=0&&!pulledClear&&Vector2.Distance(screenPoint,pointerDown)>60)pulledClear=true;
  for(int i=0;i<pieces.Length;i++)if(CanFit(selected,i))foreach(var anchor in SocketAnchors(i)){
   var sp=cam.WorldToScreenPoint(anchor);float pixels=Vector2.Distance(screenPoint,sp);
   if(sp.z>0&&pixels<pixelRadius&&cam.pixelRect.Contains(sp)){float candidate=pixels+sp.z*.04f;if(candidate<score){score=candidate;best=i;}}
  }
  if(best>=0&&pulledClear&&cam.pixelRect.Contains(screenPoint)){position=SlotPosition(best);AimSocket(best);}
  MoveGrab(position);
 }

 public void MoveGrab(Vector3 worldPosition){if(!dragging||!token)return;token.transform.position=worldPosition;if(pulledFrom>=0&&!pulledClear&&Vector3.Distance(worldPosition,SlotPosition(pulledFrom))>.65f)pulledClear=true;int nearest=ClosestSocket(worldPosition);AimSocket(nearest);bool near=nearest>=0&&pulledClear&&Vector3.Distance(worldPosition,Socket)<=snapRadius;if(near){token.transform.localScale=Vector3.one;Quaternion q=engine.locomotive.rotation;var loose=token.GetComponent<SteamLoosePiece>();int template=loose?loose.templateSlot:selected;
 if(pieces[selected].fitKey=="wheel"){var from=pieces[template].members[0].transform;var to=pieces[nearest].members[0].transform;var fromFrame=from.localRotation*(from.localPosition.z<0?Quaternion.Euler(180,0,0):Quaternion.identity);var toFrame=to.localRotation*(to.localPosition.z<0?Quaternion.Euler(180,0,0):Quaternion.identity);q*=toFrame*Quaternion.Inverse(fromFrame);}
 else if(pieces[template].members.Length==1&&pieces[nearest].members.Length==1)q*=pieces[nearest].members[0].homeRotation*Quaternion.Inverse(pieces[template].members[0].homeRotation);
 token.transform.rotation=q;}bool safe=SafeToLeave(selected,worldPosition,token.transform.rotation,token.transform.localScale.x);if(safe){safePositions[selected]=worldPosition;safeRotations[selected]=token.transform.rotation;safeScales[selected]=token.transform.localScale.x;}UpdatePlacementHints();Notify(near?"同规格接口已对齐 · 松手安装":safe?"松手，零件会留在这里":"继续向外拉，或对准空闲的同类接口",.3f);}
 Vector3 HalfSize(int i,Quaternion rotation,float scale){var size=pieces[i].size*.5f*scale;var a=rotation*Vector3.right*size.x;var b=rotation*Vector3.up*size.y;var c=rotation*Vector3.forward*size.z;return new Vector3(Mathf.Abs(a.x)+Mathf.Abs(b.x)+Mathf.Abs(c.x),Mathf.Abs(a.y)+Mathf.Abs(b.y)+Mathf.Abs(c.y),Mathf.Abs(a.z)+Mathf.Abs(b.z)+Mathf.Abs(c.z));}
 public bool SafeToLeave(int i,Vector3 world,Quaternion rotation,float scale){Vector3 p=engine.locomotive.InverseTransformPoint(world),e=HalfSize(i,rotation,scale)+Vector3.one*.25f;return p.x+e.x< -4.15f||p.x-e.x>4.15f||p.y+e.y<-.15f||p.y-e.y>3.85f||p.z+e.z< -1.65f||p.z-e.z>1.65f;}
 public bool ReleaseGrab(){if(!dragging||!token)return false;dragging=false;if(activeSocket>=0&&CanFit(selected,activeSocket)&&pulledClear&&Vector3.Distance(token.transform.position,Socket)<=snapRadius){int i=selected;slotOccupants[activeSocket]=i;pieceSlots[i]=activeSocket;pieces[i].installed=true;pulledFrom=-1;acceptedDrops++;history.Push(i);Destroy(token);looseObjects[i]=null;token=null;ClearSelection();selected=i;engine.ApplyPose();RefreshShelf();RefreshInfo();UpdateProgress();if(showReference)RebuildReference();if(presentation)presentation.PlaySnap();lastDrop="installed";Notify(Complete?"全部装好！可以启动机车了。":"已安装 · "+pieces[i].title,3);return true;}if(tabletop&&!SettleOnWorkbench()){ReturnToShelf();lastDrop="returned";return false;}if(SafeToLeave(selected,token.transform.position,token.transform.rotation,token.transform.localScale.x)){safePositions[selected]=token.transform.position;safeRotations[selected]=token.transform.rotation;safeScales[selected]=token.transform.localScale.x;looseDrops++;lastDrop="placed";Notify("已留在空间 · 随时可以再次拿起",3);}else{rejectedDrops++;token.transform.SetPositionAndRotation(safePositions[selected],safeRotations[selected]);token.transform.localScale=Vector3.one*safeScales[selected];lastDrop="blocked";Notify("这里离机车太近，已保留在上一个安全位置。",4);}RefreshShelf();return false;}
 void ParkToken(){if(!token||selected<0)return;var p=pieces[selected];float scale=1;token.transform.localScale=Vector3.one*scale;token.transform.rotation=engine.locomotive.rotation;Vector3 point=engine.studioCamera.ViewportToWorldPoint(new Vector3(.82f,.34f,engine.distance*.8f));if(!SafeToLeave(selected,point,token.transform.rotation,scale)){var local=engine.locomotive.InverseTransformPoint(point);local.z=Mathf.Sign(engine.studioCamera.transform.position.z)*(1.95f+HalfSize(selected,token.transform.rotation,scale).z);point=engine.locomotive.TransformPoint(local);}token.transform.position=point;}
 public void LeaveInSpace(){if(tabletop&&dragging)ReleaseGrab();ClearSelection();RefreshInfo();RefreshShelfHighlights();Notify("零件保留在空间中。",2);}
 public void TidyLoose(){ClearSelection();for(int i=0;i<looseObjects.Length;i++){if(looseObjects[i])Destroy(looseObjects[i]);looseObjects[i]=null;}RefreshShelf();RefreshInfo();Notify("散放的零件已收回陈列架，安装进度保留。",3);}

 public void ReturnToShelf(){if(selected>=0&&looseObjects[selected]){Destroy(looseObjects[selected]);looseObjects[selected]=null;token=null;}ClearSelection();RefreshInfo();RefreshShelf();Notify("零件已放回，可继续自由选择。",3);}
 public bool TakeInstalled(int index){if(index<0||!pieces[index].installed)return false;int slot=pieceSlots[index];var doorsV11=GetComponent<LocomotiveDoorsV11>();if(doorsV11)doorsV11.CloseGroup(pieces[slot].key);engine.running=false;engine.travel=false;engine.principle=false;effects.fireEnabled=false;effects.fireDoorOpen=false;slotOccupants[slot]=-1;pieceSlots[index]=-1;pieces[index].installed=false;ClearSelection();engine.phase=25;engine.ApplyPose();SelectPiece(index);if(token){Destroy(token);token=Instantiate(pieces[slot].prefab,SlotPosition(slot),engine.locomotive.rotation);token.name="拆下_"+pieces[index].title;var loose=token.AddComponent<SteamLoosePiece>();loose.index=index;loose.templateSlot=slot;looseObjects[index]=token;}pulledFrom=slot;pulledClear=false;dragging=true;UpdateProgress();if(showReference)RebuildReference();Notify("已抓住 · 向外拉出后可放在空间中",3);return true;}
 public void RemoveSelected(){if(selected>=0)TakeInstalled(selected);}
 public void Undo(){while(history.Count>0){int i=history.Pop();if(pieces[i].installed){selected=i;RemoveSelected();return;}}}
 public void ToggleHint(){showHint=!showHint;hintLabel.text=showHint?"提示：开":"位置提示";UpdatePlacementHints();}
 public void ToggleReference(){showReference=!showReference;RebuildReference();referenceLabel.text=showReference?"隐藏参考":"整机参考";}
 void Ghostify(GameObject go){foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=2;foreach(var col in go.GetComponentsInChildren<Collider>(true))Destroy(col);foreach(var r in go.GetComponentsInChildren<Renderer>(true)){r.sharedMaterials=Enumerable.Repeat(ghostMaterial,r.sharedMaterials.Length).ToArray();r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;}}
 void RebuildReference(){if(reference)Destroy(reference);reference=null;if(!showReference)return;reference=new GameObject("整机参考轮廓");foreach(var p in pieces.Where((p,i)=>!SlotInstalled(i))){var g=Instantiate(p.prefab,engine.locomotive.TransformPoint(p.centre),engine.locomotive.rotation,reference.transform);g.name="参考_"+p.key;Ghostify(g);}}
 public void ToggleRun(){if(!Complete){Notify("装好剩余零件后即可启动。",3);return;}LeaveInSpace();engine.running=!engine.running;effects.fireEnabled=true;}
 public void ToggleCutaway(){if(!Complete)return;engine.principle=!engine.principle;engine.ApplyPose();Notify(engine.principle?"橙红：燃烧烟气   金色：进汽   青色：排汽":"返回完整外观",5);}
 public void FireboxView(){if(!Complete)return;engine.principle=true;engine.doorsOpen=true;var d=GetComponent<LocomotiveDoorsV11>();if(d)d.OpenAll();engine.focus=new Vector3(-1.25f,2.05f,0);engine.yaw=-70;engine.pitch=14;engine.distance=6.7f;effects.fireEnabled=true;effects.fireDoorOpen=true;engine.ApplyPose();engine.SetCamera();}
 public void Reverse(){if(Complete){var drive=GetComponent<LocomotiveDriveV9>();if(engine.travel&&drive)drive.Move(-drive.requestedDirection);else engine.Reverse();}}
 public void Notify(string text,float seconds){if(toast)toast.text=text;toastUntil=Time.unscaledTime+seconds;}
 void UpdateProgress(){if(tabletop){effects.fireEnabled=Complete;return;}progress.text=InstalledCount.ToString("00")+" / "+pieces.Length+"  已安装";runButton.interactable=Complete;cutawayButton.interactable=Complete;effects.fireEnabled=Complete;}
 void RefreshInfo(){if(tabletop)return;infoCard.SetActive(selected>=0);if(selected<0){infoTitle.text="选一个零件，开始拼装";infoDescription.text="从右侧拿取，按自己的顺序装到机车上。";infoMeta.text="自由拼装  /  不限顺序";removeButton.gameObject.SetActive(false);returnButton.gameObject.SetActive(false);return;}var p=pieces[selected];infoTitle.text=p.title;infoDescription.text=p.description;infoMeta.text=(p.installed?"已安装":"正在选择")+"  /  "+new[]{"轮组","传动","车体"}[p.category];removeButton.gameObject.SetActive(false);returnButton.gameObject.SetActive(!p.installed);}
 public void SetCategory(int value){category=value;page=0;RefreshShelf();}
 public void PageBy(int delta){var ids=Filtered();int pages=Mathf.Max(1,Mathf.CeilToInt(ids.Length/8f));page=(page+delta+pages)%pages;RefreshShelf();}
 int[] Filtered(){return Enumerable.Range(0,pieces.Length).Where(i=>category==0||pieces[i].category==category-1).ToArray();}
 void RefreshShelf(){if(!shelfRoot)return;foreach(var g in shelfModels)if(g)Destroy(g);shelfModels.Clear();var ids=Filtered();int pages=Mathf.Max(1,Mathf.CeilToInt(ids.Length/8f));page=Mathf.Clamp(page,0,pages-1);pageLabel.text=(page+1)+" / "+pages;for(int cat=0;cat<categoryLabels.Length;cat++)categoryLabels[cat].color=category==cat?Gold:Paper;
  for(int slot=0;slot<8;slot++){int at=page*8+slot;visibleSlots[slot]=at<ids.Length?ids[at]:-1;slotLabels[slot].text=at<ids.Length?pieces[ids[at]].title:"";if(at>=ids.Length)continue;var p=pieces[ids[at]];if(p.installed){slotLabels[slot].text+="  已装";continue;}if(looseObjects!=null&&looseObjects[ids[at]])slotLabels[slot].text+=" · 空间";var model=Instantiate(p.prefab,shelfRoot);model.name="陈列_"+p.title;foreach(var t in model.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;foreach(var col in model.GetComponentsInChildren<Collider>(true))Destroy(col);float scale=1.65f/Mathf.Max(p.size.x,p.size.y,p.size.z);model.transform.localPosition=new Vector3((slot%2==0?1:-1)*1.15f,2.70f-(slot/2)*1.8f,0);model.transform.localRotation=Quaternion.Euler(15,145,0);model.transform.localScale=Vector3.one*scale;shelfModels.Add(model);}
  RefreshShelfHighlights();
  LocomotiveXRQuality.MarkShelfDirty();
 }
 void RefreshShelfHighlights(){if(tabletop)return;for(int slot=0;slot<8;slot++){int i=visibleSlots[slot];slotBackgrounds[slot].color=i==selected?new Color(.85f,.65f,.3f,.035f):slot==hoverSlot?new Color(.5f,.7f,.6f,.035f):new Color(.5f,.7f,.6f,.025f);slotLabels[slot].color=i==selected?Gold:i>=0&&pieces[i].installed?Muted:Paper;slotBorders[slot].SetActive(i>=0&&i==selected);}}
 public void HoverSlot(int slot){hoverSlot=slot;RefreshShelfHighlights();}
 public void PickSlot(int slot){int i=visibleSlots[slot];if(i<0)return;SelectPiece(i);if(!pieces[i].installed)BeginPointerDrag();}
 void BuildShelf(){shelfRoot=new GameObject("三维零件陈列架").transform;shelfRoot.position=new Vector3(0,1000,0);shelfCamera=new GameObject("陈列架相机").AddComponent<Camera>();shelfCamera.transform.position=new Vector3(0,1000,10);shelfCamera.transform.rotation=Quaternion.Euler(0,180,0);shelfCamera.orthographic=true;shelfCamera.orthographicSize=3.85f;shelfCamera.nearClipPlane=.1f;shelfCamera.farClipPlane=30;shelfCamera.clearFlags=CameraClearFlags.SolidColor;shelfCamera.backgroundColor=new Color(.048f,.092f,.080f);shelfCamera.cullingMask=1<<30;shelfCamera.allowHDR=true;shelfTexture=new RenderTexture(640,1000,24,RenderTextureFormat.ARGB32);shelfTexture.Create();shelfCamera.targetTexture=shelfTexture;GameObject.Find("ShelfView").GetComponent<UnityEngine.UI.RawImage>().texture=shelfTexture;engine.studioCamera.cullingMask &= ~((1<<30)|(1<<29));engine.studioCamera.rect=new Rect(0,0,.725f,1);
  var l=new GameObject("陈列架柔光").AddComponent<Light>();l.type=LightType.Directional;l.transform.rotation=Quaternion.Euler(35,150,0);l.color=new Color(1,.92f,.76f);l.intensity=1.1f;l.cullingMask=1<<30;l.shadows=LightShadows.None;
 }
 RectTransform Rect(Transform parent,string name,Vector2 min,Vector2 max,Vector2 low,Vector2 high){var g=new GameObject(name,typeof(RectTransform));var r=(RectTransform)g.transform;r.SetParent(parent,false);r.anchorMin=min;r.anchorMax=max;r.offsetMin=low;r.offsetMax=high;return r;}
 RectTransform PanelAt(Transform parent,string name,Vector2 min,Vector2 max,Vector2 low,Vector2 high,Color color){var r=Rect(parent,name,min,max,low,high);var img=r.gameObject.AddComponent<UnityEngine.UI.Image>();img.color=color;img.raycastTarget=false;return r;}
 TMP_Text Text(Transform parent,string name,string text,float size,Color color,Vector2 min,Vector2 max,Vector2 low,Vector2 high,TextAlignmentOptions align=TextAlignmentOptions.MidlineLeft){var r=Rect(parent,name,min,max,low,high);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=text;t.fontSize=size;t.color=color;t.alignment=align;t.raycastTarget=false;t.enableWordWrapping=true;return t;}
 UnityEngine.UI.Button Button(Transform parent,string name,string caption,Vector2 min,Vector2 max,Vector2 low,Vector2 high,UnityEngine.Events.UnityAction action,bool accent=false){var r=PanelAt(parent,name,min,max,low,high,accent?new Color(.73f,.54f,.29f):new Color(.105f,.185f,.155f));r.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;var b=r.gameObject.AddComponent<UnityEngine.UI.Button>();b.onClick.AddListener(action);var col=b.colors;col.highlightedColor=new Color(1.20f,1.18f,1.1f);col.disabledColor=new Color(.5f,.5f,.5f,.5f);b.colors=col;Text(r,"Label",caption,17,accent?new Color(.035f,.065f,.05f):Paper,Vector2.zero,Vector2.one,new Vector2(5,2),new Vector2(-5,-2),TextAlignmentOptions.Center);return b;}
 void BuildUI(){var go=new GameObject("自由拼装界面",typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;var scale=go.GetComponent<UnityEngine.UI.CanvasScaler>();scale.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1600,1000);scale.matchWidthOrHeight=.5f;var ui=go.transform;
  var header=PanelAt(ui,"Header",new Vector2(0,1),Vector2.one,new Vector2(0,-84),Vector2.zero,Panel);Text(header,"Brand","蒸汽工坊",30,Paper,Vector2.zero,new Vector2(.3f,1),new Vector2(28,20),new Vector2(0,-13));Text(header,"Edition","自由拼装",17,Gold,new Vector2(.19f,0),new Vector2(.4f,1),Vector2.zero,Vector2.zero);progress=Text(header,"Progress","",21,Paper,new Vector2(.5f,0),new Vector2(.72f,1),Vector2.zero,new Vector2(-20,0),TextAlignmentOptions.MidlineRight);Text(header,"ShelfTitle","零件陈列架",24,Paper,new Vector2(.745f,0),Vector2.one,Vector2.zero,new Vector2(-24,0));
  var shelf=PanelAt(ui,"Shelf",new Vector2(.735f,.105f),new Vector2(.99f,.90f),Vector2.zero,Vector2.zero,Panel);
  categoryLabels=new TMP_Text[4];for(int i=0;i<4;i++){int cat=i;categoryLabels[i]=Button(shelf,"Category"+i,new[]{"全部","轮组","传动","车体"}[i],new Vector2(i*.25f,1),new Vector2((i+1)*.25f,1),new Vector2(5,-42),new Vector2(-5,-4),()=>SetCategory(cat)).GetComponentInChildren<TMP_Text>();}
  var view=Rect(shelf,"ShelfView",new Vector2(0,.09f),new Vector2(1,.92f),new Vector2(6,0),new Vector2(-6,0));view.gameObject.AddComponent<UnityEngine.UI.RawImage>().raycastTarget=false;
  slotBorders=new GameObject[8];slotBackgrounds=new UnityEngine.UI.Image[8];slotLabels=new TMP_Text[8];for(int i=0;i<8;i++){int col=i%2,row=i/2;var r=PanelAt(view,"PieceSlot"+i,new Vector2(col*.5f,1-(row+1)*.25f),new Vector2((col+1)*.5f,1-row*.25f),new Vector2(3,4),new Vector2(-3,-4),Color.clear);slotBackgrounds[i]=r.GetComponent<UnityEngine.UI.Image>();slotBackgrounds[i].raycastTarget=true;var slot=r.gameObject.AddComponent<SteamPuzzleShelfSlot>();slot.owner=this;slot.index=i;var border=Rect(r,"SelectionBorder",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);slotBorders[i]=border.gameObject;PanelAt(border,"Top",new Vector2(0,1),Vector2.one,new Vector2(0,-2),Vector2.zero,Gold);PanelAt(border,"Bottom",Vector2.zero,new Vector2(1,0),Vector2.zero,new Vector2(0,2),Gold);PanelAt(border,"Left",Vector2.zero,new Vector2(0,1),Vector2.zero,new Vector2(2,0),Gold);PanelAt(border,"Right",new Vector2(1,0),Vector2.one,new Vector2(-2,0),Vector2.zero,Gold);slotLabels[i]=Text(r,"PartName","",15,Paper,Vector2.zero,new Vector2(1,.2f),new Vector2(2,0),new Vector2(-2,0),TextAlignmentOptions.Center);}
  Button(shelf,"PreviousPage","上一页",new Vector2(0,0),new Vector2(.3f,.075f),new Vector2(8,7),new Vector2(-3,-1),()=>PageBy(-1));pageLabel=Text(shelf,"Page","",18,Muted,new Vector2(.3f,0),new Vector2(.7f,.075f),Vector2.zero,Vector2.zero,TextAlignmentOptions.Center);Button(shelf,"NextPage","下一页",new Vector2(.7f,0),new Vector2(1,.075f),new Vector2(3,7),new Vector2(-8,-1),()=>PageBy(1));
  var info=PanelAt(ui,"SelectionCard",new Vector2(0,.13f),new Vector2(.405f,.29f),new Vector2(24,0),new Vector2(0,0),Panel);infoCard=info.gameObject;PanelAt(info,"Accent",Vector2.zero,new Vector2(0,1),Vector2.zero,new Vector2(3,0),Gold);infoMeta=Text(info,"Meta","",14,Gold,new Vector2(0,.77f),Vector2.one,new Vector2(20,0),new Vector2(-16,-7));infoTitle=Text(info,"Title","",25,Paper,new Vector2(0,.47f),new Vector2(1,.79f),new Vector2(20,0),new Vector2(-16,0));infoDescription=Text(info,"Description","",18,Paper,new Vector2(0,.15f),new Vector2(1,.49f),new Vector2(20,0),new Vector2(-16,0));
  returnButton=Button(ui,"ReturnPiece","放回零件架",new Vector2(.025f,.077f),new Vector2(.16f,.12f),Vector2.zero,Vector2.zero,ReturnToShelf);removeButton=Button(ui,"RemovePiece","拆下零件",new Vector2(.025f,.077f),new Vector2(.16f,.12f),Vector2.zero,Vector2.zero,RemoveSelected);
  toast=Text(ui,"Status","",19,Paper,new Vector2(.02f,.87f),new Vector2(.70f,.914f),Vector2.zero,Vector2.zero);
  if(!tabletop)V9Dashboard.Build(this,ui);else{
      var bindings=new GameObject("Internal bindings").transform;bindings.SetParent(ui,false);
      runButton=Button(bindings,"Run","",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,ToggleRun);
      cutawayButton=Button(bindings,"Cutaway","",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,ToggleCutaway);
      runLabel=runButton.GetComponentInChildren<TMP_Text>();hintLabel=Text(bindings,"Hint","",12,Paper,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
      referenceLabel=Text(bindings,"Reference","",12,Paper,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);bindings.gameObject.SetActive(false);
  }
  Text(ui,"Controls","点门开关 / 拽出拆卸 · 右键旋转 · 中键平移\n滚轮缩放 · Esc 留在空间 · W/S 行驶 · 空格制动",16,Muted,new Vector2(.02f,.005f),new Vector2(.425f,.07f),Vector2.zero,Vector2.zero);

 }

 void BuildSpeedControl(Transform tablet){
  Text(tablet,"SpeedTitle","转速",13,Paper,new Vector2(.10f,.10f),new Vector2(.24f,.20f),Vector2.zero,Vector2.zero);
  speedReadout=Text(tablet,"SpeedValue","24 转/分",12,Gold,new Vector2(.72f,.10f),new Vector2(.91f,.20f),Vector2.zero,Vector2.zero,TextAlignmentOptions.Center);
  var root=Rect(tablet,"SpeedSlider",new Vector2(.25f,.11f),new Vector2(.70f,.19f),Vector2.zero,Vector2.zero);
  var hit=root.gameObject.AddComponent<UnityEngine.UI.Image>();hit.color=new Color(0,0,0,0);hit.raycastTarget=true;
  var track=PanelAt(root,"Track",new Vector2(0,.32f),new Vector2(1,.68f),Vector2.zero,Vector2.zero,new Color(.035f,.07f,.06f));
  var fillArea=Rect(root,"FillArea",Vector2.zero,Vector2.one,new Vector2(5,0),new Vector2(-5,0));
  var fill=PanelAt(fillArea,"Fill",new Vector2(0,.32f),new Vector2(1,.68f),Vector2.zero,Vector2.zero,Gold);
  var handleArea=Rect(root,"HandleArea",Vector2.zero,Vector2.one,new Vector2(5,0),new Vector2(-5,0));
  var handle=PanelAt(handleArea,"Handle",new Vector2(0,0),new Vector2(0,1),new Vector2(-5,-2),new Vector2(5,2),Paper);handle.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
  speedControl=root.gameObject.AddComponent<UnityEngine.UI.Slider>();speedControl.direction=UnityEngine.UI.Slider.Direction.LeftToRight;speedControl.minValue=6;speedControl.maxValue=60;speedControl.wholeNumbers=true;speedControl.fillRect=fill;speedControl.handleRect=handle;speedControl.targetGraphic=handle.GetComponent<UnityEngine.UI.Image>();speedControl.SetValueWithoutNotify(requestedRpm);speedControl.onValueChanged.AddListener(SetSpeed);SetSpeed(requestedRpm);
 }

 void OnDestroy(){if(shelfTexture){shelfTexture.Release();Destroy(shelfTexture);}}
}
public class SteamPuzzleShelfSlot : MonoBehaviour,IPointerDownHandler,IPointerEnterHandler,IPointerExitHandler {
 public LocomotiveFreeAssembly owner; public int index;
 public void OnPointerDown(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)owner.PickSlot(index);}
 public void OnPointerEnter(PointerEventData e){owner.HoverSlot(index);}
 public void OnPointerExit(PointerEventData e){owner.HoverSlot(-1);}
}




public class SteamLoosePiece : MonoBehaviour { public int index,templateSlot; }
