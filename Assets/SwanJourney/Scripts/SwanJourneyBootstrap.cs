using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SwanJourney
{
    public sealed class SwanJourneyBootstrap : MonoBehaviour
    {
        public string workshopScene;
        public SwanJourneyLoadingView loading;
        IEnumerator Start()
        {
            Application.targetFrameRate = 90;
            loading.Show(false);
            // Let the small boot scene and its Stage reach the adapter before
            // asking it to parse the large workshop. Do not preload the canyon.
            yield return null;
            yield return null;
#if UNITY_ANDROID && !UNITY_EDITOR
            while (!Application.isFocused || !SwanAtelier.SwanSpatialInputMode.Ready) yield return null;
#endif
            yield return null;
            Debug.Log("SWAN_JOURNEY_BOOT resource-load-start");
            AsyncOperation operation = null;
            try { operation = SceneManager.LoadSceneAsync(workshopScene, LoadSceneMode.Additive); }
            catch (Exception e) { Debug.LogError("SWAN_JOURNEY_BOOT_FAILED " + e); }
            if (operation == null) { loading.Fail(); yield break; }
            operation.allowSceneActivation = false;
            while (operation.progress < .9f) { loading.SetResourceProgress(operation.progress / .9f); yield return null; }
            loading.PreparingSpace();
            // Present phase 2 BEFORE scene Awake queues the blocking native import.
            // End-of-frame alone is insufficient: let the adapter flush a full frame.
            yield return null;
            yield return null;
            Debug.Log("SWAN_JOURNEY_BOOT native-phase-presented activation-release");
            operation.allowSceneActivation = true;
            while (!operation.isDone) yield return null;
            Debug.Log("SWAN_JOURNEY_BOOT unity-ready nativeReady=unverified");
            // Native load is queued by the SDK at frame end; these yields keep
            // the boot panel visible until control returns, without inventing %.
            yield return null;
            yield return null;
            var scene = SceneManager.GetSceneByPath(workshopScene);
            if (!scene.IsValid() || !scene.isLoaded) { loading.Fail(); yield break; }
            SceneManager.SetActiveScene(scene);
            loading.Hide();
            Debug.Log("SWAN_JOURNEY_BOOT workshop-handoff nativeRendering=unverified");
        }
    }
}
