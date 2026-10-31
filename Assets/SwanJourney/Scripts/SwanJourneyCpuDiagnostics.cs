using System;
using System.Globalization;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace SwanJourney
{
    // Diagnostic only; no pose, material, physics or interaction mutations.
    // Observe after app LateUpdates. Recorder values describe the preceding frame,
    // so compare window aggregates, never subtract them as exact same-frame costs.
    [DefaultExecutionOrder(100001)]
    public sealed class SwanJourneyCpuDiagnostics : MonoBehaviour
    {
        public SteamJourney journey;
        public SwanJourneyInteraction interaction;
        ProfilerRecorder lateUpdate;
        readonly StringBuilder line = new StringBuilder(1800);
        bool ownsSession, paused, previousDrive;
        int previousInstalled = -1, previousInspection = -1;
        bool previousVerify, previousRolling;
        int frames, movingFrames, lateSamples, longFrames;
        double windowStart, lastFrameTime, longestFrameMs, lateTotal;

        void OnEnable()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!journey || !journey.SwanInputActive) return;
            ownsSession = SwanJourneyCpuTimings.BeginSession(this);
            if (!ownsSession) return;
            try { lateUpdate = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "LateBehaviourUpdate", 1); }
            catch (Exception e) { Debug.LogWarning("SWAN_JOURNEY_CPU_RECORDER_UNAVAILABLE " + e.GetType().Name); }
            previousDrive = journey.inSandbox;
            ResetWindow();
            Debug.Log("SWAN_JOURNEY_CPU_DIAGNOSTICS enabled=true intervalSeconds=5 measuredCallbacks=22 kind=wallTime recorderPreviousFrame=true developmentBuild="+Debug.isDebugBuild);
#endif
        }

        void LateUpdate()
        {
            if (!ownsSession) return;
            if (paused || !Application.isFocused || !journey || journey.SwanSceneLoading)
            { ResetWindow(); return; }
            int installed = 0;
            foreach (var part in journey.modules) if (part.installed) installed++;
            foreach (var part in journey.finishing) if (part.installed) installed++;
            bool rolling = journey.inSandbox && journey.speed > .035f;
            if (previousDrive != journey.inSandbox || previousInstalled != installed ||
                previousInspection != journey.inspectionMode || previousVerify != journey.running || previousRolling != rolling)
            {
                previousDrive = journey.inSandbox; previousInstalled = installed; previousInspection = journey.inspectionMode;
                previousVerify = journey.running; previousRolling = rolling;
                ResetWindow(); return;
            }
            frames++;
            double now = Time.realtimeSinceStartupAsDouble;
            double frameMs = (now - lastFrameTime) * 1000;
            lastFrameTime = now;
            longestFrameMs = Math.Max(longestFrameMs, frameMs);
            if (frameMs >= 50) longFrames++;
            if (interaction && interaction.TwoHandActive) movingFrames++;
            if (lateUpdate.Valid && lateUpdate.LastValue > 0)
            { lateSamples++; lateTotal += lateUpdate.LastValue / 1000000.0; }
            double elapsed = now - windowStart;
            if (elapsed < 5) return;
            line.Clear();
            line.Append("SWAN_JOURNEY_CPU_WINDOW mode=").Append(previousDrive ? "drive" : "workshop")
                .Append(" installed=").Append(previousInstalled).Append(" verify=").Append(previousVerify)
                .Append(" inspection=").Append(previousInspection).Append(" rolling=").Append(previousRolling)
                .Append(" frames=").Append(frames).Append(" twoHandFrames=").Append(movingFrames)
                .Append(" elapsedSeconds=").Append(F(elapsed)).Append(" unityFps=").Append(F(frames / elapsed))
                .Append(" longestFrameMs=").Append(F(longestFrameMs)).Append(" framesAtLeast50ms=").Append(longFrames)
                .Append(" phase=").Append(movingFrames == 0 ? "idle" : movingFrames == frames ? "twoHand" : "mixed")
                .Append(" lateRecordedMs=").Append(lateSamples > 0 ? F(lateTotal / lateSamples) : "unavailable")
                .Append(" samplesPreviousFrame=true");
            double measuredTotal = 0;
            for (int i = 0; i < (int)SwanJourneyCpuTimings.Stage.Count; i++)
            {
                var stage = (SwanJourneyCpuTimings.Stage)i;
                var sample = SwanJourneyCpuTimings.Read(stage);
                if (sample.calls == 0) continue;
                measuredTotal += sample.totalMs;
                line.Append(' ').Append(stage).Append("Ms=").Append(F(sample.totalMs / frames))
                    .Append('/').Append(F(sample.maxMs)).Append('/').Append(sample.calls);
            }
            line.Append(" measuredCallbackMs=").Append(F(measuredTotal / frames))
                .Append(" units=perFrameMean/singleCallMax/calls residualIsNotSdkAttribution=true");
            Debug.Log(line.ToString());
            ResetWindow();
        }

        static string F(double value) => value.ToString("F3", CultureInfo.InvariantCulture);
        void ResetWindow()
        {
            frames = movingFrames = lateSamples = longFrames = 0;
            lateTotal = longestFrameMs = 0;
            windowStart = lastFrameTime = Time.realtimeSinceStartupAsDouble;
            if (ownsSession) SwanJourneyCpuTimings.Reset();
        }
        void OnApplicationPause(bool value) { paused = value; ResetWindow(); }
        void OnDisable()
        {
            lateUpdate.Dispose();
            if (ownsSession) SwanJourneyCpuTimings.EndSession(this);
            ownsSession = false;
        }
    }
}
