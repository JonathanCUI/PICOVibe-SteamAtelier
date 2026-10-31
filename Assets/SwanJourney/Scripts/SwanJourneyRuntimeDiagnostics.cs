using System;
using Unity.Profiling;
using UnityEngine;

namespace SwanJourney
{
    // The Unity camera remains available to authored layout code, but must not
    // draw the scene a second time on the native SpatialAdapter player.
    [DefaultExecutionOrder(9000)]
    public sealed class SwanJourneyRuntimeDiagnostics : MonoBehaviour
    {
        public SteamJourney journey;
        public Camera previewCamera;
        readonly float[] frameMilliseconds = new float[1800];
        readonly float[] sorted = new float[1800];
        ProfilerRecorder mainThread, gcAlloc;
        int count, frameCount, collectionStart, correctedMasks;
        double windowStart, elapsed, mainTotal;
        long allocatedBytes;
        bool announced, paused, modeInitialized, lastSandbox;

        void Start()
        {
            // Runs after the partner Start (which requests 72). This is an app
            // pacing target, not a refresh-rate change or a measured FPS claim.
            if (journey && journey.SwanInputActive)
            {
                int previous = Application.targetFrameRate;
                Application.targetFrameRate = 90;
                QualitySettings.vSyncCount = 0;
                Debug.Log("SWAN_JOURNEY_FRAME_TARGET previous=" + previous + " requested=90 actualFps=unmeasured");
            }
        }

        void OnEnable()
        {
            ResetWindow();
#if UNITY_ANDROID && !UNITY_EDITOR
            try { mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1); }
            catch (Exception e) { Debug.LogWarning("SWAN_JOURNEY_MAIN_RECORDER_UNAVAILABLE " + e.GetType().Name); }
            try { gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1); }
            catch (Exception e) { Debug.LogWarning("SWAN_JOURNEY_GC_RECORDER_UNAVAILABLE " + e.GetType().Name); }
#endif
        }

        public bool EnforceNativeCamera()
        {
            if (!journey || !journey.SwanInputActive || !previewCamera) return false;
            bool changed = previewCamera.cullingMask != 0;
            if (changed) { previewCamera.cullingMask = 0; correctedMasks++; }
            return changed;
        }

        void LateUpdate()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            EnforceNativeCamera();
            if (!announced && journey && previewCamera)
            {
                Debug.Log("SWAN_JOURNEY_RENDER_PATH nativeScene=true unityCameraMask=" + previewCamera.cullingMask
                    + " cameraEnabled=" + previewCamera.enabled + " mainThreadRecorder=" + mainThread.Valid
                    + " gcRecorder=" + gcAlloc.Valid);
                announced = true;
            }
            if (paused || !Application.isFocused) { ResetWindow(); return; }
            bool sandbox=journey&&journey.inSandbox;
            if(!modeInitialized||lastSandbox!=sandbox){modeInitialized=true;lastSandbox=sandbox;ResetWindow();}
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0) return;
            if (count < frameMilliseconds.Length) frameMilliseconds[count++] = dt * 1000;
            elapsed += dt; frameCount++;
            if (mainThread.Valid) mainTotal += mainThread.LastValue / 1000000.0;
            if (gcAlloc.Valid) allocatedBytes += gcAlloc.LastValue;
            if (Time.realtimeSinceStartupAsDouble - windowStart < 10 || count == 0) return;
            Array.Copy(frameMilliseconds, sorted, count); Array.Sort(sorted, 0, count);
            Debug.Log("SWAN_JOURNEY_RUNTIME_WINDOW mode=" + (journey && journey.inSandbox ? "drive" : "workshop")
                + " samples=" + frameCount + " appUpdateHz=" + (frameCount / Math.Max(elapsed, .001)).ToString("F1")
                + " appDeltaP50Ms=" + Percentile(.50f).ToString("F2") + " appDeltaP95Ms=" + Percentile(.95f).ToString("F2")
                + " appDeltaP99Ms=" + Percentile(.99f).ToString("F2")
                + " unityMainThreadMs=" + (mainThread.Valid ? (mainTotal / frameCount).ToString("F2") : "unavailable")
                + " gcAllocatedBytes=" + (gcAlloc.Valid ? allocatedBytes.ToString() : "unavailable")
                + " gcCollections=" + (GC.CollectionCount(0) - collectionStart) + " unityCameraMask=" + (previewCamera ? previewCamera.cullingMask : -1)
                + " correctedMasks=" + correctedMasks + " nativeCompositorMeasured=false");
            ResetWindow();
#endif
        }

        float Percentile(float q) => sorted[Mathf.Clamp(Mathf.CeilToInt(count * q) - 1, 0, count - 1)];
        void ResetWindow()
        {
            count = frameCount = 0; elapsed = mainTotal = 0; allocatedBytes = 0;
            collectionStart = GC.CollectionCount(0); windowStart = Time.realtimeSinceStartupAsDouble;
        }
        void OnApplicationPause(bool value) { paused=value; ResetWindow(); }
        void OnDisable() { mainThread.Dispose(); gcAlloc.Dispose(); }
    }
}
