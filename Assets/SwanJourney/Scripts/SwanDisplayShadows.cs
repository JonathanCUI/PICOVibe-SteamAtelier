using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SwanJourney
{
    // One filtered shadow map, with separate opaque silhouettes for each inspection mode.
    // Run after the day/night controller, which changes which directional light is the sun.
    [DefaultExecutionOrder(3980)]
    public sealed class SwanDisplayShadows : MonoBehaviour
    {
        public SteamJourney journey;
        public StudioWorkshopMood mood;
        public Renderer[] workshopCasters, valleyCasters;
        public Renderer[] inspectionCasters;
        public bool ReviewDisabled {get;set;}
        public bool ReviewAoDisabled {get;set;}
        public bool Active {get;private set;}
        public int ActiveCasters {get;private set;}
        public int ActiveTriangles {get;private set;}
        Light current;
        UniversalRenderPipelineAsset pipeline;
        float originalDistance;
        int previousMode=-1;
        void Awake()
        {
            pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if(pipeline)originalDistance=pipeline.shadowDistance;
        }
        void LateUpdate()
        {
            Shader.SetGlobalFloat("_SwanStructureAoBlend",ReviewAoDisabled?0:1);
            bool show=journey && journey.Complete && !journey.CompletionActive &&
                !journey.SwanSceneLoading && !journey.SwanTransitionActive && !ReviewDisabled;
            // Canyon uses the completed miniature regardless of the workshop inspection setting.
            if(journey && journey.inSandbox && journey.valleyTrain && journey.valleyTrain.Driving)
                show=journey.Complete && !journey.SwanSceneLoading && !journey.SwanTransitionActive && !ReviewDisabled;
            int mode=!show?0:journey.inSandbox?2:journey.cutaway?(journey.inspectionMode==1?3:4):1;
            if(mode!=previousMode)
            {
                SetCasters(workshopCasters,mode==1);SetCasters(valleyCasters,mode==2);
                if(inspectionCasters!=null)for(int i=0;i<inspectionCasters.Length;i++)
                    if(inspectionCasters[i])inspectionCasters[i].enabled=mode==i+3;
                previousMode=mode;ActiveCasters=ActiveTriangles=0;
                var selected=mode==1?workshopCasters:mode==2?valleyCasters:mode>=3?inspectionCasters:null;
                if(selected!=null)foreach(var r in selected)if(r && r.enabled && r.gameObject.activeInHierarchy)
                {
                    ActiveCasters++;var f=r.GetComponent<MeshFilter>();
                    if(f && f.sharedMesh)for(int i=0;i<f.sharedMesh.subMeshCount;i++)ActiveTriangles+=(int)f.sharedMesh.GetIndexCount(i)/3;
                }
            }
            var next=mode==0?null:journey.inSandbox?RenderSettings.sun:mood.key;
            if(current && current!=next)current.shadows=LightShadows.None;
            current=next;Active=current;
            if(!current)return;
            current.shadows=LightShadows.Soft;current.shadowStrength=journey.inSandbox?.65f:.92f;
            current.shadowBias=.10f;current.shadowNormalBias=.12f;current.shadowNearPlane=.02f;
            if(!journey.inSandbox)RenderSettings.sun=current;
            if(pipeline && journey.view)
            {
                // Keep the atlas around the movable exhibit, not the entire passthrough room.
                var focus=journey.inSandbox?journey.ScenePointToWorld(new Vector3(0,3,-3)):journey.engine.locomotive.position;
                float radius=journey.inSandbox?journey.SceneVectorToWorld(Vector3.right*18).magnitude:
                    journey.engine.locomotive.TransformVector(Vector3.right*7).magnitude;
                pipeline.shadowDistance=Mathf.Clamp(Vector3.Distance(journey.view.transform.position,focus)+radius+.15f,.6f,8f);
            }
        }
        static void SetCasters(Renderer[] renderers,bool value)
        {
            if(renderers==null)return;
            foreach(var r in renderers)if(r){r.shadowCastingMode=ShadowCastingMode.ShadowsOnly;r.enabled=value;}
        }
        void OnDisable()
        {
            if(current)current.shadows=LightShadows.None;
            SetCasters(workshopCasters,false);SetCasters(valleyCasters,false);
            SetCasters(inspectionCasters,false);
            if(pipeline)pipeline.shadowDistance=originalDistance;
            current=null;Active=false;previousMode=-1;
        }
    }
}
