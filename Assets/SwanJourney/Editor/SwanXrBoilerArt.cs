using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SwanJourney.Editor
{
    internal static class SwanXrBoilerArt
    {
        const string Folder="Assets/SwanJourney/Branding/BoilerAtmosphere/";
        static ParticleSystem.MinMaxCurve Range(float a,float b)=>new ParticleSystem.MinMaxCurve(a,b);
        static ParticleSystem.MinMaxCurve Curve(params Keyframe[] keys)=>new ParticleSystem.MinMaxCurve(1,new AnimationCurve(keys));

        internal static void Apply(SteamJourney j)
        {
            var vapour=SharedMaterial("RolledVapour",new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")));
            vapour.SetTexture("_BaseMap",Texture("RollingVapour",false));
            vapour.SetColor("_BaseColor",Color.white);
            vapour.SetFloat("_Surface",1);vapour.SetFloat("_Blend",0);
            vapour.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
            vapour.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            vapour.SetFloat("_SrcBlendAlpha",(float)BlendMode.One);
            vapour.SetFloat("_DstBlendAlpha",(float)BlendMode.OneMinusSrcAlpha);
            vapour.SetFloat("_ZWrite",0);vapour.SetFloat("_Cull",(float)CullMode.Off);
            vapour.SetFloat("_SoftParticlesEnabled",0);vapour.SetFloat("_CameraFadingEnabled",0);
            vapour.SetOverrideTag("RenderType","Transparent");vapour.renderQueue=3000;
            vapour.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            EditorUtility.SetDirty(vapour);

            var coal=SharedMaterial("HeatedCoal",new Material(j.emberRenderers[0].sharedMaterial));
            coal.SetTexture("_EmissionMap",Texture("EmberFissures",true));
            coal.EnableKeyword("_EMISSION");
            coal.SetColor("_EmissionColor",Color.black);
            coal.SetFloat("_Smoothness",.24f);
            coal.globalIlluminationFlags=MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(coal);
            var owner=j.gameObject.AddComponent<SwanXrBoilerEffects>();
            owner.journey=j;owner.coalTemplate=coal;j.xrBoilerEffects=owner;

            SpreadCampfire(j);
            var old=j.steam;
            var plume=NewEmitter("Boiler waterline condensation",old.transform,vapour);
            old.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
            var oldMain=old.main;oldMain.playOnAwake=false;
            var oldEmission=old.emission;oldEmission.enabled=false;
            old.GetComponent<ParticleSystemRenderer>().enabled=false;
            plume.transform.localPosition=new Vector3(0,.035f,.05f);
            j.steam=plume;
            ConfigureVapour(plume,false);

            foreach (var outlet in UnityEngine.Object.FindObjectsOfType<SwanJourneyChimneySmoke>(true))
            {
                if (outlet.gameObject.scene!=j.gameObject.scene) continue;
                var smoke=NewEmitter("Rolling exhaust from chimney lip",outlet.transform,vapour);
                smoke.transform.localPosition=Vector3.down*.085f;
                ConfigureVapour(smoke,true);
                outlet.rollingPlume=smoke;
                // The XR renderer uses a single emitter in place of the 32 crossed-card frames.
                foreach (var frame in outlet.frames) if (frame) UnityEngine.Object.DestroyImmediate(frame);
                outlet.frames=Array.Empty<GameObject>();
            }
        }

        static Texture2D Texture(string name,bool mask)
        {
            string path=Folder+name+".png";
            var imp=(TextureImporter)AssetImporter.GetAtPath(path);
            if (imp==null) throw new InvalidOperationException("Missing "+path);
            imp.alphaIsTransparency=!mask;imp.sRGBTexture=!mask;imp.mipmapEnabled=true;
            imp.wrapMode=mask?TextureWrapMode.Repeat:TextureWrapMode.Clamp;
            imp.maxTextureSize=mask?128:256;
            var a=imp.GetPlatformTextureSettings("Android");
            a.overridden=true;a.maxTextureSize=imp.maxTextureSize;a.format=TextureImporterFormat.ASTC_4x4;
            imp.SetPlatformTextureSettings(a);imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material SharedMaterial(string name,Material source)
        {
            string path=Folder+name+".mat";
            var result=AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result) { result.CopyPropertiesFromMaterial(source); UnityEngine.Object.DestroyImmediate(source); }
            else { source.name=name;AssetDatabase.CreateAsset(source,path); result=source; }
            return result;
        }

        static void SpreadCampfire(SteamJourney j)
        {
            var root=j.furnaceParticles;
            var fire=SharedMaterial("BankedCampfire",new Material(root.GetComponent<ParticleSystemRenderer>().sharedMaterial));
            fire.SetColor("_BaseColor",new Color(1.22f,.83f,.53f,.83f));
            fire.SetFloat("_SrcBlendAlpha",(float)BlendMode.One);
            fire.SetFloat("_DstBlendAlpha",(float)BlendMode.OneMinusSrcAlpha);
            EditorUtility.SetDirty(fire);
            foreach(var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var shape=ps.shape;
                shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Box;
                shape.scale=ps.name=="core"?new Vector3(.77f,.55f,.025f):new Vector3(.98f,.72f,.035f);
                shape.position=Vector3.zero;shape.rotation=Vector3.zero;
                var main=ps.main;
                if(ps.name!="ember") main.startSizeMultiplier*=1.20f;
                ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=fire;
            }
        }

        static ParticleSystem NewEmitter(string name,Transform parent,Material material)
        {
            var go=new GameObject(name);
            go.transform.SetParent(parent,false);
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
            var r=ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial=material;r.renderMode=ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;
            r.sortMode=ParticleSystemSortMode.Distance;
            return ps;
        }

        static void ConfigureVapour(ParticleSystem ps,bool chimney)
        {
            var m=ps.main;
            m.loop=true;m.playOnAwake=false;m.simulationSpace=ParticleSystemSimulationSpace.Local;
            m.scalingMode=ParticleSystemScalingMode.Hierarchy;m.startSpeed=0;m.gravityModifier=0;
            m.maxParticles=chimney?18:10;
            m.startLifetime=chimney?Range(2.25f,2.75f):Range(.95f,1.4f);
            m.startColor=chimney?new Color(.99f,.985f,.98f,.72f):new Color(.93f,.96f,1f,.54f);
            if(chimney) m.startSize=Range(.79f,1.10f);
            else
            {
                m.startSize3D=true;m.startSizeX=Range(.72f,1.02f);m.startSizeY=Range(.22f,.34f);m.startSizeZ=1;
            }
            m.startRotation=chimney?Range(-.6f,.6f):Range(-.13f,.13f);
            var shape=ps.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Box;
            shape.scale=chimney?new Vector3(.055f,.012f,.055f):new Vector3(2.38f,.015f,.22f);
            var e=ps.emission;e.enabled=true;e.rateOverTime=chimney?7.2f:0;
            var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.Local;
            // Use three curves of the same mode; Unity validates XYZ as a group.
            velocity.x=Curve(new Keyframe(0,0),new Keyframe(1,chimney?-.29f:.13f));
            velocity.y=Curve(new Keyframe(0,chimney?1.08f:.19f),new Keyframe(1,chimney?.54f:.065f));
            velocity.z=Curve(new Keyframe(0,0),new Keyframe(1,chimney?.045f:0f));
            var sz=ps.sizeOverLifetime;sz.enabled=true;
            sz.size=Curve(new Keyframe(0,.36f),new Keyframe(.42f,chimney?.77f:.87f),new Keyframe(1,1.20f));
            var spin=ps.rotationOverLifetime;spin.enabled=true;spin.z=chimney?Range(-.24f,.28f):Range(-.055f,.055f);
            var color=ps.colorOverLifetime;color.enabled=true;
            var g=new Gradient();
            g.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(chimney?.64f:0,0),new GradientAlphaKey(1,chimney?.07f:.20f),
                      new GradientAlphaKey(chimney?.73f:.68f,.54f),new GradientAlphaKey(0,1)});
            color.color=g;
            var sheet=ps.textureSheetAnimation;sheet.enabled=true;sheet.numTilesX=2;sheet.numTilesY=2;
            sheet.animation=ParticleSystemAnimationType.WholeSheet;sheet.frameOverTime=0;sheet.startFrame=Range(0,.999f);
        }
    }
}
