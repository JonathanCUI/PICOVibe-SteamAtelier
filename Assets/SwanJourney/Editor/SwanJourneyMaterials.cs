using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SwanJourney.Editor
{
    // Only generated assets and the derived Swan scene are changed. The partner's
    // saved scene, materials and raymarch shaders remain an intact desktop baseline.
    public static class SwanJourneyMaterials
    {
        public static SwanJourneyVisualBridge Configure(SteamJourney journey, Transform workbench, string generatedDirectory)
        {
            string folder=generatedDirectory+"/NativeVisuals";
            Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            var bridge=journey.GetComponent<SwanJourneyVisualBridge>()??journey.gameObject.AddComponent<SwanJourneyVisualBridge>();
            bridge.journey=journey;bridge.workbench=workbench;
            bridge.softVolume=Save(SoftVolume(),folder+"/SoftVolume.asset");
            bridge.flameVolume=Save(FlameVolume(),folder+"/FlameVolume.asset");
            bridge.cloudVolume=Save(CloudVolume(),folder+"/CloudVolume.asset");
            bridge.beamVolume=Save(BeamVolume(),folder+"/SoftBeam.asset");
            bridge.waterVolume=Save(WaterBody(),folder+"/BoilerWater.asset");
            bridge.jacketVolume=Save(WaterJacket(),folder+"/WaterJacket.asset");
            bridge.ringMesh=Save(Ring(),folder+"/FeedbackRing.asset");
            var alpha=MakeSoftTexture(folder);
            var flameAlpha=MakeFlameTexture(folder);
            var softMaps=new Dictionary<Texture2D,SoftParticleMaps>();
            SoftParticleMaps ParticleMaps(Texture2D texture)
            {
                if(!softMaps.TryGetValue(texture,out var maps))
                { maps=MakeSoftParticleMaps(texture,folder+"/SoftParticle_"+softMaps.Count.ToString("0000"));softMaps.Add(texture,maps); }
                return maps;
            }
            var map=new Dictionary<Material,Material>();
            var definitions=new List<JourneyNativeMaterial>();
            int number=0;
            Material Convert(Material source)
            {
                if(!source)return null;
                // The native TMP exporter needs its SDF/UNLIT font material, including
                // fallback sub-mesh fonts. A Standard conversion breaks generateFontMaterial.
                if(IsFontMaterial(source))return source;
                if(map.TryGetValue(source,out var existing))return existing;
                var kind=Classify(source);var color=ColorOf(source);
                switch(kind)
                {
                    case JourneyNativeMaterialKind.Cloud:color=new Color(.94f,.96f,1,1);break;
                    case JourneyNativeMaterialKind.Steam:color=new Color(.92f,.95f,.98f,.37f);break;
                    case JourneyNativeMaterialKind.Mist:color=new Color(.65f,.82f,.88f,.45f);break;
                    case JourneyNativeMaterialKind.Furnace:color=new Color(1,.40f,.05f,.85f);break;
                    case JourneyNativeMaterialKind.WaterSurface:color=new Color(.07f,.31f,.36f,.72f);break;
                    case JourneyNativeMaterialKind.WaterBody:case JourneyNativeMaterialKind.WaterJacket:color=new Color(.03f,.24f,.30f,.40f);break;
                    case JourneyNativeMaterialKind.Ghost:color=new Color(.93f,.96f,1,.16f);break;
                    case JourneyNativeMaterialKind.Beam:color=new Color(1,.84f,.62f,.18f);break;
                    case JourneyNativeMaterialKind.Halo:color=new Color(color.r,color.g,color.b,.55f);break;
                }
                var result=new Material(Shader.Find("Standard")){name="Swan V23 "+source.name,color=color};
                result.SetFloat("_Metallic",source.HasProperty("_Metallic")?source.GetFloat("_Metallic"):source.HasProperty("_MetallicPower")?source.GetFloat("_MetallicPower"):0);
                result.SetFloat("_Glossiness",source.HasProperty("_Smoothness")?source.GetFloat("_Smoothness"):source.HasProperty("_Glossiness")?source.GetFloat("_Glossiness"):source.HasProperty("_SmoothnessPower")?source.GetFloat("_SmoothnessPower"):.35f);
                string textureProperty=source.HasProperty("_BaseMap")?"_BaseMap":source.HasProperty("_MainTex")?"_MainTex":null;
                if(textureProperty!=null && source.GetTexture(textureProperty) is Texture2D)
                {
                    result.mainTexture=source.GetTexture(textureProperty);result.mainTextureScale=source.GetTextureScale(textureProperty);result.mainTextureOffset=source.GetTextureOffset(textureProperty);
                }
                if(source.shader && source.shader.name.Contains("MesaGround"))
                {
                    result.mainTexture=BakeGround(source,folder+"/Ground_"+number.ToString("0000")+".png");
                    result.mainTextureScale=Vector2.one;result.mainTextureOffset=Vector2.zero;result.SetFloat("_Glossiness",.1f);
                }
                bool soft=kind==JourneyNativeMaterialKind.Steam||kind==JourneyNativeMaterialKind.Mist||kind==JourneyNativeMaterialKind.Furnace||kind==JourneyNativeMaterialKind.Beam||kind==JourneyNativeMaterialKind.Halo;
                if(soft||(kind==JourneyNativeMaterialKind.Particle&&!result.mainTexture))result.mainTexture=alpha;
                if(kind==JourneyNativeMaterialKind.WaterBody||kind==JourneyNativeMaterialKind.WaterJacket||kind==JourneyNativeMaterialKind.WaterSurface)result.SetFloat("_Glossiness",.88f);
                CopyTexture(source,result,"_BumpMap","_NORMALMAP");CopyTexture(source,result,"_OcclusionMap",null);CopyTexture(source,result,"_MetallicGlossMap","_METALLICGLOSSMAP");CopyTexture(source,result,"_EmissionMap",null);
                if((soft||kind==JourneyNativeMaterialKind.Particle)&&kind!=JourneyNativeMaterialKind.Furnace && result.mainTexture is Texture2D sprite)
                {
                    var maps=ParticleMaps(sprite);result.mainTexture=maps.surface;result.SetTexture("_EmissionMap",maps.emission);
                    result.mainTextureScale=Vector2.one;result.mainTextureOffset=Vector2.zero;
                    result.SetTextureScale("_EmissionMap",Vector2.one);result.SetTextureOffset("_EmissionMap",Vector2.zero);
                    result.SetFloat("_Glossiness",0);result.SetFloat("_Metallic",0);
                }
                if(kind==JourneyNativeMaterialKind.Furnace)
                {
                    result.mainTexture=flameAlpha;result.mainTextureScale=Vector2.one;result.mainTextureOffset=Vector2.zero;
                    // Mask emissive RGB as well as alpha: a bright constant emission over
                    // an otherwise transparent card can reveal its rectangular boundary.
                    result.SetTexture("_EmissionMap",flameAlpha);result.SetFloat("_Glossiness",0);result.SetFloat("_Metallic",0);
                }
                if(source.HasProperty("_BumpScale"))result.SetFloat("_BumpScale",source.GetFloat("_BumpScale"));
                Color emission=source.HasProperty("_EmissionColor")?source.GetColor("_EmissionColor"):Color.black;
                // Amplify's rock shaders declare an unused white _EmissionColor.
                // Treating that declaration as real emissive light washes out the canyon at night.
                if(source.shader && (source.shader.name.Contains("LayeredRock")||source.shader.name.StartsWith("BK/")))emission=Color.black;
                if(kind==JourneyNativeMaterialKind.Halo)emission=new Color(color.r,color.g,color.b)*1.3f;
                if(kind==JourneyNativeMaterialKind.Ghost)emission=new Color(.18f,.20f,.22f);
                // Native shader features are selected from exported values. Keep the
                // masked emissive channel present before live intensity updates begin.
                if(soft && emission.maxColorComponent<.001f)emission=new Color(.001f,.001f,.001f);
                result.SetColor("_EmissionColor",emission);result.EnableKeyword("_EMISSION");result.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;
                bool transparent=kind!=JourneyNativeMaterialKind.Cloud&&(kind!=JourneyNativeMaterialKind.Surface||color.a<.98f||source.GetTag("RenderType",false)=="Transparent"||(source.HasProperty("_Surface")&&source.GetFloat("_Surface")>.5f));
                if(transparent)Transparent(result);
                bool foliage=source.shader&&(source.shader.name.Contains("VegetationLeaves")||source.shader.name.Contains("Grass")||source.shader.name.Contains("Impostor"));
                if(!transparent && ((source.HasProperty("_AlphaClip")&&source.GetFloat("_AlphaClip")>.5f)||source.IsKeywordEnabled("_ALPHATEST_ON")||source.GetTag("RenderType",false)=="TransparentCutout"||foliage))
                {
                    result.SetFloat("_Mode",1);result.SetFloat("_Cutoff",source.HasProperty("_Cutoff")?source.GetFloat("_Cutoff"):source.HasProperty("_AlphaCutoff")?source.GetFloat("_AlphaCutoff"):.5f);result.EnableKeyword("_ALPHATEST_ON");result.SetOverrideTag("RenderType","TransparentCutout");result.renderQueue=2450;
                }
                result=Save(result,folder+"/Material_"+(number++).ToString("0000")+".mat");map[source]=result;
                definitions.Add(new JourneyNativeMaterial{material=result,kind=kind,color=color,opacity=source.HasProperty("_Opacity")?source.GetFloat("_Opacity"):1});return result;
            }

            foreach(var r in workbench.GetComponentsInChildren<Renderer>(true))
            {
                if(IsTextRenderer(r))continue;
                var original=r.sharedMaterials;
                var kinds=original.Select(m=>m?Classify(m):JourneyNativeMaterialKind.Surface).ToArray();
                r.sharedMaterials=original.Select(Convert).ToArray();
                if(r is ParticleSystemRenderer particle)
                {
                    // Preserve authored emitters and their process gating, including internal flows and brake sparks.
                    var ps=r.GetComponent<ParticleSystem>();if(ps){var main=ps.main;main.scalingMode=ParticleSystemScalingMode.Hierarchy;}
                    if(particle.renderMode==ParticleSystemRenderMode.None)continue;
                    particle.shadowCastingMode=ShadowCastingMode.Off;
                }
                var filter=r.GetComponent<MeshFilter>();if(!filter||kinds.Length==0)continue;
                switch(kinds[0])
                {
                    case JourneyNativeMaterialKind.Cloud:filter.sharedMesh=bridge.cloudVolume;break;
                    case JourneyNativeMaterialKind.Steam:case JourneyNativeMaterialKind.Mist:filter.sharedMesh=bridge.softVolume;break;
                    case JourneyNativeMaterialKind.Furnace:filter.sharedMesh=bridge.flameVolume;break;
                    case JourneyNativeMaterialKind.Beam:filter.sharedMesh=bridge.beamVolume;break;
                    case JourneyNativeMaterialKind.WaterBody:filter.sharedMesh=bridge.waterVolume;break;
                    case JourneyNativeMaterialKind.WaterJacket:filter.sharedMesh=bridge.jacketVolume;break;
                }
            }
            // Runtime-created puffs/flight copies must start from the same exported materials.
            // SerializedObject edits only scene components, never their referenced source asset files.
            foreach(var component in workbench.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if(!component||component==bridge||component is TMPro.TMP_Text||component is TMPro.TMP_SubMesh||component is TMPro.TMP_SubMeshUI)continue;
                var so=new SerializedObject(component);var p=so.GetIterator();bool changed=false;
                while(p.Next(true))if(p.propertyType==SerializedPropertyType.ObjectReference&&p.objectReferenceValue is Material material)
                {
                    p.objectReferenceValue=Convert(material);changed=true;
                }
                if(changed)so.ApplyModifiedPropertiesWithoutUndo();
            }
            // Both authored volumetric plume pools share this mesh; their CPU lifetime and
            // free-air confinement are kept exactly where the original controllers own them.
            foreach(var atmosphere in workbench.GetComponentsInChildren<MesaAtmosV16>(true))atmosphere.volumeMesh=bridge.softVolume;
            // The authored process explicitly replaced fire particles with flameVolume,
            // but rateOverTime=0 is not a live native-particle update in SDK 6.1.
            // Disable emission in the exported data; keep the component and all source
            // references intact. SteamJourney.SetEmission(fire,0) does not re-enable it.
            if(journey.fire && journey.flameVolume)
            {
                var emission=journey.fire.emission;emission.enabled=false;emission.rateOverTime=0;
                journey.fire.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                var renderer=journey.fire.GetComponent<ParticleSystemRenderer>();if(renderer)renderer.enabled=false;
            }
            bridge.materials=definitions.ToArray();
            bridge.scanMaterial=Save(Effect("Native blue reveal",new Color(.10f,.68f,1,.13f),new Color(.05f,.55f,1)*1.4f),folder+"/BlueReveal.mat");
            bridge.goldMaterial=Save(Effect("Native completion gold",new Color(.91f,.64f,.23f,1),new Color(1,.54f,.10f)*.9f,false),folder+"/CompletionGold.mat");
            bridge.goldMaterial.SetFloat("_Metallic",.78f);bridge.goldMaterial.SetFloat("_Glossiness",.72f);
            bridge.rippleMaterial=Save(Effect("Native contact ripple",new Color(.38f,.75f,.83f,.42f),new Color(.10f,.31f,.38f)),folder+"/WaterRipple.mat");
            var flowMaps=ParticleMaps(alpha);
            ConfigureFlowParticles(journey,workbench,folder,flowMaps.surface,flowMaps.emission);
            EditorUtility.SetDirty(bridge);AssetDatabase.SaveAssets();
            Debug.Log("SWAN_JOURNEY_MATERIALS converted="+map.Count+" plumePools=42+32 nativeReveal=true nativeGold=true");
            return bridge;
        }

        static void ConfigureFlowParticles(SteamJourney journey,Transform workbench,string folder,Texture2D alpha,Texture2D emissionMask)
        {
            var flow=journey.GetComponent<SwanJourneyFlowParticles>()??journey.gameObject.AddComponent<SwanJourneyFlowParticles>();
            flow.journey=journey;
            var root=new GameObject("Native process flow particles · bounded pool");root.transform.SetParent(workbench,false);flow.poolRoot=root.transform;
            var mesh=Save(FlowParticleMesh(),folder+"/FlowParticle.asset");
            var palette=new Dictionary<Color32,Material[]>();var sources=new List<SwanJourneyFlowParticles.Source>();
            foreach(var field in journey.fluidFields??Array.Empty<JourneyFluidField>())
            {
                if(!field||!field.system)continue;
                Color32 key=field.tint;key.a=255;
                if(!palette.TryGetValue(key,out var materials))
                {
                    materials=new Material[SwanJourneyFlowParticles.AlphaLevels.Length];
                    for(int i=0;i<materials.Length;i++)
                    {
                        Color color=key;color.a=SwanJourneyFlowParticles.AlphaLevels[i];
                        var material=Effect("Native flow "+key.r+"-"+key.g+"-"+key.b+" alpha "+i,color,new Color(color.r,color.g,color.b)*.18f);
                        material.mainTexture=alpha;material.SetTexture("_EmissionMap",emissionMask);material.SetFloat("_Glossiness",0);
                        materials[i]=Save(material,folder+"/Flow_"+key.r+"_"+key.g+"_"+key.b+"_"+i+".mat");
                    }
                    palette.Add(key,materials);
                }
                sources.Add(new SwanJourneyFlowParticles.Source{field=field,alphaMaterials=materials});
            }
            flow.sources=sources.ToArray();flow.markers=new MeshRenderer[SwanJourneyFlowParticles.MaximumMarkers];
            var first=sources.Count>0?sources[0].alphaMaterials[0]:null;
            for(int i=0;i<flow.markers.Length;i++)
            {
                var marker=new GameObject("Flow wisp "+i.ToString("000"),typeof(MeshFilter),typeof(MeshRenderer));marker.transform.SetParent(root.transform,false);
                marker.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=marker.GetComponent<MeshRenderer>();renderer.sharedMaterial=first;
                renderer.enabled=false;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;flow.markers[i]=renderer;
            }
            EditorUtility.SetDirty(flow);
            Debug.Log("SWAN_JOURNEY_FLOW_MATERIALS sources="+sources.Count+" pool="+flow.markers.Length+" palettes="+palette.Count+" trianglesPerMarker="+(mesh.triangles.Length/3));
        }

        static Mesh FlowParticleMesh()
        {
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
            for(int i=0;i<3;i++)
            {
                var rotation=Quaternion.Euler(i==1?90:0,i==2?90:0,0);
                SoftDisc(vertices,uv,indices,Vector3.zero,rotation,.5f,8);
            }
            return Build("Native flow soft discs · 48 triangles",vertices,uv,indices);
        }

        static JourneyNativeMaterialKind Classify(Material material)
        {
            string s=material.shader?material.shader.name.ToLowerInvariant():"";
            string n=material.name.ToLowerInvariant();
            if(s.Contains("driftingcloud"))return material.HasProperty("_Steam")&&material.GetFloat("_Steam")>.5f?JourneyNativeMaterialKind.Steam:JourneyNativeMaterialKind.Cloud;
            if(s.Contains("cottoncloud"))return JourneyNativeMaterialKind.Cloud;
            if(s.Contains("waterjacket"))return JourneyNativeMaterialKind.WaterJacket;
            if(s.Contains("waterbody"))return JourneyNativeMaterialKind.WaterBody;
            if(s.Contains("watersurface"))return JourneyNativeMaterialKind.WaterSurface;
            if(s.Contains("cylindermist"))return JourneyNativeMaterialKind.Mist;
            if(s.Contains("furnace")||s.Contains("volumetricfire")||n.Contains("volumetricfire"))return JourneyNativeMaterialKind.Furnace;
            if(s.Contains("softlampbeam"))return JourneyNativeMaterialKind.Beam;
            if(s.Contains("halo")||s.Contains("lampstar"))return JourneyNativeMaterialKind.Halo;
            if(s.Contains("ghost"))return JourneyNativeMaterialKind.Ghost;
            if(s.Contains("mist")||s.Contains("whitesteam")||s.Contains("dust")||s.Contains("sparkle")||s.Contains("bubble")||s.Contains("particles"))return JourneyNativeMaterialKind.Particle;
            return JourneyNativeMaterialKind.Surface;
        }
        static bool IsTextRenderer(Renderer r)=>r.GetComponent<TMPro.TMP_Text>()||r.GetComponent<TMPro.TMP_SubMesh>()||r.GetComponent<TMPro.TMP_SubMeshUI>();
        static bool IsFontMaterial(Material m)=>m && ((m.shader && (m.shader.name.StartsWith("TextMeshPro/",StringComparison.OrdinalIgnoreCase)||m.shader.name.StartsWith("TMPro/",StringComparison.OrdinalIgnoreCase)))
            || (m.HasProperty("_FaceColor")&&m.HasProperty("_GradientScale")&&m.HasProperty("_MainTex")));
        static Color ColorOf(Material m)=>m.HasProperty("_BaseColor")?m.GetColor("_BaseColor"):m.HasProperty("_Color")?m.GetColor("_Color"):m.HasProperty("_Tint")?m.GetColor("_Tint"):Color.white;
        static void CopyTexture(Material from,Material to,string property,string keyword){if(from.HasProperty(property)&&from.GetTexture(property) is Texture2D texture){to.SetTexture(property,texture);if(keyword!=null)to.EnableKeyword(keyword);}}
        static Material Effect(string name,Color color,Color emission,bool transparent=true)
        {
            var m=new Material(Shader.Find("Standard")){name=name,color=color};m.SetColor("_EmissionColor",emission);
            // StandardShaderGUI validates keywords from these flags on reimport.
            // The default EmissiveIsBlack flag would erase _EMISSION even while a
            // nonblack color and density texture are present (including alpha index 0).
            m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.EnableKeyword("_EMISSION");if(transparent)Transparent(m);return m;
        }
        static void Transparent(Material m){m.SetOverrideTag("RenderType","Transparent");m.SetFloat("_Mode",2);m.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);m.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);m.SetInt("_ZWrite",0);m.DisableKeyword("_ALPHATEST_ON");m.DisableKeyword("_ALPHAPREMULTIPLY_ON");m.EnableKeyword("_ALPHABLEND_ON");m.renderQueue=3000;}
        static T Save<T>(T asset,string path)where T:UnityEngine.Object
        {
            var old=AssetDatabase.LoadAssetAtPath<T>(path);
            if(old){EditorUtility.CopySerialized(asset,old);UnityEngine.Object.DestroyImmediate(asset);EditorUtility.SetDirty(old);return old;}
            AssetDatabase.CreateAsset(asset,path);return asset;
        }
        static Texture2D MakeSoftTexture(string folder)
        {
            const int n=128;var t=new Texture2D(n,n,TextureFormat.RGBA32,false);
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float u=(x+.5f)/n*2-1,v=(y+.5f)/n*2-1,r=u*u+v*v;
                float shape=Mathf.Clamp01(1-r);float detail=.64f+.36f*Mathf.PerlinNoise(u*3.7f+9,v*3.7f+17);
                float alpha=shape*shape*detail;float light=.82f+.18f*Mathf.Clamp01(v*.5f+.5f);
                t.SetPixel(x,y,new Color(light,light,light,alpha));
            }
            t.Apply();string path=folder+"/SoftDensity.png";File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        sealed class SoftParticleMaps { public Texture2D surface,emission; }
        static SoftParticleMaps MakeSoftParticleMaps(Texture2D source,string prefix)
        {
            // Work only on derived images. Preserve the authored interior color/density,
            // but explicitly feather all sprite edges and mask emission by that density.
            var copy=Readable(source);int width=Mathf.Max(64,copy.width),height=Mathf.Max(64,copy.height);
            var surface=new Texture2D(width,height,TextureFormat.RGBA32,false);
            var emission=new Texture2D(width,height,TextureFormat.RGBA32,false);
            try
            {
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                {
                    var c=width==copy.width&&height==copy.height?copy.GetPixel(x,y):copy.GetPixelBilinear((x+.5f)/width,(y+.5f)/height);float u=(x+.5f)/width*2-1,v=(y+.5f)/height*2-1;
                    float edge=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.86f,1,Mathf.Sqrt(u*u+v*v)));
                    if(x==0||y==0||x==width-1||y==height-1)edge=0;
                    float alpha=c.a*edge;float rgbGuard=Mathf.Clamp01(alpha*8);
                    surface.SetPixel(x,y,new Color(c.r*rgbGuard,c.g*rgbGuard,c.b*rgbGuard,alpha));
                    emission.SetPixel(x,y,new Color(c.r*alpha,c.g*alpha,c.b*alpha,alpha));
                }
                surface.Apply();emission.Apply();
                return new SoftParticleMaps{surface=SaveSoftImage(surface,prefix+"_Surface.png"),emission=SaveSoftImage(emission,prefix+"_Emission.png")};
            }
            finally {UnityEngine.Object.DestroyImmediate(copy);UnityEngine.Object.DestroyImmediate(surface);UnityEngine.Object.DestroyImmediate(emission);}
        }
        static Texture2D SaveSoftImage(Texture2D image,string path)
        {
            File.WriteAllBytes(path,image.EncodeToPNG());AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.alphaSource=TextureImporterAlphaSource.FromInput;
            // alphaIsTransparency dilates bright RGB into transparent pixels; the native
            // emissive mask deliberately needs those edge pixels to stay black.
            importer.alphaIsTransparency=false;importer.wrapMode=TextureWrapMode.Clamp;
            importer.mipmapEnabled=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static Texture2D MakeFlameTexture(string folder)
        {
            const int size=128;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=(x+.5f)/size*2-1,v=(y+.5f)/size;
                float edge=Mathf.Clamp01((1-Mathf.Abs(u))*2.8f);
                float density=edge*edge*Mathf.SmoothStep(0,1,v/.08f)*(1-Mathf.SmoothStep(.62f,1,v));
                density*=.72f+.28f*Mathf.PerlinNoise(u*3.5f+11,v*6+7);
                if(x==0||y==0||x==size-1||y==size-1)density=0;
                texture.SetPixel(x,y,new Color(density,density,density,density));
            }
            texture.Apply();string path=folder+"/SoftFlame.png";File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=false; // Preserve black emissive border pixels.
            importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=true;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static Texture2D Readable(Texture source)
        {
            if(!source)return null;
            int width=Mathf.Min(1024,source.width),height=Mathf.Min(1024,source.height);
            var rt=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var old=RenderTexture.active;
            try {Graphics.Blit(source,rt);RenderTexture.active=rt;var copy=new Texture2D(width,height,TextureFormat.RGBA32,false);copy.ReadPixels(new Rect(0,0,width,height),0,0);copy.Apply();copy.wrapMode=source.wrapMode;return copy;}
            finally {RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);}
        }
        static Texture2D BakeGround(Material source,string path)
        {
            var control=source.HasProperty("_Control")?Readable(source.GetTexture("_Control")):null;
            var control1=source.HasProperty("_Control1")?Readable(source.GetTexture("_Control1")):null;
            var layers=new Texture2D[5];var scales=new Vector2[5];var offsets=new Vector2[5];
            for(int i=0;i<5;i++){string key="_Splat"+i;if(!source.HasProperty(key))continue;layers[i]=Readable(source.GetTexture(key));scales[i]=source.GetTextureScale(key);offsets[i]=source.GetTextureOffset(key);}
            const int size=1024;var baked=new Texture2D(size,size,TextureFormat.RGB24,false);var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=(x+.5f)/size,v=(y+.5f)/size;var w=control?control.GetPixelBilinear(u,v):Color.red;float extra=control1?control1.GetPixelBilinear(u,v).r:0;
                float total=Mathf.Max(.0001f,w.r+w.g+w.b+w.a+extra);var color=Color.black;
                for(int i=0;i<5;i++)if(layers[i]){float weight=(i<4?w[i]:extra)/total;if(weight>.0001f)color+=layers[i].GetPixelBilinear(u*scales[i].x+offsets[i].x,v*scales[i].y+offsets[i].y)*weight;}
                color.a=1;pixels[y*size+x]=color;
            }
            baked.SetPixels(pixels);baked.Apply();File.WriteAllBytes(path,baked.EncodeToPNG());UnityEngine.Object.DestroyImmediate(baked);
            if(control)UnityEngine.Object.DestroyImmediate(control);if(control1)UnityEngine.Object.DestroyImmediate(control1);foreach(var t in layers)if(t)UnityEngine.Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static Mesh SoftVolume()
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var ix=new List<int>();
            for(int i=0;i<3;i++)for(int j=0;j<3;j++)
            {
                var q=Quaternion.Euler(i==1?90:0,i==2?90:0,j*19);var center=q*(Vector3.forward*(j-1)*.15f);
                SoftDisc(v,uv,ix,center,q,.46f,12);
            }
            return Build("Soft radial volume discs · 216 triangles",v,uv,ix);
        }
        static void SoftDisc(List<Vector3> v,List<Vector2> uv,List<int> ix,Vector3 center,Quaternion rotation,float radius,int segments)
        {
            // The actual silhouette is circular even if a native material temporarily
            // receives its color before its texture; no square corners are submitted.
            for(int face=0;face<2;face++)
            {
                int first=v.Count;v.Add(center);uv.Add(new Vector2(.5f,.5f));
                for(int i=0;i<segments;i++)
                {
                    float angle=i*Mathf.PI*2/segments;var unit=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                    v.Add(center+rotation*new Vector3(unit.x*radius,unit.y*radius,0));uv.Add(unit*.5f+Vector2.one*.5f);
                }
                for(int i=0;i<segments;i++)
                {
                    ix.Add(first);ix.Add(first+1+(face==0?i:(i+1)%segments));ix.Add(first+1+(face==0?(i+1)%segments:i));
                }
            }
        }
        static Mesh FlameVolume()
        {
            // Tapered bent tongues, not the old raymarch bounding cube or rectangular
            // particles. All motion/intensity still comes from the authored burn/time.
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
            const int strips=4;
            for(int tongue=0;tongue<7;tongue++)for(int plane=0;plane<2;plane++)
            {
                float seed=tongue*2.399963f,top=.69f+.19f*Mathf.Sin(seed*1.7f+1);
                var center=new Vector3(Mathf.Sin(seed)*.24f,0,Mathf.Cos(seed)*.24f);
                var side=Quaternion.Euler(0,plane*90+tongue*31,0)*Vector3.right;
                Vector3 Point(float h,float sign)
                {
                    float width=.115f*Mathf.Pow(1-h,.8f)+.002f;
                    return center+side*(sign*width+Mathf.Sin(h*5+seed)*h*.045f)+Vector3.up*(-.48f+top*h);
                }
                for(int strip=0;strip<strips;strip++)
                {
                    float a=strip/(float)strips,b=(strip+1)/(float)strips;int start=uv.Count;
                    Quad(vertices,uv,indices,Point(a,-1),Point(a,1),Point(b,1),Point(b,-1));
                    for(int face=0;face<2;face++){int k=start+face*4;uv[k]=new Vector2(0,a);uv[k+1]=new Vector2(1,a);uv[k+2]=new Vector2(1,b);uv[k+3]=new Vector2(0,b);}
                }
            }
            return Build("Seven soft coal flames · 224 triangles",vertices,uv,indices);
        }
        static Mesh CloudVolume()
        {
            // Three world-visible clouds need a stable silhouette from both eyes.
            // Opaque overlapping lobes avoid the striping of edge-on alpha card stacks.
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
            var centres=new[]{new Vector3(-.27f,-.10f,0),new Vector3(-.12f,.02f,-.02f),new Vector3(.10f,.04f,.04f),new Vector3(.28f,-.08f,0),new Vector3(-.07f,-.18f,.09f),new Vector3(.15f,-.15f,-.09f),new Vector3(-.28f,.055f,.10f),new Vector3(-.15f,.20f,.04f),new Vector3(.04f,.245f,.05f),new Vector3(.25f,.105f,.08f),new Vector3(-.07f,-.01f,.23f),new Vector3(.16f,-.045f,.23f)};
            var radii=new[]{new Vector3(.16f,.16f,.20f),new Vector3(.18f,.23f,.22f),new Vector3(.20f,.25f,.22f),new Vector3(.15f,.17f,.19f),new Vector3(.26f,.11f,.20f),new Vector3(.23f,.13f,.20f),new Vector3(.10f,.12f,.13f),new Vector3(.12f,.13f,.14f),new Vector3(.13f,.12f,.13f),new Vector3(.12f,.13f,.14f),new Vector3(.12f,.14f,.11f),new Vector3(.12f,.12f,.12f)};
            const int rings=10,segments=16;
            for(int k=0;k<centres.Length;k++)
            {
                int offset=vertices.Count;
                for(int y=0;y<=rings;y++)for(int x=0;x<=segments;x++)
                {
                    float a=y*Mathf.PI/rings,b=x*Mathf.PI*2/segments;var p=new Vector3(Mathf.Sin(a)*Mathf.Cos(b),Mathf.Cos(a),Mathf.Sin(a)*Mathf.Sin(b));
                    vertices.Add(centres[k]+Vector3.Scale(p,radii[k]));uv.Add(new Vector2(x/(float)segments,y/(float)rings));
                }
                for(int y=0;y<rings;y++)for(int x=0;x<segments;x++){int a=offset+y*(segments+1)+x,b=a+segments+1;indices.AddRange(new[]{a,a+1,b,a+1,b+1,b});}
            }
            return Build("Rounded three-dimensional cloud lobes",vertices,uv,indices);
        }
        static Mesh BeamVolume()
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var ix=new List<int>();
            for(int i=0;i<3;i++){var q=Quaternion.Euler(0,0,i*60);Quad(v,uv,ix,q*new Vector3(-.025f,0,-.49f),q*new Vector3(.025f,0,-.49f),q*new Vector3(.42f,0,.49f),q*new Vector3(-.42f,0,.49f));}
            return Build("Soft tapered lamp cards",v,uv,ix);
        }
        static Mesh WaterBody()
        {
            var polygon=new List<Vector2>();for(int i=0;i<96;i++){float a=i*Mathf.PI*2/96;polygon.Add(new Vector2(Mathf.Cos(a)*.5f,Mathf.Sin(a)*.5f));}
            polygon=Clip(polygon,0,.402985f);polygon=Clip(polygon,1,.33f);return Extrude("Clipped cylindrical boiler water",polygon);
        }
        static List<Vector2> Clip(List<Vector2> source,int axis,float value)
        {
            var output=new List<Vector2>();for(int i=0;i<source.Count;i++){var a=source[i];var b=source[(i+1)%source.Count];bool ai=a[axis]<=value,bi=b[axis]<=value;if(ai)output.Add(a);if(ai!=bi)output.Add(Vector2.Lerp(a,b,(value-a[axis])/(b[axis]-a[axis])));}return output;
        }
        static Mesh WaterJacket()
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var ix=new List<int>();
            const int steps=48;
            for(int i=0;i<steps;i++)
            {
                float za=-.75f+i*.75f/steps,zb=-.75f+(i+1)*.75f/steps;
                float Top(float z)=>Mathf.Min(2.72f,2.18f+Mathf.Sqrt(Mathf.Max(0,.5625f-z*z)));
                float Bottom(float z)=>z<-.53f?1.43f:2.05f+Mathf.Sqrt(Mathf.Max(0,.2809f-z*z));
                var a=new Vector2((Bottom(za)-2.14f)/1.6f,za/1.5f);var b=new Vector2((Top(za)-2.14f)/1.6f,za/1.5f);var c=new Vector2((Top(zb)-2.14f)/1.6f,zb/1.5f);var d=new Vector2((Bottom(zb)-2.14f)/1.6f,zb/1.5f);
                Vector3 P(float x,Vector2 p)=>new Vector3(x,p.x,p.y);
                Quad(v,uv,ix,P(-.5f,a),P(-.5f,b),P(-.5f,c),P(-.5f,d));Quad(v,uv,ix,P(.5f,d),P(.5f,c),P(.5f,b),P(.5f,a));
                Quad(v,uv,ix,P(-.5f,b),P(.5f,b),P(.5f,c),P(-.5f,c));Quad(v,uv,ix,P(-.5f,d),P(.5f,d),P(.5f,a),P(-.5f,a));
                if(i==0)Quad(v,uv,ix,P(-.5f,a),P(.5f,a),P(.5f,b),P(-.5f,b));if(i==steps-1)Quad(v,uv,ix,P(-.5f,c),P(.5f,c),P(.5f,d),P(-.5f,d));
            }
            return Build("Cutaway water jacket shell",v,uv,ix);
        }
        static Mesh Extrude(string name,List<Vector2> polygon)
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var ix=new List<int>();var center=polygon.Aggregate(Vector2.zero,(a,b)=>a+b)/polygon.Count;
            for(int i=0;i<polygon.Count;i++){var a=polygon[i];var b=polygon[(i+1)%polygon.Count];var a0=new Vector3(-.5f,a.x,a.y);var a1=new Vector3(.5f,a.x,a.y);var b0=new Vector3(-.5f,b.x,b.y);var b1=new Vector3(.5f,b.x,b.y);Quad(v,uv,ix,a0,a1,b1,b0);Quad(v,uv,ix,new Vector3(-.5f,center.x,center.y),a0,b0,b0);Quad(v,uv,ix,new Vector3(.5f,center.x,center.y),b1,a1,a1);}return Build(name,v,uv,ix);
        }
        static Mesh Ring()
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var ix=new List<int>();for(int i=0;i<96;i++){float a=i*Mathf.PI*2/96,b=(i+1)*Mathf.PI*2/96;Quad(v,uv,ix,new Vector3(Mathf.Cos(a)*.49f,0,Mathf.Sin(a)*.49f),new Vector3(Mathf.Cos(a)*.5f,0,Mathf.Sin(a)*.5f),new Vector3(Mathf.Cos(b)*.5f,0,Mathf.Sin(b)*.5f),new Vector3(Mathf.Cos(b)*.49f,0,Mathf.Sin(b)*.49f));}return Build("Native thin feedback ring",v,uv,ix);
        }
        static void Quad(List<Vector3> vertices,List<Vector2> uv,List<int> indices,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {
            int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d,a,b,c,d});uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up,Vector2.zero,Vector2.right,Vector2.one,Vector2.up});indices.AddRange(new[]{n,n+1,n+2,n,n+2,n+3,n+4,n+6,n+5,n+4,n+7,n+6});
        }
        static Mesh Build(string name,List<Vector3> vertices,List<Vector2> uv,List<int> indices){var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
    }
}
