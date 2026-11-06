using System;
using System.Collections;
using System.Linq;
using SwanJourney;
using TMPro;
using UnityEngine;
using Phase = UnityEngine.InputSystem.TouchPhase;

public sealed partial class SteamXrTrial
{
    IEnumerator ReviewConsoleTeaching()
    {
        CancelHeld();replay=true;detailReview=true;
        var teaching=journey.GetComponent<SwanConsoleTeaching>();
        var c=journey.GetComponent<StudioSpatialConsoleV18>();var cards=journey.GetComponent<StudioPartCards>();
        ConsoleCheck(teaching && c && cards,"references");
        Debug.Log("XR_CONSOLE_READY");
        FrameTrain();yield return new WaitForSecondsRealtime(1);
        yield return ConsoleShot("assembly-overview");
        PlaceView(c.console,new Vector3(.2f,4.8f,6.2f),new Vector3(0,.25f,.05f));
        yield return ConsoleShot("console-start");
        teaching.Refresh();ConsoleCheck(teaching.Phase=="pick-up","initial-guidance");
        foreach(var action in new[]{JourneyAction.Introduction,JourneyAction.Cutaway,JourneyAction.GuidePrevious,
            JourneyAction.GuideNext,JourneyAction.Verify,JourneyAction.FinishAll,JourneyAction.Drive})
            ConsoleHit(action);
        yield return CompletionWindow("console-idle",7);
        bool visible=cards.visible;
        yield return ConsoleTap(JourneyAction.Introduction);yield return new WaitForSecondsRealtime(1.5f);
        ConsoleCheck(cards.visible!=visible,"introduction-toggle");
        yield return ConsoleTap(JourneyAction.Introduction);yield return new WaitForSecondsRealtime(1.5f);
        ConsoleCheck(cards.visible==visible,"introduction-restore");
        int page=cards.SelectedIndex;
        yield return ConsoleTap(JourneyAction.GuideNext);yield return new WaitForSecondsRealtime(1.1f);
        ConsoleCheck(cards.SelectedIndex==(page+1)%17,"guide-next");
        yield return ConsoleTap(JourneyAction.GuidePrevious);yield return new WaitForSecondsRealtime(1.1f);
        ConsoleCheck(cards.SelectedIndex==page,"guide-previous");
        int mode=journey.inspectionMode;
        yield return ConsoleTap(JourneyAction.Cutaway);yield return null;
        ConsoleCheck(journey.inspectionMode==(mode+1)%3,"inspection-selector");
        journey.inspectionMode=1;journey.cutaway=true;journey.ApplyVisibility();
        var first=journey.Current;ConsoleCheck(journey.SwanBeginGrab(first),"grab-start");
        journey.MoveGrab(first.preview.transform.position);teaching.Refresh();
        ConsoleCheck(teaching.Phase=="holding","holding-guidance");
        yield return ConsoleShot("holding");
        journey.MoveGrab(journey.trainAnchor.TransformPoint(first.previewCentre));teaching.Refresh();
        ConsoleCheck(journey.SnapReady && teaching.Phase=="aligned","aligned-guidance");
        yield return ConsoleShot("aligned");
        ConsoleCheck(journey.SwanEndGrab() && first.installed,"manual-install");
        teaching.Refresh();ConsoleCheck(teaching.Phase=="seated","installed-guidance");
        yield return ConsoleShot("installed");
        yield return ConsoleTap(JourneyAction.Verify);yield return new WaitForSecondsRealtime(3);
        teaching.Refresh();ConsoleCheck(journey.running && teaching.Phase=="observing","verify-guidance");
        yield return ConsoleShot("verifying");yield return CompletionWindow("console-verification",7);
        yield return ConsoleTap(JourneyAction.Verify);yield return null;ConsoleCheck(!journey.running,"verify-off");
        yield return ConsoleTap(JourneyAction.FinishAll);yield return null;
        ConsoleCheck(journey.AutoAssembling,"automatic-button");teaching.Refresh();
        ConsoleCheck(teaching.Phase=="automatic","automatic-guidance");
        double timeout=Time.realtimeSinceStartupAsDouble+40;
        while((journey.AutoAssembling || journey.CompletionActive) && Time.realtimeSinceStartupAsDouble<timeout)yield return null;
        teaching.Refresh();ConsoleCheck(journey.Complete && teaching.Phase=="complete","completed-guidance");
        yield return ConsoleShot("completed");yield return CompletionWindow("console-completed",7);
        yield return ConsoleTap(JourneyAction.Drive);
        timeout=Time.realtimeSinceStartupAsDouble+70;
        while((!journey.inSandbox || journey.SwanSceneLoading || journey.SwanTransitionActive) && Time.realtimeSinceStartupAsDouble<timeout)yield return null;
        ConsoleCheck(journey.inSandbox && !journey.SwanTransitionActive,"departure");
        PlaceView(journey.consoleRoot.transform,new Vector3(0,5.2f,7),new Vector3(0,.7f,0));
        teaching.Refresh();ConsoleCheck(!teaching.driving.driveMessage.gameObject.activeSelf && string.IsNullOrEmpty(teaching.driving.driveMessage.text),"driving-caption-hidden");
        yield return ConsoleShot("driving-ready");yield return CompletionWindow("console-valley",7);
        journey.SwanSetControl(JourneyAction.Brake,1);teaching.Refresh();ConsoleCheck(teaching.Phase=="braking","brake-guidance");
        yield return ConsoleShot("braking");
        journey.SetMode(false);timeout=Time.realtimeSinceStartupAsDouble+40;
        while((journey.SwanSceneLoading || journey.SwanTransitionActive) && Time.realtimeSinceStartupAsDouble<timeout)yield return null;
        PlaceView(c.console,new Vector3(.2f,4.8f,6.2f),new Vector3(0,.25f,.05f));
        yield return new WaitForSecondsRealtime(.5f);
        // Protected reset remains a two-action operation.
        journey.SwanAct(JourneyAction.Reset);ConsoleCheck(journey.Complete,"closed-reset-guard");
        journey.SwanAct(JourneyAction.ResetGuard);journey.SwanAct(JourneyAction.Reset);
        yield return new WaitForSecondsRealtime(3);teaching.Refresh();
        ConsoleCheck(!journey.Complete && journey.stage==0 && teaching.Phase=="pick-up","reset");
        yield return ConsoleShot("reset");
        replay=false;detailReview=false;
        Debug.Log("XR_CONSOLE_DONE buttons=true workshopCaptionHidden=true drivingGuidance=true reset=true");
    }
    IEnumerator ConsoleShot(string label)
    {
        yield return new WaitForSecondsRealtime(.5f);
        var message=journey.GetComponent<StudioSpatialConsoleV18>().message;
        ConsoleCheck(!message.gameObject.activeSelf && string.IsNullOrEmpty(message.text),"caption-hidden-"+label);
        Debug.Log("XR_CONSOLE_SHOT "+label);yield return new WaitForSecondsRealtime(1.2f);
    }
    RaycastHit ConsoleHit(JourneyAction action)
    {
        var c=journey.GetComponent<StudioSpatialConsoleV18>();
        var control=c.console.GetComponentsInChildren<JourneyControl>().Single(x=>x.action==action);
        var box=control.GetComponent<Collider>();var point=box.bounds.center;
        var hit=focus.Pick(new Ray(view.transform.position,(point-view.transform.position).normalized));
        var selected=hit.collider?hit.collider.GetComponentInParent<JourneyControl>():null;
        ConsoleCheck(selected && selected.action==action,"gaze-"+action);
        var touched=focus.TouchButton(point,.006f);
        var target=touched?touched.GetComponentInParent<JourneyControl>():null;
        ConsoleCheck(target && target.action==action,"touch-"+action);
        return hit;
    }
    IEnumerator ConsoleTap(JourneyAction action)
    {
        Physics.SyncTransforms();var hit=ConsoleHit(action);
        Send(0,Phase.Began,hit.point,hit.collider.gameObject,hit.point);yield return null;
        Send(0,Phase.Ended,hit.point,hit.collider.gameObject,hit.point);yield return new WaitForSecondsRealtime(.2f);
    }
    static void ConsoleCheck(bool ok,string name)
    {if(!ok){Debug.LogError("XR_CONSOLE_FAIL "+name);throw new InvalidOperationException("Console review: "+name);}Debug.Log("XR_CONSOLE_CHECK "+name+" passed=true");}
    static void ConsoleTextFits(TMP_Text t)
    {
        t.ForceMeshUpdate(true);
        ConsoleCheck(t.preferredWidth<=t.rectTransform.rect.width+.01f && t.preferredHeight<=t.rectTransform.rect.height+.01f,
            "text-fit "+t.name+" "+t.preferredWidth.ToString("F3")+"x"+t.preferredHeight.ToString("F3"));
        ConsoleCheck(t.textInfo.characterInfo.Take(t.textInfo.characterCount).All(ch=>
            ch.character<'\u4e00' || ch.character>'\u9fff' || t.font.HasCharacter(ch.character)),"chinese-glyphs");
    }
}
