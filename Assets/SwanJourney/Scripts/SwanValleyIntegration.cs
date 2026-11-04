using UnityEngine;
using UnityEngine.Rendering;

namespace SwanJourney
{
    // Four soft footprints share one mesh. Tunnel shading stays in the existing opaque pass.
    [DefaultExecutionOrder(3950)]
    public sealed class SwanValleyIntegration : MonoBehaviour
    {
        public SteamJourney journey;
        public MesaDayNightV13 sky;
        public Texture3D tunnelField;
        public Vector3 fieldMin, fieldSize;
        public Material contactMaterial;
        public StudioWorkshopMood mood;
        public Texture originalWorkshopReflection,softWorkshopReflection;
        public Vector3[] vehicleCentres, vehicleSizes;
        public bool ReviewDisabled { get; set; }
        public int ContactTriangles => 8;
        public bool ContactVisible => surface && surface.enabled;
        public void SetReflectionReview(bool original)
        {
            mood.workshopReflection=original?originalWorkshopReflection:softWorkshopReflection;
            if(!journey.inSandbox)mood.ApplyImmediate();
        }
        Mesh mesh;
        MeshRenderer surface;
        readonly Vector3[] vertices = new Vector3[16];
        readonly Matrix4x4[] lastMatrices = new Matrix4x4[4];
        bool previousVisible;
        static readonly int Space=Shader.PropertyToID("_SwanTunnelSpace");
        static readonly int Field=Shader.PropertyToID("_SwanTunnelField");
        static readonly int Minimum=Shader.PropertyToID("_SwanTunnelMin");
        static readonly int Inverse=Shader.PropertyToID("_SwanTunnelInv");
        static readonly int Settings=Shader.PropertyToID("_SwanTunnelSettings");

        void Start()
        {
            var go=new GameObject("Train contact shading",typeof(MeshFilter),typeof(MeshRenderer));
            go.transform.SetParent(journey.spatialRoot,false);
            surface=go.GetComponent<MeshRenderer>();surface.sharedMaterial=contactMaterial;
            surface.shadowCastingMode=ShadowCastingMode.Off;surface.receiveShadows=false;
            surface.lightProbeUsage=LightProbeUsage.Off;surface.reflectionProbeUsage=ReflectionProbeUsage.Off;
            mesh=new Mesh{name="Four vehicle contact footprints"};mesh.MarkDynamic();
            var uv=new Vector2[16];var triangles=new int[24];
            for(int i=0;i<4;i++)
            {
                int v=i*4,t=i*6;
                uv[v]=Vector2.zero;uv[v+1]=Vector2.right;uv[v+2]=Vector2.up;uv[v+3]=Vector2.one;
                triangles[t]=v;triangles[t+1]=v+2;triangles[t+2]=v+1;
                triangles[t+3]=v+1;triangles[t+4]=v+2;triangles[t+5]=v+3;
            }
            mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;
            mesh.bounds=new Bounds(new Vector3(0,2,0),new Vector3(35,8,35));
            go.GetComponent<MeshFilter>().sharedMesh=mesh;surface.enabled=false;
            Shader.SetGlobalTexture(Field,tunnelField);
            Shader.SetGlobalVector(Minimum,fieldMin);
            Shader.SetGlobalVector(Inverse,new Vector3(1/fieldSize.x,1/fieldSize.y,1/fieldSize.z));
        }

        void LateUpdate()
        {
            bool visible=!ReviewDisabled && journey.inSandbox && journey.valleyTrain.Driving &&
                !journey.SwanSceneLoading && !journey.SwanTransitionActive;
            Shader.SetGlobalVector(Settings,new Vector4(visible?1:0,sky?sky.Daylight:1,0,0));
            if(!surface)return;
            surface.enabled=visible;
            if(!visible){previousVisible=false;return;}
            Shader.SetGlobalMatrix(Space,journey.SpatialWorldToLocal);
            bool changed=!previousVisible;
            for(int i=0;i<4;i++)
            {
                var tr=journey.valleyTrain.vehicles[i];
                var m=journey.SpatialWorldToLocal*tr.localToWorldMatrix;
                if(lastMatrices[i]==m && previousVisible)continue;
                changed=true;lastMatrices[i]=m;
                var center=m.MultiplyPoint3x4(vehicleCentres[i]);
                var along=m.MultiplyVector(Vector3.right*vehicleSizes[i].x*.5f);
                var across=m.MultiplyVector(Vector3.forward*vehicleSizes[i].z*.5f);
                // Track is level in authored space. Keep the card above the ballast,
                // below wheel centres; depth testing leaves raised rails unpainted.
                const float ground=1.61f;
                int v=i*4;
                vertices[v]=center-along-across;vertices[v+1]=center+along-across;
                vertices[v+2]=center-along+across;vertices[v+3]=center+along+across;
                for(int k=0;k<4;k++)vertices[v+k].y=ground;
            }
            if(changed)mesh.vertices=vertices;
            previousVisible=true;
        }
        void OnDisable(){Shader.SetGlobalVector(Settings,Vector4.zero);if(surface)surface.enabled=false;previousVisible=false;}
        void OnDestroy(){if(surface)Destroy(surface.gameObject);if(mesh)Destroy(mesh);}
    }
}
