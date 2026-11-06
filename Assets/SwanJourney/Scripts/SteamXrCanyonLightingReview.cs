using System.Collections;
using SwanJourney;
using UnityEngine;

public sealed partial class SteamXrTrial
{
    IEnumerator CanyonShot(string name)
    {
        yield return new WaitForSecondsRealtime(.7f);
        Debug.Log("XR_CANYON_SHOT "+name);
        yield return new WaitForSecondsRealtime(1.5f);
    }
    IEnumerator ReviewCanyonLighting()
    {
        CancelHeld();replay=true;detailReview=true;
        var cards=journey.GetComponent<StudioPartCards>();if(cards && cards.visible)cards.Toggle();
        while(!journey.Complete && journey.InstallCurrent())yield return null;
        while(journey.CompletionActive)yield return null;
        Debug.Log("XR_CANYON_READY");
        journey.Act(JourneyAction.Drive);
        double deadline=Time.realtimeSinceStartupAsDouble+90;
        while((!journey.inSandbox || journey.SwanSceneLoading || journey.SwanTransitionActive) && Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        var bake=Object.FindObjectOfType<SwanCanyonBakedLighting>();
        if(!bake || !bake.Applied || journey.SwanTransitionActive){Debug.LogError("XR_CANYON_FAIL load");yield break;}
        var sky=journey.GetComponent<SwanValleyIntegration>().sky;sky.automatic=false;sky.SetTime(.46f);
        journey.SwanSetControl(JourneyAction.Brake,1);journey.SwanSetControl(JourneyAction.Throttle,0);
        journey.speed=0;journey.routeDistance=5;journey.SendMessage("PlaceTrain");
        PlaceView(journey.spatialRoot,new Vector3(1,20.5f,24),new Vector3(0,7,0));
        bake.SetReview(false);yield return CanyonShot("day-before");
        bake.SetReview(true);yield return CanyonShot("day-after");
        for(int pass=0;pass<2;pass++)
        {
            bake.SetReview(false);
            yield return CompletionWindow("canyon-original-"+pass,7,null,age=>{journey.routeDistance=4+age;journey.SendMessage("PlaceTrain");});
            bake.SetReview(true);
            yield return CompletionWindow("canyon-baked-"+pass,7,null,age=>{journey.routeDistance=4+age;journey.SendMessage("PlaceTrain");});
        }
        PlaceView(bake.transform,new Vector3(.48f,.30f,-.52f),new Vector3(-.04f,.075f,-.02f));
        bake.SetReview(false);yield return CanyonShot("rail-foot-before");
        bake.SetReview(true);yield return CanyonShot("rail-foot-after");
        var path=new JourneyPath();
        PlaceView(journey.spatialRoot,path.At(16)+Vector3.up*1.2f,path.At(20)+Vector3.up*.5f);
        bake.SetReview(false);yield return CanyonShot("portal-before");
        bake.SetReview(true);yield return CanyonShot("portal-after");
        sky.SetTime(.9f);
        PlaceView(journey.spatialRoot,new Vector3(1,20.5f,24),new Vector3(0,7,0));
        yield return new WaitForSecondsRealtime(2);
        bake.SetReview(false);yield return CanyonShot("night-before");
        yield return CompletionWindow("canyon-night-original",7);
        bake.SetReview(true);yield return CanyonShot("night-after");
        yield return CompletionWindow("canyon-night-baked",7);
        // Verify local UV binding survives a tabletop pose change and a complete unload/reload.
        var tabletop=journey.spatialRoot;var position=tabletop.position;var rotation=tabletop.rotation;var scale=tabletop.localScale;
        tabletop.position+=Vector3.right*.12f;tabletop.rotation=Quaternion.AngleAxis(15,Vector3.up)*rotation;tabletop.localScale=scale*.9f;
        yield return CanyonShot("night-moved");
        tabletop.SetPositionAndRotation(position,rotation);tabletop.localScale=scale;
        journey.SetMode(false);deadline=Time.realtimeSinceStartupAsDouble+40;
        while((journey.SwanTransitionActive || journey.SwanSceneLoading) && Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        yield return new WaitForSecondsRealtime(2);
        journey.Act(JourneyAction.Drive);deadline=Time.realtimeSinceStartupAsDouble+90;
        while((!journey.inSandbox || journey.SwanSceneLoading || journey.SwanTransitionActive) && Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        var reload=Object.FindObjectOfType<SwanCanyonBakedLighting>();
        bool restored=reload && reload.Applied && reload.surfaces.Length==19 && !journey.SwanTransitionActive;
        Debug.Log("XR_CANYON_CHECK reload="+restored+" receivers="+(reload?reload.surfaces.Length:0));
        if(!restored){Debug.LogError("XR_CANYON_FAIL reload");yield break;}
        sky.SetTime(.46f);sky.automatic=true;replay=false;detailReview=false;
        Debug.Log("XR_CANYON_DONE");
    }
}
