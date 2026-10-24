using UnityEngine;
[DefaultExecutionOrder(450)] public class LocomotiveDoorsV11 : MonoBehaviour {
 public LocomotiveController engine; public LocomotiveThermalEffects thermal;
 public bool[] open=new bool[2]; public float[] angles=new float[2]; public float fireAngle;
 public void CloseAll(){open=new bool[2];angles=new float[2];fireAngle=0;engine.doorsOpen=false;thermal.fireDoorOpen=false;Apply();}
 public void CloseGroup(string key){if(key=="cab"){open=new bool[2];angles=new float[2];engine.doorsOpen=false;}if(key=="firebox"){thermal.fireDoorOpen=false;fireAngle=0;}Apply();}
 public void ToggleAll(){bool next=!(open[0]||open[1]);open[0]=open[1]=next;engine.doorsOpen=next;}
 public void OpenAll(){open[0]=open[1]=true;engine.doorsOpen=true;}
 public bool Toggle(LocomotivePart part){if(!part)return false;if(part.partId=="M02-D"){thermal.fireDoorOpen=!thermal.fireDoorOpen;return true;}for(int i=0;i<engine.doors.Length;i++)if(part.transform==engine.doors[i]){open[i]=!open[i];return true;}return false;}
 public void Tick(float dt){for(int i=0;i<2;i++)angles[i]=Mathf.MoveTowards(angles[i],open[i]?60:0,dt*100);fireAngle=Mathf.MoveTowards(fireAngle,thermal.fireDoorOpen?78:0,dt*100);Apply();}
 void LateUpdate(){Tick(Time.deltaTime);}
 public void Apply(){if(!engine||engine.exploded)return;for(int i=0;i<engine.doors.Length;i++)engine.doors[i].localRotation=Quaternion.Euler(0,(i==0?-1:1)*angles[i],0);if(thermal&&thermal.fireDoor)thermal.fireDoor.localRotation=Quaternion.Euler(0,fireAngle,0)*thermal.fireDoorRotation;}
}
