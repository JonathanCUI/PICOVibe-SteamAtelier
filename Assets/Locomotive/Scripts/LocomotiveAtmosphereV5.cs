using UnityEngine;
[DefaultExecutionOrder(300)] public class LocomotiveAtmosphereV5 : MonoBehaviour {
 public LocomotiveController engine; public LocomotiveThermalEffects effects; public LocomotivePresentationV4 presentation;
 public GameObject fireVolume,blastSteam,smokeboxGas,headlightBeam; Material jet;int previousPulse;float pulse;
 void Start(){if(blastSteam)jet=blastSteam.GetComponent<Renderer>().material;}
 void LateUpdate(){bool heat=effects.fireEnabled&&effects.HasModule(0)&&!engine.exploded&&!engine.assemblyLesson;bool complete=!effects.freeAssembly||effects.freeAssembly.Complete;
 if(fireVolume)fireVolume.SetActive(heat);bool steam=complete&&engine.running&&engine.principle;
 if(blastSteam)blastSteam.SetActive(steam);if(smokeboxGas)smokeboxGas.SetActive(heat&&complete&&engine.principle);
 if(effects.exhaustPulses!=previousPulse){pulse=1;previousPulse=effects.exhaustPulses;}pulse=Mathf.MoveTowards(pulse,0,Time.deltaTime*3.5f);if(jet)jet.SetFloat("_Density",8f+pulse*15);
 if(headlightBeam)headlightBeam.SetActive(presentation.lightBlend>.001f&&(!effects.freeAssembly||effects.freeAssembly.HasPiece("headlamp")));
 }
}
