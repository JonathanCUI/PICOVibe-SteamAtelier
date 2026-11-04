using System;
using System.Collections;
using System.Collections.Generic;
using SwanJourney;
using UnityEngine;

public sealed partial class SteamXrTrial
{
    IEnumerator CompletionWindow(string label,float seconds,Action begin=null,Action<float> tick=null)
    {
        var samples=new List<float>(1024);
        var spikes=new List<string>();
        double start=Time.realtimeSinceStartupAsDouble,last=start;
        Debug.Log($"XR_CELEBRATION_WINDOW {label} seconds={seconds:F2}");
        begin?.Invoke();
        while(Time.realtimeSinceStartupAsDouble-start<seconds)
        {
            tick?.Invoke((float)(Time.realtimeSinceStartupAsDouble-start));
            yield return null;
            double now=Time.realtimeSinceStartupAsDouble;
            float ms=(float)((now-last)*1000);
            if(ms>25)spikes.Add($"{now-start:F3}s:{ms:F1}ms");
            samples.Add(ms);last=now;
        }
        samples.Sort();
        Debug.Log($"XR_CELEBRATION_RESULT {label} fps={samples.Count/(last-start):F2} frames={samples.Count} p95ms={samples[(samples.Count-1)*95/100]:F2} p99ms={samples[(samples.Count-1)*99/100]:F2} maxms={samples[samples.Count-1]:F2} focused={Application.isFocused}");
        if(spikes.Count>0)Debug.Log($"XR_CELEBRATION_SPIKES {label} "+string.Join(", ",spikes));
    }

    IEnumerator ReviewCompletion(bool demo)
    {
        CancelHeld();replay=true;detailReview=true;
        var fx=journey.GetComponent<SwanAssemblyCelebration>();
        var owner=journey.GetComponent<StudioAssemblyFinishV9>();
        var cards=journey.GetComponent<StudioPartCards>();
        if(!fx || !owner){Debug.LogError("XR_CELEBRATION_FAIL missing effect");yield break;}
        if(cards && cards.visible)cards.Toggle();
        int count=journey.modules.Length+journey.finishing.Length;
        for(int i=0;i<count-1;i++)
        {
            if(!journey.InstallCurrent()){Debug.LogError("XR_CELEBRATION_FAIL preinstall");yield break;}
            yield return null;
        }
        var train=journey.engine.locomotive;
        float side=train.InverseTransformPoint(view.transform.position).z>=0?1:-1;
        // Fixed close three-quarter view makes both curls and the scanning direction reviewable.
        PlaceView(train,new Vector3(3.8f,5.8f,11.5f*side),new Vector3(0,1.35f,0));
        yield return new WaitForSecondsRealtime(3);
        fx.Prepare();
        int initialPlays=fx.Plays;
        if(demo)
        {
            var hud=FindObjectOfType<SwanAtelier.SwanFrameRateHud>(true);
            if(hud){hud.enabled=false;hud.VisualTransform.gameObject.SetActive(false);}
            Debug.Log("XR_CELEBRATION_DEMO_READY");
            yield return new WaitForSecondsRealtime(1);
            journey.InstallCurrent();
            yield return new WaitForSecondsRealtime(5.5f);
            PlaceView(train,new Vector3(1f,3.8f,8.5f*side),new Vector3(.1f,1.4f,0));
            yield return new WaitForSecondsRealtime(1);
            owner.Begin(train);
            for(float age=0;age<SwanAssemblyCelebration.Duration;age+=Time.deltaTime)
            {owner.SetProgress(age/SwanAssemblyCelebration.Duration);yield return null;}
            owner.Restore();
            yield return new WaitForSecondsRealtime(1.5f);
            Debug.Log($"XR_CELEBRATION_DEMO_DONE clean={fx.IdleClean}");
            replay=false;detailReview=false;yield break;
        }
        yield return CompletionWindow("before-final-part",6);
        bool suppress=Requested("xr_completion_no_fx");
        fx.SuppressForReview=suppress;
        yield return CompletionWindow(suppress?"first-completion-disabled":"first-completion",4.15f,()=>journey.InstallCurrent());
        fx.SuppressForReview=false;
        if(!journey.Complete || fx.Plays!=initialPlays+1)
        {Debug.LogError("XR_CELEBRATION_FAIL final-part-trigger");yield break;}
        yield return new WaitForSecondsRealtime(.5f);
        bool clean=fx.IdleClean && !owner.Active && !journey.CompletionActive;
        yield return CompletionWindow("after-completion",6);
        // Same complete geometry and view, alternating no-effect/effect windows.
        // Keep the emulator host-capture setting identical for both timing runs.
        for(int pass=0;pass<3;pass++)
        {
            yield return CompletionWindow("control-"+pass,4);
            yield return CompletionWindow("celebration-"+pass,4,()=>owner.Begin(train),
                age=>owner.SetProgress(age/SwanAssemblyCelebration.Duration));
            owner.Restore();
            clean &= fx.IdleClean && !owner.Active;
        }
        int materials=fx.MaterialCount,triangles=fx.PaperTriangles;
        for(int pass=0;pass<8;pass++)
        {
            owner.Begin(train);owner.SetProgress(.35f);yield return null;
            if(pass%2==0)owner.Cancel();else owner.Restore();
            clean &= fx.IdleClean && !owner.Active && fx.MaterialCount==materials && fx.PaperTriangles==triangles;
        }
        owner.Begin(train);owner.SetProgress(.30f);journey.ResetJourney();yield return null;
        bool reset=fx.IdleClean && !owner.Active && !journey.Complete;
        // Exercise the actual automatic-assembly entry as well as manual final placement.
        int beforeAuto=fx.Plays;
        bool automatic=journey.StartAssemblySequence();
        double deadline=Time.realtimeSinceStartupAsDouble+30;
        while(journey.AutoAssembling && Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        automatic &= journey.Complete && !journey.AutoAssembling && fx.Plays==beforeAuto+1 && fx.IdleClean;
        // Scene changes must never carry the temporary materials or paper into the valley.
        owner.Begin(train);owner.SetProgress(.32f);
        journey.Act(JourneyAction.Drive);
        deadline=Time.realtimeSinceStartupAsDouble+90;
        while((!journey.inSandbox || journey.SwanSceneLoading || journey.SwanTransitionActive) && Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        bool valley=journey.inSandbox && !journey.SwanSceneLoading && fx.IdleClean;
        if(valley)
        {
            var sky=journey.GetComponent<MesaAtmosV16>().cloudTransforms[0].parent;
            PlaceView(sky,new Vector3(1,20.5f,24),new Vector3(0,7,0));
            journey.SwanSetControl(JourneyAction.Brake,0);journey.SwanSetControl(JourneyAction.Throttle,.75f);
            yield return new WaitForSecondsRealtime(6);
            yield return CompletionWindow("valley-moving",8);
        }
        journey.SetMode(false);
        deadline=Time.realtimeSinceStartupAsDouble+30;
        while(journey.SwanTransitionActive && Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        PlaceView(train,new Vector3(3.8f,5.8f,11.5f*side),new Vector3(0,1.35f,0));
        yield return new WaitForSecondsRealtime(2);
        yield return CompletionWindow("workshop-return",6);
        Debug.Log($"XR_CELEBRATION_CHECK clean={clean} reset={reset} automatic={automatic} valley={valley} materialStable={materials==fx.MaterialCount} triangles={triangles} pieces={SwanAssemblyCelebration.RibbonCount+SwanAssemblyCelebration.StripCount} idleClean={fx.IdleClean}");
        if(!clean || !reset || !automatic || !valley || !fx.IdleClean || triangles>1000 || materials!=fx.MaterialCount)
            Debug.LogError("XR_CELEBRATION_FAIL lifecycle");
        replay=false;detailReview=false;Debug.Log("XR_CELEBRATION_DONE");
    }
}
