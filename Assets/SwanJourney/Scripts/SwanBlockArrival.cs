using System.Collections.Generic;
using UnityEngine;

namespace SwanJourney
{
    [DefaultExecutionOrder(2160)]
    public sealed class SwanBlockArrival : MonoBehaviour
    {
        public SteamJourney journey;
        public JourneySceneArrival arrival;
        public SwanJourneySceneLoader loader;
        public Transform workshopConsole;
        public Shader sliceShader;
        public AudioSource scanCue;
        readonly List<Slice> exhibit = new List<Slice>(2);
        static readonly int Space = Shader.PropertyToID("_SwanScanToLocal");
        static readonly int Range = Shader.PropertyToID("_Slice");
        public int ExhibitGroups => exhibit.Count;

        void OnEnable()
        {
            arrival.nativeBegin=BeginExhibit;arrival.nativeProgress=Advance;arrival.nativeCancel=EndExhibit;
        }
        void Start()
        {
            // Hold progress at zero until the XR camera has been placed in the room.
            arrival.manual=true;arrival.Request();
        }
        void OnDisable()
        {
            if(arrival)
            {
                arrival.Cancel();
                arrival.nativeBegin=null;arrival.nativeProgress=null;arrival.nativeCancel=null;
            }
            EndExhibit();
        }
        void EndExhibit(){foreach(var slice in exhibit)slice.Restore();exhibit.Clear();}
        void BeginExhibit()
        {
            EndExhibit();
            if(scanCue)scanCue.Play();
            float start=Time.realtimeSinceStartup;
            // Slight cant follows the sloping instrument fascia from its near lip to its back edge.
            var surface=new Slice(this);var desk=new Slice(this,-.18f);var loose=new Slice(this);
            bool valley=journey.inSandbox;
            Transform console=valley?journey.consoleRoot.transform:workshopConsole;
            Transform root=(valley?journey.sandboxRoot:journey.workshopRoot).transform;
            var sky=valley?journey.GetComponent<MesaDayNightV13>()?.skyRoot:null;
            var plaque=valley?null:root.Find("Anchored teaching plaque V18");
            void Collect(Transform parent)
            {
                foreach(var r in parent.GetComponentsInChildren<Renderer>())
                {
                    // The loose piece can be several times as tall as the instrument desk.
                    // It must not stretch the desk's local reveal range.
                    var group=r.transform.IsChildOf(console)?desk:
                        (!valley && r.transform.IsChildOf(journey.previewRoot.transform)?loose:surface);
                    group.Add(r,(sky && r.transform.IsChildOf(sky.transform)) || (plaque && r.transform.IsChildOf(plaque)));
                }
            }
            Collect(root);
            if(valley)
            {
                var payload=loader.Environment;
                if(payload && !payload.transform.IsChildOf(root))Collect(payload.transform);
            }
            surface.Seal();desk.Seal();exhibit.Add(surface);exhibit.Add(desk);
            if(loose.Count>0){loose.Seal();exhibit.Add(loose);}
            Debug.Log($"XR_BLOCK_BEGIN valley={valley} exhibit={surface.Count} desk={desk.Count} loose={loose.Count} setupMs={(Time.realtimeSinceStartup-start)*1000:F2} cue={(scanCue && scanCue.isPlaying ? scanCue.clip.name : "off")}");
        }
        void Advance(float t)
        {
            Shader.SetGlobalMatrix(Space,journey.spatialRoot.worldToLocalMatrix);
            foreach(var slice in exhibit)slice.Set(t);
        }

        sealed class Slice
        {
            struct Binding {public Renderer r;public Material[] original,temporary;}
            struct Gate {public Renderer r;public float top;}
            readonly List<Binding> bindings=new List<Binding>();
            readonly List<Gate> gates=new List<Gate>();
            readonly Dictionary<Material,Material> variants=new Dictionary<Material,Material>();
            readonly HashSet<Renderer> included=new HashSet<Renderer>();
            readonly SwanBlockArrival owner;
            readonly float slope;
            float low=float.PositiveInfinity,high=float.NegativeInfinity;
            float maxWidth;
            Vector4 range;
            public int Count=>included.Count;
            public Slice(SwanBlockArrival owner,float slope=0){this.owner=owner;this.slope=slope;}
            public void Add(Renderer r,bool decoration)
            {
                if(!r.enabled || r.forceRenderingOff || !included.Add(r))return;
                var b=r.localBounds;
                var matrix=owner.journey.spatialRoot.worldToLocalMatrix*r.localToWorldMatrix;
                var center=matrix.MultiplyPoint3x4(b.center);
                float y=center.y+slope*center.z;
                float extent=Mathf.Abs(matrix.m10+slope*matrix.m20)*b.extents.x+
                    Mathf.Abs(matrix.m11+slope*matrix.m21)*b.extents.y+Mathf.Abs(matrix.m12+slope*matrix.m22)*b.extents.z;
                var originals=r.sharedMaterials;
                bool lit=!decoration && r is MeshRenderer && originals.Length>0;
                foreach(var m in originals)if(!m || m.renderQueue>=2450 || m.shader.name!="Universal Render Pipeline/Lit")lit=false;
                if(lit)
                {
                    low=Mathf.Min(low,y-extent);high=Mathf.Max(high,y+extent);
                    float width=Mathf.Abs(matrix.m00)*b.size.x+Mathf.Abs(matrix.m01)*b.size.y+Mathf.Abs(matrix.m02)*b.size.z;
                    maxWidth=Mathf.Max(maxWidth,width);
                    var temporary=new Material[originals.Length];
                    for(int i=0;i<originals.Length;i++)
                    {
                        var src=originals[i];
                        if(!variants.TryGetValue(src,out var mat))
                        {
                            mat=new Material(owner.sliceShader);mat.CopyPropertiesFromMaterial(src);
                            mat.renderQueue=-1;
                            if(src.IsKeywordEnabled("_NORMALMAP"))mat.EnableKeyword("_NORMALMAP");
                            if(src.IsKeywordEnabled("_METALLICSPECGLOSSMAP"))mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                            variants.Add(src,mat);
                        }
                        temporary[i]=mat;
                    }
                    r.sharedMaterials=temporary;bindings.Add(new Binding{r=r,original=originals,temporary=temporary});
                }
                else
                {
                    r.forceRenderingOff=true;
                    gates.Add(new Gate{r=r,top=decoration || r is ParticleSystemRenderer?float.PositiveInfinity:y+extent});
                }
            }
            public void Seal()
            {
                if(float.IsPositiveInfinity(low)){low=0;high=1;}
                float cell=Mathf.Max(.001f,(high-low)*.031f);
                range=new Vector4(low-2*cell,high+4*cell,cell,0);
                foreach(var mat in variants.Values)
                {
                    mat.SetFloat("_CellWidth",Mathf.Max(cell,maxWidth*.020f));
                    mat.SetFloat("_SliceSlope",slope);
                }
                gates.Sort((a,b)=>b.top.CompareTo(a.top));
                Set(0);
            }
            public void Set(float t)
            {
                range.w=Mathf.SmoothStep(0,1,t);
                foreach(var mat in variants.Values)mat.SetVector(Range,range);
                float cleared=Mathf.Lerp(range.x,range.y,range.w)-2.2f*range.z;
                for(int i=gates.Count-1;i>=0 && gates[i].top<=cleared;i--)
                {
                    if(gates[i].r)gates[i].r.forceRenderingOff=false;
                    gates.RemoveAt(i);
                }
            }
            public void Restore()
            {
                foreach(var b in bindings)
                {
                    if(!b.r)continue;
                    var slots=b.r.sharedMaterials;
                    for(int i=0;i<Mathf.Min(slots.Length,b.temporary.Length);i++)
                        if(slots[i]==b.temporary[i])slots[i]=b.original[i];
                    b.r.sharedMaterials=slots;
                }
                foreach(var g in gates)if(g.r)g.r.forceRenderingOff=false;
                foreach(var m in variants.Values)Destroy(m);
            }
        }
    }
}
