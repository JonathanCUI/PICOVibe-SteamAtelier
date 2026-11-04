using System.Collections;
using SwanJourney;
using UnityEngine;

public sealed partial class SteamXrTrial
{
    IEnumerator IntegrationShot(string name)
    {
        yield return new WaitForSecondsRealtime(.6f);
        Debug.Log("XR_INTEGRATION_SHOT "+name);
        yield return new WaitForSecondsRealtime(1.7f);
    }
    IEnumerator ReviewVisualIntegration()
    {
        CancelHeld();replay=true;detailReview=true;
        var integration=journey.GetComponent<SwanValleyIntegration>();
        var seat=journey.GetComponent<SwanPartSeatFeedback>();
        var cards=journey.GetComponent<StudioPartCards>();
        if(cards && cards.visible)cards.Toggle();
        var train=journey.engine.locomotive;
        float side=train.InverseTransformPoint(view.transform.position).z>=0?1:-1;
        PlaceView(train,new Vector3(2.5f,3.2f,7.5f*side),new Vector3(0,1.35f,0));
        Debug.Log("XR_INTEGRATION_READY");yield return new WaitForSecondsRealtime(2);
        var first=journey.Current;
        int before=seat.Plays;
        journey.InstallCurrent();
        yield return new WaitForSecondsRealtime(.12f);
        bool trigger=seat.Active && seat.Plays==before+1;
        yield return new WaitForSecondsRealtime(.6f);
        bool restored=!seat.Active;
        yield return CompletionWindow("part-idle",4);
        yield return CompletionWindow("part-feedback",4,null,age=>{if(!seat.Active)seat.Begin(first);});
        seat.Restore();
        while(!journey.Complete && journey.InstallCurrent())yield return null;
        while(journey.CompletionActive)yield return null;
        bool finalClean=!seat.Active;
        integration.SetReflectionReview(true);yield return IntegrationShot("workshop-original");
        yield return CompletionWindow("workshop-original",5);
        integration.SetReflectionReview(false);yield return IntegrationShot("workshop-soft-reflection");
        yield return CompletionWindow("workshop-soft-reflection",5);
        journey.Act(JourneyAction.Drive);
        double deadline=Time.realtimeSinceStartupAsDouble+90;
        while((!journey.inSandbox || journey.SwanSceneLoading || journey.SwanTransitionActive) && Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(!journey.inSandbox || journey.SwanSceneLoading || journey.SwanTransitionActive)
        {Debug.LogError("XR_INTEGRATION_FAIL valley load");yield break;}
        var sky=integration.sky;sky.automatic=false;sky.SetTime(.46f);
        journey.SwanSetControl(JourneyAction.Brake,1);journey.SwanSetControl(JourneyAction.Throttle,0);
        journey.speed=0;journey.routeDistance=5;journey.SendMessage("PlaceTrain");
        var p=journey.WorldPointToScene(journey.trainAnchor.position);
        PlaceView(journey.spatialRoot,p+new Vector3(2.5f,3.7f,5.2f),p+Vector3.up*.6f);
        integration.ReviewDisabled=true;yield return IntegrationShot("contact-before");
        integration.ReviewDisabled=false;yield return IntegrationShot("contact-after");
        var routePath=new JourneyPath();
        PlaceView(journey.spatialRoot,new Vector3(1,20.5f,24),new Vector3(0,7,0));
        for(int pass=0;pass<2;pass++)
        {
            integration.ReviewDisabled=true;
            yield return CompletionWindow("valley-control-"+pass,6,null,age=>{journey.routeDistance=4+age;journey.SendMessage("PlaceTrain");});
            integration.ReviewDisabled=false;
            yield return CompletionWindow("valley-integrated-"+pass,6,null,age=>{journey.routeDistance=4+age;journey.SendMessage("PlaceTrain");});
        }
        // Look along the open rail into each portal, not through the adjacent rock face.
        for(int portal=0;portal<2;portal++)
        {
            float route=portal==0?19.5f:25.5f;
            journey.routeDistance=route;journey.SendMessage("PlaceTrain");
            var eye=routePath.At(portal==0?16:29)+Vector3.up*.9f;
            PlaceView(journey.spatialRoot,eye,routePath.At(portal==0?20:25)+Vector3.up*.5f);
            integration.ReviewDisabled=true;yield return IntegrationShot("portal-"+portal+"-before");
            integration.ReviewDisabled=false;yield return IntegrationShot("portal-"+portal+"-after");
            yield return CompletionWindow("tunnel-moving-"+portal,8,null,age=>
            {journey.routeDistance=(portal==0?17:24)+age*.6f;journey.SendMessage("PlaceTrain");});
        }
        float dark=1,light=0;
        foreach(float route in new[]{16f,17f,18f,19f,20f,21f,22f,23f,24f,25f,26f,27f,28f})
        {
            var point=routePath.At(route)+Vector3.up*.5f;
            var uv=Vector3.Scale(point-integration.fieldMin,new Vector3(1/integration.fieldSize.x,1/integration.fieldSize.y,1/integration.fieldSize.z));
            float exposure=uv.x<0||uv.x>1||uv.y<0||uv.y>1||uv.z<0||uv.z>1?1:integration.tunnelField.GetPixelBilinear(uv.x,uv.y,uv.z).r;
            dark=Mathf.Min(dark,exposure);light=Mathf.Max(light,exposure);
            Debug.Log($"XR_INTEGRATION_EXPOSURE route={route} visibility={exposure:F3} position={point}");
        }
        bool spatial=dark<.35f && light>.95f;
        sky.SetTime(.9f);journey.routeDistance=5;journey.SendMessage("PlaceTrain");p=journey.WorldPointToScene(journey.trainAnchor.position);
        PlaceView(journey.spatialRoot,p+new Vector3(2.5f,3.7f,5.2f),p+Vector3.up*.6f);
        yield return new WaitForSecondsRealtime(2);yield return IntegrationShot("night-lights");
        yield return CompletionWindow("valley-night",6);
        bool visible=integration.ContactVisible;
        journey.SetMode(false);
        deadline=Time.realtimeSinceStartupAsDouble+30;
        while(journey.SwanTransitionActive && Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        yield return new WaitForSecondsRealtime(2);
        bool leave=!integration.ContactVisible && Shader.GetGlobalVector("_SwanTunnelSettings").x==0;
        journey.ResetJourney();yield return null;bool reset=!seat.Active;
        FrameTrain();
        Debug.Log($"XR_INTEGRATION_CHECK trigger={trigger} restored={restored} finalClean={finalClean} contact={visible} leave={leave} reset={reset} spatial={spatial} contactTris={integration.ContactTriangles} seatMaterials={seat.CachedMaterials}");
        if(!trigger || !restored || !finalClean || !visible || !leave || !reset || !spatial)Debug.LogError("XR_INTEGRATION_FAIL lifecycle");
        sky.automatic=true;replay=false;detailReview=false;
        Debug.Log("XR_INTEGRATION_DONE");
    }
}
