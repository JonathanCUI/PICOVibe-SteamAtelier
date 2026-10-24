using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ByteDance.PICO.SpatialAdapter.Component;

namespace SwanAtelier.Editor
{
    [InitializeOnLoad]
    public static class SwanAtelierChecks
    {
        static readonly List<string> failures=new List<string>();
        static double started;
        static bool performed;
        static SwanAtelierChecks() { if(SessionState.GetBool("AtelierChecksRunning",false)) Attach(); }
        public static void Run() { EditorSceneManager.OpenScene(SwanAtelierBuild.ScenePath); SessionState.SetBool("AtelierChecksRunning",true); Attach(); EditorApplication.isPlaying=true; }
        static void Attach() { started=EditorApplication.timeSinceStartup; performed=false; EditorApplication.update-=Tick; EditorApplication.update+=Tick; Application.logMessageReceived-=Log; Application.logMessageReceived+=Log; }
        static void Log(string message,string stack,LogType type) { if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)failures.Add(message); }
        static void Check(bool result,string message) { if(!result)throw new Exception(message); }
        static void Tick()
        {
            double elapsed=EditorApplication.timeSinceStartup-started;
            if(elapsed>120){failures.Add("Play mode check timed out");Finish();return;}
            if(!EditorApplication.isPlaying||performed||elapsed<6)return;
            var game=UnityEngine.Object.FindObjectOfType<SwanAtelierController>(); if(!game||!game.Ready)return;
            performed=true;
            try {
                Check(game.assembly.Complete&&game.assembly.InstalledCount==45,"Startup assembly incomplete");
                Check(UnityEngine.Object.FindObjectsOfType<UnityEngine.EventSystems.StandaloneInputModule>(true).Length==0,"Legacy input module present");
                Check(!game.engine.enabled&&!game.drive.enabled&&!game.atelier.enabled&&!game.assembly.enabled,"Desktop update still active");
                CheckHoverAndHud(game);
                SwanAtelierGestureChecks.Run(game);
                var actions=game.content.GetComponentsInChildren<AtelierControl>(true).Select(c=>c.action).ToHashSet();
                foreach(var action in new[]{AtelierAction.Throttle,AtelierAction.Reverse,AtelierAction.Brake,AtelierAction.Whistle,AtelierAction.Lights,AtelierAction.Cutaway,AtelierAction.Observation,AtelierAction.ResetGuard,AtelierAction.Assembly,AtelierAction.Category,AtelierAction.TrayPrevious,AtelierAction.TrayNext,AtelierAction.TrayPart})Check(actions.Contains(action),"Missing physical control "+action);
                int idle=game.PoseUpdates; for(int n=0;n<90;n++)game.TickSimulation(1f/90); Check(game.PoseUpdates==idle,"Idle mechanism repeatedly updates");
                game.SetThrottle(.6f);Check(game.engine.running,"Throttle did not start engine");
                float phase=game.engine.phase;game.TickSimulation(.25f);Check(Mathf.Abs(Mathf.DeltaAngle(phase,game.engine.phase))>1,"Mechanism did not advance");
                game.Brake();Check(!game.engine.running,"Stationary brake failed");
                game.Activate(AtelierAction.Reverse);game.SetThrottle(.6f);Check(!game.engine.running&&game.atelier.direction==0,"Neutral failed to prevent power");
                game.Activate(AtelierAction.Reverse);game.SetThrottle(.6f);Check(game.engine.running&&game.engine.direction==-1,"Reverse failed");
                game.Brake();game.Activate(AtelierAction.Observation);game.SetThrottle(.6f);
                for(int n=0;n<700;n++)game.TickSimulation(.02f);
                Check(Mathf.Abs(game.engine.travelled)<=game.drive.limit+.08f&&!game.drive.powered,"Track endpoint failed");
                game.CompleteAssembly();game.Activate(AtelierAction.Cutaway);Check(game.engine.principle,"Cutaway failed");game.CompleteAssembly();
                game.Activate(AtelierAction.Assembly);Check(!game.HoldReset(2)&&game.assembly.Complete,"Closed guard allowed reset");
                game.Activate(AtelierAction.ResetGuard);game.Activate(AtelierAction.Assembly);Check(!game.HoldReset(.5f),"Reset happened too early");
                game.CancelReset();Check(game.atelier.resetProgress==0&&!game.HoldReset(2),"Cancel did not stop reset");
                game.Activate(AtelierAction.Assembly);Check(game.HoldReset(1.21f)&&game.assembly.InstalledCount==0,"Protected reset failed");
                Check(game.atelier.trayModels.Any(m=>m),"Empty assembly tray has no models");
                game.Activate(AtelierAction.Category);Check(game.atelier.trayCategory==1&&game.atelier.trayPage==0,"Category failed");
                game.Activate(AtelierAction.TrayNext);Check(game.atelier.trayPage<game.atelier.PageCount,"Paging out of bounds");
                game.CompleteAssembly();
                for(int i=0;i<45;i++) {
                    var loose=game.TakePiece(i);Check(loose&&game.assembly.InstalledCount==44,"Cannot remove piece "+i);
                    Check(loose.GetComponent<SpatialAdapterHoverEffect>(),"Loose piece lost hover: "+i);
                    CheckMeshTargets(loose.transform,"Loose piece "+i);
                    foreach(var part in game.assembly.pieces[i].members)Check(!game.assembly.IsPartVisible(part),"Installed member still visible: "+i);
                    loose.transform.position=game.assembly.SlotPosition(i)+Vector3.one;Check(!game.ReleasePiece(i),"Far piece snapped: "+i);
                    loose.transform.position=game.assembly.SlotPosition(i);Check(game.ReleasePiece(i)&&game.assembly.Complete,"Correct piece failed to snap: "+i);
                }
                game.Activate(AtelierAction.Interior);var fire=game.engine.parts.First(p=>p.partId=="M02-D");Check(game.doors.Toggle(fire),"Fire door click failed");
                for(int n=0;n<90;n++)game.TickSimulation(1f/90);
                Check(game.doors.angles[0]>59&&game.doors.fireAngle>77,"Doors did not animate while paused");
                game.CompleteAssembly();
                foreach(var mirror in game.content.GetComponentsInChildren<SwanTextMirror>(true))Check(mirror.source&&mirror.output,"Broken native text binding");
                Check(PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android)=="com.picovibe.steamatelier","Package would overwrite old app");
                Debug.Log("ATELIER_SWAN_CHECKS_PASSED pieces=45 checks=hover-coverage,loose-hover,fps-sampling,fps-pause-reset,hud-pose-mapping,hud-nonblocking,physical-controls,throttle,brake,neutral,reverse,travel-boundary,cutaway,protected-reset,cancel,category,paging,remove,reject,snap,doors,native-text,new-package");
            } catch(Exception e) { failures.Add(e.ToString()); }
            Finish();
        }
        static void CheckHoverAndHud(SwanAtelierController game)
        {
            foreach(var control in game.content.GetComponentsInChildren<AtelierControl>(true)) {
                Check(control.GetComponent<Collider>()&&control.GetComponent<SpatialAdapterHoverEffect>(),"Control cannot hover: "+control.name);
                Check(control.GetComponentsInChildren<MeshRenderer>(true).Any(r=>r.enabled&&!r.GetComponent<TMPro.TMP_Text>()),"Control has no highlight surface: "+control.name);
            }
            foreach(var part in game.content.GetComponentsInChildren<LocomotivePart>(true))
                if(part.GetComponentInChildren<Collider>(true)) {
                    Check(part.GetComponent<SpatialAdapterHoverEffect>(),"Part has no hover: "+part.name);
                    CheckMeshTargets(part.transform,"Installed part "+part.name);
                }
            foreach(var target in game.GetComponentsInChildren<SwanAtelierTarget>(true))Check(target.GetComponent<SpatialAdapterHoverEffect>(),"Utility button has no hover");
            Check(!game.GetComponent<SpatialAdapterHoverEffect>()&&!game.content.GetComponent<SpatialAdapterHoverEffect>(),"Whole stage should not glow for a single target");
            var hud=UnityEngine.Object.FindObjectOfType<SwanFrameRateHud>();Check(hud&&hud.meshText,"FPS HUD missing");
            var xr=UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            Check(xr&&xr.InitManagerOnStart&&xr.Manager.activeLoaders.Count==1&&xr.Manager.activeLoaders[0] is ByteDance.PICO.XR.PXR_Loader,"Spatial input provider is not enabled exclusively");
            Check(ByteDance.PICO.XR.PXR_ProjectSetting.GetProjectConfig().isSpatialAdapter,"PICO loader must stay in Spatial mode");
            Check(UnityEngine.Object.FindObjectOfType<SwanSpatialInputMode>(),"Stage/input mode synchronizer missing");
            Check(hud.GetComponentsInChildren<Collider>(true).Length==0,"FPS HUD blocks interactions");
            Check(!hud.transform.IsChildOf(game.transform),"Moving workbench moves the HUD");
            Check(!hud.VisualTransform.parent,"HUD native visual must be an independently tracked root");
            Check(!hud.DisplayText.Contains("\n"),"FPS should use one unobtrusive line");
            Check(hud.meshText.GetComponentsInChildren<Collider>(true).Length==0,"FPS glyphs intercept interaction");
            Check(hud.meshText.GetComponentsInChildren<SpatialAdapterNativeText>(true).Length==0,"FPS still uses native text placement");
            hud.meshText.Show(90);
            Check(hud.meshText.variants.Count(v=>v.activeSelf)==2 && hud.meshText.variants[20].activeSelf && hud.meshText.variants[22].activeSelf,"90 FPS glyph selection incorrect");
            hud.meshText.Show(123);
            Check(hud.meshText.variants.Count(v=>v.activeSelf)==3 && hud.meshText.variants[1].activeSelf && hud.meshText.variants[13].activeSelf && hud.meshText.variants[25].activeSelf,"123 FPS glyph selection incorrect");
            hud.meshText.Show(0);
            Check(hud.meshText.variants.Count(v=>v.activeSelf)==1 && hud.meshText.variants[22].activeSelf,"0 FPS glyph selection incorrect");
            var counter=new SwanFrameRateCounter(); counter.Sample(0);
            for(int i=1;i<=30;i++)counter.Sample(i/60.0);
            Check(Math.Abs(counter.Fps-60)<.001&&Math.Abs(counter.Milliseconds-1000.0/60)<.001,"60 FPS sample incorrect");
            counter.Sample(1.5);Check(Math.Abs(counter.Fps-1)<.001,"Long frame was hidden from FPS sample");
            counter.Reset();counter.Sample(500);Check(!counter.HasSample,"Resume retained stale FPS");
            for(int i=1;i<=45;i++)counter.Sample(500+i/90.0);
            Check(Math.Abs(counter.Fps-90)<.001,"Reset/90 FPS sample incorrect");
            var pose=new Pose(new Vector3(1,1.6f,-2),Quaternion.Euler(10,60,0));
            var root=Matrix4x4.TRS(new Vector3(2,.2f,1),Quaternion.Euler(0,35,0),Vector3.one*1.7f);
            hud.ApplyHeadPose(pose,root.inverse);
            Vector3 offset=Quaternion.Inverse(pose.rotation)*(root.MultiplyPoint3x4(hud.VisualTransform.position)-pose.position);
            Check(Vector3.Distance(offset,hud.viewOffset)<.0001f&&offset.x<0&&offset.y>0&&offset.z>0,"HUD left/top view offset mapping failed");
            var movedPose=new Pose(new Vector3(-.5f,1.8f,1),Quaternion.Euler(-15,-70,8));
            hud.ApplyHeadPose(movedPose,root.inverse);
            offset=Quaternion.Inverse(movedPose.rotation)*(root.MultiplyPoint3x4(hud.VisualTransform.position)-movedPose.position);
            Check(Vector3.Distance(offset,hud.viewOffset)<.0001f,"HUD drifted after head translation/rotation");
            Check(Quaternion.Angle(root.rotation*hud.VisualTransform.rotation,movedPose.rotation)<.05f,"HUD did not rotate with view");
            hud.enabled=false;Check(!hud.meshText.gameObject.activeSelf,"Disabled HUD remained visible");
            hud.enabled=true;Check(hud.meshText.gameObject.activeSelf,"Re-enabled HUD stayed hidden");
        }
        static void CheckMeshTargets(Transform root,string context)
        {
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true)) {
                var filter=renderer.GetComponent<MeshFilter>();
                if(!renderer.enabled||renderer.GetComponent<TMPro.TMP_Text>()||!filter||!filter.sharedMesh||filter.sharedMesh.vertexCount==0)continue;
                var collider=renderer.GetComponent<BoxCollider>();
                Check(collider&&renderer.GetComponent<SpatialAdapterHoverEffect>(),context+" mesh/hit/highlight separated: "+renderer.name);
                Check(collider.enabled&&collider.size.x>0&&collider.size.y>0&&collider.size.z>0,context+" hit target is disabled or empty");
                Check(Vector3.Distance(collider.center,filter.sharedMesh.bounds.center)<.0001f,context+" hit target is offset from mesh");
            }
        }
        static void Finish() {
            SessionState.SetBool("AtelierChecksRunning",false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
            File.WriteAllText("Builds/SwanAtelier/playmode-checks.json",JsonUtility.ToJson(new Result{passed=failures.Count==0,failures=failures.ToArray()},true));
            if(failures.Count>0)Debug.LogError("ATELIER_SWAN_CHECKS_FAILED "+string.Join(" | ",failures));
            EditorApplication.Exit(failures.Count==0?0:1);
        }
        [Serializable]class Result { public bool passed; public string[] failures; }
    }
}

