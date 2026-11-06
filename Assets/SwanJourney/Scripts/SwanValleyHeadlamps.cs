using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SwanJourney
{
    [DefaultExecutionOrder(3110)]
    public sealed class SwanValleyHeadlamps : MonoBehaviour
    {
        public SteamJourney journey;
        public MesaDayNightV13 sky;
        public GameObject litParts;
        public Light[] spots;
        public MeshRenderer[] lenses, beams;
        public float Brightness { get; private set; }
        public bool Lit => isActiveAndEnabled && Brightness > 0f;
        MaterialPropertyBlock block;
        float previousScale=-1;
        UniversalRenderPipelineAsset lightingPipeline;
        int previousLightLimit;
        void OnDisable()
        {
            Brightness=0f; previousScale=-1;
            if(litParts)litParts.SetActive(false);
            RestoreLightLimit();
        }
        void RestoreLightLimit()
        {
            if(lightingPipeline && lightingPipeline.maxAdditionalLightsCount==4)
                lightingPipeline.maxAdditionalLightsCount=previousLightLimit;
            lightingPipeline=null;
        }
        void LateUpdate()
        {
            float target=journey.inSandbox ? Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,.12f,sky.Daylight)) : 0f;
            float previous=Brightness;
            Brightness=journey.inSandbox ? Mathf.MoveTowards(previous,target,Mathf.Min(Time.unscaledDeltaTime,.1f)/2.4f) : 0f;
            bool on=Brightness>0f;
            if(litParts.activeSelf!=on)litParts.SetActive(on);
            if(!on){if(lightingPipeline)RestoreLightLimit();return;}
            float scale=Mathf.Abs(spots[0].transform.lossyScale.z);
            if(scale==previousScale && Brightness==previous)return;
            previousScale=scale;
            // Two environment lights share Forward's per-object list with the two short-range spots.
            var pipeline=UniversalRenderPipeline.asset;
            if(pipeline && pipeline.maxAdditionalLightsCount<4 && !lightingPipeline)
            {
                lightingPipeline=pipeline;previousLightLimit=pipeline.maxAdditionalLightsCount;
                pipeline.maxAdditionalLightsCount=4;
            }
            foreach(var spot in spots)
            {
                spot.range=14f*scale;spot.intensity=105f*Brightness*scale*scale;
            }
            if(block==null)block=new MaterialPropertyBlock();
            block.Clear();block.SetColor("_BaseColor",new Color(1f,.87f,.56f,Brightness));
            foreach(var lens in lenses)lens.SetPropertyBlock(block);
            block.Clear();block.SetFloat("_Density",.23f*Brightness);
            foreach(var beam in beams)beam.SetPropertyBlock(block);
        }
    }
}
