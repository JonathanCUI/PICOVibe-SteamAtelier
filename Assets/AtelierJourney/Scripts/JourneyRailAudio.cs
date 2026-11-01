using UnityEngine;
public sealed partial class SteamJourney {
 public AudioClip railSound,fireLoop;
 AudioSource exhaustAudio,railAudio,fireAudio;readonly bool[,] exhaustWasOpen=new bool[2,2];bool soundReady;float jointTravel;
 void UpdateRailAudio(float dt){
  if(!exhaustAudio){exhaustAudio=gameObject.AddComponent<AudioSource>();exhaustAudio.playOnAwake=false;exhaustAudio.volume=.34f;railAudio=gameObject.AddComponent<AudioSource>();railAudio.playOnAwake=false;railAudio.volume=.14f;fireAudio=gameObject.AddComponent<AudioSource>();fireAudio.clip=fireLoop;fireAudio.loop=true;fireAudio.playOnAwake=false;fireAudio.volume=.055f;}
  bool active=(inSandbox||running||ResidualEnergy)&&!paused;
  // During a hard stop the contact friction must be audible above the exhaust pulses.
  exhaustAudio.volume=Mathf.MoveTowards(exhaustAudio.volume,.34f*(1-.60f*HardBrakeIntensity),dt*.8f);
  bool moving=active&&(inSandbox?speed>.035f:WheelPower>.1f&&modules[5].installed&&brakeApplied<.99f);
  for(int i=0;i<engine.sides.Length;i++){var p=engine.sides[i].pose;for(int e=0;e<2;e++){bool open=ValvePortOpen(i,e==0?3:4);if(soundReady&&moving&&open&&!exhaustWasOpen[i,e]&&chuffSound){exhaustAudio.pitch=.96f+i*.04f;exhaustAudio.PlayOneShot(chuffSound,inSandbox?.55f+.45f*throttle:.70f);if(smoke&&!bakedChimney&&inSandbox&&ChimneyConnected)smoke.Emit(3);}exhaustWasOpen[i,e]=open;}}
  soundReady=true;
  // Rail-joint double knocks removed; retain steam exhaust, wheel friction and whistle.
  fireAudio.volume=Mathf.Lerp(fireAudio.volume,active?(inSandbox?.025f:FireLevel*.065f):0,dt*4);if(active&&!fireAudio.isPlaying)fireAudio.Play();if(!active&&fireAudio.isPlaying)fireAudio.Stop();
  if(!active){exhaustAudio.Stop();railAudio.Stop();}
 }
 float benchStroke;
 void ApplyCylinderBench(){
  if(!modules[4].installed||modules[5].installed)return;
  // With no wheel-driven valve gear there is only one power stroke, not an unexplained self-oscillation.
  foreach(var side in engine.sides){float z=side.sign*1.085f;float slider=Mathf.Lerp(1.87f,2.43f,benchStroke);var pose=side.pose;pose.slider=slider;pose.V.x=2.447+.14;side.pose=pose;
   side.valve.localPosition=new Vector3(3.29f,1.03f,z);side.stem.localPosition=new Vector3(2.897f,1.02f,z);side.crosshead.localPosition=new Vector3(slider,.65f,z);side.wristpin.localPosition=side.crosshead.localPosition;side.pistonRod.localPosition=new Vector3(slider+.50f,.65f,z);side.piston.localPosition=new Vector3(slider+1,.65f,z);
  }
 }
}
