using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace SwanJourney.Editor
{
    internal static class SwanXrValleyLocomotiveArt
    {
        const string Folder="Assets/SwanJourney/Branding/ValleyHeadlamps/";
        const float BeamDip=9f;
        internal static void Apply(SteamJourney j,SteamJourney original,MesaDayNightV13 sky)
        {
            var weather=original.GetComponent<MesaAtmosphereV14>();
            if(!weather || weather.lamps.Length!=2)throw new InvalidOperationException("Original pair of locomotive lamps missing");
            var unit=j.valleyTrain.vehicles[0].gameObject.AddComponent<SwanValleyHeadlamps>();
            j.valleyHeadlamps=unit;unit.journey=j;unit.sky=sky;
            unit.litParts=new GameObject("Headlamps on");
            unit.litParts.transform.SetParent(unit.transform,false);
            unit.spots=new Light[2];unit.lenses=new MeshRenderer[2];unit.beams=new MeshRenderer[2];
            var lensMat=MaterialAsset("Warm headlamp lens","Universal Render Pipeline/Unlit");
            lensMat.SetFloat("_Surface",1f);lensMat.SetFloat("_ZWrite",0f);
            lensMat.SetFloat("_SrcBlend",(int)BlendMode.SrcAlpha);lensMat.SetFloat("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);
            lensMat.SetFloat("_SrcBlendAlpha",(int)BlendMode.One);lensMat.SetFloat("_DstBlendAlpha",(int)BlendMode.OneMinusSrcAlpha);
            lensMat.SetOverrideTag("RenderType","Transparent");lensMat.renderQueue=(int)RenderQueue.Transparent;
            lensMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");EditorUtility.SetDirty(lensMat);
            var beamMat=MaterialAsset("Soft headlamp scatter","SwanJourney/XR Headlamp Scatter");
            var beamMesh=BeamMesh();
            for(int i=0;i<2;i++)
            {
                var src=weather.lamps[i].transform;
                var socket=new GameObject("Forward lamp "+i).transform;socket.SetParent(unit.litParts.transform,false);
                var point=original.engine.locomotive.InverseTransformPoint(src.position);
                var dir=original.engine.locomotive.InverseTransformDirection(src.forward);
                socket.localPosition=j.trainAnchor.InverseTransformPoint(j.engine.locomotive.TransformPoint(point));
                var localDirection=j.trainAnchor.InverseTransformDirection(j.engine.locomotive.TransformDirection(dir));
                socket.localRotation=Quaternion.LookRotation(localDirection,Vector3.up)*Quaternion.Euler(5,0,0);
                var aim=new GameObject("Track illumination").transform;
                aim.SetParent(socket,false);aim.localRotation=Quaternion.Euler(BeamDip,0,0);
                var light=aim.gameObject.AddComponent<Light>();unit.spots[i]=light;
                light.type=LightType.Spot;light.shadows=LightShadows.None;light.innerSpotAngle=18;light.spotAngle=50;
                light.color=new Color(1,.85f,.64f);light.intensity=0;light.range=.2f;
                var face=GameObject.CreatePrimitive(PrimitiveType.Sphere);face.name="Warm lens";
                Object.DestroyImmediate(face.GetComponent<Collider>());face.transform.SetParent(socket,false);
                face.transform.localScale=new Vector3(.23f,.23f,.035f);
                unit.lenses[i]=face.GetComponent<MeshRenderer>();SetSurface(unit.lenses[i],lensMat);
                var beam=new GameObject("Visible headlight beam",typeof(MeshFilter),typeof(MeshRenderer));
                beam.transform.SetParent(socket,false);beam.GetComponent<MeshFilter>().sharedMesh=beamMesh;
                unit.beams[i]=beam.GetComponent<MeshRenderer>();SetSurface(unit.beams[i],beamMat);
            }
            unit.litParts.SetActive(false);

            var atmosphere=j.GetComponent<MesaAtmosV16>();
            var driving=j.valleyTrain.drivingChimney;
            driving.enabled=false;
            var go=new GameObject("V16 valley exhaust in tabletop space");
            go.transform.SetParent(j.sandboxRoot.transform,false);
            var trail=go.AddComponent<ParticleSystem>();trail.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=trail.main;main.maxParticles=42;main.loop=true;main.playOnAwake=true;main.startSpeed=0;
            main.startLifetime=1;main.gravityModifier=0;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
            main.simulationSpace=ParticleSystemSimulationSpace.Custom;main.customSimulationSpace=j.spatialRoot;
            var emission=trail.emission;emission.enabled=false;var shape=trail.shape;shape.enabled=false;
            var sheet=trail.textureSheetAnimation;sheet.enabled=true;sheet.numTilesX=2;sheet.numTilesY=2;
            sheet.frameOverTime=0;sheet.startFrame=new ParticleSystem.MinMaxCurve(0,.999f);
            var r=trail.GetComponent<ParticleSystemRenderer>();
            SetSurface(r,driving.rollingPlume.GetComponent<Renderer>().sharedMaterial);
            r.sortMode=ParticleSystemSortMode.Distance;atmosphere.valleyTrail=trail;
        }
        static Material MaterialAsset(string name,string shaderName)
        {
            string path=Folder+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat){var shader=Shader.Find(shaderName);if(!shader)throw new InvalidOperationException(shaderName);
                mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}
            return mat;
        }
        static void SetSurface(Renderer r,Material m)
        {
            r.sharedMaterial=m;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;
        }
        static Mesh BeamMesh()
        {
            string path=Folder+"Headlamp beam strip.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var axis=Quaternion.Euler(BeamDip,0,0)*Vector3.forward;
            var end=axis*12f;
            if(mesh && mesh.vertexCount==8 && (mesh.vertices[2]-(end-2.55f*Vector3.right)).sqrMagnitude<1e-8f)return mesh;
            bool create=!mesh;
            if(create)mesh=new Mesh{name="Headlamp beam and lens halo"};
            mesh.vertices=new[]{new Vector3(-.09f,0,0),new Vector3(.09f,0,0),end-2.55f*Vector3.right,end+2.55f*Vector3.right,
                new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(-1,1,0),new Vector3(1,1,0)};
            mesh.normals=new[]{axis,axis,axis,axis,Vector3.forward,Vector3.forward,Vector3.forward,Vector3.forward};
            mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one,new Vector2(0,2),new Vector2(1,2),new Vector2(0,3),new Vector2(1,3)};
            mesh.triangles=new[]{0,2,1,1,2,3,4,6,5,5,6,7};
            mesh.bounds=new Bounds(end*.5f,new Vector3(5.2f,7.2f,15f));
            if(create)AssetDatabase.CreateAsset(mesh,path);else EditorUtility.SetDirty(mesh);
            return mesh;
        }
    }
}
