using UnityEngine;

namespace SwanAtelier
{
    [DefaultExecutionOrder(1600)]
    public sealed class SwanCutawaySteam:MonoBehaviour
    {
        public LocomotiveController engine;
        public LocomotiveThermalEffects thermal;
        public MeshRenderer[] volumes;
        void LateUpdate(){Refresh();}
        public void Refresh()
        {
            bool visible=engine.principle&&!engine.exploded&&!engine.assemblyLesson;
            foreach(var side in engine.sides) {
                if(side.exhaustLeft)side.exhaustLeft.gameObject.SetActive(false);
                if(side.exhaustRight)side.exhaustRight.gameObject.SetActive(false);
                float piston=(float)side.pose.slider+1;
                for(int end=0;end<2;end++) {
                    var chamber=end==0?side.admissionLeft:side.admissionRight;
                    float lo=end==0?2.82f:piston+.04f,hi=end==0?piston-.04f:3.48f;
                    float length=Mathf.Max(.006f,hi-lo);
                    chamber.gameObject.SetActive(visible);
                    if(!visible)continue;
                    chamber.localPosition=new Vector3((lo+hi)*.5f,.65f,side.sign*1.085f);
                    chamber.localScale=new Vector3(length,1,1);
                }
            }
            if(thermal.flows==null)return;
            foreach(var flow in thermal.flows) {
                bool on=visible&&thermal.HasModule(flow.module);
                // At rest the arrows describe all routes; while powered, the
                // existing valve timing determines which direction is active.
                if(engine.running&&flow.gate!=0&&flow.side>=0) {
                    var pose=engine.sides[flow.side].pose;
                    on&=flow.gate==1?pose.LeftAdmission>.001:flow.gate==2?pose.RightAdmission>.001:
                        flow.gate==3?pose.LeftExhaust>.001:flow.gate==4?pose.RightExhaust>.001:pose.LeftExhaust>.001||pose.RightExhaust>.001;
                }
                float total=0;for(int j=1;j<flow.points.Length;j++)total+=Vector3.Distance(flow.points[j-1],flow.points[j]);
                for(int k=0;k<flow.arrows.Length;k++) {
                    var arrow=flow.arrows[k];if(!arrow)continue;
                    arrow.gameObject.SetActive(on&&total>.001f);if(!on||total<=.001f)continue;
                    float distance=Mathf.Repeat((engine.running?thermal.flowClock*flow.speed:0)+total*k/flow.arrows.Length+flow.offset,total);
                    for(int j=1;j<flow.points.Length;j++) {
                        float length=Vector3.Distance(flow.points[j-1],flow.points[j]);if(length<.0001f)continue;
                        if(distance<=length) {arrow.localPosition=Vector3.Lerp(flow.points[j-1],flow.points[j],distance/length);
                            arrow.localRotation=Quaternion.LookRotation(flow.points[j]-flow.points[j-1]);break;}
                        distance-=length;
                    }
                }
            }
        }
    }
}
