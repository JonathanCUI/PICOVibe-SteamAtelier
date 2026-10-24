using UnityEngine;
using TMPro;
using UnityEngine.Rendering.Universal;
[DefaultExecutionOrder(100)]
public class LocomotivePresentationV4 : MonoBehaviour {
 public LocomotiveController engine; public LocomotiveThermalEffects effects; public Light headlight;
 public Renderer lampLens; public AudioClip fireClip,steamClip,chuffClip,snapClip,pickClip,whistleClip;
 public TMP_Text headlightLabel,audioLabel; public bool lightOn=true,audioOn=true; public int playedChuffs;
 public RenderTexture tabletTexture; public GameObject tabletRoot; public Camera tabletCamera;
 public float lightBlend; AudioSource fire,steam,chuff,ui,whistle; Material lens; int lastPulse;
 void Start(){fire=Source("燃煤声",fireClip,true,new Vector3(-1.3f,1.8f,0),.24f);steam=Source("蒸汽声",steamClip,true,new Vector3(.7f,2.7f,0),.12f);chuff=Source("排汽声",null,false,new Vector3(2.7f,3.6f,0),.55f);ui=Source("装配反馈",null,false,Vector3.zero,.45f);ui.spatialBlend=0;whistle=Source("汽笛",null,false,new Vector3(-.77f,3.1f,.43f),.38f);if(lampLens)lens=lampLens.material;UpdateLabels();}
 AudioSource Source(string name,AudioClip clip,bool loop,Vector3 pos,float volume){var go=new GameObject(name);go.transform.SetParent(engine.locomotive,false);go.transform.localPosition=pos;var a=go.AddComponent<AudioSource>();a.clip=clip;a.loop=loop;a.playOnAwake=false;a.spatialBlend=.65f;a.minDistance=5;a.maxDistance=35;a.rolloffMode=AudioRolloffMode.Linear;a.volume=volume;return a;}
 void Update(){bool burning=effects.fireEnabled&&effects.HasModule(0);Loop(fire,audioOn&&burning);Loop(steam,audioOn&&burning&&engine.running);if(effects.exhaustPulses>lastPulse&&audioOn&&engine.running){chuff.pitch=Random.Range(.94f,1.05f);chuff.PlayOneShot(chuffClip);playedChuffs++;}lastPulse=effects.exhaustPulses;bool installed=!engine.freeAssembly||(engine.freeAssembly.tabletop?engine.freeAssembly.HasPiece("rheadlamp"):engine.freeAssembly.HasPiece("headlamp"));lightBlend=Mathf.MoveTowards(lightBlend,lightOn&&installed?1:0,Time.deltaTime*(lightOn?2.2f:1.6f));if(headlight){headlight.enabled=lightBlend>.001f;headlight.intensity=8*lightBlend*lightBlend;}if(lens){lens.EnableKeyword("_EMISSION");lens.SetColor("_EmissionColor",new Color(1,.82f,.58f)*(3.5f*lightBlend));}UpdateLabels();}
 void Loop(AudioSource a,bool active){if(!a)return;if(active&&!a.isPlaying)a.Play();else if(!active&&a.isPlaying)a.Stop();}
 void UpdateLabels(){if(headlightLabel)headlightLabel.text=lightOn?"车灯 开":"车灯 关";if(audioLabel)audioLabel.text=audioOn?"声音 开":"声音 关";}
 public void ToggleHeadlight(){lightOn=!lightOn;UpdateLabels();PlayPick();}
 public void ToggleAudio(){audioOn=!audioOn;if(!audioOn){if(chuff)chuff.Stop();if(whistle)whistle.Stop();if(ui)ui.Stop();}UpdateLabels();}
 public void PlayPick(){if(audioOn&&ui)ui.PlayOneShot(pickClip,.65f);}
 public void PlaySnap(){if(audioOn&&ui)ui.PlayOneShot(snapClip);}
 public void Whistle(){if(audioOn&&whistle&&engine.freeAssembly.Complete)whistle.PlayOneShot(whistleClip);}
 public Texture CreateTablet(GameObject prefab){tabletRoot=Instantiate(prefab);tabletRoot.name="三维操作平板";tabletRoot.transform.position=new Vector3(0,2000,0);foreach(var t in tabletRoot.GetComponentsInChildren<Transform>(true))t.gameObject.layer=29;tabletCamera=new GameObject("平板预览相机").AddComponent<Camera>();tabletCamera.transform.position=new Vector3(0,2000,-2);tabletCamera.transform.rotation=Quaternion.identity;tabletCamera.orthographic=true;tabletCamera.orthographicSize=.31f;tabletCamera.cullingMask=1<<29;tabletCamera.clearFlags=CameraClearFlags.SolidColor;tabletCamera.backgroundColor=Color.clear;tabletCamera.nearClipPlane=.1f;tabletCamera.farClipPlane=5;tabletCamera.allowHDR=false;tabletCamera.GetUniversalAdditionalCameraData().renderShadows=false;tabletTexture=new RenderTexture(960,600,24,RenderTextureFormat.ARGB32);tabletTexture.Create();tabletCamera.targetTexture=tabletTexture;var l=new GameObject("平板柔光").AddComponent<Light>();l.type=LightType.Directional;l.transform.rotation=Quaternion.Euler(35,-25,0);l.cullingMask=1<<29;l.intensity=1.1f;l.shadows=LightShadows.None;return tabletTexture;}
 void OnDestroy(){if(tabletTexture){tabletTexture.Release();Destroy(tabletTexture);}if(lens)Destroy(lens);}
}
