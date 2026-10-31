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
        public Material scanMaterial, goldMaterial, rippleMaterial;
        public int NativeAnimatedRenderers => animated.Count;
        public int NativeWindowCount => windows == null ? 0 : windows.Where(r=>r).Distinct().Count();
        public int NativeAnimatedWindowCount => windows == null ? 0 : windows.Where(r=>r).Distinct().Count(r=>animated.Any(a=>a.renderer==r));
        public bool ScanActive => scanning;
        public bool GoldActive => finishing;
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
        }
        sealed class SweepEntry
        {
            public Renderer renderer;
            public Material[] original, effect;
            public bool effectApplied;
            public float coordinate;
            public void ShowEffect(bool show)
            {
                if (show == effectApplied || !renderer) return;
                renderer.sharedMaterials = show ? effect : original;
                effectApplied = show;
            }
        }
        readonly List<Animated> animated = new List<Animated>();
        readonly Dictionary<Material, JourneyNativeMaterial> lookup = new Dictionary<Material, JourneyNativeMaterial>();
        readonly HashSet<Renderer> seen = new HashSet<Renderer>();
        readonly List<SweepEntry> scan = new List<SweepEntry>(), gold = new List<SweepEntry>();
        readonly List<GameObject> goldRings = new List<GameObject>();
        MaterialPropertyBlock block;
        JourneySceneArrival arrival;
        StudioAssemblyFinishV9 finish;
        StudioSpatialRefineV19 refinement;
        StudioWaterImpulseV10 waterImpulses;
        MesaDayNightV13 sky;
        Renderer[] windows;
        Transform scanRing, waterRing;
        float discoverUntil, nextDiscover, waveAge = 10;
        int impactCount;
        bool scanning, finishing, lastMode;
        Bounds scanBounds;
        float goldMin, goldMax;
        Material rippleInstance;

        void Awake()
        {
            if (!journey) journey = GetComponent<SteamJourney>();
            if (!workbench) workbench = transform;
            DisableReplacedFlameEmitter();
            block = new MaterialPropertyBlock();
            foreach (var m in materials) if (m != null && m.material) lookup[m.material] = m;
            arrival = Find<JourneySceneArrival>();
            finish = Find<StudioAssemblyFinishV9>();
            refinement = Find<StudioSpatialRefineV19>();
            waterImpulses = Find<StudioWaterImpulseV10>();
            sky = Find<MesaDayNightV13>();
            windows = refinement ? refinement.passengerWindows : Array.Empty<Renderer>();
            if (arrival) { arrival.nativeBegin = BeginArrival; arrival.nativeProgress = ArrivalProgress; arrival.nativeCancel = EndArrival; }
            if (finish) { finish.nativeBegin = BeginGold; finish.nativeProgress = GoldProgress; finish.nativeRestore = EndGold; }
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
                // Restore pending reveal materials before releasing the private
                // animated copies that those reveal entries may refer to.
                bool removedScan = false;
                for (int i = scan.Count - 1; i >= 0; i--)
                {
                    var entry = scan[i];
                    if (entry.renderer && !Under(entry.renderer, previous)) continue;
                    entry.ShowEffect(false);
                    scan.RemoveAt(i); removedScan = true;
                }
                if (removedScan && scan.Count == 0)
                {
                    scanning = false;
                    if (scanRing) scanRing.gameObject.SetActive(false);
                }
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
                    if (entry.instance) Destroy(entry.instance);
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
                var instance = new Material(source) { name = source.name + " · live native" };
                instance.EnableKeyword("_EMISSION");
                var all = r.sharedMaterials;
                for (int i = 0; i < all.Length; i++) if (all[i] == source) all[i] = instance;
                r.sharedMaterials = all;
                animated.Add(new Animated { renderer = r, definition = definition, instance = instance, originalEmission = source.GetColor("_EmissionColor") });
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
                if (!a.hasApplied || !a.wasVisible || !a.appliedColor.Equals(color) || !a.appliedEmission.Equals(emission))
                {
                    a.instance.color = color;
                    a.instance.SetColor("_EmissionColor", emission);
                    a.appliedColor = color; a.appliedEmission = emission;
                    a.hasApplied = true;
                    LastMaterialWrites += 2;
                }
                a.wasVisible = true;
                // Native Standard material updates are reliable; custom MPB names are not a shader port.
                if (hasProperties) { r.SetPropertyBlock(null); LastPropertyBlocksConsumed++; }
            }
            WaterFeedback();
        }

        void WaterFeedback()
        {
            if (!waterImpulses || !journey.waterSurface || !ringMesh || !rippleMaterial) return;
            if (!waterRing)
            {
                rippleInstance = new Material(rippleMaterial);
                waterRing = MakeRing("Native water contact ripple", journey.waterSurface, rippleInstance);
                waterRing.gameObject.SetActive(false);
            }
            if (impactCount != waterImpulses.ImpactCount) { impactCount = waterImpulses.ImpactCount; waveAge = 0; }
            waveAge += Time.deltaTime;
            bool visible = waveAge < 1.1f && !journey.inSandbox && journey.cutaway && journey.modules[1].installed;
            if(waterRing.gameObject.activeSelf!=visible)waterRing.gameObject.SetActive(visible);
            if (!visible) return;
            waterRing.localPosition = new Vector3(0,.006f,0);
            waterRing.localRotation = Quaternion.identity;
            waterRing.localScale = Vector3.one * Mathf.Lerp(.04f,.85f,waveAge/1.1f);
            var c = rippleMaterial.color; c.a *= Mathf.Sin(Mathf.Clamp01(waveAge/1.1f)*Mathf.PI);
            rippleInstance.color = c;
        }

        void BeginArrival()
        {
            EndArrival(); EndGold();
            var roots = journey.inSandbox
                ? new[] { journey.sandboxRoot, deferredEnvironment ? deferredEnvironment.gameObject : null }
                : new[] { journey.workshopRoot, journey.previewRoot };
            bool first = true;
            foreach (var r in roots.Where(o=>o).SelectMany(o=>o.GetComponentsInChildren<MeshRenderer>()).Distinct())
            {
                if (!r.enabled || r.GetComponent<TMPro.TMP_Text>()) continue;
                var original = r.sharedMaterials;
                if (original.Length == 0 || original.Any(m => !m || m.renderQueue >= 2500)) continue;
                var bounds = LocalBounds(r, workbench);
                if (first) {scanBounds=bounds;first=false;} else scanBounds.Encapsulate(bounds);
                scan.Add(new SweepEntry {renderer=r,original=original,effect=Enumerable.Repeat(scanMaterial,original.Length).ToArray(),coordinate=bounds.center.y});
            }
            scanning = scan.Count > 0;
            if (scanning && ringMesh)
            {
                if(!scanRing) scanRing = MakeRing("Native rising scene scan",workbench,scanMaterial);
                scanRing.gameObject.SetActive(true);
                scanRing.localScale = new Vector3(scanBounds.size.x*1.5f,1,scanBounds.size.z*1.5f);
            }
            ArrivalProgress(0);
        }

        void ArrivalProgress(float progress)
        {
            if (!scanning) return;
            float y = Mathf.Lerp(scanBounds.min.y-.02f,scanBounds.max.y+.02f,Mathf.SmoothStep(0,1,progress));
            foreach (var e in scan) e.ShowEffect(e.coordinate > y);
            if (scanRing) scanRing.localPosition=new Vector3(scanBounds.center.x,y,scanBounds.center.z);
        }
        void EndArrival()
        {
            foreach (var e in scan) e.ShowEffect(false);
            scan.Clear();scanning=false;
            if(scanRing)scanRing.gameObject.SetActive(false);
        }

        void BeginGold(Transform train)
        {
            EndGold(); if(!train||!goldMaterial)return;
            foreach(var r in train.GetComponentsInChildren<MeshRenderer>())
            {
                var mats=r.sharedMaterials;
                if(!r.enabled||mats.Length==0||mats.Any(m=>!m||m.renderQueue>=2500)||r.GetComponent<TMPro.TMP_Text>())continue;
                var p=train.InverseTransformPoint(r.bounds.center);
                gold.Add(new SweepEntry {renderer=r,original=mats,effect=Enumerable.Repeat(goldMaterial,mats.Length).ToArray(),coordinate=p.x-p.y*.35f});
            }
            finishing=gold.Count>0;
            if(finishing){goldMin=gold.Min(e=>e.coordinate);goldMax=gold.Max(e=>e.coordinate);}
            if(goldRings.Count==0 && ringMesh && scanMaterial && journey.engine && journey.engine.sides!=null)
                foreach(var side in journey.engine.sides) foreach(var wheel in side.wheels)
                {
                    var ring=MakeRing("Native wheel lock glint",wheel,scanMaterial);
                    ring.localPosition=Vector3.zero;ring.localRotation=Quaternion.Euler(90,0,0);ring.localScale=Vector3.one*.45f;
                    goldRings.Add(ring.gameObject);
                }
        }
        void GoldProgress(float progress)
        {
            if(!finishing)return;
            float width=Mathf.Max(.3f,(goldMax-goldMin)*.20f);
            float head=Mathf.Lerp(goldMin-width,goldMax+width,progress);
            foreach(var e in gold) e.ShowEffect(Mathf.Abs(e.coordinate-head)<width);
            bool visible=progress>.15f&&progress<.85f;
            foreach(var r in goldRings)if(r&&r.activeSelf!=visible)r.SetActive(visible);
        }
        void EndGold()
        {
            foreach(var e in gold) e.ShowEffect(false);
            gold.Clear();finishing=false;foreach(var r in goldRings)if(r)r.SetActive(false);
        }
        Transform MakeRing(string label,Transform parent,Material material)
        {
            var go=new GameObject(label,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=ringMesh;var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;return go.transform;
        }
        static Bounds LocalBounds(Renderer r,Transform root)
        {
            var mesh=r.GetComponent<MeshFilter>();var b=mesh&&mesh.sharedMesh?mesh.sharedMesh.bounds:new Bounds(Vector3.zero,Vector3.one);
            var matrix=root.worldToLocalMatrix*r.localToWorldMatrix;var result=new Bounds(matrix.MultiplyPoint3x4(b.center),Vector3.zero);
            for(int i=0;i<8;i++)result.Encapsulate(matrix.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));return result;
        }
        void OnDisable() {EndArrival();EndGold();if(waterRing)waterRing.gameObject.SetActive(false);}
        void OnDestroy()
        {
            SetDeferredEnvironment(null);
            if(arrival){arrival.nativeBegin=null;arrival.nativeProgress=null;arrival.nativeCancel=null;}
            if(finish){finish.nativeBegin=null;finish.nativeProgress=null;finish.nativeRestore=null;}
            foreach(var a in animated)if(a.instance)Destroy(a.instance);if(rippleInstance)Destroy(rippleInstance);
        }
    }
}
