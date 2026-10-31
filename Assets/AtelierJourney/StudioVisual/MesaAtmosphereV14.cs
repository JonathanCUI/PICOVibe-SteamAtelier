using System.Linq;using UnityEngine;using UnityEngine.Rendering;
[DefaultExecutionOrder(3200)]public sealed class MesaAtmosphereV14:MonoBehaviour {
 public SteamJourney journey;public MesaDayNightV13 sky;public Light[] lamps;public Renderer[] bulbs,beams,halos;public ParticleSystem[] dust;public float ReviewDust=-1;public float DustStrength{get;private set;}public float Elapsed{get;private set;}
 MaterialPropertyBlock block;Vector3[] origins;Vector3[] scales;
 void Awake(){block=new MaterialPropertyBlock();origins=sky.clouds.Select(t=>t.localPosition).ToArray();scales=sky.clouds.Select(t=>t.localScale).ToArray();}
 void LateUpdate(){if(!journey||!sky)return;bool active=journey.inSandbox;Elapsed+=Time.unscaledDeltaTime;
 Shader.SetGlobalColor("_MesaAmbientSky",RenderSettings.ambientSkyColor);Shader.SetGlobalColor("_MesaAmbientHorizon",RenderSettings.ambientEquatorColor);Shader.SetGlobalColor("_MesaAmbientGround",RenderSettings.ambientGroundColor);
 if(active)for(int k=0;k<sky.clouds.Length;k++){var t=sky.clouds[k];float phase=k*2.37f;t.localPosition=origins[k]+new Vector3(Mathf.Sin(Elapsed*(.019f+k*.0017f)+phase)*1.15f,Mathf.Sin(Elapsed*.12f+phase)*.11f,Mathf.Cos(Elapsed*.021f+phase)*.55f);float breathe=1+Mathf.Sin(Elapsed*.075f+phase)*.018f;t.localScale=Vector3.Scale(scales[k],new Vector3(breathe,1+(breathe-1)*.6f,2-breathe));}
 for(int k=0;k<lamps.Length;k++){var l=lamps[k];bool on=active&&l.gameObject.activeInHierarchy&&l.enabled;float day=sky.Daylight;l.intensity=Mathf.Lerp(18,11,day);l.range=6.5f*journey.SpatialScale;l.spotAngle=34;l.innerSpotAngle=17;l.color=new Color(1,.78f,.48f);
 if(k<bulbs.Length&&bulbs[k]){block.Clear();block.SetColor("_EmissionColor",on?new Color(1,.69f,.30f)*10:Color.black);bulbs[k].SetPropertyBlock(block);}
 if(k<beams.Length){var r=beams[k];r.gameObject.SetActive(on);if(on){float length=5.6f;r.transform.position=l.transform.position+l.transform.forward*(length*.5f*journey.SpatialScale);r.transform.rotation=l.transform.rotation;r.transform.localScale=new Vector3(2.15f,2.15f,length);block.Clear();block.SetFloat("_Strength",Mathf.Lerp(.48f,.12f,day));r.SetPropertyBlock(block);}}
 if(k<halos.Length){var r=halos[k];r.gameObject.SetActive(on);if(on){r.transform.position=l.transform.position+l.transform.forward*(.012f*journey.SpatialScale);r.transform.rotation=journey.view.transform.rotation;r.transform.localScale=Vector3.one*.23f;float facing=Mathf.Pow(Mathf.Clamp01(Vector3.Dot(l.transform.forward,(journey.view.transform.position-l.transform.position).normalized)),2);block.Clear();block.SetColor("_Tint",new Color(1,.63f,.25f,.28f*facing));r.SetPropertyBlock(block);}}
 }
 float pulse=Mathf.Max(0,Mathf.Sin((Elapsed+7)*Mathf.PI*2/39));DustStrength=active?(ReviewDust>=0?ReviewDust:pulse*pulse*pulse):0;
 foreach(var ps in dust){var em=ps.emission;em.rateOverTime=3.4f*DustStrength;if(active&&!ps.isPlaying)ps.Play();if(!active&&ps.isPlaying)ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
 }
}
