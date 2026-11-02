using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SwanJourney
{
    public sealed class SwanValleyHeadlamps : MonoBehaviour
    {
        public SteamJourney journey;
        public MesaDayNightV13 sky;
        public GameObject litParts;
        public Light[] spots;
        public MeshRenderer[] lenses, beams;
        public bool Lit => litParts.activeSelf && isActiveAndEnabled;
        MaterialPropertyBlock block;
        float previousScale=-1;
        int previousLevel=-1;
        UniversalRenderPipelineAsset lightingPipeline;
        int previousLightLimit;
        void OnEnable(){previousScale=-1;previousLevel=-1;}
        void OnDisable(){RestoreLightLimit();}
        void RestoreLightLimit()
        {
            if(lightingPipeline && lightingPipeline.maxAdditionalLightsCount==4)
                lightingPipeline.maxAdditionalLightsCount=previousLightLimit;
            lightingPipeline=null;
            previousLevel=-1;
        }
        void LateUpdate()
        {
            bool on=journey.inSandbox && journey.HeadlampsSelected;
            if(litParts.activeSelf!=on)litParts.SetActive(on);
            if(!on){if(lightingPipeline)RestoreLightLimit();return;}
            float scale=Mathf.Abs(spots[0].transform.lossyScale.z);
            int level=Mathf.RoundToInt((1-sky.Daylight)*24);
            if(scale==previousScale && level==previousLevel)return;
            previousScale=scale;previousLevel=level;
            // Two environment lights share Forward's per-object list with the two short-range spots.
            var pipeline=UniversalRenderPipeline.asset;
            if(pipeline && pipeline.maxAdditionalLightsCount<4 && !lightingPipeline)
            {
                lightingPipeline=pipeline;previousLightLimit=pipeline.maxAdditionalLightsCount;
                pipeline.maxAdditionalLightsCount=4;
            }
            float night=level/24f;
            foreach(var spot in spots)
            {
                spot.range=14f*scale;spot.intensity=Mathf.Lerp(11f,155f,night)*scale*scale;
            }
            if(block==null)block=new MaterialPropertyBlock();
            block.Clear();block.SetColor("_BaseColor",Color.Lerp(new Color(.94f,.72f,.36f),new Color(1,.87f,.56f),night));
            foreach(var lens in lenses)lens.SetPropertyBlock(block);
            block.Clear();block.SetFloat("_Density",Mathf.Lerp(.025f,.23f,night));
            foreach(var beam in beams)beam.SetPropertyBlock(block);
        }
    }
}
