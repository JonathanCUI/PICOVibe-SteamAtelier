using System;
using UnityEngine;

namespace SwanJourney
{
    // Bakes stay in local UV space as the player moves and scales the exhibit.
    [DefaultExecutionOrder(3970)]
    public sealed class SwanCanyonBakedLighting : MonoBehaviour
    {
        [Serializable] public class Surface
        {
            public MeshRenderer renderer;
            public Mesh originalMesh, bakedMesh;
            public Material[] originalMaterials, bakedMaterials;
        }
        public Surface[] surfaces;
        public float[] lightTimes;
        public Texture2D[] lightStates;
        public bool ReviewLightingDisabled {get;set;}
        public bool Applied {get;private set;}
        MesaDayNightV13 sky;
        int previous=-1;
        void Awake(){sky=FindObjectOfType<MesaDayNightV13>();SetReview(true);}
        void LateUpdate()
        {
            bool ready=Applied && !ReviewLightingDisabled && sky && lightStates!=null && lightStates.Length==4;
            Shader.SetGlobalFloat("_SwanCanyonLightEnabled",ready?1:0);
            if(!ready)return;
            Shader.SetGlobalColor("_SwanCanyonAmbientSky",RenderSettings.ambientSkyColor.linear);
            Shader.SetGlobalColor("_SwanCanyonAmbientHorizon",RenderSettings.ambientEquatorColor.linear);
            Shader.SetGlobalColor("_SwanCanyonAmbientGround",RenderSettings.ambientGroundColor.linear);
            float time=sky.timeOfDay;int a,b;float lo,hi;
            if(time<.20f || time>=.82f){a=b=3;lo=0;hi=1;}
            else if(time<lightTimes[0]){a=3;b=0;lo=.20f;hi=lightTimes[0];}
            else if(time<lightTimes[1]){a=0;b=1;lo=lightTimes[0];hi=lightTimes[1];}
            else if(time<lightTimes[2]){a=1;b=2;lo=lightTimes[1];hi=lightTimes[2];}
            else{a=2;b=3;lo=lightTimes[2];hi=.82f;}
            int pair=a*4+b;
            if(pair!=previous){Shader.SetGlobalTexture("_SwanCanyonLightA",lightStates[a]);Shader.SetGlobalTexture("_SwanCanyonLightB",lightStates[b]);previous=pair;}
            Shader.SetGlobalFloat("_SwanCanyonLightBlend",Mathf.SmoothStep(0,1,Mathf.InverseLerp(lo,hi,time)));
        }
        void OnDisable(){Shader.SetGlobalFloat("_SwanCanyonLightEnabled",0);previous=-1;}
        public void SetReview(bool baked)
        {
            if(surfaces==null)return;
            foreach(var surface in surfaces)
            {
                if(!surface.renderer)continue;
                surface.renderer.GetComponent<MeshFilter>().sharedMesh=baked?surface.bakedMesh:surface.originalMesh;
                surface.renderer.sharedMaterials=baked?surface.bakedMaterials:surface.originalMaterials;
            }
            Applied=baked;
        }
    }
}
