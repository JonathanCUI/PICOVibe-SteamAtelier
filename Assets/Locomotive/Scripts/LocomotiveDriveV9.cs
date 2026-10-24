using UnityEngine;
[DefaultExecutionOrder(-100)]
public class LocomotiveDriveV9:MonoBehaviour {
 public LocomotiveController engine;public float limit=42f,signedRpm;public int requestedDirection=1;public bool powered;public string state="装配中";public float SpeedKmh=>Mathf.Abs(signedRpm)*Mathf.PI*1.3f*.06f;
 public void Move(int direction){var w=engine.freeAssembly;if(!w||!w.Complete)return;w.LeaveInSpace();requestedDirection=direction<0?-1:1;powered=true;engine.travel=true;w.effects.fireEnabled=true;}
 public void Brake(){powered=false;if(!engine.travel){engine.running=false;signedRpm=0;}}
 public void Toggle(){if(engine.travel){if(powered)Brake();else Move(requestedDirection);}else{var w=engine.freeAssembly;if(!w||!w.Complete)return;w.LeaveInSpace();engine.running=!engine.running;w.effects.fireEnabled=true;}}
 public void Observation(){if(Mathf.Abs(signedRpm)>.05f){Brake();engine.freeAssembly.Notify("先制动停车，再切换原地演示。",3);return;}engine.travel=false;engine.running=false;powered=false;signedRpm=0;}
 public void ResetDrive(){powered=false;signedRpm=0;requestedDirection=1;state="装配中";engine.travel=false;engine.travelled=0;}
 public void Tick(float dt){var w=engine.freeAssembly;if(!w||!w.Complete){powered=false;signedRpm=0;engine.running=false;engine.travel=false;state="装配中";return;}if(!engine.travel){signedRpm=0;state=engine.running?"原地演示":"整备完成";return;}
  const float metresPerRpm=Mathf.PI*1.3f/60;float speed=Mathf.Abs(signedRpm),target=powered?w.requestedRpm:0;bool changing=powered&&speed>.01f&&Mathf.Sign(signedRpm)!=requestedDirection;
  if(changing)target=0;float remaining=limit-requestedDirection*engine.travelled;float safe=Mathf.Sqrt(Mathf.Max(0,2*48*metresPerRpm*(remaining-.05f)))/metresPerRpm;target=Mathf.Min(target,safe);
  float next=Mathf.MoveTowards(speed,target,(target<speed?48:24)*dt);int sign=speed>.001f?(signedRpm>0?1:-1):requestedDirection;signedRpm=next*sign;if(next<.005f){signedRpm=0;if(remaining<.08f){powered=false;state="到达终端";}else state=changing?"换向就绪":"制动停车";}else state=changing?"换向制动":!powered?"正在制动":target<w.requestedRpm-.1f?"终端减速":"轨道行驶";
  engine.rpm=Mathf.Abs(signedRpm);if(Mathf.Abs(signedRpm)>.001f)engine.direction=signedRpm>0?1:-1;engine.running=Mathf.Abs(signedRpm)>.001f;
 }
 void Update(){Tick(Time.deltaTime);if(engine.freeAssembly&&engine.freeAssembly.desktopInput&&engine.freeAssembly.Complete){if(Input.GetKeyDown(KeyCode.W))Move(1);if(Input.GetKeyDown(KeyCode.S))Move(-1);if(Input.GetKeyDown(KeyCode.Space))Brake();}}
 void LateUpdate(){if(engine.travel)engine.SetCamera();}
}
