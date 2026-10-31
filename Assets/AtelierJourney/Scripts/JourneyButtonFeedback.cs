using UnityEngine;

// Short physical travel, softly lit state and a small tactile click communicate every press.
public sealed class JourneyButtonFeedback:MonoBehaviour {
 public float travel=.025f;public JourneyAction action;public Transform cap;public Renderer lens;public SteamJourney owner;
 Vector3 rest;float pressedAt=-10;MaterialPropertyBlock block;
 void Awake(){if(cap)rest=cap.localPosition;block=new MaterialPropertyBlock();}
 public void Press(){pressedAt=Time.unscaledTime;}
 void LateUpdate(){if(!owner)return;float age=Time.unscaledTime-pressedAt;float pulse=age<.34f?Mathf.Sin(Mathf.Clamp01(age/.34f)*Mathf.PI):0;
  bool on=action==JourneyAction.Verify&&owner.running;
  if(cap)cap.localPosition=rest-Vector3.up*(travel*pulse+(on?.012f:0));
  if(!lens||action==JourneyAction.Verify)return;
  bool ready=action==JourneyAction.FinishAll?!owner.Complete&&!owner.inSandbox:action==JourneyAction.Drive?owner.Complete:!owner.Teaching;
  var colour=action==JourneyAction.Drive?new Color(.72f,.13f,.07f):new Color(.10f,.25f,.32f);
  float glow=ready?.12f:.01f;if(action==JourneyAction.FinishAll&&owner.AutoAssembling)glow=.35f+.15f*Mathf.Sin(Time.unscaledTime*6);
  block.Clear();block.SetColor("_BaseColor",colour*(ready?1:.45f));block.SetColor("_EmissionColor",new Color(.08f,.50f,.65f)*(glow+pulse*.65f));lens.SetPropertyBlock(block);
 }
}
