using System;
using System.Linq;
using UnityEngine;
using ByteDance.PICO.SpatialAdapter.Component;
using TouchPhase=UnityEngine.InputSystem.TouchPhase;

namespace SwanAtelier.Editor
{
    public static class SwanConsoleChecks
    {
        static void Check(bool value,string reason){if(!value)throw new Exception("Console: "+reason);}
        static SwanConsoleHitTarget Target(SwanAtelierController game,AtelierAction action)
        {
            return game.content.GetComponentsInChildren<AtelierControl>(true).First(c=>c.action==action)
                .GetComponentsInChildren<SwanConsoleHitTarget>(true).First();
        }
        static void Send(SwanAtelierController game,GameObject target,TouchPhase phase,Vector3 hand,Vector3 hit,double time)
        {
            game.QueueGestureSample(new SwanGestureSample{id=5011,phase=phase,pinch=true,target=target,handPosition=hand,hitPosition=hit});
            game.ProcessGestureFrame(time,.016f);
        }
        static void Tap(SwanAtelierController game,AtelierAction action,double time)
        {
            var target=Target(game,action);var p=target.transform.position;
            Send(game,target.gameObject,TouchPhase.Began,p,p,time);Send(game,target.gameObject,TouchPhase.Ended,p,p,time+.08);
        }
        public static void Run(SwanAtelierController game)
        {
            Vector3 position=game.transform.position,scale=game.transform.localScale;Quaternion rotation=game.transform.rotation;
            bool lights=game.presentation.lightOn;
            try {
                game.ResetSpatialGestures();game.CompleteAssembly();
                foreach(var control in game.content.GetComponentsInChildren<AtelierControl>(true)) {
                    var targets=control.GetComponentsInChildren<SwanConsoleHitTarget>(true);
                    Check(targets.Length>0,"control has no mesh hit target: "+control.name);
                    foreach(var target in targets)Check(target.hitBox&&target.GetComponent<MeshRenderer>()&&target.GetComponent<SpatialAdapterHoverEffect>(),"hit and highlight separated");
                    if(!control.GetComponent<MeshRenderer>())Check(!control.GetComponent<Collider>(),"empty ancestor intercepts control hit");
                }
                var throttle=Target(game,AtelierAction.Throttle);var controlRoot=throttle.GetComponentInParent<AtelierControl>();
                Vector3 hand=new Vector3(-.2f,1,.5f),hit=throttle.hitBox.bounds.center;
                Send(game,throttle.gameObject,TouchPhase.Began,hand,hit,100);
                game.ProcessGestureFrame(100.3,.016f);
                Send(game,throttle.gameObject,TouchPhase.Moved,hand+controlRoot.transform.parent.up*.07f,hit,100.4);
                Check(game.atelier.throttle>.6f&&game.engine.running,"stationary gaze with a moving pinch hand did not open throttle");
                float phase=game.engine.phase;game.TickSimulation(.2f);
                Check(Mathf.Abs(Mathf.DeltaAngle(phase,game.engine.phase))>1,"hand-operated throttle did not move mechanism");
                Send(game,throttle.gameObject,TouchPhase.Moved,hand,hit,100.5);
                Check(game.atelier.throttle<.01f&&!game.engine.running,"returning the hand did not close throttle");
                Send(game,throttle.gameObject,TouchPhase.Ended,hand,hit,100.6);
                game.transform.rotation=Quaternion.Euler(0,105,0);game.transform.localScale=scale*.26f;
                foreach(var target in game.GetComponentsInChildren<SwanConsoleHitTarget>(true)) {
                    target.RefreshSize();var size=Vector3.Scale(target.hitBox.size,target.transform.lossyScale);
                    Check(Mathf.Abs(size.x)>=.0139f&&Mathf.Abs(size.y)>=.0139f&&Mathf.Abs(size.z)>=.0139f,"scaled control is too small to target: "+target.name+" "+size);
                }
                hit=throttle.hitBox.bounds.center;
                Send(game,throttle.gameObject,TouchPhase.Began,hand,hit,101);game.ProcessGestureFrame(101.3,.016f);
                Send(game,throttle.gameObject,TouchPhase.Moved,hand-controlRoot.transform.parent.forward*.03f,hit,101.4);
                Check(game.atelier.throttle>.6f&&game.engine.running,"forward push on rotated/scaled panel failed");
                Send(game,throttle.gameObject,TouchPhase.Ended,hand,hit,101.5);
                Tap(game,AtelierAction.Brake,102);Check(!game.engine.running,"brake mesh input failed");
                Tap(game,AtelierAction.Reverse,103);Check(game.atelier.direction==0,"neutral mesh input failed");
                Tap(game,AtelierAction.Reverse,104);Check(game.atelier.direction==-1,"reverse mesh input failed");
                Tap(game,AtelierAction.Lights,105);Check(game.presentation.lightOn!=lights,"light mesh input failed");
                Tap(game,AtelierAction.Observation,106);Check(game.atelier.travelling,"travel-mode mesh input failed");
                game.transform.SetPositionAndRotation(position,rotation);game.transform.localScale=scale;game.CompleteAssembly();
                Tap(game,AtelierAction.Cutaway,107);Check(game.engine.principle,"cutaway mesh input failed");
                var view=game.GetComponentInChildren<SwanCutawaySteam>(true);Check(view&&!game.engine.cylinderSteam.enabled,"legacy volume driver still active");
                view.Refresh();
                Check(view.volumes.Count(r=>r)>4,"cutaway chambers missing");
                foreach(var renderer in view.volumes.Where(r=>r)) {
                    var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
                    Check(mesh&&mesh.vertexCount>100,"volume still uses a box proxy");
                    Check(renderer.sharedMaterial.shader.name=="Standard"&&renderer.sharedMaterial.color.a<.25f,"steam is not exportable transparent geometry");
                }
                Check(game.thermal.flows.Any(f=>f.arrows.Any(a=>a&&a.gameObject.activeInHierarchy)),"idle cutaway has no visible flow routes");
                game.SetThrottle(.5f);game.TickSimulation(.1f);view.Refresh();
                Tap(game,AtelierAction.Cutaway,108);view.Refresh();
                Check(!game.engine.principle&&!game.engine.principleParts.activeSelf,"cutaway did not close");
                Check(game.thermal.flows.All(f=>f.arrows.All(a=>!a||!a.gameObject.activeInHierarchy)),"flow indicators remained visible outside cutaway");
                Debug.Log("ATELIER_SWAN_CONSOLE_CHECKS_PASSED mesh-targets,small-scale-targets,stationary-gaze,hand-lift,forward-push,rotation,brake,neutral,reverse,lights,mode,cutaway-rounded-volumes,idle-routes");
            } finally {
                game.ResetSpatialGestures();game.transform.SetPositionAndRotation(position,rotation);game.transform.localScale=scale;
                if(game.presentation.lightOn!=lights)game.presentation.ToggleHeadlight();game.CompleteAssembly();
            }
        }
    }
}
