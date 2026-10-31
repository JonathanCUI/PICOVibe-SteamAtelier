using System.Collections;
using UnityEngine;

// One scene-owned stereo bed shared by both exhibits; it survives mode changes and replay.
[DisallowMultipleComponent]
public sealed class JourneyBackgroundMusic : MonoBehaviour {
 public AudioClip music;
 [Range(0,1)] public float level=.12f;
 public float crossfadeSeconds=2.5f;
 readonly AudioSource[] voices=new AudioSource[2];
 int current;bool blending;float blend,started,duckUntil,duckGain=1;
 public bool Playing=>voices[0]&&(voices[0].isPlaying||voices[1].isPlaying);
 public float CurrentGain=>level*duckGain;
 public int LoopTransitions{get;private set;}
 public void Duck(float seconds){duckUntil=Mathf.Max(duckUntil,Time.unscaledTime+Mathf.Clamp(seconds,.18f,8));}
 IEnumerator Start(){
  if(!music)yield break;
  for(int i=0;i<2;i++){var a=gameObject.AddComponent<AudioSource>();voices[i]=a;a.clip=music;a.playOnAwake=false;a.loop=false;a.spatialBlend=0;a.dopplerLevel=0;a.priority=220;a.volume=0;a.bypassReverbZones=true;}
  music.LoadAudioData();while(music.loadState==AudioDataLoadState.Loading)yield return null;
  started=Time.unscaledTime;voices[0].Play();
 }
 void Update(){
  if(!voices[0])return;
  float dt=Time.unscaledDeltaTime;
  duckGain=Mathf.MoveTowards(duckGain,Time.unscaledTime<duckUntil?.32f:1,dt*(Time.unscaledTime<duckUntil?5f:.65f));
  float gain=level*duckGain*Mathf.SmoothStep(0,1,(Time.unscaledTime-started)/2.2f);
  var active=voices[current];var next=voices[1-current];float fade=Mathf.Min(crossfadeSeconds,music.length*.15f);
  if(!blending&&active.time>=music.length-fade){next.time=0;next.Play();blending=true;blend=0;LoopTransitions++;}
  if(blending){blend+=dt;float t=Mathf.Clamp01(blend/fade);active.volume=gain*(1-t);next.volume=gain*t;if(t>=1){active.Stop();current=1-current;blending=false;}}
  else{active.volume=gain;next.volume=0;}
 }
}
