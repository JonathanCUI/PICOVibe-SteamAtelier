using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SwanJourney.Editor
{
    // Reuses the actual SurrounDead FX_Fire used by HOUND's prologue cooking fire.
    // Imported source assets are kept separate; adaptation only touches this clone.
    internal static class SwanJourneyCampfireBuild
    {
        internal const string Folder = "Assets/SwanJourney/Branding/CampfireSource";
        internal static void Configure(SteamJourney journey)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/FX_Fire.prefab");
            var original = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/FX_fire.mat");
            if (!prefab || !original) throw new InvalidOperationException("Missing original HOUND campfire assets");
            var mat = new Material(original) {name="HOUND campfire - native additive"};
            mat.shader = SwanJourneyUrp.Require("Universal Render Pipeline/Particles/Unlit");
            mat.SetFloat("_SoftParticlesEnabled",0);mat.SetFloat("_CameraFadingEnabled",0);
            mat.SetFloat("_DistortionEnabled",0);mat.SetFloat("_Cull",0);
            mat.SetFloat("_Surface",1);mat.SetFloat("_Blend",2);
            mat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);mat.SetFloat("_DstBlend",(float)BlendMode.One);
            mat.SetFloat("_ZWrite",0);mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType","Transparent");mat.renderQueue=3000;
            var saved=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/FurnaceCampfire.mat");
            if(saved){EditorUtility.CopySerialized(mat,saved);UnityEngine.Object.DestroyImmediate(mat);}else{saved=mat;AssetDatabase.CreateAsset(saved,Folder+"/FurnaceCampfire.mat");}
            EditorUtility.SetDirty(saved);
            var texture=(TextureImporter)AssetImporter.GetAtPath(Folder+"/FX_fire.png");
            texture.maxTextureSize=512;texture.alphaIsTransparency=true;texture.mipmapEnabled=true;
            texture.wrapMode=TextureWrapMode.Clamp;
            var android=texture.GetPlatformTextureSettings("Android");android.overridden=true;android.maxTextureSize=512;android.format=TextureImporterFormat.ASTC_4x4;texture.SetPlatformTextureSettings(android);texture.SaveAndReimport();

            if(journey.furnaceParticles)UnityEngine.Object.DestroyImmediate(journey.furnaceParticles.gameObject);
            if(journey.pausedFurnace)UnityEngine.Object.DestroyImmediate(journey.pausedFurnace.gameObject);
            var root=UnityEngine.Object.Instantiate(prefab);
            root.name="HOUND campfire in furnace";
            root.transform.SetParent(journey.flameVolume.transform.parent,false);
            root.transform.localPosition=journey.flameVolume.transform.localPosition+Vector3.up*.08f;
            root.transform.localRotation=Quaternion.Euler(-90,0,0);
            root.transform.localScale=Vector3.one*.95f;
            var system=root.GetComponent<ParticleSystem>();
            system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach(var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                if(ps.name=="smoke") {UnityEngine.Object.DestroyImmediate(ps.gameObject);continue;}
                var main=ps.main;main.simulationSpace=ParticleSystemSimulationSpace.Local;
                main.scalingMode=ParticleSystemScalingMode.Hierarchy;
                main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
                main.playOnAwake=true;
                // Unity gravity is in world units, unlike the scaled emitter and
                // starting velocity. Keep the original campfire's proportions on
                // the miniature train instead of sending embers above the boiler.
                main.gravityModifierMultiplier*=Mathf.Abs(root.transform.lossyScale.x);
                // Preserve original flame/lifetime/color/size curves. Only bound
                // density for a miniature furnace; sparks are deliberately sparse.
                var e=ps.emission;
                if(ps==system){main.maxParticles=48;e.rateOverTime=16;}
                else if(ps.name=="core"){main.maxParticles=32;e.rateOverTime=16;}
                else if(ps.name=="base"){main.maxParticles=24;e.rateOverTime=12;}
                else if(ps.name=="ember"){main.maxParticles=4;e.rateOverTime=2;}
                else throw new InvalidOperationException("Unexpected source campfire layer: "+ps.name);
                var r=ps.GetComponent<ParticleSystemRenderer>();r.sharedMaterial=saved;
                r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
                r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;
            }
            journey.furnaceParticles=system;
            BakePause(journey,saved);
            system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);root.SetActive(false);
            journey.flameVolume.gameObject.SetActive(false);journey.flameVolume.enabled=false;
            EditorUtility.SetDirty(journey);
        }

        static void BakePause(SteamJourney journey,Material material)
        {
            var root=journey.furnaceParticles;
            var systems=root.GetComponentsInChildren<ParticleSystem>(true);
            foreach(var p in systems){p.useAutoRandomSeed=false;p.randomSeed=(uint)(71+p.name.Length*13);}
            root.Simulate(2.3f,true,true,true);
            var positions=new List<Vector3>();var uvs=new List<Vector2>();var colors=new List<Color32>();var indices=new List<int>();
            foreach(var ps in systems)
            {
                if (ps.name=="ember") continue; // Pause freezes the flame body, not airborne sparks.
                var particles=new ParticleSystem.Particle[ps.main.maxParticles];int count=ps.GetParticles(particles);
                for(int i=0;i<count;i++)
                {
                    var p=particles[i];Color color=p.GetCurrentColor(ps);float size=p.GetCurrentSize(ps);
                    if(color.a<.06f||size<.025f)continue;
                    var centre=root.transform.localRotation*root.transform.InverseTransformPoint(ps.transform.TransformPoint(p.position));
                    for(int plane=0;plane<2;plane++)
                    {
                        float angle=(plane*90+i*23)*Mathf.Deg2Rad;
                        var right=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*size*.5f;var up=Vector3.up*size*.5f;int n=positions.Count;
                        positions.AddRange(new[]{centre-right-up,centre+right-up,centre+right+up,centre-right+up});
                        uvs.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});
                        colors.AddRange(new Color32[]{color,color,color,color});indices.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
                    }
                }
            }
            if(positions.Count==0)throw new InvalidOperationException("Campfire pause sampling produced no particles");
            var mesh=new Mesh{name="HOUND campfire paused particle sample"};mesh.SetVertices(positions);mesh.SetUVs(0,uvs);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            string path=Folder+"/CampfirePaused.asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved){EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);}else{saved=mesh;AssetDatabase.CreateAsset(saved,path);}
            var go=new GameObject("Paused HOUND campfire",typeof(MeshFilter),typeof(MeshRenderer));
            go.transform.SetParent(root.transform.parent,false);go.transform.localPosition=root.transform.localPosition;go.transform.localScale=root.transform.localScale;
            go.GetComponent<MeshFilter>().sharedMesh=saved;var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;
            r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;
            journey.pausedFurnace=r;go.SetActive(false);EditorUtility.SetDirty(saved);
            Debug.Log("HOUND_CAMPFIRE layers="+systems.Length+" maxParticles="+systems.Sum(p=>p.main.maxParticles)+" pauseTriangles="+indices.Count/3+" pauseBounds="+saved.bounds);
        }
    }
}
