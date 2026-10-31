using UnityEngine;

public sealed partial class SteamJourney {
 public AudioClip brakeFrictionSound,brakeReleaseSound;
 public float HardBrakeIntensity {get;private set;}
 public int BrakeSparkCount {get;private set;}
 public bool BrakeAudioPlaying=>brakeFrictionAudio&&brakeFrictionAudio.isPlaying;
 public float BrakeAudioGain=>brakeFrictionAudio?brakeFrictionAudio.volume:0;
 AudioSource brakeFrictionAudio,brakeReleaseAudio;
 float brakeSparkCredit,lastBrakeCommand;
 int brakeContact;
 // Visual slip cue for a hard stop, not a claim that ordinary braking always produces sparks.
 public static float HardBrakeDemand(float velocity,float deceleration,float command,float cylinderPressure){
  return Mathf.InverseLerp(.18f,.9f,velocity)*Mathf.InverseLerp(.55f,1.4f,deceleration)
   *Mathf.InverseLerp(.65f,.95f,command)*Mathf.InverseLerp(.65f,2.2f,cylinderPressure);
 }
 void UpdateBrakeEffects(float dt,float deceleration){
  if(!brakeFrictionAudio){
   brakeFrictionAudio=gameObject.AddComponent<AudioSource>();brakeFrictionAudio.playOnAwake=false;brakeFrictionAudio.loop=true;brakeFrictionAudio.clip=brakeFrictionSound;brakeFrictionAudio.volume=0;brakeFrictionAudio.priority=72;brakeFrictionAudio.dopplerLevel=0;
   brakeReleaseAudio=gameObject.AddComponent<AudioSource>();brakeReleaseAudio.playOnAwake=false;brakeReleaseAudio.volume=.10f;
  }
  float demand=inSandbox?HardBrakeDemand(speed,deceleration,brake,BrakeCylinderPressure):0;
  HardBrakeIntensity=Mathf.MoveTowards(HardBrakeIntensity,demand,dt*(demand>HardBrakeIntensity?8:4));
  if(!inSandbox||speed<.035f)HardBrakeIntensity=0;
  // Fixed emission is disabled: each spark is placed on one of the six actual rail contact patches.
  SetEmission(sparks,0);
  if(inSandbox&&HardBrakeIntensity>.01f&&sparks){
   brakeSparkCredit+=dt*240*HardBrakeIntensity;
   while(brakeSparkCredit>=1){brakeSparkCredit--;int k=brakeContact++%6;float z=(k%2==0?-1:1)*.775f;
    float u=Mathf.Repeat(brakeContact*.6180339f,1),v=Mathf.Repeat(brakeContact*.4142135f,1);
    var point=new Vector3((k/2-1)*1.45f,.035f,z);
    var velocity=new Vector3(-direction*(.9f+u*1.3f),.9f+v*1.2f,Mathf.Sign(z)*(.35f+u*.65f));
    var e=new ParticleSystem.EmitParams{position=engine.locomotive.TransformPoint(point),velocity=engine.locomotive.TransformVector(velocity),startLifetime=.16f+v*.20f,startSize=.014f+u*.015f,startColor=new Color(1,.65f+u*.25f,.18f,1)};
    sparks.Emit(e,1);BrakeSparkCount++;
   }
  }else brakeSparkCredit=0;
  // Brake pressure supplies friction; moving steel supplies the squeal. Retain the natural
  // inharmonic recording, using only a modest speed-related pitch change instead of a siren glide.
  float movement=Mathf.Sqrt(Mathf.InverseLerp(.035f,1.4f,speed));
  float soundTarget=inSandbox?Mathf.InverseLerp(.15f,2.8f,BrakeCylinderPressure)*movement*(.24f+.40f*HardBrakeIntensity):0;
  brakeFrictionAudio.volume=Mathf.MoveTowards(brakeFrictionAudio.volume,soundTarget,dt*(soundTarget>brakeFrictionAudio.volume?2.5f:2.2f));
  brakeFrictionAudio.pitch=Mathf.MoveTowards(brakeFrictionAudio.pitch,.88f+Mathf.Clamp01(speed/2.6f)*.14f,dt*.18f);
  if(brakeFrictionAudio.volume>.003f&&!brakeFrictionAudio.isPlaying)brakeFrictionAudio.Play();
  if(brakeFrictionAudio.volume<=.003f&&brakeFrictionAudio.isPlaying)brakeFrictionAudio.Stop();
  if(inSandbox&&lastBrakeCommand>.65f&&brake<.10f&&BrakeCylinderPressure>.3f&&brakeReleaseSound)brakeReleaseAudio.PlayOneShot(brakeReleaseSound);
  lastBrakeCommand=brake;
 }
}
