using UnityEngine;
public enum AtelierAction { Throttle,Reverse,Brake,Whistle,Lights,Cutaway,Observation,Assembly,TrayPrevious,TrayNext,TrayPart,Home,Interior,Category,ResetGuard }
public class AtelierControl:MonoBehaviour {
 public AtelierAction action;public int index;public string caption;public Transform pivot;
 Vector3 rest;float pressedUntil;
 void Awake(){if(action==AtelierAction.TrayPrevious||action==AtelierAction.TrayNext){pivot=transform;rest=transform.localPosition;}}
 public void PressFeedback(){pressedUntil=Time.unscaledTime+.16f;}
 void LateUpdate(){if((action==AtelierAction.TrayPrevious||action==AtelierAction.TrayNext)&&pivot)pivot.localPosition=Vector3.Lerp(pivot.localPosition,rest+Vector3.down*(Time.unscaledTime<pressedUntil?.025f:0),1-Mathf.Exp(-Time.unscaledDeltaTime*35));}
}
