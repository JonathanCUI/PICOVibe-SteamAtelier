using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SwanJourney
{
    [DefaultExecutionOrder(3150)]
    public sealed class SwanJourneySceneLoader : MonoBehaviour
    {
        public enum LoadingState { Unloaded, Loading, Loaded, Unloading, Failed }
        public SteamJourney journey;
        public Transform environmentAnchor;
        public SwanJourneyPresentation presentation;
        public SwanJourneyVisualBridge visuals;
        public string environmentScenePath;
        public const string EnvironmentResource = "SwanJourneyCanyon";
        public LoadingState State { get; private set; }
        public SwanJourneyEnvironment Environment { get; private set; }
        public string LastError { get; private set; }
        public bool Busy => State == LoadingState.Loading || State == LoadingState.Unloading;
        public int LoadCount { get; private set; }
        public int UnloadCount { get; private set; }
        Transform[] coreSupports;
        Scene loadedScene;
        SwanJourneyEnvironment ownedPayload;
        bool requestedSandbox, paused, quitting;
        double phaseStarted, previousPhase;
        int loadAttempt;

        void Awake()
        {
            coreSupports = presentation ? presentation.sandboxSupports.ToArray() : Array.Empty<Transform>();
            if (journey) journey.SwanModeRequest = RequestMode;
            if (presentation) presentation.SynchronizeEnvironmentPose = FollowEnvironment;
        }

        bool RequestMode(bool sandbox)
        {
            requestedSandbox = sandbox;
            if (!sandbox)
            {
                if (Environment && State == LoadingState.Loaded)
                {
                    // Called before SetMode(false) starts the workshop reveal.
                    // Restore canyon materials and drop its renderer references first.
                    DetachEnvironment();
                    SetState(LoadingState.Unloading);
                    StartCoroutine(UnloadEnvironment());
                }
                return true;
            }
            if (State == LoadingState.Loaded && Environment) return true;
            if (!Busy)
            {
                loadAttempt++;
                phaseStarted = previousPhase = Time.realtimeSinceStartupAsDouble;
                LogLoadPhase("request");
                SetState(LoadingState.Loading);
                SwanJourneyLoadingView.Current?.Show(true);
                journey.SwanNotify("正在载入山谷铁路，请稍候…");
                StartCoroutine(LoadEnvironment());
            }
            return false;
        }

        IEnumerator LoadEnvironment()
        {
            LastError = null;
            ResourceRequest operation = null;
            LogLoadPhase("prefab-resource-call");
            try { operation = Resources.LoadAsync<GameObject>(EnvironmentResource); }
            catch (Exception e) { LastError = e.Message; }
            if (operation == null)
            {
                Fail(LastError ?? "Unity did not create a prefab load operation");
                yield break;
            }
            LogLoadPhase("prefab-resource-returned");
            while (!operation.isDone)
            {
                SwanJourneyLoadingView.Current?.SetResourceProgress(operation.progress);
                yield return null;
            }
            SwanJourneyLoadingView.Current?.PreparingSpace();
            yield return null;
            yield return null;
            if (!requestedSandbox || !journey || !journey.Complete)
            {
                SetState(LoadingState.Unloading);
                SwanJourneyLoadingView.Current?.Hide();
                yield return UnloadEnvironment();
                yield break;
            }
            LogLoadPhase("native-phase-presented-prefab-instantiate");
            SwanJourneyEnvironment payload = null;
            try { payload = InstantiateEnvironment(operation.asset as GameObject); }
            catch(Exception e) { LastError=e.Message; }
            if (!payload)
            {
                string failure = LastError ?? "Loaded environment prefab has no payload";
                SetState(LoadingState.Unloading);
                yield return ReleaseScene();
                Fail(failure);
                yield break;
            }
            LogLoadPhase("bindings-and-unity-pose-ready");
            LoadCount++;
            Debug.Log("SWAN_JOURNEY_DEFERRED_UNITY_READY resource=" + EnvironmentResource
                + " loads=" + LoadCount + " nativePresentation=unverified");
            // The adapter queues native prefab creation in its frame-end callback.
            // Give that callback a complete update cycle; this is not a native
            // completion signal and must not be reported as visual verification.
            yield return null;
            LogLoadPhase("first-frame-boundary-returned");
            while (paused) yield return null;
            yield return null;
            LogLoadPhase("second-frame-boundary-returned");
            AdoptEnvironment();
            // Flush the public Transform.SetParent change after native loading.
            yield return null;
            SetState(LoadingState.Loaded);
            if (requestedSandbox && journey && journey.Complete)
            {
                Environment.gameObject.SetActive(true);
                journey.SetMode(true);
                presentation.ApplyModeNow();
                LogLoadPhase("driving-unity-mode-applied");
                Debug.Log("SWAN_JOURNEY_DEFERRED_UNITY_POSE attempt=" + loadAttempt
                    + " anchorScale=" + environmentAnchor.lossyScale.ToString("F5")
                    + " payloadScale=" + Environment.transform.lossyScale.ToString("F5")
                    + " payloadPosition=" + Environment.transform.position.ToString("F3")
                    + " rootActive=" + Environment.gameObject.activeInHierarchy
                    + " nativeScale=unverified nativeReady=unverified");
                yield return null;
                if (State != LoadingState.Loading) SwanJourneyLoadingView.Current?.Hide();
            }
            else
            {
                SwanJourneyLoadingView.Current?.Hide();
                DetachEnvironment();
                SetState(LoadingState.Unloading);
                yield return UnloadEnvironment();
            }
        }

        public SwanJourneyEnvironment InstantiateEnvironment(GameObject prefab)
        {
            if(!prefab || !presentation)throw new InvalidOperationException("Deferred canyon prefab is unavailable");
            var instance=Instantiate(prefab,presentation.transform,false);
            try {
                var payload=instance.GetComponent<SwanJourneyEnvironment>();
                AttachEnvironment(payload);
                return payload;
            } catch {
                if(Application.isPlaying)Destroy(instance);else DestroyImmediate(instance);
                throw;
            }
        }

        // Also used by the editor's scene-boundary checks. This method changes
        // references and pose; adoption is a separate step after native import.
        public void AttachEnvironment(SwanJourneyEnvironment payload)
        {
            if (!payload || !environmentAnchor || !presentation || !visuals)
                throw new InvalidOperationException("Deferred environment bindings are incomplete");
            if (Environment && Environment != payload)
                throw new InvalidOperationException("An environment is already attached");
            if (coreSupports == null) coreSupports = presentation.sandboxSupports.ToArray();
            Environment = payload;
            ownedPayload = payload;
            if (payload.gameObject.scene != presentation.gameObject.scene)
                loadedScene = payload.gameObject.scene;
            FollowEnvironment();
            payload.gameObject.SetActive(true);
            presentation.sandboxSupports = coreSupports.Concat(payload.supports).Where(t => t).Distinct().ToArray();
            visuals.SetDeferredEnvironment(payload.transform);
        }

        public void AdoptEnvironment()
        {
            if(!Environment || !presentation)return;
            Environment.gameObject.SetActive(true);
            if(Environment.transform.parent!=presentation.transform)
                Environment.transform.SetParent(presentation.transform,true);
            FollowEnvironment();
            Environment.GetComponent<SwanJourneyNodeAlignmentDiagnostics>()?.ExpectRuntimeParent(presentation.transform);
            Debug.Log("SWAN_JOURNEY_CANYON_ADOPTED owner="+Environment.gameObject.scene.name
                +" parent="+Environment.transform.parent.name+" localPosition="+Environment.transform.localPosition.ToString("F4")
                +" localScale="+Environment.transform.localScale.ToString("F4")+" nativeParent=unverified");
        }

        public void ReleaseOwnedEnvironment()
        {
            if(Environment)DetachEnvironment();
            var payload=ownedPayload;ownedPayload=null;
            if(!payload)return;
            if(Application.isPlaying)Destroy(payload.gameObject);
            else DestroyImmediate(payload.gameObject);
        }

        public void DetachEnvironment()
        {
            if (visuals) visuals.SetDeferredEnvironment(null);
            if (presentation && coreSupports != null) presentation.sandboxSupports = coreSupports.ToArray();
            if (Environment) Environment.gameObject.SetActive(false);
            Environment = null;
        }

        IEnumerator UnloadEnvironment()
        {
            // Let the original SetMode move the common train back into workshop
            // before destroying any additive objects.
            yield return null;
            yield return ReleaseScene();
            UnloadCount++;
            SetState(LoadingState.Unloaded);
            Debug.Log("SWAN_JOURNEY_DEFERRED_UNLOADED count=" + UnloadCount + " nativeBufferRelease=unverified");
            if (requestedSandbox && journey && journey.Complete) RequestMode(true);
        }

        IEnumerator ReleaseScene()
        {
            if (Environment) DetachEnvironment();
            ReleaseOwnedEnvironment();
            // Allow both Unity and the adapter to process destruction before
            // releasing the additive scene and its referenced resources.
            yield return null;
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                var unloading = SceneManager.UnloadSceneAsync(loadedScene);
                if (unloading != null) yield return unloading;
            }
            loadedScene = default;
            yield return Resources.UnloadUnusedAssets();
        }

        public void FollowEnvironment()
        {
            if (!Environment || !environmentAnchor) return;
            // The common content is still at workshop scale while the adapter imports.
            // Apply the destination pose to the payload only, including the anchor offset.
            var pivot = presentation ? presentation.content : null;
            if (State == LoadingState.Loading && pivot && pivot.localScale.x > 0 && environmentAnchor.IsChildOf(pivot))
                Environment.Follow(environmentAnchor, pivot, presentation.sandboxScale / pivot.localScale.x);
            else
                Environment.Follow(environmentAnchor);
        }

        void LateUpdate() { using (SwanJourney.SwanJourneyCpuTimings.Measure(SwanJourney.SwanJourneyCpuTimings.Stage.SceneLoader)) CpuMeasuredLateUpdate(); }
    void CpuMeasuredLateUpdate()
        {
            if (!Environment) return;
            FollowEnvironment();
            // The adapter imports an active payload over multiple frame boundaries.
            // Keep the attached instance active until the mode commits or cancels.
            if (State == LoadingState.Loading) return;
            bool visible = journey && journey.inSandbox && State == LoadingState.Loaded;
            if (Environment.gameObject.activeSelf != visible) Environment.gameObject.SetActive(visible);
        }

        void SetState(LoadingState state)
        {
            State = state;
            if (journey) journey.SwanSceneLoading = Busy;
        }
        void LogLoadPhase(string phase)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            Debug.Log("SWAN_JOURNEY_DEFERRED_PHASE attempt=" + loadAttempt + " phase=" + phase
                + " elapsedSeconds=" + (now - phaseStarted).ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                + " sincePreviousSeconds=" + (now - previousPhase).ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                + " frame=" + Time.frameCount + " paused=" + paused + " nativeReady=unverified");
            previousPhase = now;
        }
        void Fail(string message)
        {
            LastError = message;
            SwanJourneyLoadingView.Current?.Fail();
            SetState(LoadingState.Failed);
            Debug.LogError("SWAN_JOURNEY_DEFERRED_FAILED " + message);
            if (journey) journey.SwanNotify("山谷铁路暂未载入，装配进度已保留，可再次点击出发。");
        }
        void OnApplicationPause(bool value) { paused = value; }
        void OnApplicationQuit() { quitting = true; }
        void OnDestroy()
        {
            if (presentation && presentation.SynchronizeEnvironmentPose == FollowEnvironment)
                presentation.SynchronizeEnvironmentPose = null;
            if (journey)
            {
                if (journey.SwanModeRequest == RequestMode) journey.SwanModeRequest = null;
                journey.SwanSceneLoading = false;
            }
            if (!Application.isPlaying || quitting) return;
            ReleaseOwnedEnvironment();
            if (loadedScene.IsValid() && loadedScene.isLoaded) SceneManager.UnloadSceneAsync(loadedScene);
        }
    }
}
