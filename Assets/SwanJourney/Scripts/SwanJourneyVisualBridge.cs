using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace SwanJourney
{
    public enum JourneyNativeMaterialKind { Surface, Particle, Cloud, Steam, Mist, Furnace, WaterSurface, WaterBody, WaterJacket, Ghost, Beam, Halo }
    [Serializable] public sealed class JourneyNativeMaterial
    {
        public Material material;
        public JourneyNativeMaterialKind kind;
        public Color color = Color.white;
        public float opacity = 1;
    }

    // The authored process owns visibility, motion, pressure and smoke lifetimes.
    // This bridge translates only its visual parameters to exported PBR resources.
    [DefaultExecutionOrder(9000)]
    public sealed class SwanJourneyVisualBridge : MonoBehaviour
    {
        public SteamJourney journey;
        public Transform workbench;
        public Transform deferredEnvironment;
        public JourneyNativeMaterial[] materials = Array.Empty<JourneyNativeMaterial>();
        public Mesh softVolume, flameVolume, cloudVolume, beamVolume, waterVolume, jacketVolume, ringMesh;
        public Material scanMaterial, rippleMaterial;
        public Transform completionBadge;
        public int NativeAnimatedRenderers => animated.Count;
        public int NativeOwnedMaterialCount => animated.Select(a=>a.instance).Where(m=>m).Distinct().Count();
        public int NativeSharedGhostCount => sharedGhosts.Count;
        public int NativeWindowCount => windows == null ? 0 : windows.Where(r=>r).Distinct().Count();
        public int NativeAnimatedWindowCount => windows == null ? 0 : windows.Where(r=>r).Distinct().Count(r=>animated.Any(a=>a.renderer==r));
        public bool GoldActive => completionBadge && completionBadge.gameObject.activeSelf;
        // Per-update work counters, not frame-time or native-rendering measurements.
        public int LastAnimatedVisited { get; private set; }
        public int LastHiddenSkipped { get; private set; }
        public int LastMaterialWrites { get; private set; }
        public int LastPropertyBlockReads { get; private set; }
        public int LastPropertyBlocksConsumed { get; private set; }

        sealed class Animated
        {
            public Renderer renderer;
            public JourneyNativeMaterial definition;
            public Material instance;
            public Color originalEmission;
            public Color appliedColor, appliedEmission;
            public bool hasApplied, wasVisible;
            public SharedGhost sharedGhost;
        }
        // Ghost output depends only on its source definition and the journey-wide
        // SnapReady flag, never on per-renderer blocks. Keep renderers separate
        // (including transparent sorting) while sharing identical live parameters.
        sealed class SharedGhost
        {
            public Material instance;
            public int references;
            public bool hasApplied;
            public Color color, emission;
        }
        readonly List<Animated> animated = new List<Animated>();
        readonly Dictionary<Material, JourneyNativeMaterial> lookup = new Dictionary<Material, JourneyNativeMaterial>();
        readonly Dictionary<Material, SharedGhost> sharedGhosts = new Dictionary<Material, SharedGhost>();
        readonly HashSet<Renderer> seen = new HashSet<Renderer>();
        MaterialPropertyBlock block;
        StudioAssemblyFinishV9 finish;
        StudioSpatialRefineV19 refinement;
        MesaDayNightV13 sky;
        Renderer[] windows;
        float discoverUntil, nextDiscover;
        bool lastMode;

        void Awake()
        {
            if (!journey) journey = GetComponent<SteamJourney>();
            if (!workbench) workbench = transform;
            DisableReplacedFlameEmitter();
            block = new MaterialPropertyBlock();
            foreach (var m in materials) if (m != null && m.material) lookup[m.material] = m;
            finish = Find<StudioAssemblyFinishV9>();
            refinement = Find<StudioSpatialRefineV19>();
            sky = Find<MesaDayNightV13>();
            windows = refinement ? refinement.passengerWindows : Array.Empty<Renderer>();
            if (finish) { finish.nativeBegin = BeginGold; finish.nativeRestore = EndGold; }
            discoverUntil = Time.unscaledTime + 5;
            Discover();
        }

        void Start() { Discover(); }
        T Find<T>() where T:Component => journey.GetComponent<T>() ?? workbench.GetComponentInChildren<T>(true);

        public void SetDeferredEnvironment(Transform environment)
        {
            if (ReferenceEquals(deferredEnvironment, environment)) return;
            var previous = deferredEnvironment;
            if (!ReferenceEquals(previous, null))
            {
                // Only these per-renderer instances belong to the detached root.
                // Shared exported materials and the global lookup remain registered.
                for (int i = animated.Count - 1; i >= 0; i--)
                {
                    var entry = animated[i];
                    if (entry.renderer && !Under(entry.renderer, previous)) continue;
                    if (entry.renderer && entry.instance)
                    {
                        var bound = entry.renderer.sharedMaterials;
                        bool changed = false;
                        for (int slot = 0; slot < bound.Length; slot++)
                            if (bound[slot] == entry.instance) { bound[slot] = entry.definition.material; changed = true; }
                        if (changed) entry.renderer.sharedMaterials = bound;
                    }
                    seen.Remove(entry.renderer);
                    ReleaseMaterial(entry);
                    animated.RemoveAt(i);
                }
                seen.RemoveWhere(renderer => !renderer || Under(renderer, previous));
            }
            deferredEnvironment = environment;
            // The loader can bind before Awake, or attach a live additive scene.
            // Awake fills lookup; later calls can discover the new root immediately.
            if (deferredEnvironment && block != null) DiscoverRoot(deferredEnvironment);
        }

        static bool Under(Renderer renderer, Transform root)
            => renderer && root && (renderer.transform == root || renderer.transform.IsChildOf(root));

        void Discover()
        {
            if (workbench) DiscoverRoot(workbench);
            if (deferredEnvironment && (!workbench || !deferredEnvironment.IsChildOf(workbench))) DiscoverRoot(deferredEnvironment);
        }

        void DiscoverRoot(Transform root)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!r || seen.Contains(r)) continue;
                // StudioPartCards now owns a clone of its serialized native line
                // material. Do not replace that binding after Start.
                var source = r.sharedMaterial;
                if (!source || !lookup.TryGetValue(source, out var definition)) continue;
                if (r.GetComponent<TMPro.TMP_Text>() || r.GetComponent<TMPro.TMP_SubMesh>() || r.GetComponent<TMPro.TMP_SubMeshUI>()) continue;
                bool hasParameters = r.HasPropertyBlock();
                bool isWindow = windows != null && Array.IndexOf(windows, r) >= 0;
                if (definition.kind == JourneyNativeMaterialKind.Surface && !hasParameters && !isWindow) continue;
                seen.Add(r);
                // Particle color/alpha remains owned by the ParticleSystem, not a per-renderer tint.
                if (r is ParticleSystemRenderer) continue;
                SharedGhost shared=null;
                Material instance;
                if(definition.kind==JourneyNativeMaterialKind.Ghost)
                {
                    if(!sharedGhosts.TryGetValue(source,out shared))
                    {
                        shared=new SharedGhost{instance=new Material(source){name=source.name+" · shared native ghost"}};
                        shared.instance.EnableKeyword("_EMISSION");
                        sharedGhosts.Add(source,shared);
                    }
                    shared.references++;instance=shared.instance;
                }
                else
                {
                    instance = new Material(source) { name = source.name + " · live native" };
                    instance.EnableKeyword("_EMISSION");
                }
                var all = r.sharedMaterials;
                for (int i = 0; i < all.Length; i++) if (all[i] == source) all[i] = instance;
                r.sharedMaterials = all;
                animated.Add(new Animated { renderer = r, definition = definition, instance = instance, originalEmission = source.GetColor("_EmissionColor"),sharedGhost=shared });
                var filter = r.GetComponent<MeshFilter>();
                if (!filter) continue;
                // Export already chose a native mesh for authored renderers before reducing it.
                // Dynamic puffs still need the native fallback when they are first discovered.
                if (filter.sharedMesh && filter.sharedMesh.name.EndsWith(" • tabletop", StringComparison.Ordinal)) continue;
                switch (definition.kind)
                {
                    case JourneyNativeMaterialKind.Cloud: if(cloudVolume)filter.sharedMesh=cloudVolume;break;
                    case JourneyNativeMaterialKind.Steam:
                    case JourneyNativeMaterialKind.Mist: if (softVolume) filter.sharedMesh = softVolume; break;
                    case JourneyNativeMaterialKind.Furnace: if (flameVolume) filter.sharedMesh = flameVolume; else if(softVolume) filter.sharedMesh = softVolume; break;
                    case JourneyNativeMaterialKind.Beam: if (beamVolume) filter.sharedMesh = beamVolume; break;
                    case JourneyNativeMaterialKind.WaterBody: if (waterVolume) filter.sharedMesh = waterVolume; break;
                    case JourneyNativeMaterialKind.WaterJacket: if (jacketVolume) filter.sharedMesh = jacketVolume; break;
                }
            }
        }

        void DisableReplacedFlameEmitter()
        {
            // SDK 6.1 native emitters sync active/enabled, not runtime emission rates.
            // The source has already replaced this zero-rate emitter with flameVolume.
            if(!journey || !journey.fire || !journey.flameVolume)return;
            var emission=journey.fire.emission;emission.enabled=false;emission.rateOverTime=0;
            journey.fire.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
            var renderer=journey.fire.GetComponent<ParticleSystemRenderer>();if(renderer)renderer.enabled=false;
        }

        void LateUpdate() { using (SwanJourney.SwanJourneyCpuTimings.Measure(SwanJourney.SwanJourneyCpuTimings.Stage.VisualBridge)) CpuMeasuredLateUpdate(); }
    void CpuMeasuredLateUpdate()
        {
            LastAnimatedVisited = LastHiddenSkipped = LastMaterialWrites = LastPropertyBlockReads = LastPropertyBlocksConsumed = 0;
            if (!journey) return;
            if (Time.unscaledTime >= nextDiscover && (Time.unscaledTime < discoverUntil || journey.inSandbox != lastMode))
            {
                Discover(); nextDiscover = Time.unscaledTime + .35f;
            }
            lastMode = journey.inSandbox;
            float daylight = sky && journey.inSandbox ? sky.Daylight : 1;
            foreach (var a in animated)
            {
                var r = a.renderer;
                if (!r || !a.instance) continue;
                LastAnimatedVisited++;
                // Unity camera visibility is not native Spatial visibility. Use
                // authored enable/active state, and leave hidden MPBs unconsumed.
                if (!r.enabled || !r.gameObject.activeInHierarchy)
                {
                    a.wasVisible = false;
                    LastHiddenSkipped++;
                    continue;
                }
                r.GetPropertyBlock(block);
                LastPropertyBlockReads++;
                bool hasProperties = !block.isEmpty;
                var color = a.definition.color;
                var emission = a.originalEmission;
                if (block.HasColor("_BaseColor")) color = block.GetColor("_BaseColor");
                else if (block.HasColor("_Color")) color = block.GetColor("_Color");
                if (block.HasColor("_EmissionColor")) emission = block.GetColor("_EmissionColor");
                switch (a.definition.kind)
                {
                    case JourneyNativeMaterialKind.Cloud:
                        color = Color.Lerp(new Color(.42f,.51f,.65f,1),color,daylight);
                        color.a = 1;
                        emission = new Color(color.r,color.g,color.b)*.045f;
                        break;
                    case JourneyNativeMaterialKind.Steam:
                        color = Color.Lerp(new Color(.34f,.43f,.58f,color.a), color, daylight);
                        color.a *= block.HasFloat("_Opacity") ? Mathf.Clamp01(block.GetFloat("_Opacity")) : a.definition.opacity;
                        // Match the authored lifetime fade in both opacity and self-light.
                        emission = new Color(color.r,color.g,color.b) * (.12f * color.a);
                        break;
                    case JourneyNativeMaterialKind.Mist:
                        color.a *= Mathf.Clamp01((block.HasFloat("_Density") ? block.GetFloat("_Density") : 0) * 2.4f);
                        emission = new Color(.25f,.4f,.48f) * color.a;
                        break;
                    case JourneyNativeMaterialKind.Furnace:
                        float burn = block.HasFloat("_Burn") ? block.GetFloat("_Burn") : journey.FireLevel;
                        float flutter = .85f + .10f*Mathf.Sin(journey.FlowTime*9) + .05f*Mathf.Sin(journey.FlowTime*17);
                        color.a *= burn * flutter; emission = new Color(2.2f,.44f,.035f) * burn * flutter;
                        break;
                    case JourneyNativeMaterialKind.Beam:
                        color.a *= block.HasFloat("_Strength") ? block.GetFloat("_Strength") : .4f;
                        emission = new Color(color.r,color.g,color.b) * (.6f * color.a);
                        break;
                    case JourneyNativeMaterialKind.Halo:
                        if (block.HasColor("_Tint")) color = block.GetColor("_Tint");
                        emission = new Color(color.r,color.g,color.b) * (1.3f * color.a);
                        break;
                    case JourneyNativeMaterialKind.WaterSurface:
                        float boil = journey.WaterHeat;
                        color = Color.Lerp(color,new Color(.12f,.48f,.53f,color.a),boil*.25f);
                        break;
                    case JourneyNativeMaterialKind.Ghost:
                        // Exported edge strips write depth. Keep the fill legible even without bloom.
                        color = a.definition.color;
                        emission = a.originalEmission;
                        if (color.a < .99f && journey.SnapReady) color.a = Mathf.Min(1, color.a * 1.25f);
                        break;
                }
                bool changed=a.sharedGhost!=null
                    ? !a.sharedGhost.hasApplied||!a.sharedGhost.color.Equals(color)||!a.sharedGhost.emission.Equals(emission)
                    : !a.hasApplied||!a.wasVisible||!a.appliedColor.Equals(color)||!a.appliedEmission.Equals(emission);
                if (changed)
                {
                    a.instance.color = color;
                    a.instance.SetColor("_EmissionColor", emission);
                    a.appliedColor = color; a.appliedEmission = emission;
                    a.hasApplied = true;
                    if(a.sharedGhost!=null){a.sharedGhost.hasApplied=true;a.sharedGhost.color=color;a.sharedGhost.emission=emission;}
                    LastMaterialWrites += 2;
                }
                a.wasVisible = true;
                // Native Standard material updates are reliable; custom MPB names are not a shader port.
                if (hasProperties) { r.SetPropertyBlock(null); LastPropertyBlocksConsumed++; }
            }
        }

        void BeginGold(Transform train)
        {
            if (!completionBadge || !train || !isActiveAndEnabled) return;
            completionBadge.localRotation = Quaternion.identity;
            if (SwanAtelier.SwanHeadPose.TryRead(out var head))
            {
                var direction = completionBadge.position - head.position;
                if (direction.sqrMagnitude > .0001f)
                {
                    var up = Mathf.Abs(Vector3.Dot(direction.normalized, train.up)) > .98f ? train.forward : train.up;
                    completionBadge.rotation = Quaternion.LookRotation(direction, up);
                }
            }
            completionBadge.gameObject.SetActive(true);
        }
        void EndGold()
        {
            if (GoldActive) completionBadge.gameObject.SetActive(false);
        }
        void OnDisable() { EndGold(); }
        void ReleaseMaterial(Animated entry)
        {
            if(entry.sharedGhost!=null)
            {
                if(--entry.sharedGhost.references>0)return;
                sharedGhosts.Remove(entry.definition.material);
            }
            if(entry.instance)
            {
                if(Application.isPlaying)Destroy(entry.instance);else DestroyImmediate(entry.instance);
            }
        }
        void OnDestroy()
        {
            SetDeferredEnvironment(null);
            if(finish){finish.nativeBegin=null;finish.nativeProgress=null;finish.nativeRestore=null;}
            foreach(var a in animated)ReleaseMaterial(a);animated.Clear();sharedGhosts.Clear();
        }
    }
}
