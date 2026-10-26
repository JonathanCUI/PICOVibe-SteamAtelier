using System;
using ByteDance.PICO.XR;
using UnityEngine;
using UnityEngine.XR;

namespace SwanAtelier.Editor
{
    public static class SwanTabletopChecks
    {
        static void Check(bool ok,string message) {if(!ok)throw new Exception("Tabletop: "+message);}
        static PxrSpatialMeshInfo Table(Guid id,float height,PxrSemanticLabel label=PxrSemanticLabel.Table)
        {
            return new PxrSpatialMeshInfo {uuid=id,state=MeshChangeState.Added,position=new Vector3(0,height,0),rotation=Quaternion.identity,
                vertices=new[]{new Vector3(-1,0,-1),new Vector3(1,0,-1),new Vector3(1,0,1),new Vector3(-1,0,1)},
                indices=new ushort[]{0,2,1,0,3,2},labels=new[]{label,label,label,label}};
        }
        public static void Run(SwanAtelierController game)
        {
            Check(game.tabletop && game.tabletop.supportPoints.Length==45 && game.tabletop.supportSamplesPerFootprint==15,"Missing rail/console/tray support groups");
            Check(game.tabletop.transform==game.transform,"Workbench must be the only snapped root");
            var go=new GameObject("Tabletop verification fixture");
            try {
                var placement=go.AddComponent<SwanTabletopPlacement>();placement.enabled=false;
                placement.baseHeight=-.1f;placement.contactCentre=new Vector3(0,-.1f,0);
                placement.supportPoints=new[]{new Vector3(-.4f,-.1f,-.3f),new Vector3(.4f,-.1f,-.3f),new Vector3(.4f,-.1f,.3f),new Vector3(-.4f,-.1f,.3f),new Vector3(0,-.1f,0)};
                Guid id=Guid.NewGuid();var table=Table(id,.48f);
                var floor=Table(Guid.NewGuid(),0,PxrSemanticLabel.Floor);placement.Surface.Update(floor);
                Check(placement.Surface.TriangleCount==0,"Floor accepted as table");
                var tilted=table;tilted.rotation=Quaternion.Euler(25,0,0);placement.Surface.Update(tilted);
                Check(placement.Surface.TriangleCount==0,"Steep geometry accepted");
                var underside=table;underside.indices=new ushort[]{0,1,2,0,2,3};placement.Surface.Update(underside);
                Check(placement.Surface.TriangleCount==0,"Underside accepted as tabletop");
                placement.Surface.Update(table);
                Check(placement.Surface.TryHeight(new Vector3(.2f,.5f,.2f),.1f,out float h)&&Mathf.Abs(h-.48f)<.001f,"World mesh pose conversion");
                // A reconstruction seam must not reject a correctly placed base;
                // a real large gap and a distant table edge must still reject it.
                var scan=new SwanTableSurface();var left=Table(Guid.NewGuid(),.48f);var right=Table(Guid.NewGuid(),.48f);
                for(int i=0;i<4;i++){left.vertices[i].x=left.vertices[i].x<0?-1:-.03f;right.vertices[i].x=right.vertices[i].x<0?.03f:1;}
                scan.Update(left);scan.Update(right);
                var seam=new Vector3(0,.48f,0);
                Check(!scan.TryHeight(seam,.01f,out _)&&scan.TryHeight(seam,.01f,out _,.035f),"Small scan seam not tolerated");
                Check(scan.Fits(placement.supportPoints,new Vector3(0,.58f,0),Quaternion.identity,Vector3.one,.48f),"Small scan seam rejected a supported footprint");
                Check(!scan.TryHeight(new Vector3(1.08f,.48f,0),.03f,out _,SwanTableSurface.SupportTolerance),"Tolerance extended too far beyond a table edge");
                for(int i=0;i<4;i++){if(left.vertices[i].x>-.5f)left.vertices[i].x=-.12f;if(right.vertices[i].x<.5f)right.vertices[i].x=.12f;}
                scan.Update(left);scan.Update(right);
                Check(!scan.TryHeight(seam,.03f,out _,SwanTableSurface.SupportTolerance),"Large unsupported gap filled");
                Check(!scan.TryHeight(new Vector3(.5f,.6f,0),.03f,out _,SwanTableSurface.SupportTolerance),"Different height treated as coplanar support");
                var footprint=new Vector3[45];
                for(int group=0;group<3;group++)for(int x=0;x<5;x++)for(int z=0;z<3;z++)
                    footprint[group*15+x*3+z]=new Vector3(-.4f+x*.2f,-.1f,-.3f+z*.3f);
                footprint[0].x=footprint[1].x=-1.08f;
                Check(placement.Surface.Fits(footprint,new Vector3(0,.58f,0),Quaternion.identity,Vector3.one,.48f,15),"Two missing perimeter samples vetoed a supported footprint");
                for(int i=0;i<5;i++)footprint[i].x=-1.2f;
                Check(!placement.Surface.Fits(footprint,new Vector3(0,.58f,0),Quaternion.identity,Vector3.one,.48f,15),"One under-supported object accepted by total scene coverage");
                for(int x=0;x<5;x++)for(int z=0;z<3;z++)footprint[x*3+z]=new Vector3(-.4f+x*.2f,-.1f,-.3f+z*.3f);
                Check(!scan.Fits(footprint,new Vector3(0,.58f,0),Quaternion.identity,Vector3.one,.48f,15),"Central unsupported gap accepted by coverage ratio");
                placement.ApplyPose(new Vector3(0,.61f,0),Quaternion.Euler(0,30,0),Vector3.one,Vector3.zero,1);
                Check(placement.Attached&&Mathf.Abs(go.transform.position.y-.584f)<.001f,"Snap must use actual support bottom + clearance");
                placement.BeginManipulation();var at=go.transform.position;
                placement.ApplyPose(at+Vector3.up*.03f,go.transform.rotation,Vector3.one,Vector3.up*.03f,2);
                Check(placement.Attached&&Vector3.Distance(at,go.transform.position)<.001f,"Small upward motion must remain sticky");
                placement.ApplyPose(at+Vector3.up*.03f,go.transform.rotation,Vector3.one,Vector3.up*.03f,2.02f);
                Check(!placement.Attached&&go.transform.position.y-at.y<.01f,"Lift threshold or release continuity");
                placement.ApplyPose(at,Quaternion.identity,Vector3.one,Vector3.zero,3);
                Check(!placement.Attached,"Detached hand must not instantly reattach before clearance/release");
                placement.EndManipulation();placement.ApplyPose(at,Quaternion.identity,Vector3.one,Vector3.zero,4);
                Check(placement.Attached,"Releasing near table should allow placement");
                placement.BeginManipulation();at=go.transform.position;
                placement.ApplyPose(at+Vector3.right*.1f,Quaternion.Euler(0,20,0),Vector3.one*.8f,Vector3.right*.1f,5);
                Check(placement.Attached&&go.transform.position.x>.09f&&Mathf.Abs(go.transform.position.y-.564f)<.001f,"Slide/yaw/scale must keep base on table");
                at=go.transform.position;var rotation=go.transform.rotation;var scale=go.transform.localScale;
                placement.ApplyPose(at+Vector3.right*2,Quaternion.Euler(0,80,0),Vector3.one*3,Vector3.right*.1f,6);
                Check(placement.TooLarge&&Vector3.Distance(at,go.transform.position)<.001f&&go.transform.localScale==scale&&Quaternion.Angle(rotation,go.transform.rotation)<.01f,"Table edge must retain last supported pose");
                var removed=table;removed.state=MeshChangeState.Removed;placement.Surface.Update(removed);
                Check(placement.Surface.TriangleCount==0,"Removed mesh retained");
                placement.ApplyPose(at,rotation,scale,Vector3.zero,7);
                Check(placement.Attached&&Vector3.Distance(at,go.transform.position)<.001f,"Tracking loss must not drop or relocate workbench");
                placement.EndManipulation();
                placement.Surface.Update(table);
                var hands=new SwanTwoHandManipulation(go.transform);at=go.transform.position;
                Vector3 a=new Vector3(-.2f,1,0),b=new Vector3(.2f,1,0);
                Check(hands.Begin(a,b),"Two-hand tabletop grab failed");
                hands.Step(a+Vector3.up*.03f,b+Vector3.up*.03f,.02f);
                Check(placement.Attached&&Mathf.Abs(go.transform.position.y-at.y)<.001f,"Two-hand path bypassed sticky lift");
                hands.Step(a+Vector3.up*.06f,b+Vector3.up*.06f,.02f);
                Check(!placement.Attached&&go.transform.position.y-at.y<.01f,"Two-hand release threshold or continuity failed");
                hands.End();
                go.transform.position=new Vector3(0,1.5f,0);at=go.transform.position;
                hands.Begin(a,b);hands.Step(a+Vector3.up*.03f,b+Vector3.up*.03f,.02f);
                Check(go.transform.position.y>at.y+.01f&&!placement.Attached,"Airborne workbench acquired table resistance");
                hands.End();
            } finally {UnityEngine.Object.DestroyImmediate(go);}
            Debug.Log("SWAN_TABLE_CHECKS_PASS world mesh / floor and underside rejection / bottom alignment / sticky lift / hysteresis / yaw-scale / edge bounds / tracking loss");
        }
    }
}
