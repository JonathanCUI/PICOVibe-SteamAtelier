using UnityEngine;
// Translates the combination-lever output to the existing inside-admission rocker plane.
// The solver and all original rod lengths remain unchanged.
[DefaultExecutionOrder(2600)]
public sealed class StudioValveBridge : MonoBehaviour {
 public SteamJourney journey;public Transform[] bridges;
 void LateUpdate() { using (SwanJourney.SwanJourneyCpuTimings.Measure(SwanJourney.SwanJourneyCpuTimings.Stage.Valve)) CpuMeasuredLateUpdate(); }
    void CpuMeasuredLateUpdate(){Sync();}
 public void Sync(){if(!journey||bridges==null)return;for(int i=0;i<bridges.Length;i++){var s=journey.engine.sides[i];if(bridges[i]){bridges[i].localPosition=s.pose.V.AtZ(s.sign*1.37f);bridges[i].localRotation=Quaternion.identity;}}}
}
