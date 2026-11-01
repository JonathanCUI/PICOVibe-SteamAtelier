using System.Collections;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using ByteDance.PICO.SpatialAdapter;

namespace SwanAtelier
{
    [DisallowMultipleComponent]
    public sealed class SwanSpatialInputMode : MonoBehaviour
    {
        public static bool Ready { get; private set; }
        Coroutine monitor;
        bool paused;
        bool foregroundChanged;
#if UNITY_ANDROID && !UNITY_EDITOR
        XRManagerSettings manager;
        bool ownsLoader;
#endif
        void OnEnable() { Ready = false; monitor = StartCoroutine(Monitor()); }
        IEnumerator Monitor()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Finish Start, then let an opened, focused Stage settle. A camera
            // object alone also exists while the headset is asleep.
            yield return null;
            var tick = new WaitForSecondsRealtime(.25f);
            double focusedSince = -1, missingSince = -1;
            bool synced = false, warned = false;
            int retries = 0, validSamples = 0;
            while (true)
            {
                double now = Time.realtimeSinceStartupAsDouble;
                var camera = SpatialCamera.Current;
                if (foregroundChanged)
                {
                    foregroundChanged = false;
                    Ready = false; focusedSince = missingSince = -1;
                    synced = warned = false; retries = validSamples = 0;
                }
                if (paused || !Application.isFocused || !camera || !camera.WindowOpen)
                {
                    Ready = false; focusedSince = missingSince = -1;
                    synced = warned = false; retries = validSamples = 0;
                    yield return tick; continue;
                }
                if (focusedSince < 0) focusedSince = now;
                if (now - focusedSince < .5) { yield return tick; continue; }
                if (!manager)
                {
                    var settings = XRGeneralSettings.Instance;
                    if (!settings || !settings.Manager)
                    {
                        Debug.LogError("SWAN_ATELIER_XR_STAGE_INIT_FAILED settings-missing");
                        yield break;
                    }
                    manager = settings.Manager;
                }
                if (!manager.activeLoader)
                {
                    manager.InitializeLoaderSync();
                    ownsLoader = manager.activeLoader != null;
                    if (!ownsLoader)
                    {
                        Debug.LogError("SWAN_ATELIER_XR_STAGE_INIT_FAILED loader-missing");
                        yield break;
                    }
                    manager.StartSubsystems();
                }
                if (!synced)
                {
                    Sync("focused-stage"); synced = true; missingSince = now;
                }
                var input = manager.activeLoader.GetLoadedSubsystem<XRInputSubsystem>();
                // Resume a subsystem stopped during a focus transition, using
                // Unity's public subsystem lifecycle without rebuilding the Stage.
                if (input != null && !input.running) input.Start();
                if (input != null && input.running && SwanHeadPose.TryRead(out _))
                {
                    if (++validSamples >= 2 && !Ready)
                    {
                        Ready = true;
                        var supported = input.GetSupportedTrackingOriginModes();
                        if ((supported & TrackingOriginModeFlags.Floor) != 0
                            && input.GetTrackingOriginMode() != TrackingOriginModeFlags.Floor)
                            input.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
                        Debug.Log("SWAN_ATELIER_XR_STAGE_READY tracked=True owned=" + ownsLoader + " retries=" + retries);
                    }
                    missingSince = -1;
                }
                else
                {
                    validSamples = 0;
                    if (Ready) Debug.LogWarning("SWAN_ATELIER_XR_TRACKING_LOST");
                    Ready = false;
                    if (missingSince < 0) missingSince = now;
                    if (now - missingSince >= 4 && retries < 2 && input != null)
                    {
                        retries++;
                        Debug.LogWarning("SWAN_ATELIER_XR_INPUT_RETRY attempt=" + retries);
                        input.Stop();
                        yield return null;
                        if (!paused && Application.isFocused && camera && camera.WindowOpen)
                        {
                            input.Start(); Sync("input-retry");
                        }
                        else synced = false;
                        missingSince = Time.realtimeSinceStartupAsDouble;
                    }
                    else if (now - missingSince >= 4 && retries >= 2 && !warned)
                    {
                        warned = true;
                        Debug.LogError("SWAN_ATELIER_XR_TRACKING_UNAVAILABLE retries-exhausted; waiting for tracking or next foreground transition");
                    }
                }
                yield return tick;
            }
#else
            yield break;
#endif
        }
        static void Sync(string reason)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            ByteDance.PICO.XR.PXR_SpatialAdapterSpatialMode.SyncSpatialAdapterSpatialMode();
            Debug.Log("SWAN_ATELIER_XR_SPATIAL_MODE_SYNCED reason=" + reason);
#endif
        }
        void OnApplicationPause(bool value) { paused = value; foregroundChanged = true; if (value) Ready = false; }
        void OnApplicationFocus(bool value) { foregroundChanged = true; if (!value) Ready = false; }
        void OnDisable()
        {
            Ready = false;
            if (monitor != null) { StopCoroutine(monitor); monitor = null; }
        }
        void OnDestroy()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (ownsLoader && manager != null) { manager.StopSubsystems(); manager.DeinitializeLoader(); }
#endif
        }
    }
}
