using UnityEngine;
[DefaultExecutionOrder(3450)]
public sealed class SpatialLightingV20:MonoBehaviour {
 public SteamJourney journey;public Texture3D tunnelAmbient;public Vector3 fieldMin,fieldSize;
 void LateUpdate() { using (SwanJourney.SwanJourneyCpuTimings.Measure(SwanJourney.SwanJourneyCpuTimings.Stage.Lighting)) CpuMeasuredLateUpdate(); }
    void CpuMeasuredLateUpdate(){if(journey&&journey.SwanInputActive)return;Shader.SetGlobalMatrix("_TunnelWorldToLocal20",journey?journey.SpatialWorldToLocal:Matrix4x4.identity);Shader.SetGlobalTexture("_TunnelAmbient20",tunnelAmbient);Shader.SetGlobalVector("_TunnelMin20",fieldMin);Shader.SetGlobalVector("_TunnelInv20",new Vector3(1/fieldSize.x,1/fieldSize.y,1/fieldSize.z));Shader.SetGlobalFloat("_TunnelActive20",journey.inSandbox?1:0);}
 void OnDisable(){if(!journey||!journey.SwanInputActive)Shader.SetGlobalFloat("_TunnelActive20",0);}
}
