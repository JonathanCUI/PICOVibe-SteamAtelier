using UnityEngine;
[DefaultExecutionOrder(2700)]
public sealed class CanyonMoodV11:MonoBehaviour {
 public SteamJourney journey;public Renderer[] workshopFloor;bool applied;float oldDistance;bool[] floorStates;
 void LateUpdate(){var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;if(!journey||!journey.inSandbox){if(applied&&pipeline)pipeline.shadowDistance=oldDistance;if(applied&&workshopFloor!=null)for(int i=0;i<workshopFloor.Length;i++)if(workshopFloor[i])workshopFloor[i].enabled=floorStates[i];applied=false;return;}if(applied)return;applied=true;if(workshopFloor!=null){floorStates=new bool[workshopFloor.Length];for(int i=0;i<workshopFloor.Length;i++)if(workshopFloor[i]){floorStates[i]=workshopFloor[i].enabled;workshopFloor[i].enabled=false;}}if(pipeline){oldDistance=pipeline.shadowDistance;pipeline.shadowDistance=75;}var m=UnityEngine.Object.FindObjectOfType<StudioWorkshopMood>();if(!m)return;
  m.key.intensity=1.8f;m.key.color=new Color(1,.83f,.65f);m.key.transform.rotation=Quaternion.Euler(32,-58,0);
  m.fill.intensity=.62f;m.fill.color=new Color(.78f,.87f,1);m.fill.transform.rotation=Quaternion.Euler(34,126,0);
  m.rim.intensity=.95f;m.rim.color=new Color(.82f,.90f,1);m.rim.transform.rotation=Quaternion.Euler(24,195,0);
  RenderSettings.ambientSkyColor=new Color(.34f,.40f,.48f);RenderSettings.ambientEquatorColor=new Color(.23f,.24f,.26f);RenderSettings.ambientGroundColor=new Color(.14f,.12f,.10f);RenderSettings.reflectionIntensity=.85f;journey.view.backgroundColor=new Color(.055f,.078f,.095f);
 }
}
