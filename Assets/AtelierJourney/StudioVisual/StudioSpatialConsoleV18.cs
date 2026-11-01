using System.Linq;
using UnityEngine;
using TMPro;

// Scene-anchored controls: the existing actions retain ownership of assembly and simulation.
[DefaultExecutionOrder(2900)]
public sealed class StudioSpatialConsoleV18 : MonoBehaviour {
 public SteamJourney journey;
 public Transform console,guideAnchor,rotary,introLever,guard;
 public TMP_Text assembled,selected,modeLabel,introLabel,resetLabel,message;
 public GameObject guideFurniture;
 public bool GuardOpen {get;private set;}
 public int InstalledCount=>journey.modules.Count(m=>m.installed)+journey.finishing.Count(m=>m.installed);
 StudioPartCards cards;
 void Start(){cards=GetComponent<StudioPartCards>();}
 public void ToggleIntroduction(){if(!cards)cards=GetComponent<StudioPartCards>();if(cards)cards.Toggle();}
 public void StepPart(int d){if(!cards)cards=GetComponent<StudioPartCards>();if(!cards)return;cards.Step(d);journey.SelectSpatialPart(cards.SelectedIndex);}
 public void ToggleGuard(){GuardOpen=!GuardOpen;}
 public bool TryReset(){if(!GuardOpen)return false;GuardOpen=false;return true;}
 void LateUpdate() { using (SwanJourney.SwanJourneyCpuTimings.Measure(SwanJourney.SwanJourneyCpuTimings.Stage.Console)) CpuMeasuredLateUpdate(); }
    void CpuMeasuredLateUpdate(){
  if(!journey)return;journey.ApplySpatialHud();
  if(!cards)cards=GetComponent<StudioPartCards>();
  assembled.text=InstalledCount.ToString("00")+" / "+(journey.modules.Length+journey.finishing.Length).ToString("00");
  selected.text=((cards?cards.SelectedIndex:0)+1).ToString("00")+" / "+(journey.modules.Length+journey.finishing.Length).ToString("00");
  string mode=!journey.cutaway?"实体":journey.inspectionMode==1?"透视":"剖面";
  modeLabel.text="观察 · "+mode;
  introLabel.text="介绍  "+(cards&&cards.visible?"开":"关");
  if(rotary)rotary.localRotation=Quaternion.Slerp(rotary.localRotation,Quaternion.Euler(0,journey.inspectionMode*48-48,0),1-Mathf.Exp(-Time.unscaledDeltaTime*15));
  if(introLever)introLever.localRotation=Quaternion.Slerp(introLever.localRotation,Quaternion.Euler(cards&&cards.visible?-22:22,0,0),1-Mathf.Exp(-Time.unscaledDeltaTime*15));
  if(guard)guard.localRotation=Quaternion.Slerp(guard.localRotation,Quaternion.Euler(GuardOpen?-112:0,0,0),1-Mathf.Exp(-Time.unscaledDeltaTime*12));
  resetLabel.text=GuardOpen?"按下重置":"掀盖重置";
  if(message)message.text=journey.SpatialMessage;
  if(guideFurniture)guideFurniture.SetActive(cards&&cards.visible&&!cards.verificationHidden&&!journey.inSandbox);
 }
}

public sealed partial class SteamJourney {
 public string SpatialMessage=>Time.unscaledTime<noticeUntil&&noticeText&&!string.IsNullOrEmpty(noticeText.text)?noticeText.text:running?ProcessCaption():AutoAssembling?"机械合体 · 零件依次装入":Complete?"装配完成 · 按「出发」进入山谷铁路":"拿起展示台组件，靠近高亮接口松手装入";
 public void ApplySpatialHud(){
  var root=progress?progress.transform.parent:null;if(root)root.gameObject.SetActive(false);
  if(cutButton)cutButton.gameObject.SetActive(false);
  if(footer)footer.gameObject.SetActive(false);
  if(noticeText)noticeText.gameObject.SetActive(false);
  if(drivePanel)drivePanel.SetActive(false);
  if(processStatus)processStatus.gameObject.SetActive(false);
 }
 public void SelectSpatialPart(int index){if(Teaching||index<modules.Length||index>=modules.Length+finishing.Length)return;finishIndex=index-modules.Length;RefreshPreview();SaveProgress();}
}
