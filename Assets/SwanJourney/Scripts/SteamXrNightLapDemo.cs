using System.Collections;
using SwanAtelier;
using SwanJourney;
using UnityEngine;

public sealed partial class SteamXrTrial
{
    // Explicit recording launch flag only; normal player startup is unchanged.
    IEnumerator RecordNightLap()
    {
        CancelHeld();replay=true;detailReview=true;
        var hud=FindObjectOfType<SwanFrameRateHud>(true);
        if(hud){hud.enabled=false;hud.VisualTransform.gameObject.SetActive(false);}
        var cards=journey.GetComponent<StudioPartCards>();
        if(cards && cards.visible)cards.Toggle();
        while(!journey.Complete && journey.InstallCurrent())yield return null;
        while(journey.CompletionActive)yield return null;
        if(!journey.Complete){Debug.LogError("XR_NIGHT_LAP_FAIL incomplete train");yield break;}
        journey.Act(JourneyAction.Drive);
        double deadline=Time.realtimeSinceStartupAsDouble+90;
        while((!journey.inSandbox || journey.SwanSceneLoading || journey.SwanTransitionActive) && Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(!journey.inSandbox || journey.SwanSceneLoading || journey.SwanTransitionActive)
        {Debug.LogError("XR_NIGHT_LAP_FAIL canyon load");yield break;}
        var sky=journey.GetComponent<SwanValleyIntegration>().sky;
        sky.automatic=false;sky.SetTime(.93f);
        journey.SwanSetControl(JourneyAction.Brake,1);journey.SwanSetControl(JourneyAction.Throttle,0);
        journey.speed=0;journey.routeDistance=0;journey.SendMessage("PlaceTrain");
        PlaceView(journey.spatialRoot,new Vector3(1,18.0f,21.5f),new Vector3(0,6.5f,0));
        yield return new WaitForSecondsRealtime(5);
        Debug.Log("XR_NIGHT_LAP_READY");
        yield return new WaitForSecondsRealtime(3);
        journey.SwanSetControl(JourneyAction.Brake,0);journey.SwanSetControl(JourneyAction.Throttle,.68f);
        var path=new JourneyPath();float previous=journey.routeDistance,travelled=0;
        double start=Time.realtimeSinceStartupAsDouble;deadline=start+120;
        Debug.Log("XR_NIGHT_LAP_BEGIN length="+path.Length);
        while(travelled<path.Length && Time.realtimeSinceStartupAsDouble<deadline)
        {
            yield return null;
            travelled+=Mathf.Repeat(journey.routeDistance-previous,path.Length);
            previous=journey.routeDistance;
        }
        if(travelled<path.Length){Debug.LogError("XR_NIGHT_LAP_FAIL incomplete lap distance="+travelled);yield break;}
        Debug.Log($"XR_NIGHT_LAP_COMPLETE distance={travelled:F3} length={path.Length:F3} seconds={Time.realtimeSinceStartupAsDouble-start:F2}");
        yield return new WaitForSecondsRealtime(2);
        replay=false;detailReview=false;Debug.Log("XR_NIGHT_LAP_DONE");
    }
}
