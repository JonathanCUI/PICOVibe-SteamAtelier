using System;
using System.Diagnostics;
using Unity.Profiling;

namespace SwanJourney
{
    // Fixed slots, struct scopes and Stopwatch ticks: no per-callback allocations.
    // Wall time includes native calls/waits made inside the measured callback.
    public static class SwanJourneyCpuTimings
    {
        public enum Stage
        {
            TransformFilter, NativeUi, VisualBridge, Input, Presentation, FlowParticles,
            PartCards, Atmosphere, Clouds, Refine, Console, Simulation, DayNight,
            SteamTunnel, Arrival, WorkshopMood, Lighting, Water, Valve, WorkshopSteam,
            SceneLoader, NodeAlignment, Count
        }

        public struct Sample { public int calls; public double totalMs, maxMs; }
        static readonly long[] totals = new long[(int)Stage.Count];
        static readonly long[] maxima = new long[(int)Stage.Count];
        static readonly int[] calls = new int[(int)Stage.Count];
        static readonly ProfilerMarker[] markers = CreateMarkers();
        static object owner;
        static int generation;
        public static bool Enabled => owner != null;

        static ProfilerMarker[] CreateMarkers()
        {
            var result = new ProfilerMarker[(int)Stage.Count];
            for (int i = 0; i < result.Length; i++) result[i] = new ProfilerMarker("SwanJourney." + (Stage)i);
            return result;
        }

        public static bool BeginSession(object requester)
        {
            if (requester == null || owner != null) return false;
            owner = requester; Reset(); return true;
        }

        public static void EndSession(object requester)
        {
            if (!ReferenceEquals(owner, requester)) return;
            owner = null; Reset();
        }

        public static void Reset()
        {
            generation++;
            Array.Clear(totals, 0, totals.Length);
            Array.Clear(maxima, 0, maxima.Length);
            Array.Clear(calls, 0, calls.Length);
        }

        public static Sample Read(Stage stage)
        {
            int i = (int)stage;
            double ms = 1000.0 / Stopwatch.Frequency;
            return new Sample { calls = calls[i], totalMs = totals[i] * ms, maxMs = maxima[i] * ms };
        }

        public static Scope Measure(Stage stage) => new Scope(stage, Enabled);

        public struct Scope : IDisposable
        {
            readonly int index, epoch;
            readonly long start;
            bool active;
            internal Scope(Stage stage, bool enabled)
            {
                index = (int)stage; epoch = generation; active = enabled;
                start = enabled ? Stopwatch.GetTimestamp() : 0;
                if (enabled) markers[index].Begin();
            }
            public void Dispose()
            {
                if (!active) return;
                active = false;
                long ticks = Stopwatch.GetTimestamp() - start;
                markers[index].End();
                if (!Enabled || epoch != generation) return;
                totals[index] += ticks; calls[index]++;
                if (ticks > maxima[index]) maxima[index] = ticks;
            }
        }
    }
}
