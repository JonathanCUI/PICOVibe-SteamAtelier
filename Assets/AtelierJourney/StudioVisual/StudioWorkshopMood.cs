using UnityEngine;using UnityEngine.Rendering;using UnityEngine.Rendering.Universal;
// Lighting mode only. Does not alter assembly, driving, input or particle controllers.
[DefaultExecutionOrder(2500)]
public sealed class StudioWorkshopMood:MonoBehaviour {
 public SteamJourney journey;public Light key,fill,rim;public Texture workshopReflection,dayReflection;public GameObject workshopRoot;
 bool initialized,last;Quaternion keyRotation,fillRotation,rimRotation;DepthOfField depth;
 void Awake(){keyRotation=key.transform.localRotation;fillRotation=fill.transform.localRotation;rimRotation=rim.transform.localRotation;var q=FindObjectOfType<StudioVisualQuality>();if(q&&q.grading)q.grading.profile.TryGet<DepthOfField>(out depth);ApplyImmediate();}
 void LateUpdate() { using (SwanJourney.SwanJourneyCpuTimings.Measure(SwanJourney.SwanJourneyCpuTimings.Stage.WorkshopMood)) CpuMeasuredLateUpdate(); }
    void CpuMeasuredLateUpdate(){if(!journey)return;bool day=journey.inSandbox;
  if(depth){depth.active=false;float distance=Vector3.Distance(journey.view.transform.position,journey.trainAnchor.TransformPoint(new Vector3(0,1.6f,0)));depth.gaussianStart.value=distance+4.5f;depth.gaussianEnd.value=distance+14;}
  if(initialized&&last==day)return;ApplyImmediate();
 }
 public void ApplyImmediate(){if(!journey)return;bool day=journey.inSandbox;initialized=true;last=day;RenderSettings.sun=key;fill.shadows=LightShadows.None;key.enabled=fill.enabled=rim.enabled=true;
  // Decoration is a child container; the controller itself must remain active.
  foreach(Transform t in transform)if(t.name=="Workshop decoration visibility")t.gameObject.SetActive(false);
  key.intensity=day?2:1.8f;fill.intensity=day?.4f:.46f;rim.intensity=day?.55f:1.05f;
  key.color=day?new Color(1,.9f,.76f):new Color(1,.882f,.729f);fill.color=new Color(.788f,.867f,.949f);rim.color=new Color(.824f,.898f,1);
  if(day){key.transform.rotation=journey.SceneRotationToWorld(Quaternion.Euler(63,-38,0));fill.transform.rotation=journey.SceneRotationToWorld(Quaternion.Euler(40,135,0));rim.transform.rotation=journey.SceneRotationToWorld(Quaternion.Euler(22,200,0));}else{key.transform.localRotation=keyRotation;fill.transform.localRotation=fillRotation;rim.transform.localRotation=rimRotation;}
  RenderSettings.customReflectionTexture=day?dayReflection:workshopReflection;RenderSettings.reflectionIntensity=day?.65f:.70f;
  RenderSettings.ambientSkyColor=day?new Color(.4f,.45f,.52f):new Color(.569f,.686f,.796f)*.30f;
  RenderSettings.ambientEquatorColor=day?new Color(.27f,.24f,.2f):new Color(.443f,.518f,.608f)*.21f;
  RenderSettings.ambientGroundColor=day?new Color(.14f,.12f,.09f):new Color(.298f,.290f,.282f)*.16f;
  journey.view.backgroundColor=day?new Color(.46f,.54f,.61f):new Color(.078f,.114f,.145f);
 }
}
