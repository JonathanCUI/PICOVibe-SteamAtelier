using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SwanJourney.Editor
{
    public static class SwanJourneyRenderPathChecks
    {
        [Serializable] sealed class Report
        {
            public bool passed, nativeRenderingValidated, devicePerformanceMeasured;
            public string scope="Actual SteamJourney.BuildUI and UpdateText on generated scene; native camera guard and desktop branch";
            public List<string> checks=new List<string>();
            public string failure;
        }
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Edit Mode required");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save the current scene before running camera checks");
            var setup=EditorSceneManager.GetSceneManagerSetup();var report=new Report();
            try
            {
                EditorSceneManager.OpenScene(SwanJourneyBuild.ScenePath);
                var journey=UnityEngine.Object.FindObjectOfType<SteamJourney>(true);
                var guard=UnityEngine.Object.FindObjectOfType<SwanJourneyRuntimeDiagnostics>(true);
                Check(journey&&guard&&guard.journey==journey&&guard.previewCamera==journey.view,"Generated native-only camera binding is complete");
                journey.suppressPersistence=true;journey.view.cullingMask=0;
                Invoke(journey,"BuildUI");
                Check(journey.view.cullingMask==0,"Real Swan BuildUI does not restore layer-zero rendering after SDK initialization");
                var overlays=SceneManager.GetActiveScene().GetRootGameObjects().Where(g=>g.name=="Journey · State and contextual controls").ToArray();
                Check(overlays.Length==1&&!overlays[0].activeSelf,"Desktop overlay subtree is inactive on Swan, not merely Canvas.enabled=false");
                journey.SwanNotify("Camera regression message");
                Check(journey.SpatialMessage=="Camera regression message","Native message still reads the preserved inactive overlay reference");
                Invoke(journey,"UpdateText");
                Check(journey.trayTitle&&!string.IsNullOrEmpty(journey.trayTitle.text),"World-space tray title still updates while desktop overlay is skipped");
                Check(!overlays[0].activeSelf,"Updating world controls does not revive desktop overlay");
                journey.view.cullingMask=1;
                Check(guard.EnforceNativeCamera()&&journey.view.cullingMask==0,"Guard recovers a later layer-zero restoration");
                Check(!guard.EnforceNativeCamera(),"Stable native camera needs no repeated mask write");
                journey.SwanInputActive=false;journey.view.cullingMask=1;
                Check(!guard.EnforceNativeCamera()&&journey.view.cullingMask==1,"Guard leaves original desktop camera untouched");
                journey.view.cullingMask=0;Invoke(journey,"BuildUI");
                Check((journey.view.cullingMask&1)==1,"Desktop BuildUI retains its original layer-zero render behavior");
                report.passed=true;Debug.Log("SWAN_JOURNEY_RENDER_PATH_CHECKS_PASS checks="+report.checks.Count);
            }
            catch(Exception e){report.failure=e.ToString();throw;}
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                if(setup.Length>0&&setup.All(s=>!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(setup);
                Directory.CreateDirectory(SwanJourneyBuild.Output);
                File.WriteAllText(SwanJourneyBuild.Output+"/render-path-checks.json",JsonUtility.ToJson(report,true));
            }
            void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);report.checks.Add(message);}
        }
        static void Invoke(object target,string method)=>target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);
    }
}
