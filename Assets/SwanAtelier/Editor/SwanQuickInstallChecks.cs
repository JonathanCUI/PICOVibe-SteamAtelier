using System;
using System.Linq;
using UnityEngine;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace SwanAtelier.Editor
{
    public static class SwanQuickInstallChecks
    {
        static void Check(bool value, string message) { if (!value) throw new Exception("Quick install: " + message); }
        static void Send(SwanAtelierController game, long id, GameObject hit, TouchPhase phase, Vector3 p, double now)
        {
            // A terminal sample may reset its interaction kind. The last down
            // sample still determines whether this was a pinch.
            game.QueueGestureSample(new SwanGestureSample { id=id, target=hit, phase=phase, pinch=phase!=TouchPhase.Ended, handPosition=p, hitPosition=p });
            game.ProcessGestureFrame(now, .016f);
        }
        static GameObject Loose(SwanAtelierController game, int piece)
        {
            var loose=game.TakePiece(piece); loose.transform.position+=Vector3.up*.4f; game.ReleasePiece(piece,true); return loose;
        }
        static void Tap(SwanAtelierController game, GameObject loose, double now, long id=8101, float duration=.08f)
        {
            var hit=loose.GetComponentInChildren<MeshRenderer>().gameObject; var p=loose.transform.position;
            Send(game,id,hit,TouchPhase.Began,p,now); Send(game,id,hit,TouchPhase.Ended,p,now+duration);
        }
        public static void Run(SwanAtelierController game)
        {
            Vector3 position=game.transform.position,scale=game.transform.localScale; Quaternion rotation=game.transform.rotation;
            try {
                game.ResetSpatialGestures(); game.CompleteAssembly(); int count=game.QuickInstallCount;
                var loose=Loose(game,0); Tap(game,loose,30);
                Check(!game.assembly.pieces[0].installed,"single tap installed part");
                Tap(game,loose,30.2,8102);
                Check(game.PartsAnimating&&!game.assembly.pieces[0].installed,"quick install teleported instead of starting a flight");
                Vector3 start=loose.transform.position,destination=game.assembly.SlotPosition(0);
                game.TickPartMotions(.11f);
                Check(Vector3.Distance(start,loose.transform.position)>.05f&&Vector3.Distance(destination,loose.transform.position)>.05f,"flight has no visible intermediate position");
                game.TickPartMotions(.12f);
                Check(game.assembly.Complete && game.QuickInstallCount==count+1 && !game.PlacementGhost,"double pinch failed");

                loose=Loose(game,0); Tap(game,loose,31); Tap(game,loose,31.8);
                Check(!game.assembly.pieces[0].installed,"slow taps counted as double");
                game.ResetSpatialGestures(); Tap(game,loose,32,duration:.5f); Tap(game,loose,32.6);
                Check(!game.assembly.pieces[0].installed,"hold counted as short tap");

                game.ResetSpatialGestures(); var hit=loose.GetComponentInChildren<MeshRenderer>().gameObject; var p=loose.transform.position;
                Send(game,8101,hit,TouchPhase.Began,p,33);
                Send(game,8101,hit,TouchPhase.Moved,p+Vector3.right*.06f,33.06);
                Send(game,8101,hit,TouchPhase.Ended,p,33.1); Tap(game,loose,33.25);
                Check(!game.assembly.pieces[0].installed,"out-and-back drag counted as tap");

                game.ResetSpatialGestures(); var other=Loose(game,1); Tap(game,loose,34); Tap(game,other,34.2);
                Check(!game.assembly.pieces[0].installed&&!game.assembly.pieces[1].installed,"different targets formed a double tap");
                game.ResetSpatialGestures(); Tap(game,loose,35);
                Send(game,8101,hit,TouchPhase.Began,p,35.2); Send(game,8101,hit,TouchPhase.Canceled,p,35.25); Tap(game,loose,35.4);
                Check(!game.assembly.pieces[0].installed,"canceled input armed a double tap");

                game.ResetSpatialGestures(); Tap(game,loose,36); game.ResetSpatialGestures(); Tap(game,loose,36.2);
                Check(!game.assembly.pieces[0].installed,"pause retained first tap");
                game.ResetSpatialGestures(); Tap(game,loose,37);
                Send(game,8101,hit,TouchPhase.Began,p,37.2);
                Send(game,8102,hit,TouchPhase.Began,p+Vector3.right*.3f,37.22);
                Check(game.TwoHandActive&&!game.assembly.pieces[0].installed,"second hand triggered install instead of scene manipulation");
                game.ResetSpatialGestures(); Tap(game,loose,37.4);
                Check(!game.assembly.pieces[0].installed,"two-hand gesture retained first tap");

                // The actual destination can be a different empty, compatible slot.
                game.ResetSpatialGestures(); game.CompleteAssembly();
                var pair=Enumerable.Range(0,45).GroupBy(i=>game.assembly.pieces[i].fitKey).FirstOrDefault(g=>g.Count()>1);
                if(pair!=null) {
                    int a=pair.First(),b=pair.Skip(1).First(); loose=Loose(game,a); Loose(game,b);
                    loose.transform.position=game.assembly.SlotPosition(b)+(game.assembly.SlotPosition(b)-game.assembly.SlotPosition(a)).normalized*.4f;
                    Tap(game,loose,38); Tap(game,loose,38.2);
                    game.TickPartMotions(.23f);
                    Check(game.assembly.pieceSlots[a]==b,"quick install picked incompatible/different destination");
                }
                // Stage transformation must not change snapping coordinates.
                game.ResetSpatialGestures(); game.CompleteAssembly();
                game.transform.SetPositionAndRotation(position+Vector3.right*.3f,Quaternion.Euler(0,95,0)); game.transform.localScale=Vector3.one*.26f;
                loose=Loose(game,0); Tap(game,loose,39); Tap(game,loose,39.2);
                game.TickPartMotions(.23f);
                Check(game.assembly.Complete,"scaled/rotated stage quick install failed");
                loose=Loose(game,0);Tap(game,loose,40);Tap(game,loose,40.2);game.TickPartMotions(.05f);
                start=loose.transform.position;game.ResetSpatialGestures();game.TickPartMotions(1);
                Check(!game.PartsAnimating&&!game.assembly.pieces[0].installed&&Vector3.Distance(start,loose.transform.position)<.001f,"pause did not stop in-flight work safely");
                game.CompleteAssembly();
                if(pair!=null) {
                    int a=pair.First(),b=pair.Skip(1).First();var first=Loose(game,a);var second=Loose(game,b);
                    Tap(game,first,41);Tap(game,first,41.2);Tap(game,second,41.5);Tap(game,second,41.7);
                    game.TickPartMotions(.23f);
                    Check(game.assembly.pieces[a].installed&&game.assembly.pieces[b].installed&&game.assembly.pieceSlots[a]!=game.assembly.pieceSlots[b],"simultaneous flights collided in one slot");
                }
                Debug.Log("ATELIER_SWAN_QUICK_INSTALL_CHECKS_PASSED double-pinch,single,slow,long,drag,different-target,cancel,pause,two-hand-priority,compatible-slot,stage-transform");
            } finally {
                game.ResetSpatialGestures(); game.transform.SetPositionAndRotation(position,rotation); game.transform.localScale=scale; game.CompleteAssembly();
            }
        }
    }
}
