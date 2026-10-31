using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SwanJourney.Editor
{
    // The SDK's pre-build dependency traversal exports the prefab first.
    // Strip our editor-only reference from the Player scene before the SDK's
    // order-0 scene processing, without adding/removing any Transform nodes.
    public sealed class SwanJourneyBuildDependencyStripper : IProcessSceneWithReport
    {
        public int callbackOrder => -1000;
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if(report!=null)Strip(scene);
        }
        public static void Strip(Scene scene)
        {
            foreach(var root in scene.GetRootGameObjects())
                foreach(var marker in root.GetComponentsInChildren<SwanJourneyBuildDependencies>(true))
                    Object.DestroyImmediate(marker);
        }
    }
}
