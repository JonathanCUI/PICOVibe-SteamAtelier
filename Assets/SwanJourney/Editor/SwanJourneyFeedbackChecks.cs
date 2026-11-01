using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SwanJourney.Editor
{
    public static class SwanJourneyFeedbackChecks
    {
        [Serializable] sealed class Report
        {
            public bool passed, nativeRenderingValidated, devicePerformanceMeasured;
            public string scope="Generated scene plus focused card, visibility, camera, TMP, material and flow regressions";
            public List<string> visibilityChecks=new List<string>();
            public List<string> suites=new List<string>();
            public string failure;
        }
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Edit Mode required");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save the current scene before running feedback checks");
            var report=new Report();
            try
            {
                Fresh(()=>{report.visibilityChecks=SwanJourneyVisibilityChecks.Run();});report.suites.Add("visibility");
                Fresh(SwanJourneyCardChecks.Run);report.suites.Add("prepared-cards");
                Fresh(SwanJourneyRenderPathChecks.Run);report.suites.Add("native-camera");
                Fresh(SwanJourneyUiChecks.Run);report.suites.Add("TMP");
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                SwanJourneyHiddenUpdateChecks.Run();report.suites.Add("hidden-material-updates");
                Fresh(SwanJourneyFlowChecks.Run);report.suites.Add("flow-lifecycle");
                Fresh(SwanJourneyDeferredChecks.Run);report.suites.Add("deferred-scene-placement");
                Fresh(SwanJourneyQuickInstallChecks.Run);report.suites.Add("quick-install-gestures");
                Fresh(SwanJourneyPerformanceChecks.Run);report.suites.Add("performance-and-loading");
                Fresh(SwanJourneyBaselineChecks.Run);report.suites.Add("baseline-geometry-and-isolation");
                report.passed=true;Debug.Log("SWAN_JOURNEY_FEEDBACK_CHECKS_PASS suites="+report.suites.Count+" visibility="+report.visibilityChecks.Count);
            }
            catch(Exception e){report.failure=e.ToString();throw;}
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                EditorSceneManager.OpenScene(SwanJourneyBuild.ScenePath);
                Directory.CreateDirectory(SwanJourneyBuild.Output);
                File.WriteAllText(SwanJourneyBuild.Output+"/feedback-checks.json",JsonUtility.ToJson(report,true));
            }
        }
        static void Fresh(Action check)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            EditorSceneManager.OpenScene(SwanJourneyBuild.ScenePath);
            check();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }
    }
}
