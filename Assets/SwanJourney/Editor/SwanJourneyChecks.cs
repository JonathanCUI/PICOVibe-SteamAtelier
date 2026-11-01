using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ByteDance.PICO.SpatialAdapter.Component;
using SwanAtelier;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SwanJourney.Editor
{
    [InitializeOnLoad]
    public static class SwanJourneyChecks
    {
        static readonly List<string> failures=new List<string>();
        static readonly List<string> passed=new List<string>();
        static double started,at;
        static float simulationAt;
        static int phase;
        static float routeBefore,phaseBefore,flowBefore;
        static SwanJourneyInteraction input;
        static SteamJourney journey;
        static SwanJourneyChecks(){if(SessionState.GetBool("SwanJourneyChecks",false))Attach();}
        public static void Run()
        {
            SwanJourneyBuild.Generate();
            SwanJourneyBuild.ClearExportCaches();
            // Use the SDK's supported Play-to-PICO path. The official server
            // must be running on the connected Swan before invoking these checks.
            SessionState.SetBool("SwanJourneyChecks",true);Attach();EditorApplication.isPlaying=true;
        }
        static void Attach(){started=EditorApplication.timeSinceStartup;at=started;phase=0;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
        static void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)failures.Add(message+"\n"+stack);}
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);passed.Add(message);}
        static void Advance(){phase++;at=EditorApplication.timeSinceStartup;simulationAt=Time.time;}
        static void Tick()
        {
            if(EditorApplication.timeSinceStartup-started>480){failures.Add("Play mode timed out at phase "+phase);Finish();return;}
            if(!EditorApplication.isPlaying)return;
            double elapsed=phase==0?EditorApplication.timeSinceStartup-at:Time.time-simulationAt;
            try {
                if(phase==0) {
                    input=UnityEngine.Object.FindObjectOfType<SwanJourneyInteraction>();
                    if(!input||!input.Ready||elapsed<4)return;
                    journey=input.journey;journey.suppressPersistence=true;
                    Check(journey.modules.Length==7&&journey.finishing.Length==10,"17 assembly groups with paired headlamps");
                    Check(PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android)==SwanJourneyBuild.PackageId,"Independent V23 application identifier");
                    Check(UnityEngine.Object.FindObjectsOfType<UnityEngine.EventSystems.StandaloneInputModule>(true).Length==0,"No legacy mouse input module");
                    Check(journey.SwanInputActive&&journey.spatialRoot,"Native input and spatial coordinates active");
                    Check(UnityEngine.Object.FindObjectOfType<SwanFrameRateHud>(true).meshText,"Independent view-following mesh FPS retained, including recording builds with the HUD hidden");
                    Check(input.GetComponent<SwanTabletopPlacement>().supportPoints.Length>0,"Tabletop footprints configured");
                    Check(input.GetComponent<SwanJourneyNativeUi>().ConvertedLabelCount>10,"World-space labels exported to native text");
                    var flow=journey.GetComponent<SwanJourneyFlowParticles>();
                    Check(flow&&flow.SourceCount==125&&flow.UsesArrows&&flow.PoolCapacity==4*SwanJourneyFlowParticles.ArrowsPerKind,"All 125 process paths bound to a bounded directional arrow pool");
                    Check(flow.poolRoot.GetComponentsInChildren<Collider>(true).Length==0&&flow.poolRoot.GetComponentsInChildren<SpatialAdapterHoverEffect>(true).Length==0,"Process flow cannot intercept gaze or pinch input");
                    foreach(var module in journey.modules.Concat(journey.finishing)) {
                        Check(module.preview&&SwanTwoHandManipulation.Finite(module.socket),"Preview and socket present: "+module.title);
                        Check(module.preview.GetComponentsInChildren<SpatialAdapterHoverEffect>(true).Length>0,"Preview hover: "+module.title);
                        Check(module.objects.Where(g=>g).SelectMany(g=>g.GetComponentsInChildren<SpatialAdapterHoverEffect>(true)).Any(),"Installed hover: "+module.title);
                    }
                    journey.ResetJourney();Capture("workshop-start");
                    for(int i=0;i<17;i++)Check(journey.InstallCurrent(),"Manual install "+i);
                    Check(journey.Complete,"All 17 groups assemble");Advance();
                } else if(phase==1&&elapsed>3) {
                    Check(!journey.CompletionActive,"Completion effect restores input");Capture("workshop-assembled");
                    SwanJourneyGestureChecks.Run(input);passed.Add("Gesture regression suite");
                    journey.suppressPersistence=true;Check(journey.FinishRemaining(),"Automatic assembly begins from missing groups");Advance();
                } else if(phase==2&&elapsed>13) {
                    Check(journey.Complete&&!journey.AutoAssembling&&!journey.CompletionActive,"Automatic assembly and finish effect complete");
                    int prior=journey.inspectionMode;journey.SwanAct(JourneyAction.Cutaway);Check(journey.inspectionMode==(prior+1)%3,"Inspection mode cycle");
                    journey.SwanAct(JourneyAction.Verify);Check(journey.running,"Thermal verification begins");phaseBefore=journey.engine.phase;flowBefore=journey.FlowTime;Advance();
                } else if(phase==3&&journey.FlowTime-flowBefore>25) {
                    Check(journey.modules[0].observed>0,"Combustion observation progresses");
                    Check(Mathf.Abs(Mathf.DeltaAngle(phaseBefore,journey.engine.phase))>.01f,"Connected process drives mechanism");
                    var flow=journey.GetComponent<SwanJourneyFlowParticles>();
                    Check(flow.ActiveMarkerCount>0&&flow.ActiveMarkerCount<=flow.PoolCapacity,"Native flow follows live process particles within budget");
                    Check(flow.RepresentedFieldCount==flow.ActiveFieldCount,"Every active process path receives a visible sample");
                    Capture("workshop-cutaway-running");journey.SwanAct(JourneyAction.Drive);Advance();
                } else if(phase==4) {
                    var loader=UnityEngine.Object.FindObjectOfType<SwanJourneySceneLoader>();
                    if(!loader)throw new Exception("Sandbox arrival has no deferred scene loader");
                    if(loader.State==SwanJourneySceneLoader.LoadingState.Failed)
                        throw new Exception("Deferred canyon load failed: "+loader.LastError);
                    // Loading is asynchronous and may span many simulation frames.
                    // Bound it by wall time so a paused or stalled Player cannot wait forever.
                    if(EditorApplication.timeSinceStartup-at>120)
                        throw new Exception("Sandbox load/arrival timed out after 120 seconds: state="+loader.State
                            +", loading="+journey.SwanSceneLoading+", sandbox="+journey.inSandbox
                            +", transition="+journey.SwanTransitionActive);
                    if(loader.State!=SwanJourneySceneLoader.LoadingState.Loaded||journey.SwanSceneLoading
                        ||!journey.inSandbox||journey.SwanTransitionActive||elapsed<=3)return;
                    Check(journey.inSandbox&&!journey.SwanTransitionActive,"Sandbox arrival completes");
                    Check(journey.coaches.Length==3,"Three passenger coaches retained");
                    Check(journey.GetComponent<SwanJourneyFlowParticles>().ActiveMarkerCount==0,"Process flow hidden outside workshop");
                    var visual=journey.GetComponent<SwanJourneyVisualBridge>();
                    Check(visual&&visual.NativeWindowCount==30&&visual.NativeAnimatedWindowCount==30,"All 30 passenger windows have native animation bindings");
                    var windowRenderers=journey.GetComponent<StudioSpatialRefineV19>().passengerWindows.Where(r=>r).Distinct().ToArray();
                    Check(journey.coaches.All(c=>windowRenderers.Count(w=>w.transform.IsChildOf(c))==10),"Each coach retains 10 passenger windows");
                    Check(journey.SwanSetControl(JourneyAction.Throttle,.85f),"Continuous spatial throttle accepted");
                    routeBefore=journey.routeDistance;Advance();
                } else if(phase==5&&elapsed>7) {
                    Check(journey.speed>.1f&&Mathf.Abs(journey.routeDistance-routeBefore)>.1f,"Train accelerates and follows route");
                    int direction=journey.direction;journey.SwanSetControl(JourneyAction.Reverse,-direction);Check(journey.direction==direction,"Direction change blocked while moving");
                    var sky=UnityEngine.Object.FindObjectOfType<MesaDayNightV13>(true);Check(sky,"Day/night system retained");sky.automatic=false;sky.SetTime(.4f);Capture("sandbox-day");sky.SetTime(.9f);
                    journey.SwanSetControl(JourneyAction.Throttle,0);journey.SwanSetControl(JourneyAction.Brake,1);Advance();
                } else if(phase==6&&elapsed>9) {
                    Check(journey.speed<.03f,"Progressive air brake brings train to rest");
                    journey.SwanSetControl(JourneyAction.Reverse,-1);Check(journey.direction==-1,"Reverse selectable at rest");
                    Capture("sandbox-night");journey.SetMode(false);Advance();
                } else if(phase==7) {
                    var loader=UnityEngine.Object.FindObjectOfType<SwanJourneySceneLoader>();
                    if(!loader)throw new Exception("Workshop return has no deferred scene loader");
                    if(loader.State==SwanJourneySceneLoader.LoadingState.Failed)
                        throw new Exception("Deferred canyon release failed: "+loader.LastError);
                    if(EditorApplication.timeSinceStartup-at>120)
                        throw new Exception("Workshop unload/arrival timed out after 120 seconds: state="+loader.State
                            +", loading="+journey.SwanSceneLoading+", sandbox="+journey.inSandbox
                            +", transition="+journey.SwanTransitionActive);
                    if(loader.State!=SwanJourneySceneLoader.LoadingState.Unloaded||journey.SwanSceneLoading
                        ||journey.inSandbox||journey.SwanTransitionActive||elapsed<=3)return;
                    Check(!journey.inSandbox&&!journey.SwanTransitionActive&&journey.Complete,"Return to workshop preserves assembly");
                    Capture("workshop-return");Finish();
                }
            } catch(Exception error){failures.Add(error.ToString());Finish();}
        }
        static void Capture(string name)
        {
            var camera=journey.view;var target=RenderTexture.GetTemporary(1600,1000,24);var previous=RenderTexture.active;
            try {camera.targetTexture=target;camera.Render();RenderTexture.active=target;var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();Directory.CreateDirectory(SwanJourneyBuild.Output);File.WriteAllBytes(SwanJourneyBuild.Output+"/"+name+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);}
            finally {camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);}
        }
        static void Finish()
        {
            SessionState.SetBool("SwanJourneyChecks",false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
            Directory.CreateDirectory(SwanJourneyBuild.Output);File.WriteAllText(SwanJourneyBuild.Output+"/playmode-checks.json",JsonUtility.ToJson(new Report{passed=failures.Count==0,checks=passed.ToArray(),failures=failures.ToArray()},true));
            if(journey)journey.suppressPersistence=true;
            Debug.Log("SWAN_JOURNEY_CHECKS "+(failures.Count==0?"PASS":"FAIL")+" checks="+passed.Count+" errors="+failures.Count);
            EditorApplication.Exit(failures.Count==0?0:1);
        }
        [Serializable] sealed class Report { public bool passed;public string[] checks,failures; }
    }
}
