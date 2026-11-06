using System.Collections.Generic;
using UnityEngine;

namespace SwanJourney
{
    [DefaultExecutionOrder(10020)]
    public sealed class SwanPartSeatFeedback : MonoBehaviour
    {
        public SteamJourney journey;
        public Shader seatShader;
        public bool ReviewDisabled {get;set;}
        public int Plays {get;private set;}
        public bool Active=>bindings.Count>0;
        public int CachedMaterials=>variants.Count;
        readonly Dictionary<Material,Material> variants=new Dictionary<Material,Material>();
        readonly List<Binding> bindings=new List<Binding>();
        struct Binding {public MeshRenderer r;public Material[] original,temporary;}
        JourneyModule current;
        float age;
        static readonly int Pulse=Shader.PropertyToID("_SwanSeatPulse");
        void OnEnable(){if(journey)journey.PartSeated+=Begin;}
        void OnDisable(){if(journey)journey.PartSeated-=Begin;Restore();}
        public void Begin(JourneyModule module)
        {
            Restore();
            if(ReviewDisabled || journey.inSandbox || journey.Complete || journey.AutoAssembling || !seatShader)return;
            current=module;age=0;Plays++;
            foreach(var part in module.objects)
            {
                if(!part)continue;
                foreach(var r in part.GetComponentsInChildren<MeshRenderer>())
                {
                    if(!r.enabled || r.forceRenderingOff)continue;
                    var original=r.sharedMaterials;var temporary=(Material[])original.Clone();bool changed=false;
                    for(int i=0;i<original.Length;i++)
                    {
                        var src=original[i];
                        if(!src || (src.shader.name!="Universal Render Pipeline/Lit" && src.shader.name!="SwanJourney/XR Structure Surface") || src.renderQueue>=2450)continue;
                        if(!variants.TryGetValue(src,out var mat))
                        {
                            mat=new Material(seatShader);mat.CopyPropertiesFromMaterial(src);mat.renderQueue=-1;
                            variants.Add(src,mat);
                        }
                        temporary[i]=mat;changed=true;
                    }
                    if(changed){r.sharedMaterials=temporary;bindings.Add(new Binding{r=r,original=original,temporary=temporary});}
                }
            }
            Shader.SetGlobalFloat(Pulse,0);
        }
        void Update()
        {
            if(!Active)return;
            if(!current.installed || journey.inSandbox || journey.Complete || journey.SwanTransitionActive){Restore();return;}
            age+=Mathf.Min(Time.unscaledDeltaTime,.1f);
            if(age>=.52f){Restore();return;}
            float rise=Mathf.SmoothStep(0,1,age/.07f);
            float fall=1-Mathf.SmoothStep(0,1,Mathf.Max(0,age-.07f)/.45f);
            Shader.SetGlobalFloat(Pulse,rise*fall);
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
            bindings.Clear();current=null;Shader.SetGlobalFloat(Pulse,0);
        }
        void OnDestroy(){Restore();foreach(var m in variants.Values)Destroy(m);variants.Clear();}
    }
}
