using System;
using UnityEngine;

namespace SwanJourney
{
    // SpatialAdapter translates emitter settings, not ParticleSystem.SetParticles arrays.
    // Sample the partner's existing CPU simulation; never advance or re-create its flow model.
    [DefaultExecutionOrder(9100)]
    public sealed class SwanJourneyFlowParticles : MonoBehaviour
    {
        public const int MaximumMarkers = 288;
        public const int SamplesPerField = 12;
        public static readonly float[] AlphaLevels = { .04f, .09f, .16f, .26f, .40f, .57f, .76f, 1f };

        [Serializable] public sealed class Source
        {
            public JourneyFluidField field;
            public Material[] alphaMaterials;
        }
        public SteamJourney journey;
        public Transform poolRoot;
        public Source[] sources = Array.Empty<Source>();
        public MeshRenderer[] markers = Array.Empty<MeshRenderer>();
        public int SourceCount => sources.Length;
        public int PoolCapacity => markers.Length;
        public int ActiveMarkerCount { get; private set; }
        public int ActiveFieldCount { get; private set; }
        public int RepresentedFieldCount { get; private set; }
        public int LastSourceParticleCount { get; private set; }

        sealed class State
        {
            public Source source;
            public ParticleSystem system;
            public ParticleSystemRenderer renderer;
            public readonly ParticleSystem.Particle[] selected = new ParticleSystem.Particle[SamplesPerField];
            public readonly ParticleSystem.Particle[] next = new ParticleSystem.Particle[SamplesPerField];
            public int count, quota;
            public bool visible;
        }
        // JourneyFluidField itself has a fixed 384-wisp capacity.
        readonly ParticleSystem.Particle[] readBuffer = new ParticleSystem.Particle[384];
        readonly int[] eligible = new int[384];
        State[] states;
        Transform[] markerTransforms;
        float nextRefresh;

        void Awake()
        {
            states = new State[sources.Length];
            for (int i = 0; i < sources.Length; i++)
            {
                var source = sources[i];
                var system = source.field ? source.field.system : null;
                states[i] = new State { source = source, system = system,
                    renderer = system ? system.GetComponent<ParticleSystemRenderer>() : null };
            }
            markerTransforms = new Transform[markers.Length];
            for (int i = 0; i < markers.Length; i++) if (markers[i])
            {
                markerTransforms[i] = markers[i].transform;
                markers[i].enabled = false;
            }
            Debug.Log("SWAN_JOURNEY_FLOW_BRIDGE sources=" + SourceCount + " pool=" + PoolCapacity + " source=GetParticles");
        }

        void LateUpdate() { using (SwanJourney.SwanJourneyCpuTimings.Measure(SwanJourney.SwanJourneyCpuTimings.Stage.FlowParticles)) CpuMeasuredLateUpdate(); }
    void CpuMeasuredLateUpdate()
        {
            if (!journey || !journey.SwanInputActive || !poolRoot || states == null) { HideAll(); return; }
            bool show = !journey.inSandbox && journey.cutaway;
            foreach (var state in states)
            {
                state.visible = show && state.system && state.system.gameObject.activeInHierarchy
                    && state.renderer && state.renderer.enabled;
                // The source owner writes enabled each Update. Hide only its drawing after sampling
                // that visibility; keep the ParticleSystem and all thermal/lifetime state intact.
                if (state.renderer) state.renderer.enabled = false;
            }
            if (!show) { HideAll(); return; }
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 1f / 30f;
            RefreshSamples();
        }

        void RefreshSamples()
        {
            ActiveFieldCount = LastSourceParticleCount = 0;
            foreach (var state in states)
            {
                state.quota = 0;
                if (!state.visible) { state.count = 0; continue; }
                int count = state.system.GetParticles(readBuffer);
                LastSourceParticleCount += count;
                int available = 0;
                for (int i = 0; i < count; i++)
                    if (readBuffer[i].remainingLifetime > 0 && readBuffer[i].GetCurrentColor(state.system).a > 5)
                        eligible[available++] = i;
                SelectStableSamples(state, available);
                if (state.count > 0) ActiveFieldCount++;
            }

            // Give every currently populated path a share before filling additional samples.
            // Parallel boiler tubes cannot consume the entire pool and hide the steam/air route.
            int capacity = Mathf.Min(MaximumMarkers, markers.Length), allocated = 0;
            for (int level = 0; level < SamplesPerField && allocated < capacity; level++)
                foreach (var state in states)
                    if (state.count > level && allocated < capacity) { state.quota++; allocated++; }

            int used = 0;
            RepresentedFieldCount = 0;
            foreach (var state in states)
            {
                if (state.quota == 0) continue;
                RepresentedFieldCount++;
                var main = state.system.main;
                Transform simulation = main.simulationSpace == ParticleSystemSimulationSpace.Custom
                    ? main.customSimulationSpace : state.system.transform;
                bool world = main.simulationSpace == ParticleSystemSimulationSpace.World;
                Vector3 sizeScale = main.scalingMode == ParticleSystemScalingMode.Hierarchy
                    ? Abs(state.system.transform.lossyScale)
                    : main.scalingMode == ParticleSystemScalingMode.Local ? Abs(state.system.transform.localScale) : Vector3.one;
                Vector3 rootScale = Abs(poolRoot.lossyScale);
                for (int i = 0; i < state.quota; i++)
                {
                    var sample = state.selected[i];
                    var renderer = markers[used]; var target = markerTransforms[used++];
                    if (!renderer || !target) continue;
                    Vector3 worldPosition = world || !simulation ? sample.position : simulation.TransformPoint(sample.position);
                    Vector3 worldSize = Vector3.Scale(sample.GetCurrentSize3D(state.system), sizeScale);
                    var color = sample.GetCurrentColor(state.system);
                    var palette = state.source.alphaMaterials;
                    int alpha = NearestAlpha(color.a / 255f);
                    var material = palette != null && alpha < palette.Length ? palette[alpha] : null;
                    if (!material || !Finite(worldPosition) || !Finite(worldSize)) { renderer.enabled = false; continue; }
                    target.localPosition = poolRoot.InverseTransformPoint(worldPosition);
                    var rotation = Quaternion.Euler(sample.rotation3D);
                    target.localRotation = Quaternion.Inverse(poolRoot.rotation) * (world || !simulation ? rotation : simulation.rotation * rotation);
                    target.localScale = new Vector3(worldSize.x / Mathf.Max(.00001f, rootScale.x),
                        worldSize.y / Mathf.Max(.00001f, rootScale.y), worldSize.z / Mathf.Max(.00001f, rootScale.z));
                    if (renderer.sharedMaterial != material) renderer.sharedMaterial = material;
                    if (!renderer.enabled) renderer.enabled = true;
                }
            }
            for (int i = used; i < ActiveMarkerCount; i++) if (markers[i]) markers[i].enabled = false;
            ActiveMarkerCount = used;
        }

        void SelectStableSamples(State state, int available)
        {
            int selected = 0;
            // Preserve particle identity until it dies instead of jumping between array slots
            // when JourneyFluidField removes a wisp by swapping its last array element.
            for (int old = 0; old < state.count && selected < SamplesPerField; old++)
                for (int n = 0; n < available; n++)
                {
                    var particle = readBuffer[eligible[n]];
                    if (particle.randomSeed != state.selected[old].randomSeed) continue;
                    state.next[selected++] = particle; break;
                }
            int remaining = Mathf.Min(SamplesPerField - selected, available);
            // Spread first selection across the occupied path, including its travelling front.
            for (int n = 0; n < available && selected < SamplesPerField; n++)
            {
                int index = remaining > 0 && n < remaining ? n * available / remaining : n;
                var particle = readBuffer[eligible[Mathf.Min(index, available - 1)]];
                bool duplicate = false;
                for (int k = 0; k < selected; k++) if (state.next[k].randomSeed == particle.randomSeed) { duplicate = true; break; }
                if (!duplicate) state.next[selected++] = particle;
            }
            state.count = selected;
            Array.Copy(state.next, state.selected, selected);
        }

        static int NearestAlpha(float alpha)
        {
            int result = 0; float best = float.MaxValue;
            for (int i = 0; i < AlphaLevels.Length; i++)
            {
                float distance = Mathf.Abs(alpha - AlphaLevels[i]);
                if (distance < best) { best = distance; result = i; }
            }
            return result;
        }
        static Vector3 Abs(Vector3 value) => new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        void HideAll()
        {
            for (int i = 0; i < ActiveMarkerCount; i++) if (markers[i]) markers[i].enabled = false;
            ActiveMarkerCount = ActiveFieldCount = RepresentedFieldCount = LastSourceParticleCount = 0;
        }
        void OnDisable() { HideAll(); }
    }
}
