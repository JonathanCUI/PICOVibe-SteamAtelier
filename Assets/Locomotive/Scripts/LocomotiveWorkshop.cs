using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

[Serializable] public class SteamModule {
 public string title,action,meaning,feedback;
 public GameObject prefab; public Vector3 centre; public float previewScale=1;
}
// Desktop input is an adapter; device integrations can call BeginGrab / MoveGrab / ReleaseGrab.
public class LocomotiveWorkshop : MonoBehaviour {
 public LocomotiveController engine; public LocomotiveThermalEffects effects;
 public SteamModule[] modules; public Material ghostMaterial;
 public Canvas canvas; public GameObject welcomePanel,lessonPanel,explorePanel,completionPanel;
 public TMP_Text heading,body,hint,progress,feedback,completeText,flowLegend,runCaption;
 public Button installButton,continueButton,homeButton; public Image[] progressDots;
 public enum Mode { Welcome, Assembly, Feedback, Complete, Explore }
 public Mode mode; public int installed; public bool dragging; public bool desktopInput=true; public int rejectedDrops,acceptedDrops;
 public float snapRadius=.85f; public GameObject token,ghost;
 float startedAt,feedbackUntil; Vector3 trayPosition; Quaternion trayRotation; Plane dragPlane;
 readonly Dictionary<LocomotivePart,int> groups=new Dictionary<LocomotivePart,int>();
 public bool Guided => mode==Mode.Assembly||mode==Mode.Feedback;
 public Vector3 Socket => engine.locomotive.TransformPoint(modules[Mathf.Min(installed,5)].centre);
 public static int ModuleFor(LocomotivePart p){string id=p.partId;
  if(id=="P02"||id=="P07"||id.StartsWith("M02")||id=="V2-BOILER")return 0;
  if(id=="P13"||id.StartsWith("P20")||id=="P18"||id=="P19"||id=="M03-C"||id=="V2-CYL")return 1;
  if(id=="P12"||id=="P14"||id=="P16"||id=="P15C"||id=="P15W")return 2;
  if(id.StartsWith("M04")&&p.stage!=0||id=="M03-D"||id=="M03-S"||id=="M03-P"||id=="P21"||id=="P22")return 3;
  if(id=="M05-S"||id=="P29")return 4;
  if(id=="P03"||id=="P06"||id=="M05-E"||id=="M01"||id=="V2-SMOKE")return 5;
  if(p.stage>=5||id=="M05-W")return 6;return -1;
 }
 public bool IsPartVisible(LocomotivePart p){if(!Guided)return true;int g;if(!groups.TryGetValue(p,out g)){g=ModuleFor(p);groups[p]=g;}return g<installed;}
 void Start(){engine.workshop=this;EnsureHomeButton();ShowWelcome();}
 void EnsureHomeButton(){if(homeButton)return;var go=new GameObject("ReturnHome",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button));var rt=(RectTransform)go.transform;rt.SetParent(canvas.transform,false);rt.anchorMin=rt.anchorMax=new Vector2(.5f,1);rt.pivot=new Vector2(.5f,1);rt.anchoredPosition=new Vector2(0,-35);rt.sizeDelta=new Vector2(170,46);go.GetComponent<UnityEngine.UI.Image>().color=new Color(.12f,.25f,.22f);homeButton=go.GetComponent<UnityEngine.UI.Button>();homeButton.onClick.AddListener(ShowWelcome);var caption=new GameObject("Caption",typeof(RectTransform),typeof(TextMeshProUGUI));var cr=(RectTransform)caption.transform;cr.SetParent(rt,false);cr.anchorMin=Vector2.zero;cr.anchorMax=Vector2.one;cr.offsetMin=cr.offsetMax=Vector2.zero;var t=caption.GetComponent<TextMeshProUGUI>();t.font=heading.font;t.text="返回首页";t.fontSize=20;t.color=new Color(.94f,.94f,.87f);t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;}

 void Update(){if(desktopInput&&Input.GetKeyDown(KeyCode.Escape)){ShowWelcome();return;}
  if(mode==Mode.Assembly&&desktopInput){
   if(Input.GetMouseButtonDown(0)&&!(EventSystem.current&&EventSystem.current.IsPointerOverGameObject())){RaycastHit hit;if(Physics.Raycast(engine.studioCamera.ScreenPointToRay(Input.mousePosition),out hit,100)&&token&&hit.collider.transform.IsChildOf(token.transform)){BeginGrab();dragPlane=new Plane(-engine.studioCamera.transform.forward,Socket);}}
   if(dragging){float d;var ray=engine.studioCamera.ScreenPointToRay(Input.mousePosition);if(dragPlane.Raycast(ray,out d))MoveGrab(ray.GetPoint(d));if(Input.GetMouseButtonUp(0))ReleaseGrab();}
   if(ghost){float a=.15f+Mathf.Sin(Time.time*3)*.04f;ghostMaterial.SetColor("_BaseColor",new Color(.27f,.91f,.67f,a));}
  }
  if(mode==Mode.Feedback&&continueButton)continueButton.interactable=Time.unscaledTime>=feedbackUntil;
  if(runCaption)runCaption.text=engine.running?"暂停观察":"启动机车";
  if(flowLegend)flowLegend.gameObject.SetActive(engine.principle&&!Guided);
 }
 void ClearToken(){if(token)Destroy(token);if(ghost)Destroy(ghost);token=ghost=null;dragging=false;}
 void Panels(){if(homeButton)homeButton.gameObject.SetActive(mode!=Mode.Welcome);welcomePanel.SetActive(mode==Mode.Welcome);lessonPanel.SetActive(Guided);completionPanel.SetActive(mode==Mode.Complete);explorePanel.SetActive(mode==Mode.Explore);progress.gameObject.SetActive(Guided);if(flowLegend)flowLegend.gameObject.SetActive(engine.principle&&!Guided);}
 public void ShowWelcome(){feedback.text="从六个关键组件理解蒸汽机车的工作原理";ClearToken();mode=Mode.Welcome;installed=6;engine.ResetView();engine.inputEnabled=false;engine.yaw=25;engine.pitch=15;engine.distance=14.8f;engine.focus=new Vector3(.9f,1.65f,0);engine.SetCamera();effects.fireEnabled=true;Panels();}
 public void BeginLesson(){effects.fireDoorOpen=false;ClearToken();installed=0;acceptedDrops=0;rejectedDrops=0;startedAt=Time.unscaledTime;engine.ResetView();engine.inputEnabled=false;engine.rpm=7;effects.fireEnabled=false;mode=Mode.Assembly;SetupStep();}
 void SetupStep(){mode=Mode.Assembly;engine.running=false;engine.principle=installed>0;engine.phase=25;engine.focus=new Vector3(.15f,1.48f,0);engine.yaw=15;engine.pitch=18;engine.distance=15.8f;engine.ApplyPose();engine.SetCamera();Panels();
  var m=modules[installed];heading.text=(installed+1).ToString("00")+"  "+m.title;body.text=m.meaning;hint.text=m.action+"\n拖动右侧组件到绿色轮廓，松手装入。";feedback.text="拿起 → 对齐 → 松手";progress.text="装配进度  "+installed+" / 6";installButton.gameObject.SetActive(true);continueButton.gameObject.SetActive(false);
  for(int i=0;i<progressDots.Length;i++)progressDots[i].color=i<installed?new Color(.23f,.64f,.43f):i==installed?new Color(.85f,.64f,.30f):new Color(.23f,.31f,.29f);
  token=Instantiate(m.prefab);token.name="当前可抓取教学组件";token.transform.localScale=Vector3.one*m.previewScale;
  trayPosition=engine.studioCamera.ViewportToWorldPoint(new Vector3(.82f,.32f,10));trayRotation=Quaternion.identity;token.transform.SetPositionAndRotation(trayPosition,trayRotation);
  ghost=Instantiate(m.prefab,Socket,engine.locomotive.rotation);ghost.name="装配位置提示";
  foreach(var collider in ghost.GetComponentsInChildren<Collider>())Destroy(collider);foreach(var r in ghost.GetComponentsInChildren<Renderer>()){var a=new Material[r.sharedMaterials.Length];for(int i=0;i<a.Length;i++)a[i]=ghostMaterial;r.sharedMaterials=a;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;}
 }
 public bool BeginGrab(){if(mode!=Mode.Assembly||!token)return false;dragging=true;feedback.text="对准绿色轮廓；接近后会自动吸附。";return true;}
 public void MoveGrab(Vector3 worldPosition){if(!dragging||!token)return;token.transform.position=worldPosition;bool near=Vector3.Distance(worldPosition,Socket)<snapRadius;token.transform.localScale=Vector3.one*(near?1:modules[installed].previewScale);feedback.text=near?"已对齐 · 松手装入":"对准绿色轮廓；接近后会自动吸附。";}
 public bool ReleaseGrab(){if(!dragging)return false;dragging=false;if(Vector3.Distance(token.transform.position,Socket)<=snapRadius){Accept();return true;}rejectedDrops++;token.transform.SetPositionAndRotation(trayPosition,trayRotation);token.transform.localScale=Vector3.one*modules[installed].previewScale;feedback.text="还差一点，靠近绿色轮廓再试一次。也可以点击「帮我装入」。";return false;}
 public void AssistInstall(){if(mode==Mode.Assembly)Accept();}
 void Accept(){ClearToken();int step=installed;installed++;acceptedDrops++;mode=Mode.Feedback;engine.principle=true;engine.running=installed>=3;effects.fireEnabled=true;engine.ApplyPose();heading.text="已装好 · "+modules[step].title;body.text=modules[step].feedback;hint.text=step==2||step==3?"当前为机构慢速示意；接上管路后再观察进排汽。":"观察刚才安装的组件如何参与工作。";feedback.text=step<5?"准备好了就继续下一步。":"六个环节已连通，可以启动整台机车。";progress.text="装配进度  "+installed+" / 6";installButton.gameObject.SetActive(false);continueButton.gameObject.SetActive(true);continueButton.interactable=false;feedbackUntil=Time.unscaledTime+2;for(int i=0;i<progressDots.Length;i++)progressDots[i].color=i<installed?new Color(.23f,.64f,.43f):new Color(.23f,.31f,.29f);
  FocusFeedback(step);
 }
 void FocusFeedback(int i){if(i==0){engine.focus=new Vector3(.0f,2.15f,0);engine.yaw=10;engine.pitch=23;engine.distance=10.5f;}else if(i<4){engine.focus=new Vector3(1.85f,.95f,.9f);engine.yaw=0;engine.pitch=18;engine.distance=8.5f;}else{engine.focus=new Vector3(.6f,1.8f,0);engine.distance=13;engine.yaw=20;engine.pitch=21;}engine.SetCamera();}
 public void Continue(){if(mode!=Mode.Feedback)return;if(installed<6){SetupStep();return;}mode=Mode.Complete;engine.principle=false;engine.running=true;engine.distance=14.5f;engine.yaw=25;engine.pitch=15;engine.focus=new Vector3(0,1.6f,0);engine.ApplyPose();engine.SetCamera();int seconds=Mathf.RoundToInt(Time.unscaledTime-startedAt);completeText.text="你已连接热源、汽缸、传动、配汽、进汽与排汽。\n现在，观察热量如何变成车轮的转动。\n\n本次用时 "+(seconds/60)+" 分 "+(seconds%60)+" 秒";Panels();}
 public void Explore(){feedback.text="拖动旋转视角 · 滚轮缩放 · 选择下方观察主题";ClearToken();mode=Mode.Explore;installed=6;engine.ResetView();engine.inputEnabled=true;effects.fireEnabled=true;Panels();}
 public void RunComplete(){Explore();engine.running=true;}
 public void ToggleCutaway(){engine.TogglePrinciple();effects.fireEnabled=true;engine.ApplyPose();engine.SetCamera();Panels();}
 public void FireboxView(){engine.principle=true;engine.doorsOpen=true;engine.focus=new Vector3(-1.2f,2.05f,0);engine.yaw=-70;engine.pitch=14;engine.distance=6.7f;effects.fireDoorOpen=true;engine.ApplyPose();engine.SetCamera();Panels();}
 public void SteamView(){engine.principle=true;engine.running=true;engine.focus=new Vector3(.7f,1.9f,0);engine.yaw=12;engine.pitch=20;engine.distance=11.5f;engine.ApplyPose();engine.SetCamera();Panels();}
 public void MechanismView(){engine.principle=true;engine.running=true;engine.focus=new Vector3(1.9f,.95f,1.0f);engine.yaw=0;engine.pitch=15;engine.distance=7.4f;engine.ApplyPose();engine.SetCamera();Panels();}
 public void WholeView(){effects.fireDoorOpen=false;engine.ResetView();engine.SetCamera();Panels();}
}
