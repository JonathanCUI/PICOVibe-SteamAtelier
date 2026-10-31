using System;
using System.Linq;
using SwanAtelier;
using UnityEngine;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace SwanJourney.Editor
{
    public static class SwanJourneyGestureChecks
    {
        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("Swan Journey gestures: " + message);
        }
        static GameObject Target(JourneyModule module)
        {
            var collider = module.preview.GetComponentsInChildren<Collider>(true).FirstOrDefault(c => c.enabled && c.gameObject.activeInHierarchy);
            Check(collider, "module preview has no live native hit collider: " + module.title);
            return collider.gameObject;
        }
        static void Send(SwanJourneyInteraction input, long id, GameObject target, TouchPhase phase, Vector3 hit, Vector3 hand)
        {
            input.QueueGestureSample(new SwanGestureSample { id=id, target=target, phase=phase, pinch=true, hitPosition=hit, handPosition=hand });
        }
        static void Tap(SwanJourneyInteraction input, JourneyModule module, ref double now, bool canceled=false, long id=7101)
        {
            var hit=module.preview.transform.position;var hand=hit+Vector3.back*.35f;var target=Target(module);
            Send(input,id,target,TouchPhase.Began,hit,hand);input.ProcessGestureFrame(now,.001f);
            now+=.08;
            Send(input,id,target,canceled?TouchPhase.Canceled:TouchPhase.Ended,hit,hand);input.ProcessGestureFrame(now,.001f);
            now+=.10;
        }
        static void Park(SteamJourney journey, JourneyModule module, Vector3 worldOffset)
        {
            if(module.installed)Check(journey.DetachModule(module),"cannot prepare detached module");
            module.parked=true;
            module.preview.transform.position=journey.trainAnchor.TransformPoint(module.previewCentre)+worldOffset;
            journey.RefreshPreview();
        }
        static void Pair(SwanJourneyInteraction input, Vector3 a,Vector3 b, ref double now, float dt=1f/90)
        {
            // Reverse delivery order on every frame. IDs, rather than the
            // primary/secondary slot order, must preserve the hand baseline.
            Send(input,7302,null,TouchPhase.Moved,b,b);
            Send(input,7301,null,TouchPhase.Moved,a,a);
            now+=dt;input.ProcessGestureFrame(now,dt);
        }
        static void CheckExplosion(SwanJourneyInteraction input, ref double now)
        {
            var journey=input.journey;var all=journey.modules.Concat(journey.finishing).ToArray();
            int count=input.ExplodeCount;
            Check(input.ExplodeAll()&&input.ExplodingCount==18,"explode did not start all 18 installed groups");
            Check(all.All(m=>!m.installed&&m.parked&&m.preview.activeInHierarchy),"explode bypassed Journey detach state or hid a parked preview");
            Check(journey.stage==0&&all.All(m=>!m.verified&&m.observed==0),"explode did not invalidate stage/observation state");
            input.TickExplosion(10);
            Check(input.Exploding,"one stalled frame consumed the whole explosion animation");
            for(int i=0;i<30;i++)input.TickExplosion(1f/90);
            Check(!input.Exploding&&input.ExplodeCount==count+1,"explosion did not settle after 0.30 seconds");
            Check(all.All(m=>Vector3.Distance(journey.trainAnchor.InverseTransformPoint(m.preview.transform.position),m.previewCentre)>1.3f),"exploded groups were not offset from their sockets");

            // Leave one preview parked. Re-exploding the installed subset must
            // not pull an already detached object away from its user position.
            for(int i=0;i<17;i++)Check(journey.InstallCurrent(),"prepare installed subset for explosion takeover");
            var parked=all.First(m=>!m.installed);var parkedPose=parked.preview.transform.position;
            Check(input.ExplodeAll()&&input.ExplodingCount==17,"partial explode changed the missing-group contract");
            for(int i=0;i<8;i++)input.TickExplosion(1f/90);
            Check(Vector3.Distance(parked.preview.transform.position,parkedPose)<.00001f,"explode moved a previously parked preview");
            var localPoses=all.Select(m=>journey.trainAnchor.InverseTransformPoint(m.preview.transform.position)).ToArray();
            var hit=all[0].preview.transform.position;var a=hit+Vector3.back*.35f;var b=a+Vector3.right*.4f;
            Send(input,7301,Target(all[0]),TouchPhase.Began,hit,a);
            Send(input,7302,null,TouchPhase.Began,b,b);now+=.01;input.ProcessGestureFrame(now,1f/90);
            Check(input.TwoHandActive&&!input.Exploding,"two hands failed to take over an explosion");
            for(int i=0;i<30;i++)input.TickExplosion(1f/90);
            Check(all.All(m=>!m.installed&&m.parked),"explosion cancellation installed or unparked a group");
            for(int i=0;i<all.Length;i++)Check(Vector3.Distance(localPoses[i],journey.trainAnchor.InverseTransformPoint(all[i].preview.transform.position))<.00001f,"explosion cancellation jumped group "+i);
            input.ResetSpatialGestures();

            // Restore the starting assembly, then detach the first part to
            // cancel completion and leave the existing gesture suite unblocked.
            for(int i=0;i<18;i++)Check(journey.InstallCurrent(),"restore assembly after explosion checks");
            Check(journey.DetachModule(all[0]),"cancel restored completion for remaining gesture checks");
        }
        public static void Run(SwanJourneyInteraction input)
        {
            Check(input&&input.Ready,"input bridge is not ready");
            var journey=input.journey;var root=input.workbenchRoot;
            Check(journey.modules.Length==7&&journey.finishing.Length==11,"expected the V23 7 + 11 state model");
            Check(journey.Complete&&!journey.inSandbox&&!journey.SwanInputBlocked,"start with completed workshop and settled arrival/completion effects");
            var position=root.position;var rotation=root.rotation;var scale=root.localScale;
            bool persistence=journey.suppressPersistence,locked=journey.verificationInputLock;
            double now=1000;
            journey.suppressPersistence=true;
            try
            {
                input.ResetSpatialGestures();
                CheckExplosion(input,ref now);
                var first=journey.modules[0];var second=journey.modules[1];
                Park(journey,first,root.right*.45f+root.up*.20f);
                Park(journey,second,-root.right*.45f+root.up*.20f);
                int returns=input.QuickReturnCount;
                Tap(input,second,ref now);Tap(input,second,ref now);
                Check(!input.QuickReturning&&!second.installed&&input.QuickReturnCount==returns,"out-of-order double pinch bypassed the teaching stage");
                Tap(input,first,ref now);Tap(input,first,ref now,id:7102);
                Check(input.QuickReturning&&!first.installed,"double pinch did not start a visible return flight");
                for(int i=0;i<30;i++)input.TickQuickReturn(1f/90);
                Check(first.installed&&!second.installed&&input.QuickReturnCount==returns+1,"current module double pinch did not use Journey installation");
                Check(journey.Current==second,"return did not advance the original Journey stage");

                // An interrupted pinch cannot count as either half of a double.
                Tap(input,second,ref now);Tap(input,second,ref now,canceled:true);Tap(input,second,ref now);
                Check(!input.QuickReturning&&!second.installed,"canceled pinch participated in double pinch");
                input.ResetSpatialGestures();

                // Cancel after reaching the socket: snapReady must never cause
                // an install on tracking loss/application pause.
                var start=second.preview.transform.position;var hand=start+Vector3.back*.35f;
                Send(input,7201,Target(second),TouchPhase.Began,start,hand);input.ProcessGestureFrame(now,.001f);
                now+=.30;
                var destination=journey.trainAnchor.TransformPoint(second.previewCentre);
                Send(input,7201,Target(second),TouchPhase.Moved,destination,hand+destination-start);input.ProcessGestureFrame(now,1f/90);
                Check(journey.SwanHeldModule==second,"single pinch failed to acquire a parked module");
                Check(journey.SnapReady,"local-space magnetic socket was not reached");
                now+=.01;
                Send(input,7201,Target(second),TouchPhase.Canceled,destination,hand+destination-start);input.ProcessGestureFrame(now,1f/90);
                Check(!second.installed&&journey.SwanHeldModule==null&&second.parked,"cancel at socket installed or lost the module");
                input.ResetSpatialGestures();Park(journey,second,-root.right*.45f+root.up*.20f);

                // Two hands take ownership from a committed single-part grab.
                start=second.preview.transform.position;Vector3 a=start+Vector3.back*.35f,b=a+Vector3.right*.4f;
                Send(input,7301,Target(second),TouchPhase.Began,start,a);input.ProcessGestureFrame(now,.001f);
                now+=.30;
                Send(input,7301,Target(second),TouchPhase.Moved,start,a);input.ProcessGestureFrame(now,1f/90);
                Check(journey.SwanHeldModule==second,"takeover setup did not commit the first hand");
                Send(input,7302,null,TouchPhase.Began,b,b);now+=.01;input.ProcessGestureFrame(now,1f/90);
                Check(input.TwoHandActive&&journey.SwanHeldModule==null&&!second.installed,"second hand failed to take over without installation");
                var beforeMove=root.position;var beforeTurn=root.rotation;
                a+=Vector3.right*.08f;b+=Vector3.right*.08f;
                for(int i=0;i<30;i++)Pair(input,a,b,ref now);
                Check(Vector3.Distance(root.position,beforeMove)>.04f,"two-hand translation did not move the whole root");
                Check(Quaternion.Angle(root.rotation,beforeTurn)<.1f,"slot-order swap caused an unwanted turn");

                Vector3 centre=(a+b)*.5f;
                for(float distance=.43f;distance<=1.001f;distance+=.03f)Pair(input,centre-Vector3.right*distance*.5f,centre+Vector3.right*distance*.5f,ref now);
                a=centre-Vector3.right*.5f;b=centre+Vector3.right*.5f;
                for(int i=0;i<50;i++)Pair(input,a,b,ref now);
                Check(input.SceneScale<=SwanTwoHandManipulation.MaximumScale+.001f&&input.SceneScale>1.49f,"upper two-hand scale bound failed");
                for(float distance=.97f;distance>=.08f;distance-=.03f)Pair(input,centre-Vector3.right*distance*.5f,centre+Vector3.right*distance*.5f,ref now);
                a=centre-Vector3.right*.04f;b=centre+Vector3.right*.04f;
                for(int i=0;i<50;i++)Pair(input,a,b,ref now);
                Check(input.SceneScale>=SwanTwoHandManipulation.MinimumScale-.001f&&input.SceneScale<.261f,"lower two-hand scale bound failed");
                var beforeInvalid=root.position;
                Pair(input,centre-Vector3.right*.01f,centre+Vector3.right*.01f,ref now);
                Check(!input.TwoHandActive&&Vector3.Distance(root.position,beforeInvalid)<.001f,"near-coincident hands produced a root jump");
                Check(!second.installed&&input.QuickReturnCount==returns+1,"two-hand gesture leaked into part installation");
                input.ResetSpatialGestures();
                journey.verificationInputLock=true;
                Tap(input,second,ref now);Tap(input,second,ref now);
                Check(!input.QuickReturning&&journey.SwanHeldModule==null,"verification input lock failed");
                Debug.Log("SWAN_JOURNEY_GESTURES_PASSED checks=18-groups,explode-animation,explode-partial,explode-takeover,double-pinch,stage-order,canceled-tap,canceled-snap,two-hand-takeover,stable-ids,scale-bounds,tracking-rejection,input-lock");
            }
            finally
            {
                input.ResetSpatialGestures();root.SetPositionAndRotation(position,rotation);root.localScale=scale;
                journey.ResetJourney();journey.GetComponent<JourneySceneArrival>()?.Cancel();
                journey.verificationInputLock=locked;journey.suppressPersistence=persistence;
            }
        }
    }
}
