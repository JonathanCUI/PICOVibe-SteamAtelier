using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SwanJourney
{
    // The existing completion coroutine owns timing, input lock and cancellation.
    // No Update, per-ribbon objects, particle collisions or extra lighting.
    [DefaultExecutionOrder(10010)]
    public sealed class SwanAssemblyCelebration : MonoBehaviour
    {
        public SteamJourney journey;
        public Transform frontReference;
        public Shader scanShader, paperShader;
        public const float Duration = 4f;
        public const int RibbonCount = 14, StripCount = 24;
        public bool Active { get; private set; }
        public int Plays { get; private set; }
        public int BoundSurfaces { get; private set; }
        public int MaterialCount => variants.Count;
        public int PaperTriangles => paperMesh ? (int)paperMesh.GetIndexCount(0)/3 : 0;
        public bool PaperVisible => paperRenderer && paperRenderer.enabled;
        public float Seconds { get; private set; }
        public float LastSetupMs { get; private set; }
        public bool SuppressForReview { get; set; }
        public Bounds TrainBounds { get; private set; }
        public bool IdleClean => !Active && BoundSurfaces == 0 && !PaperVisible;

        sealed class Binding
        {
            public MeshRenderer renderer;
            public Material[] original, temporary;
            public bool bound;
        }
        readonly List<Binding> bindings = new List<Binding>();
        readonly Dictionary<Material, Material> variants = new Dictionary<Material, Material>();
        StudioAssemblyFinishV9 finish;
        Transform train;
        Mesh paperMesh;
        MeshRenderer paperRenderer;
        Material paperMaterial;
        bool prepared;
        float frontX;
        static readonly int Space = Shader.PropertyToID("_SwanFinishToLocal");
        static readonly int Range = Shader.PropertyToID("_SwanFinishRange");
        static readonly int Clock = Shader.PropertyToID("_SwanFinishClock");
        static readonly int PaperSize = Shader.PropertyToID("_PaperSize");
        static readonly int PaperCentre = Shader.PropertyToID("_PaperCentre");

        IEnumerator Start()
        {
            finish = journey.GetComponent<StudioAssemblyFinishV9>();
            if (!finish || !scanShader || !paperShader) { Debug.LogError("XR_CELEBRATION_FAIL missing setup"); yield break; }
            finish.Duration = Duration;
            finish.nativeBegin = Begin;
            finish.nativeProgress = Advance;
            finish.nativeRestore = Restore;
            var bridge = journey.GetComponent<SwanJourneyVisualBridge>();
            if (bridge && bridge.completionBadge) bridge.completionBadge.gameObject.SetActive(false);
            // Cache after the entry scan has restored the actual display materials.
            yield return null;
            var arrival = journey.GetComponent<JourneySceneArrival>();
            while (arrival && arrival.Active) yield return null;
            Prepare();
        }

        public void Prepare()
        {
            if (prepared) return;
            train = journey.engine.locomotive;
            // The imported front cradle has a centred transform and offset vertices.
            // Locate its visible geometry rather than its pivot to choose scan direction.
            frontX = 0;
            int frontPieces = 0;
            if (frontReference)
                foreach (var r in frontReference.GetComponentsInChildren<MeshRenderer>(true))
                {
                    frontX += train.InverseTransformPoint(r.transform.TransformPoint(r.localBounds.center)).x;
                    frontPieces++;
                }
            frontX = frontPieces > 0 ? frontX/frontPieces : float.PositiveInfinity;
            foreach (var r in train.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter = r.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh || r.shadowCastingMode==ShadowCastingMode.ShadowsOnly) continue;
                var source = r.sharedMaterials;
                bool solid = source.Length > 0;
                foreach (var m in source)
                    if (!m || (m.shader.name != "Universal Render Pipeline/Lit" && m.shader.name != "SwanJourney/XR Structure Surface") || m.renderQueue >= 2450) solid = false;
                if (!solid) continue;
                var replacement = new Material[source.Length];
                for (int i = 0; i < source.Length; i++)
                {
                    if (!variants.TryGetValue(source[i], out var variant))
                    {
                        variant = new Material(scanShader) { name = source[i].name + " · completion scan" };
                        variant.CopyPropertiesFromMaterial(source[i]);
                        variant.renderQueue = -1;
                        variants.Add(source[i], variant);
                    }
                    replacement[i] = variant;
                }
                bindings.Add(new Binding { renderer = r, original = source, temporary = replacement });
            }
            BuildPaper();
            prepared = true;
            Debug.Log($"XR_CELEBRATION_READY cachedSurfaces={bindings.Count} sharedVariants={variants.Count} pieces={RibbonCount+StripCount} triangles={PaperTriangles} extraRenderers=1");
        }

        void Begin(Transform root)
        {
            Restore();
            Prepare();
            Plays++;
            if (SuppressForReview) return;
            float start = Time.realtimeSinceStartup;
            bool found = false;
            Bounds bounds = default;
            foreach (var b in bindings)
            {
                var r = b.renderer;
                if (!r || !r.enabled || r.forceRenderingOff || !r.gameObject.activeInHierarchy) continue;
                var box = r.localBounds;
                var matrix = train.worldToLocalMatrix * r.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = matrix.MultiplyPoint3x4(box.center + Vector3.Scale(box.extents,
                        new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                    if (!found) { bounds = new Bounds(corner,Vector3.zero); found=true; } else bounds.Encapsulate(corner);
                }
                r.sharedMaterials = b.temporary;
                b.bound = true;
                BoundSurfaces++;
            }
            if (!found) { Restore(); Debug.LogError("XR_CELEBRATION_FAIL no visible solid surfaces"); return; }
            TrainBounds = bounds;
            bool frontAtMax = frontX >= bounds.center.x;
            Shader.SetGlobalVector(Range,new Vector4(frontAtMax?bounds.max.x:bounds.min.x,
                frontAtMax?bounds.min.x:bounds.max.x,Mathf.Max(.1f,bounds.size.x),0));
            paperMaterial.SetVector(PaperSize,bounds.size);
            paperMaterial.SetVector(PaperCentre,bounds.center);
            // Include the full analytical flight envelope; no recalculation each frame.
            paperMesh.bounds = new Bounds(bounds.center + Vector3.up*bounds.size.y*.35f,
                new Vector3(bounds.size.x*1.8f,bounds.size.y*3.2f,bounds.size.z*4f));
            Active = true;
            Advance(0);
            LastSetupMs = (Time.realtimeSinceStartup-start)*1000;
            Debug.Log($"XR_CELEBRATION_BEGIN play={Plays} surfaces={BoundSurfaces} setupMs={LastSetupMs:F2} frontAtMax={frontAtMax} bounds={bounds}");
        }

        void Advance(float progress)
        {
            if (!Active) return;
            Seconds = Mathf.Clamp01(progress)*Duration;
            Shader.SetGlobalMatrix(Space,train.worldToLocalMatrix);
            Shader.SetGlobalVector(Clock,new Vector4(Seconds,0,0,0));
            paperRenderer.enabled = Seconds >= .80f && Seconds < Duration;
            // Return the original shaders as soon as the scan has ended.
            if (Seconds >= 2.05f && BoundSurfaces > 0) RestoreSurfaces();
        }

        void RestoreSurfaces()
        {
            foreach (var b in bindings)
            {
                if (!b.bound) continue;
                if (b.renderer) b.renderer.sharedMaterials = b.original;
                b.bound = false;
            }
            BoundSurfaces = 0;
        }

        void Restore()
        {
            RestoreSurfaces();
            if (paperRenderer) paperRenderer.enabled = false;
            Active = false;
            Shader.SetGlobalVector(Clock,new Vector4(10,0,0,0));
        }

        void BuildPaper()
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var uv = new List<Vector2>();
            var launch = new List<Vector4>();
            var velocity = new List<Vector4>();
            var motion = new List<Vector4>();
            var triangles = new List<int>();
            var random = new System.Random(17041);
            float R(float a,float b) => Mathf.Lerp(a,b,(float)random.NextDouble());
            // Bright paper pigments remain readable against both the room and the dark train.
            Color[] palette = { new Color(.23f,.94f,.82f),new Color(1f,.56f,.66f),
                new Color(.79f,.55f,.97f),new Color(1f,.89f,.51f) };
            for (int piece=0; piece<RibbonCount+StripCount; piece++)
            {
                bool ribbon = piece < RibbonCount;
                int segments = ribbon ? 22 : 4;
                int first = vertices.Count;
                float length = ribbon ? R(.052f,.085f) : R(.012f,.026f);
                float width = ribbon ? R(.0035f,.005f) : R(.0025f,.004f);
                float curl = ribbon ? R(3.5f,8.5f) : R(.25f,1.5f);
                float side = (piece&1)==0 ? -1 : 1;
                var spawn = new Vector4(side*R(.36f,.45f),R(-.20f,.02f),R(-.60f,.60f),R(.80f,.98f));
                var speed = new Vector4(side*R(.07f,.15f),R(1.20f,1.55f),R(-.30f,.30f),R(0,6.28f));
                var flutter = new Vector4(R(-2.4f,2.4f),R(1.5f,3.5f),R(0,6.28f),ribbon?1:0);
                var color = palette[piece%palette.Length].linear;
                for (int j=0; j<=segments; j++)
                {
                    float u=(float)j/segments, a=(u-.5f)*curl, radius=length/curl;
                    for(int edge=0;edge<2;edge++)
                    {
                        vertices.Add(new Vector3(radius*Mathf.Sin(a),(edge-.5f)*width,
                            radius*(1-Mathf.Cos(a))));
                        normals.Add(new Vector3(-Mathf.Sin(a),0,Mathf.Cos(a)));
                        colors.Add(color); uv.Add(new Vector2(u,edge));
                        launch.Add(spawn); velocity.Add(speed); motion.Add(flutter);
                    }
                    if(j<segments)
                    {
                        int k=first+j*2;
                        triangles.Add(k);triangles.Add(k+2);triangles.Add(k+1);
                        triangles.Add(k+1);triangles.Add(k+2);triangles.Add(k+3);
                    }
                }
            }
            paperMesh = new Mesh { name="38 pooled celebration paper pieces" };
            paperMesh.SetVertices(vertices);paperMesh.SetNormals(normals);paperMesh.SetColors(colors);
            paperMesh.SetUVs(0,uv);paperMesh.SetUVs(1,launch);paperMesh.SetUVs(2,velocity);paperMesh.SetUVs(3,motion);
            paperMesh.SetTriangles(triangles,0);
            var obj = new GameObject("Assembly celebration · curled paper");
            obj.transform.SetParent(train,false);
            obj.AddComponent<MeshFilter>().sharedMesh = paperMesh;
            paperRenderer = obj.AddComponent<MeshRenderer>();
            paperMaterial = new Material(paperShader) {name="Assembly paper · shared palette"};
            paperRenderer.sharedMaterial = paperMaterial;
            paperRenderer.shadowCastingMode = ShadowCastingMode.Off;
            paperRenderer.receiveShadows = false;
            paperRenderer.lightProbeUsage = LightProbeUsage.Off;
            paperRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            paperRenderer.enabled = false;
        }

        void OnDisable() { Restore(); }
        void OnDestroy()
        {
            Restore();
            if(finish && finish.nativeBegin == Begin)
            {finish.nativeBegin=null;finish.nativeProgress=null;finish.nativeRestore=null;}
            foreach(var material in variants.Values) if(material) Destroy(material);
            if(paperRenderer) Destroy(paperRenderer.gameObject);
            if(paperMesh) Destroy(paperMesh);
            if(paperMaterial) Destroy(paperMaterial);
        }
    }
}
