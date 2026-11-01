using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// Read-only teaching overlay. Assembly ownership, colliders and mechanical poses stay with SteamJourney.
[DefaultExecutionOrder(2800)]
public sealed class StudioPartCards : MonoBehaviour {
 public SteamJourney journey;
 public Transform spatialAnchor;
 public Material spatialLeaderMaterial;
 public bool IsWorldAnchored=>canvas&&canvas.renderMode==RenderMode.WorldSpace;
 [NonSerialized] public bool visible=true, verificationHidden;
 [NonSerialized] public int verificationSelection=-1;
 public int SelectedIndex {get;private set;}
 public bool CardsVisible=>canvas&&canvas.gameObject.activeSelf;
 public bool TextFits=>labels.All(t=>!t.isTextOverflowing);
 public Rect ScreenRect {get;private set;}
 JourneyModule[] parts; Renderer[][] renderers,previews;
 readonly Dictionary<Transform,int> owners=new Dictionary<Transform,int>();
 readonly RaycastHit[] hits=new RaycastHit[64];
 // Swan's derived scene stores the same authored card subtree. This gives the
 // native exporter its TMP materials, meshes and parent transforms together;
 // Start must reuse those objects instead of creating an empty native subtree.
 [SerializeField] List<TMP_Text> labels=new List<TMP_Text>();
 [SerializeField] Canvas canvas;
 [SerializeField] RectTransform cardRoot;
 [SerializeField] GameObject controls;
 [SerializeField] TMP_Text title,body,observation,indexLabel,toggleLabel,status;
 [SerializeField] LineRenderer leader;
 [SerializeField] Material lineMaterial;
 public bool HasPreparedSpatialCanvas=>canvas&&cardRoot&&title&&body&&observation&&status&&leader
  &&canvas.renderMode==RenderMode.WorldSpace&&canvas.transform.parent==spatialAnchor;
 public Canvas PreparedSpatialCanvas=>HasPreparedSpatialCanvas?canvas:null;
 Vector3 lastPointer;bool cardSideSet,cardOnRight;public Vector3 Anchor=>anchor;
 float sampleAt;int last=-1,lastCurrent=-1;bool manual;Vector3 anchor;bool anchorValid;Bounds selectedBounds;
 static readonly Color Cyan=new Color(.44f,.86f,.94f,1);
 static readonly string[] Observe={
  "观察炉排下方进风与煤层燃烧，热烟气绕过砖拱进入烟管。",
  "烟气走管内，水在管外受热；注意水面上方的蒸汽空间。",
  "乏汽从喷管向烟囱排出，带动烟箱气流；这也是抽风的来源。",
  "留意分离器的回水通道，以及干蒸汽进入调节阀后的独立管路。",
  "切换剖面，观察活塞阀交替接通两端汽口，驱动活塞往复。",
  "观察主连杆、联动杆和配汽杆的不同运动；轴销始终连接对应杆眼。",
  "先观察储气，再操作制动：列车管减压，分配阀令制动缸充气。",
  "看踏板的防滑纹、真实开孔及管路穿板位置，区分结构与表面细节。",
  "沿两侧铜管查看接头与走向，辨认管路和锅炉供水系统的关系。",
  "从车窗观察操纵区域，驾驶室为司炉与司机提供操作空间。",
  "转动视角，观察弧形顶盖、压筋和铆接边缘在光线下的变化。",
  "双侧水箱随车储水；观察箱盖、支架及与走行平台的连接。",
  "观察踏步与支架的连接，以及踏面防滑纹在侧光下的起伏。",
  "煤仓靠近驾驶室，便于司炉向火箱加煤；观察煤块与金属仓壁的差异。",
  "观察缓冲器、车钩和加强支座，外力经缓冲梁传入车架。",
  "比较后缓冲梁的安装位置，观察缓冲器与车架之间的支撑。",
  "转动视角，观察黄铜灯壳的细密拉丝、灯圈反光与玻璃层次。",
  "比较另一侧照灯，观察相同材质在不同入射光方向下的反射变化。"
 };
 public static string ObservationText(int index,int total){
  if(total==17&&index==16)return "观察两盏灯的黄铜外壳与玻璃灯面。左右灯成组安装，进入山谷后可开启车灯，查看前方照明。";
  return Observe[Mathf.Clamp(index,0,Observe.Length-1)];
 }
 void Start(){Initialize();}
 // Called only while generating the Swan derived scene. Desktop scenes keep
 // their original runtime Build path and are never saved by this method.
 public void PrepareSpatialForExport(){
  if(!journey||!journey.SwanInputActive||!spatialAnchor)
   throw new InvalidOperationException("Preparing Swan part cards requires a Swan journey and its board anchor.");
  Initialize();
  // Generation has no LateUpdate. Initialize the authored leader and card pose
  // now; this does not imply that the native exporter supports LineRenderer.
  UpdateAnchor();Layout();
 }
 void Initialize(){
  if(!journey)journey=FindObjectOfType<SteamJourney>();if(!journey){enabled=false;return;}
  parts=journey.modules.Concat(journey.finishing).ToArray();renderers=new Renderer[parts.Length][];previews=new Renderer[parts.Length][];
  for(int i=0;i<parts.Length;i++){
   renderers[i]=parts[i].objects.Where(o=>o).SelectMany(o=>o.GetComponentsInChildren<Renderer>(true)).Where(r=>r is MeshRenderer).ToArray();
   previews[i]=parts[i].preview?parts[i].preview.GetComponentsInChildren<Renderer>(true).Where(r=>r is MeshRenderer).ToArray():new Renderer[0];
   foreach(var o in parts[i].objects)if(o)owners[o.transform]=i;if(parts[i].preview)owners[parts[i].preview.transform]=i;
  }
  Build();ConfigureSpatial();SelectedIndex=journey.Complete?5:Mathf.Max(0,Array.IndexOf(parts,journey.Current));ShowContent();
 }
 void Build(){
  if(journey.SwanInputActive&&canvas){
   if(!HasPreparedSpatialCanvas)throw new InvalidOperationException("Prepared Swan part-card references are incomplete.");
   return;
  }
  // Project the part anchor into a crisp overlay: scene depth of field must never blur Chinese text.
  var root=new GameObject("Floating part introduction",typeof(RectTransform),typeof(Canvas));root.transform.SetParent(transform,false);canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=15;
  var card=new GameObject("Projected part cards",typeof(RectTransform));card.transform.SetParent(root.transform,false);cardRoot=(RectTransform)card.transform;cardRoot.sizeDelta=new Vector2(440,388);
  Panel("Purpose card",new Vector2(0,79),new Vector2(440,230));
  Panel("Observation card",new Vector2(0,-124),new Vector2(440,144));
  status=Text("PART / 部件",new Vector2(-193,173),new Vector2(386,20),14,Cyan);
  title=Text("",new Vector2(-193,141),new Vector2(386,50),27,Color.white);
  Text("工作原理",new Vector2(-193,78),new Vector2(386,24),16,Cyan);
  body=Text("",new Vector2(-193,43),new Vector2(386,106),20,new Color(.86f,.92f,.94f));
  Text("观察要点",new Vector2(-193,-72),new Vector2(386,24),17,Cyan);
  observation=Text("",new Vector2(-193,-109),new Vector2(386,73),19,new Color(.86f,.92f,.94f));
  var line=new GameObject("Part anchor leader");line.transform.SetParent(transform,false);leader=line.AddComponent<LineRenderer>();leader.useWorldSpace=true;leader.positionCount=3;leader.numCapVertices=3;leader.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;leader.receiveShadows=false;
  if(journey.SwanInputActive){
   if(!spatialLeaderMaterial||!spatialLeaderMaterial.shader)throw new InvalidOperationException("Swan part cards require a serialized native leader material.");
   lineMaterial=new Material(spatialLeaderMaterial){name=spatialLeaderMaterial.name+" · part card leader"};
  }else{
   lineMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));lineMaterial.SetFloat("_Surface",1);lineMaterial.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);lineMaterial.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);lineMaterial.SetFloat("_ZWrite",0);lineMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");lineMaterial.renderQueue=3000;
  }
  SetLineColor(new Color(.38f,.80f,.89f,.72f));leader.sharedMaterial=lineMaterial;
  var overlay=GameObject.Find("Journey · State and contextual controls");if(!overlay)return;
  controls=new GameObject("Part introduction controls",typeof(RectTransform));controls.transform.SetParent(overlay.transform,false);var cr=(RectTransform)controls.transform;cr.anchorMin=new Vector2(.025f,.165f);cr.anchorMax=new Vector2(.24f,.207f);cr.offsetMin=cr.offsetMax=Vector2.zero;
  MakeButton("Toggle",0,.40f,"介绍 开",()=>{visible=!visible;UpdateToggle();},out toggleLabel);
  MakeButton("Previous",.43f,.57f,"<",()=>Step(-1),out var prev);
  MakeButton("Next",.86f,1,">",()=>Step(1),out var next);
  var textGo=new GameObject("Part count",typeof(RectTransform),typeof(TextMeshProUGUI));textGo.transform.SetParent(controls.transform,false);indexLabel=textGo.GetComponent<TextMeshProUGUI>();indexLabel.font=journey.font;indexLabel.fontSize=16;indexLabel.alignment=TextAlignmentOptions.Center;indexLabel.color=Cyan;indexLabel.raycastTarget=false;var ir=indexLabel.rectTransform;ir.anchorMin=new Vector2(.58f,0);ir.anchorMax=new Vector2(.85f,1);ir.offsetMin=ir.offsetMax=Vector2.zero;
 }
 void Panel(string name,Vector2 position,Vector2 size){
  var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(cardRoot,false);var rt=(RectTransform)go.transform;rt.sizeDelta=size;rt.anchoredPosition=position;var bg=go.GetComponent<Image>();bg.color=new Color(.018f,.038f,.050f,.90f);bg.raycastTarget=false;
  RectLine(rt,new Vector2(0,size.y*.5f),new Vector2(size.x,1.1f),.75f);RectLine(rt,new Vector2(0,-size.y*.5f),new Vector2(size.x,1.1f),.75f);
  RectLine(rt,new Vector2(-size.x*.5f,0),new Vector2(1.1f,size.y),.75f);RectLine(rt,new Vector2(size.x*.5f,0),new Vector2(1.1f,size.y),.75f);
  RectLine(rt,new Vector2(-size.x*.5f+24,size.y*.5f),new Vector2(48,3),1);RectLine(rt,new Vector2(size.x*.5f-24,-size.y*.5f),new Vector2(48,3),1);
 }
 void RectLine(RectTransform parent,Vector2 position,Vector2 size,float alpha){var g=new GameObject("Cyan fine edge",typeof(RectTransform),typeof(Image));g.transform.SetParent(parent,false);var r=(RectTransform)g.transform;r.sizeDelta=size;r.anchoredPosition=position;var im=g.GetComponent<Image>();im.color=new Color(Cyan.r,Cyan.g,Cyan.b,alpha);im.raycastTarget=false;}
 TMP_Text Text(string value,Vector2 topLeft,Vector2 size,float fontSize,Color color){
  // Swan uses the same 3D TMP path as the visible control captions. Orthographic
  // metrics preserve the UGUI pixel layout; perspective TMP would be 10x smaller.
  bool swan=journey.SwanInputActive;
  var go=new GameObject(value.Length>0?value:"Part text",typeof(RectTransform),swan?typeof(TextMeshPro):typeof(TextMeshProUGUI));go.transform.SetParent(cardRoot,false);var t=go.GetComponent<TMP_Text>();
  if(swan)t.isOrthographic=true;
  t.font=journey.font;t.fontSize=fontSize;t.color=color;t.text=value;t.raycastTarget=false;t.enableWordWrapping=true;t.overflowMode=TextOverflowModes.Overflow;t.alignment=TextAlignmentOptions.TopLeft;var r=t.rectTransform;r.pivot=new Vector2(0,1);r.sizeDelta=size;r.anchoredPosition=topLeft;
  if(swan)r.localPosition=new Vector3(r.localPosition.x,r.localPosition.y,-2f);
  labels.Add(t);return t;
 }
 void MakeButton(string name,float left,float right,string value,UnityEngine.Events.UnityAction action,out TMP_Text label){
  var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(controls.transform,false);var r=(RectTransform)go.transform;r.anchorMin=new Vector2(left,0);r.anchorMax=new Vector2(right,1);r.offsetMin=r.offsetMax=Vector2.zero;go.GetComponent<Image>().color=new Color(.07f,.14f,.17f,.96f);var button=go.GetComponent<Button>();button.onClick.AddListener(action);button.navigation=new Navigation{mode=Navigation.Mode.None};
  var child=new GameObject("Caption",typeof(RectTransform),typeof(TextMeshProUGUI));child.transform.SetParent(go.transform,false);label=child.GetComponent<TextMeshProUGUI>();label.font=journey.font;label.fontSize=19;label.color=Color.white;label.alignment=TextAlignmentOptions.Center;label.text=value;label.raycastTarget=false;var tr=label.rectTransform;tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.offsetMin=tr.offsetMax=Vector2.zero;
 }
 void UpdateToggle(){if(toggleLabel)toggleLabel.text=visible?"介绍 开":"介绍 关";}
 public void Select(int i){SelectedIndex=Mathf.Clamp(i,0,parts.Length-1);manual=true;sampleAt=0;ShowContent();}
 public void Toggle(){visible=!visible;UpdateToggle();}
 public void Step(int d){visible=true;UpdateToggle();Select((SelectedIndex+d+parts.Length)%parts.Length);}
 void ShowContent(){
  var m=parts[SelectedIndex];title.text=m.title;body.text=m.explanation;observation.text=ObservationText(SelectedIndex,parts.Length);
  body.enableAutoSizing=true;body.fontSizeMin=17;body.fontSizeMax=20;title.enableAutoSizing=true;title.fontSizeMin=22;title.fontSizeMax=27;
  if(indexLabel)indexLabel.text=(SelectedIndex+1).ToString("D2")+" / "+parts.Length;last=SelectedIndex;
  foreach(var t in labels)t.ForceMeshUpdate();
 }
 void LateUpdate() { using (SwanJourney.SwanJourneyCpuTimings.Measure(SwanJourney.SwanJourneyCpuTimings.Stage.PartCards)) CpuMeasuredLateUpdate(); }
    void CpuMeasuredLateUpdate(){
  if(parts==null)return;
  if(!journey.SwanInputActive&&!journey.verificationInputLock&&Input.GetKeyDown(KeyCode.I)){visible=!visible;UpdateToggle();}
  bool active=visible&&!verificationHidden&&!journey.inSandbox;canvas.gameObject.SetActive(active);leader.enabled=active;if(controls)controls.SetActive(!spatialAnchor&&!journey.inSandbox);
  int current=Array.IndexOf(parts,journey.Current);if(lastCurrent!=current){if(!journey.AutoAssembling)manual=false;lastCurrent=current;}
  if(verificationSelection>=0)SelectedIndex=Mathf.Clamp(verificationSelection,0,parts.Length-1);
  else if(!journey.AutoAssembling&&!journey.Complete&&!manual&&current>=0)SelectedIndex=current;
  if(Time.unscaledTime>=sampleAt){
   sampleAt=Time.unscaledTime+.12f;
   if(!journey.SwanInputActive&&(Input.mousePosition-lastPointer).sqrMagnitude>9&&!journey.AutoAssembling&&verificationSelection<0&&!journey.verificationInputLock&&!Input.GetMouseButton(0)&&!Input.GetMouseButton(1)&&!(EventSystem.current&&EventSystem.current.IsPointerOverGameObject())){
    int count=Physics.RaycastNonAlloc(journey.view.ScreenPointToRay(Input.mousePosition),hits,100);float nearest=float.PositiveInfinity;int found=-1;
    for(int k=0;k<count;k++){if(hits[k].distance>=nearest)continue;for(var t=hits[k].transform;t;t=t.parent)if(owners.TryGetValue(t,out var i)){nearest=hits[k].distance;found=i;break;}}
    if(found>=0){SelectedIndex=found;manual=true;}
   }
   if(!journey.SwanInputActive)lastPointer=Input.mousePosition;UpdateAnchor();
  }
  if(SelectedIndex!=last){ShowContent();UpdateAnchor();}
  status.text="PART  "+(SelectedIndex+1).ToString("D2")+" / "+parts.Length+"     ·     "+(parts[SelectedIndex].installed?"已装配":"待装配");
  if(active)Layout();
 }
 void UpdateAnchor(){
  var m=parts[SelectedIndex];anchor=journey.trainAnchor.TransformPoint(m.previewCentre);selectedBounds=new Bounds(anchor,Vector3.one*.15f);anchorValid=true;

 }
 void ConfigureSpatial(){
  if(!spatialAnchor)return;
  canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=journey.view;canvas.sortingOrder=0;
  canvas.transform.SetParent(spatialAnchor,false);canvas.transform.localPosition=new Vector3(0,0,-.048f);canvas.transform.localRotation=Quaternion.identity;canvas.transform.localScale=Vector3.one*.0062f;
  var cr=(RectTransform)canvas.transform;cr.sizeDelta=new Vector2(440,388);
  cardRoot.localPosition=Vector3.zero;cardRoot.localRotation=Quaternion.identity;cardRoot.localScale=Vector3.one;
  foreach(var im in cardRoot.GetComponentsInChildren<UnityEngine.UI.Image>())im.color=im.name.Contains("card")?new Color(.018f,.028f,.032f,1):new Color(.62f,.48f,.27f,.9f);
  foreach(var text in labels)if(text.color==Cyan)text.color=new Color(.87f,.75f,.52f);
  SetLineColor(new Color(.70f,.66f,.49f,.65f));
  if(controls)controls.SetActive(false);
 }
 void Layout(){
  if(!anchorValid)return;
  if(spatialAnchor){
   var edge=spatialAnchor.TransformPoint(new Vector3(-1.37f,.35f,-.05f));
   leader.SetPosition(0,anchor);leader.SetPosition(1,edge+spatialAnchor.TransformVector(Vector3.right*-.22f));leader.SetPosition(2,edge);leader.startWidth=leader.endWidth=.006f*journey.SpatialScale;
   cardRoot.localPosition=Vector3.zero;cardRoot.localRotation=Quaternion.identity;cardRoot.localScale=Vector3.one;
   return;
  }
  var cam=journey.view;var p=cam.WorldToScreenPoint(anchor);if(p.z<=.1f){canvas.gameObject.SetActive(false);leader.enabled=false;return;}
  float width=Mathf.Clamp(Screen.width*.225f,320,420),height=width*388/440,margin=Screen.width*.025f;
  // Keep cards in free side space, off the top bar and bottom assembly controls.
  float y=Mathf.Clamp(Screen.height*.57f,Screen.height*.235f+height*.5f,Screen.height*.89f-height*.5f);
  if(!cardSideSet){cardOnRight=p.x<Screen.width*.5f;cardSideSet=true;}bool onRight=cardOnRight;
  float x=onRight?Screen.width-margin-width*.5f:margin+width*.5f;
  float depth=Mathf.Max(cam.nearClipPlane+.5f,p.z*.82f);float unit=2*depth*Mathf.Tan(cam.fieldOfView*Mathf.Deg2Rad*.5f)/Screen.height;
  cardRoot.position=new Vector3(x,y,0);cardRoot.rotation=Quaternion.identity;cardRoot.localScale=Vector3.one*(width/440);
  ScreenRect=new Rect(x-width*.5f,y-height*.5f,width,height);
  var end=cam.ScreenToWorldPoint(new Vector3(x+(onRight?-width*.5f:width*.5f),y+height*.20f,depth));var elbow=cam.ScreenToWorldPoint(new Vector3(x+(onRight?-width*.59f:width*.59f),y+height*.20f,depth));
  leader.SetPosition(0,anchor);leader.SetPosition(1,elbow);leader.SetPosition(2,end);leader.startWidth=leader.endWidth=unit*1.1f;
 }
 void SetLineColor(Color color){
  if(!lineMaterial)return;
  if(lineMaterial.HasProperty("_BaseColor"))lineMaterial.SetColor("_BaseColor",color);
  if(lineMaterial.HasProperty("_Color"))lineMaterial.SetColor("_Color",color);
 }
 static void DestroyOwned(UnityEngine.Object value){if(!value)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
 void OnDestroy(){DestroyOwned(lineMaterial);DestroyOwned(controls);if(canvas)DestroyOwned(canvas.gameObject);}
}
