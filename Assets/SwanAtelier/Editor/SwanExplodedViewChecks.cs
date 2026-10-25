using System;
using System.Linq;
using UnityEngine;
using ByteDance.PICO.SpatialAdapter.Component;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace SwanAtelier.Editor
{
    public static class SwanExplodedViewChecks
    {
        static void Check(bool value,string message) { if(!value)throw new Exception("Exploded view: "+message); }
        static void Tap(SwanAtelierController game,GameObject target,double time)
        {
            var p=target.transform.position;
            game.QueueGestureSample(new SwanGestureSample{id=9101,phase=TouchPhase.Began,pinch=true,target=target,handPosition=p,hitPosition=p});
            game.ProcessGestureFrame(time,.016f);
            game.QueueGestureSample(new SwanGestureSample{id=9101,phase=TouchPhase.Ended,pinch=true,target=target,handPosition=p,hitPosition=p});
            game.ProcessGestureFrame(time+.08,.016f);
        }
        public static void Run(SwanAtelierController game)
        {
            try {
                game.ResetSpatialGestures();game.CompleteAssembly();
                var buttons=game.GetComponentsInChildren<SwanAtelierTarget>();
                var explode=buttons.Single(b=>b.explodeAll);
                var restore=buttons.Single(b=>!b.explodeAll&&!b.moveStage);
                Check(explode.transform.localPosition.x<restore.transform.localPosition.x,"button is not to the left");
                Tap(game,explode.gameObject,50);
                Check(game.PartsAnimating,"explosion has no animation");
                Vector3 initial=game.assembly.looseObjects[0].transform.position;
                Check(Vector3.Distance(initial,game.assembly.SlotPosition(0))<.001f,"explosion teleported on start");
                game.TickPartMotionFrame(1);
                Check(game.PartsAnimating,"one long resource-upload frame skipped the whole animation");
                game.TickPartMotions(.14f);
                Check(game.PartsAnimating&&Vector3.Distance(initial,game.assembly.looseObjects[0].transform.position)>.01f,"explosion did not move at halfway");
                game.TickPartMotions(.15f);
                Check(game.assembly.InstalledCount==0&&game.assembly.looseObjects.Count(g=>g)==45,"button did not expose all 45 groups");
                Check(!game.PlacementGhost&&!game.engine.running,"explosion left ghost or mechanism active");
                var ids=game.assembly.looseObjects.Select(g=>g.GetInstanceID()).ToArray();
                for(int i=0;i<45;i++) {
                    var part=game.assembly.pieces[i];var loose=game.assembly.looseObjects[i];
                    float distance=Vector3.Distance(game.engine.locomotive.InverseTransformPoint(loose.transform.position),part.centre);
                    Check(distance>=1.189f&&distance<=2.521f,"unbounded displacement: "+i+" distance="+distance);
                    Check(game.assembly.pieceSlots[i]==-1&&game.assembly.slotOccupants[i]==-1,"occupied slot remains: "+i);
                    Check(loose.GetComponent<SpatialAdapterHoverEffect>()&&loose.GetComponentInChildren<Collider>(),"loose group lacks interaction: "+i);
                    foreach(var member in part.members)Check(!game.assembly.IsPartVisible(member),"installed copy still visible: "+i);
                }
                SwanPlacementHintChecks.Capture(game.previewCamera,"Builds/Evidence/exploded-view-editor.png");
                Tap(game,explode.gameObject,51);
                game.TickPartMotions(.29f);
                Check(ids.SequenceEqual(game.assembly.looseObjects.Select(g=>g.GetInstanceID())),"repeated explosion created duplicate groups");
                int count=game.QuickInstallCount;
                var target=game.assembly.looseObjects[0].GetComponentInChildren<MeshRenderer>().gameObject;
                Tap(game,target,52);Tap(game,target,52.2);
                game.TickPartMotions(.23f);
                Check(game.assembly.InstalledCount==1&&game.QuickInstallCount==count+1,"exploded piece cannot double-pinch install");
                Tap(game,restore.gameObject,53);
                Check(game.PartsAnimating&&!game.assembly.Complete,"restore teleported instead of animating");
                game.TickPartMotions(.14f);
                Check(game.PartsAnimating&&game.assembly.looseObjects.Any(g=>g),"restore removed its visible parts too soon");
                game.TickPartMotions(.15f);
                Check(game.assembly.Complete&&game.assembly.looseObjects.All(g=>!g)&&!game.PlacementGhost,"restore left loose groups");
                var pair=Enumerable.Range(0,45).GroupBy(i=>game.assembly.pieces[i].fitKey).First(g=>g.Count()>1);
                int a=pair.First(),b=pair.Skip(1).First();
                var swapped=game.TakePiece(a);game.TakePiece(b);
                swapped.transform.position=game.assembly.SlotPosition(b);game.ReleasePiece(a);
                Check(game.assembly.pieceSlots[a]==b,"swap precondition failed");
                Tap(game,explode.gameObject,54);
                game.TickPartMotions(.29f);
                Vector3 centre=game.assembly.pieces.Aggregate(Vector3.zero,(sum,p)=>sum+p.centre)/45;
                foreach(int i in new[]{a,b}) {
                    var piece=game.assembly.pieces[i];
                    Vector3 expected=game.engine.locomotive.TransformPoint(piece.centre+SwanAtelierController.ExplosionOffset(piece,centre));
                    Check(Vector3.Distance(expected,game.assembly.looseObjects[i].transform.position)<.001f,"swapped piece lost its canonical display lane");
                }
                // Rapidly reversing a bulk action starts from its current pose.
                game.CompleteAssemblyAnimated();game.TickPartMotions(.08f);
                initial=game.assembly.looseObjects[0].transform.position;
                game.ExplodeAllPieces();
                Check(Vector3.Distance(initial,game.assembly.looseObjects[0].transform.position)<.001f,"reversing action teleported");
                game.TickPartMotions(.29f);
                Check(game.assembly.InstalledCount==0&&!game.PartsAnimating,"old restore callback fired after reversal");
                Debug.Log("ATELIER_SWAN_EXPLODED_CHECKS_PASSED actual-button,45-groups,bounded-travel,hover,empty-slots,no-installed-duplicates,repeated-reuse,double-pinch,restore");
            } finally { game.ResetSpatialGestures();game.CompleteAssembly(); }
        }
    }
}
