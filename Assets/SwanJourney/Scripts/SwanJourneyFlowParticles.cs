using System;
using UnityEngine;

namespace SwanJourney
{
    // SpatialAdapter translates emitter settings, not ParticleSystem.SetParticles arrays.
    // Sample the partner's existing CPU simulation; never advance or re-create its flow model.
    [DefaultExecutionOrder(9100)]
    public sealed partial class SwanJourneyFlowParticles : MonoBehaviour
    {
        public const int MaximumMarkers = 288;
        public const int SamplesPerField = 4;
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
            public ParticleSystem.Particle[] selected;
            public ParticleSystem.Particle[] next;
            public int count, quota;
        }
        // JourneyFluidField itself has a fixed 384-wisp capacity.
        ParticleSystem.Particle[] readBuffer;
        int[] eligible;
        State[] states;
        Transform[] markerTransforms;
        float nextRefresh;

        void Awake()
        {
            if (!UsesArrows) { readBuffer = new ParticleSystem.Particle[384]; eligible = new int[384]; }
            states = new State[sources.Length];
            for (int i = 0; i < sources.Length; i++)
            {
                var source = sources[i];
                var system = source.field ? source.field.system : null;
                states[i] = new State { source = source, system = system,
                    renderer = system ? system.GetComponent<ParticleSystemRenderer>() : null };
                if (!UsesArrows) { states[i].selected = new ParticleSystem.Particle[SamplesPerField]; states[i].next = new ParticleSystem.Particle[SamplesPerField]; }
                if (journey && journey.SwanInputActive && states[i].renderer) states[i].renderer.enabled = false;
            }
            markerTransforms = new Transform[markers.Length];
            for (int i = 0; i < markers.Length; i++) if (markers[i])
            {
                markerTransforms[i] = markers[i].transform;
                if(UsesArrows) markers[i].gameObject.SetActive(false);
                else markers[i].enabled = false;
            }
            if(UsesArrows) InitializeArrows();
            Debug.Log("SWAN_JOURNEY_FLOW_BRIDGE sources=" + SourceCount + " pool=" + PoolCapacity + (UsesArrows ? " source=path tangents" : " source=GetParticles"));
        }

        void LateUpdate() { using (SwanJourney.SwanJourneyCpuTimings.Measure(SwanJourney.SwanJourneyCpuTimings.Stage.FlowParticles)) CpuMeasuredLateUpdate(); }
    void CpuMeasuredLateUpdate()
        {
            if (!journey || !journey.SwanInputActive || !poolRoot || states == null) { HideAll(); return; }
            bool show = !journey.inSandbox && journey.cutaway;
            if (!show) { HideAll(); return; }
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 1f / 24f;
            if (UsesArrows) DrawArrows(true); else RefreshSamples();
        }

        void RefreshSamples()
        {
            ActiveFieldCount = LastSourceParticleCount = 0;
            foreach (var state in states)
            {
                state.quota = 0;
                if (!state.source.field || !state.source.field.HasWisps || !state.system || !state.system.gameObject.activeInHierarchy) { state.count = 0; continue; }
                int count = state.system.GetParticles(readBuffer);
                LastSourceParticleCount += count;
                // All current internal paths have constant particle color. Ask Unity to
                // evaluate gradients only for sources that actually enable color curves.
                bool dynamicAlpha = state.system.colorOverLifetime.enabled || state.system.colorBySpeed.enabled;
                int available = 0;
                for (int i = 0; i < count; i++)
                    if (readBuffer[i].remainingLifetime > 0 && (dynamicAlpha ? readBuffer[i].GetCurrentColor(state.system).a : readBuffer[i].startColor.a) > 5)
                        eligible[available++] = i;
                SelectStableSamples(state, available);
                if (state.count > 0) ActiveFieldCount++;
            }

            // Give every currently populated path a share before filling additional samples.
            // Parallel boiler tubes cannot consume the entire pool and hide the steam/air route.
            int capacity = Mathf.Min(markers.Length, Mathf.Max(64, ActiveFieldCount)), allocated = 0;
            for (int level = 0; level < SamplesPerField && allocated < capacity; level++)
                foreach (var state in states)
                    if (state.count > level && allocated < capacity) { state.quota++; allocated++; }

            int used = 0;
            var toPool = poolRoot.worldToLocalMatrix; var inverseRotation = Quaternion.Inverse(poolRoot.rotation); var rootScale = Abs(poolRoot.lossyScale);
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
                    var rotation = Quaternion.Euler(sample.rotation3D);
                    target.SetLocalPositionAndRotation(toPool.MultiplyPoint3x4(worldPosition),inverseRotation * (world || !simulation ? rotation : simulation.rotation * rotation));
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
            if (UsesArrows && markerTransforms != null) { DrawArrows(false); nextRefresh=0; return; }
            for (int i = 0; i < ActiveMarkerCount; i++) if (markers[i]) markers[i].enabled = false;
            nextRefresh = 0;
            ActiveMarkerCount = ActiveFieldCount = RepresentedFieldCount = LastSourceParticleCount = 0;
        }
        void OnDisable() { HideAll(); }
    }
}
