using UnityEngine;

namespace SwanAtelier
{
    public sealed partial class SwanAtelierController
    {
        void ConfigureThrottleHand(Pointer pointer,Vector3 hand,bool pinch)
        {
            if(pointer==null||!pointer.control||pointer.control.action!=AtelierAction.Throttle||!pinch||
               !SwanTwoHandManipulation.Finite(hand)||hand.sqrMagnitude<.000001f)return;
            var frame=pointer.control.transform.parent;
            pointer.useHandForThrottle=true;pointer.startHand=hand;
            pointer.throttleLiftAxis=frame.up;pointer.throttlePushAxis=-frame.forward;
            pointer.throttleTravel=Mathf.Clamp(.10f*SceneScale,.04f,.15f);
            pointer.lastLoggedThrottle=pointer.initialThrottle;
            Debug.Log("SWAN_THROTTLE_GRAB source=hand travel="+pointer.throttleTravel.ToString("F3"));
        }
        void MoveThrottleHand(Pointer pointer,Vector3 hand)
        {
            if(!pointer.active||!SwanTwoHandManipulation.Finite(hand))return;
            Vector3 delta=hand-pointer.startHand;
            if(delta.magnitude>.6f)return;
            float lift=Vector3.Dot(delta,pointer.throttleLiftAxis),push=Vector3.Dot(delta,pointer.throttlePushAxis);
            float displacement=Mathf.Abs(push)>Mathf.Abs(lift)?push:lift;
            SetThrottle(pointer.initialThrottle+displacement/pointer.throttleTravel);
            if(Mathf.Abs(atelier.throttle-pointer.lastLoggedThrottle)>=.1f) {
                pointer.lastLoggedThrottle=atelier.throttle;
                SetMessage("汽门 "+(atelier.throttle*100).ToString("F0")+"% · "+(atelier.direction==0?"空挡":atelier.direction>0?"前进":"后退"));
                Debug.Log("SWAN_THROTTLE value="+atelier.throttle.ToString("F2")+" running="+engine.running+" direction="+atelier.direction);
            }
        }
    }
}
