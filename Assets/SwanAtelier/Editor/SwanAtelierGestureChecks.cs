using System;
using System.Linq;
using UnityEngine;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace SwanAtelier.Editor
{
    public static class SwanAtelierGestureChecks
    {
        static void Check(bool value, string message) { if (!value) throw new Exception("Two-hand: " + message); }
        public static void Run(SwanAtelierController game)
        {
            CheckTransformMath(); CheckInputPriority(game); CheckRelaxedPairing(game);
            Debug.Log("ATELIER_SWAN_GESTURE_CHECKS_PASSED translation,yaw-only,scale-limits,limit-reversal,regrab,degenerate-hands,tracking-jump,pair-priority,slot-order,release-latch,fast-tap,dedup,cancel,reset");
        }
        static void Settle(SwanTwoHandManipulation manipulation, Vector3 a, Vector3 b)
        { for (int i=0;i<45;i++) Check(manipulation.Step(a,b,1f/60),"valid pose unexpectedly cancelled"); }
        static void CheckTransformMath()
        {
            var go=new GameObject("Two-hand test root");
            try {
                go.transform.position=new Vector3(1,.9f,2);go.transform.rotation=Quaternion.Euler(0,20,0);
                var manipulation=new SwanTwoHandManipulation(go.transform);
                Vector3 midpoint=new Vector3(0,1,.5f), a=midpoint-Vector3.right*.25f,b=midpoint+Vector3.right*.25f;
                Check(manipulation.Begin(a,b),"valid pair rejected");
                Check(Vector3.Distance(go.transform.position,new Vector3(1,.9f,2))<.0001f,"begin jumped");
                Vector3 translation=new Vector3(.1f,.1f,.2f);a+=translation;b+=translation;midpoint+=translation;Settle(manipulation,a,b);
                Check(Vector3.Distance(go.transform.position,new Vector3(1,.9f,2)+translation)<.001f,"midpoint did not translate in 3D");
                for(int i=1;i<=18;i++) {
                    Vector3 half=Quaternion.Euler(0,i*5,0)*Vector3.right*.25f;a=midpoint-half;b=midpoint+half;
                    Check(manipulation.Step(a,b,1f/60),"yaw gesture cancelled");
                }
                Settle(manipulation,a,b);
                Check(Quaternion.Angle(go.transform.rotation,Quaternion.Euler(0,110,0))<.1f,"Y rotation incorrect");
                Check(Vector3.Dot(go.transform.up,Vector3.up)>.9999f,"gesture introduced pitch/roll");
                Check(Vector3.Distance(go.transform.position,new Vector3(1,.9f,2)+translation)<.001f,"rotation made workbench orbit");
                for(int i=1;i<=20;i++) { float half=.25f+i*.015f;a=midpoint-Vector3.back*half;b=midpoint+Vector3.back*half;manipulation.Step(a,b,1f/60); }
                Settle(manipulation,a,b);Check(Mathf.Abs(manipulation.Scale-1.5f)<.001f,"upper limit incorrect");
                a=midpoint-Vector3.back*.50f;b=midpoint+Vector3.back*.50f;Settle(manipulation,a,b);
                Check(manipulation.Scale<1.45f,"upper limit has reversal dead zone");
                for(int i=1;i<=35;i++) { float half=.5f-i*.012f;a=midpoint-Vector3.back*half;b=midpoint+Vector3.back*half;manipulation.Step(a,b,1f/60); }
                Settle(manipulation,a,b);Check(Mathf.Abs(manipulation.Scale-.26f)<.001f,"lower limit must be 40% of the previous 0.65 limit");
                a=midpoint-Vector3.back*.09f;b=midpoint+Vector3.back*.09f;Settle(manipulation,a,b);
                Check(manipulation.Scale>.28f,"lower limit has reversal dead zone");
                float scale=manipulation.Scale;Vector3 position=go.transform.position;Quaternion rotation=go.transform.rotation;
                manipulation.End();Check(!manipulation.Step(a+Vector3.right,b+Vector3.right,.016f),"released gesture still moves");
                Check(manipulation.Begin(a,b)&&Mathf.Abs(manipulation.Scale-scale)<.0001f,"regrab reset scale");
                Check(!manipulation.Step(new Vector3(float.NaN,0,0),b,.016f)&&!manipulation.Active,"invalid pose not cancelled");
                Check(Vector3.Distance(go.transform.position,position)<.0001f,"invalid pose moved scene");
                Check(!manipulation.Begin(a,a),"coincident hands accepted");
                Check(manipulation.Begin(midpoint-Vector3.right*.04f,midpoint+Vector3.right*.04f),"8 cm pair rejected");
                Check(manipulation.Begin(midpoint-Vector3.up*.2f,midpoint+Vector3.up*.2f),"vertical pair rejected");
                Settle(manipulation,midpoint-Vector3.up*.2f,midpoint+Vector3.up*.2f);
                Check(Quaternion.Angle(go.transform.rotation,rotation)<.1f,"vertical pair spun scene");
                Check(!manipulation.Step(midpoint+Vector3.right*5,midpoint+Vector3.right*6,.016f),"tracking teleport accepted");
            } finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        static void Send(SwanAtelierController game,long id,TouchPhase phase,GameObject target,Vector3 hand,Vector3 hit)
        { game.QueueGestureSample(new SwanGestureSample{id=id,phase=phase,pinch=true,target=target,handPosition=hand,hitPosition=hit}); }
        static void CheckRelaxedPairing(SwanAtelierController game)
        {
            var control=game.content.GetComponentsInChildren<AtelierControl>(true).First(c=>c.action==AtelierAction.Lights).gameObject;
            var foreign=new GameObject("Unrelated app target");
            bool light=game.presentation.lightOn;
            Vector3 a=new Vector3(-.15f,1,.5f),b=new Vector3(.15f,1,.5f),hit=control.transform.position;
            try {
                game.ResetSpatialGestures();
                Send(game,301,TouchPhase.Began,control,a,hit);game.ProcessGestureFrame(10,.016f);
                game.ProcessGestureFrame(10.22,.016f);
                Check(game.presentation.lightOn==light,"button committed before the second hand joining window");
                Send(game,302,TouchPhase.Began,null,b,Vector3.zero);game.ProcessGestureFrame(10.24,.016f);
                Check(game.TwoHandActive&&game.presentation.lightOn==light,"no-hit second hand was rejected or clicked button");
                game.ResetSpatialGestures();
                Send(game,301,TouchPhase.Began,null,a,Vector3.zero);game.ProcessGestureFrame(11,.016f);
                Send(game,302,TouchPhase.Began,control,b,hit);game.ProcessGestureFrame(11.2,.016f);
                Check(game.TwoHandActive,"no-hit first hand was rejected");
                game.ResetSpatialGestures();
                Send(game,301,TouchPhase.Began,null,a,Vector3.zero);Send(game,302,TouchPhase.Began,null,b,Vector3.zero);game.ProcessGestureFrame(12,.016f);
                Check(!game.TwoHandActive,"two unrelated empty pinches grabbed the stage");
                game.ResetSpatialGestures();
                Send(game,301,TouchPhase.Began,control,a,hit);Send(game,302,TouchPhase.Began,foreign,b,hit);game.ProcessGestureFrame(13,.016f);
                Check(!game.TwoHandActive,"foreign target joined the stage gesture");
                game.ResetSpatialGestures();
                game.QueueGestureSample(new SwanGestureSample{id=301,phase=TouchPhase.Began,pinch=false,target=control,handPosition=a,hitPosition=hit});
                Send(game,302,TouchPhase.Began,control,b,hit);game.ProcessGestureFrame(14,.016f);
                Send(game,301,TouchPhase.Moved,control,a,hit);game.ProcessGestureFrame(14.04,.016f);
                Check(game.TwoHandActive,"touch-to-pinch transition did not join the gesture");
                game.QueueGestureSample(new SwanGestureSample{id=301,phase=TouchPhase.Moved,pinch=false,target=control,handPosition=a,hitPosition=hit});
                game.ProcessGestureFrame(14.08,.016f);
                Check(!game.TwoHandActive,"changing from pinch to touch left two-hand manipulation running");
                Debug.Log("ATELIER_SWAN_RELAXED_PAIR_CHECKS_PASSED late-second,no-hit-first,no-hit-second,unowned-rejected,foreign-rejected,live-pinch-type");
            } finally {
                game.ResetSpatialGestures();UnityEngine.Object.DestroyImmediate(foreign);
                if(game.presentation.lightOn!=light)game.presentation.ToggleHeadlight();
            }
        }
        static void CheckInputPriority(SwanAtelierController game)
        {
            Vector3 origin=game.transform.position,scale=game.transform.localScale;Quaternion rotation=game.transform.rotation;
            bool light=game.presentation.lightOn;
            var control=game.content.GetComponentsInChildren<AtelierControl>(true).First(c=>c.action==AtelierAction.Lights).gameObject;
            var part=game.assembly.pieces.SelectMany(p=>p.members).First(p=>p&&p.gameObject.activeInHierarchy).gameObject;
            Vector3 a=new Vector3(-.2f,1,.5f),b=new Vector3(.2f,1,.5f),hit=control.transform.position;
            game.ResetSpatialGestures();
            try {
                Send(game,11,TouchPhase.Began,control,a,hit);game.ProcessGestureFrame(0,.016f);
                Check(game.presentation.lightOn==light,"first pinch immediately fired a button");
                Send(game,22,TouchPhase.Began,control,b,hit);game.ProcessGestureFrame(.08,.016f);
                Check(game.TwoHandActive&&game.presentation.lightOn==light,"second pinch failed to suppress button");
                for(int i=1;i<=30;i++) {
                    Vector3 delta=Vector3.forward*(.1f*i/30);
                    // Reverse arrival order, as primary/secondary can swap.
                    Send(game,22,TouchPhase.Moved,control,b+delta,hit);Send(game,11,TouchPhase.Moved,control,a+delta,hit);
                    game.ProcessGestureFrame(.08+i/60.0,1f/60);
                }
                Check(game.transform.position.z>origin.z+.08f,"pair did not move workbench");
                Check(Quaternion.Angle(game.transform.rotation,rotation)<.01f,"slot swap caused rotation");
                Send(game,11,TouchPhase.Ended,control,a+Vector3.forward*.1f,hit);game.ProcessGestureFrame(.7,.016f);
                Check(!game.TwoHandActive,"one-hand release did not stop");Vector3 stopped=game.transform.position;
                Send(game,22,TouchPhase.Moved,control,b+Vector3.forward*.2f,hit);game.ProcessGestureFrame(1,.016f);
                Check(Vector3.Distance(stopped,game.transform.position)<.0001f&&game.presentation.lightOn==light,"remaining hand triggered single-hand action");
                Send(game,22,TouchPhase.Ended,control,b,hit);game.ProcessGestureFrame(1.1,.016f);
                Send(game,11,TouchPhase.Began,control,a,hit);Send(game,11,TouchPhase.Began,control,a,hit);
                Send(game,11,TouchPhase.Ended,control,a,hit);game.ProcessGestureFrame(2,.016f);
                Check(game.presentation.lightOn!=light,"quick tap lost or repeated Began toggled twice");
                bool toggled=game.presentation.lightOn;
                Send(game,11,TouchPhase.Began,control,a,hit);game.ProcessGestureFrame(3,.016f);
                Send(game,11,TouchPhase.Canceled,control,a,hit);game.ProcessGestureFrame(3.05,.016f);
                Check(game.presentation.lightOn==toggled,"cancelled pinch activated button");
                Send(game,11,TouchPhase.Began,part,a,part.transform.position);Send(game,22,TouchPhase.Began,part,b,part.transform.position);
                game.ProcessGestureFrame(4,.016f);
                Check(game.TwoHandActive&&game.assembly.Complete,"two-hand grasp detached a part");
                game.ResetSpatialGestures();stopped=game.transform.position;
                Send(game,11,TouchPhase.Moved,part,a+Vector3.up*.1f,hit);Send(game,22,TouchPhase.Moved,part,b+Vector3.up*.1f,hit);
                game.ProcessGestureFrame(5,.016f);
                Check(!game.TwoHandActive&&Vector3.Distance(stopped,game.transform.position)<.0001f,"pause/reset resumed stale grip");
            } finally {
                game.ResetSpatialGestures();game.transform.SetPositionAndRotation(origin,rotation);game.transform.localScale=scale;
                if(game.presentation.lightOn!=light)game.presentation.ToggleHeadlight();
                game.CompleteAssembly();
            }
        }
    }
}
