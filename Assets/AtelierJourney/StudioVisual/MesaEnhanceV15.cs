using UnityEngine;using UnityEngine.EventSystems;
[DefaultExecutionOrder(3300)]public sealed class MesaEnhanceV15:MonoBehaviour{
 public SteamJourney journey;public MesaDayNightV13 sky;public MesaAtmosphereV14 atmosphere;public ParticleSystem plume;public bool useVolumeSteam;public Renderer[] flares;public static bool CapturesPointer{get;private set;}public bool DraggingMoon{get;private set;}public bool WasAutomatic{get;private set;}
 MaterialPropertyBlock props;float clock;Renderer legacySmoke;bool smokeWasEnabled;Vector3[] starts,scales;
 MesaAtmosV16 cloudProvider;bool swanHiddenCleaned,wasSandbox,refreshCloudProvider=true;
 public int SwanHiddenSkipCount{get;private set;}public int SwanHiddenCleanupCount{get;private set;}public int CloudWriteCount{get;private set;}
 void Awake(){var plumeMain=plume.main;plumeMain.maxParticles=420;plumeMain.startLifetime=new ParticleSystem.MinMaxCurve(6,9);plumeMain.startSize=new ParticleSystem.MinMaxCurve(.95f,1.5f);plumeMain.startColor=new Color(1,1,1,.9f);CapturesPointer=false;journey.celestialInputCapture=()=>{bool old=CapturesPointer;PollPointer();return old||CapturesPointer;};props=new MaterialPropertyBlock();legacySmoke=journey.smoke.GetComponent<Renderer>();smokeWasEnabled=legacySmoke.enabled;starts=new Vector3[sky.clouds.Length];scales=new Vector3[starts.Length];for(int i=0;i<starts.Length;i++){starts[i]=sky.clouds[i].localPosition;scales[i]=sky.clouds[i].localScale;}}
 public bool BeginCelestialDrag(Vector2 mouse){if(!journey.inSandbox)return false;float best=60;int selected=-1;var ts=new[]{sky.sun,sky.moon};for(int k=0;k<2;k++){var p=journey.view.WorldToScreenPoint(ts[k].position);float d=Vector2.Distance(mouse,new Vector2(p.x,p.y));if(p.z>0&&ts[k].localScale.x>.1f&&d<best){best=d;selected=k;}}if(selected<0)return false;WasAutomatic=sky.automatic;sky.automatic=false;DraggingMoon=selected==1;CapturesPointer=true;return true;}
 public void DragCelestial(Vector2 mouse){if(!CapturesPointer)return;float best=float.MaxValue,time=sky.timeOfDay;for(int i=0;i<720;i++){float t=i/720f;Vector3 p=journey.view.WorldToScreenPoint(sky.Orbit(t,DraggingMoon));if(p.z<=0)continue;float d=(new Vector2(p.x,p.y)-mouse).sqrMagnitude;if(d<best){best=d;time=t;}}sky.SetTime(time);}
 public void EndCelestialDrag(){CapturesPointer=false;sky.automatic=WasAutomatic;}
 public void PollPointer(){if(journey.SwanInputActive)return;if(!journey.inSandbox){if(CapturesPointer)EndCelestialDrag();return;}if(journey.verificationInputLock)return;bool over=EventSystem.current&&EventSystem.current.IsPointerOverGameObject();if(Input.GetMouseButtonDown(0)&&!over)BeginCelestialDrag(Input.mousePosition);if(CapturesPointer&&Input.GetMouseButton(0))DragCelestial(Input.mousePosition);if(CapturesPointer&&Input.GetMouseButtonUp(0))EndCelestialDrag();}
 void LateUpdate() { using (SwanJourney.SwanJourneyCpuTimings.Measure(SwanJourney.SwanJourneyCpuTimings.Stage.Clouds)) CpuMeasuredLateUpdate(); }
    void CpuMeasuredLateUpdate(){clock+=Time.unscaledDeltaTime;bool active=journey.inSandbox;legacySmoke.enabled=!active&&smokeWasEnabled&&(!journey.bakedChimney||!journey.ChimneyConnected);
  // The bench steam controller still owns its later legacy-smoke override.
  // Only hidden sandbox work is skipped; its clock continues across mode changes.
  if(journey.SwanInputActive){
   if(refreshCloudProvider||active&&!wasSandbox){cloudProvider=GetComponent<MesaAtmosV16>();refreshCloudProvider=false;}
   wasSandbox=active;
   if(!active){if(!swanHiddenCleaned){ClearHiddenSandbox();swanHiddenCleaned=true;SwanHiddenCleanupCount++;}else DarkenBulbs(true);SwanHiddenSkipCount++;return;}
  }
  swanHiddenCleaned=false;
  Shader.SetGlobalColor("_MesaAmbientSky",RenderSettings.ambientSkyColor);Shader.SetGlobalColor("_MesaAmbientHorizon",RenderSettings.ambientEquatorColor);Shader.SetGlobalColor("_MesaAmbientGround",RenderSettings.ambientGroundColor);
  float pulse=Mathf.Max(0,Mathf.Sin((clock+7)*Mathf.PI*2/39));foreach(var ps in atmosphere.dust){var emission=ps.emission;emission.rateOverTime=active?3.4f*pulse*pulse*pulse:0;if(active&&!ps.isPlaying)ps.Play();if(!active&&ps.isPlaying)ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
  bool newerClouds=journey.SwanInputActive&&cloudProvider&&cloudProvider.CloudUpdatesReady&&cloudProvider.journey==journey&&cloudProvider.sky==sky;
  for(int k=newerClouds?3:0;k<sky.clouds.Length;k++){float s=k*2.1f;var t=sky.clouds[k];t.localPosition=starts[k]+new Vector3(Mathf.Sin(clock*.028f+s)*.8f,Mathf.Sin(clock*.17f+s)*.13f,Mathf.Cos(clock*.024f+s)*.4f);t.localScale=Vector3.Scale(scales[k],new Vector3(1+Mathf.Sin(clock*.16f+s)*.035f,1+Mathf.Sin(clock*.19f+s)*.045f,1+Mathf.Cos(clock*.14f+s)*.03f));CloudWriteCount++;}
  var em=plume.emission;em.rateOverTime=active&&!useVolumeSteam?Mathf.Lerp(35,60,Mathf.Clamp01(journey.speed/3))*(.8f+.2f*Mathf.Sin(clock*8)):0;if(active&&!useVolumeSteam){plume.transform.position=journey.smoke.transform.position;var vel=plume.velocityOverLifetime;Vector3 drift=journey.SceneVectorToWorld(-journey.WorldDirectionToScene(journey.trainAnchor.right)*(.25f+journey.speed*.17f)+new Vector3(.18f,1.15f,.07f));vel.x=drift.x;vel.y=drift.y;vel.z=drift.z;if(!plume.isPlaying)plume.Play();}else if(plume.isPlaying)plume.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
  for(int k=0;k<atmosphere.lamps.Length;k++){var l=atmosphere.lamps[k];float day=sky.Daylight;bool on=active&&l.enabled&&l.gameObject.activeInHierarchy;l.intensity=Mathf.Lerp(29,18,day);l.range=10.5f*journey.SpatialScale;l.spotAngle=42;l.innerSpotAngle=3;l.color=new Color(1,.85f,.64f);var beam=atmosphere.beams[k];beam.gameObject.SetActive(on);if(on){float length=10f;beam.transform.position=l.transform.position+l.transform.forward*(length*.5f*journey.SpatialScale);beam.transform.rotation=l.transform.rotation;beam.transform.localScale=new Vector3(6.2f,6.2f,length);props.Clear();props.SetFloat("_Strength",Mathf.Lerp(.84f,.28f,day));beam.SetPropertyBlock(props);}
   props.Clear();props.SetColor("_EmissionColor",on?new Color(1,.74f,.43f)*16:Color.black);atmosphere.bulbs[k].SetPropertyBlock(props);
   var halo=atmosphere.halos[k];halo.gameObject.SetActive(on);if(on){float facing=Mathf.Pow(Mathf.Clamp01(Vector3.Dot(l.transform.forward,(journey.view.transform.position-l.transform.position).normalized)),1.6f);halo.transform.position=l.transform.position+l.transform.forward*(.018f*journey.SpatialScale);halo.transform.rotation=journey.view.transform.rotation;halo.transform.localScale=Vector3.one*.72f;props.Clear();props.SetColor("_Tint",new Color(1,.66f,.28f,.75f*facing));halo.SetPropertyBlock(props);
    var flare=flares[k];flare.gameObject.SetActive(facing>.015f);flare.transform.position=halo.transform.position;flare.transform.rotation=halo.transform.rotation;flare.transform.localScale=Vector3.one*.88f;props.Clear();props.SetColor("_Tint",new Color(1,.79f,.48f,.66f*facing));flare.SetPropertyBlock(props);
   }else flares[k].gameObject.SetActive(false);
  }
 }
 void ClearHiddenSandbox(){
  if(CapturesPointer)EndCelestialDrag();
  foreach(var ps in atmosphere.dust){var emission=ps.emission;emission.rateOverTime=0;ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
  var plumeEmission=plume.emission;plumeEmission.rateOverTime=0;plume.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
  foreach(var beam in atmosphere.beams)if(beam)beam.gameObject.SetActive(false);
  foreach(var halo in atmosphere.halos)if(halo)halo.gameObject.SetActive(false);
  foreach(var flare in flares)if(flare)flare.gameObject.SetActive(false);
  DarkenBulbs(false);
 }
 // Visible locomotive bulbs still need a black block each frame: the native
 // material bridge consumes and clears it. Truly hidden bulbs need no rewrite.
 void DarkenBulbs(bool visibleOnly){props.Clear();props.SetColor("_EmissionColor",Color.black);foreach(var bulb in atmosphere.bulbs)if(bulb&&(!visibleOnly||bulb.enabled&&bulb.gameObject.activeInHierarchy))bulb.SetPropertyBlock(props);}
 void OnEnable(){swanHiddenCleaned=false;refreshCloudProvider=true;}
 void OnDisable(){CapturesPointer=false;}
}
