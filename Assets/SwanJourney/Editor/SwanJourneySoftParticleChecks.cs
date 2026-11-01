using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace SwanJourney.Editor
{
    // Generated-asset and bridge checks only. These do not validate the native
    // particle renderer or substitute for a device image after installation.
    public static class SwanJourneySoftParticleChecks
    {
        [Serializable] sealed class Report
        {
            public bool passed, nativeRenderingValidated;
            public int materials, textures, borderPixels, reimportedMaterials;
            public List<string> checks=new List<string>();
            public string failure;
        }

        public static void Validate(SteamJourney journey)
        {
            var report=new Report();
            try
            {
                var bridge=journey?journey.GetComponent<SwanJourneyVisualBridge>():null;
                Check(bridge&&bridge.softVolume,"Generated visual bridge and plume mesh exist",report);
                DiscMesh(bridge.softVolume,216,"Plume",report);
                var flow=journey.GetComponent<SwanJourneyFlowParticles>();
                Check(flow&&flow.markers.Length==288&&flow.SourceCount==125,"All process sources and fixed marker capacity remain intact",report);
                DiscMesh(flow.markers[0].GetComponent<MeshFilter>().sharedMesh,48,"Flow",report);
                var materials=new HashSet<Material>(bridge.materials.Where(m=>m.material&&Soft(m.kind)).Select(m=>m.material));
                foreach(var source in flow.sources)foreach(var material in source.alphaMaterials)materials.Add(material);
                VerifyPersistedEmission(materials,report);
                Check(SwanJourneyFlowParticles.AlphaLevels[0]>.02f && flow.sources.All(source=>
                    source.alphaMaterials[0].color.a==SwanJourneyFlowParticles.AlphaLevels[0]
                    &&source.alphaMaterials[0].GetColor("_EmissionColor").maxColorComponent>0),
                    "Alpha index zero is a visible 4-percent palette entry, never an export exemption",report);
                var textures=new HashSet<Texture>();
                foreach(var material in materials)
                {
                    Check(material.shader.name==SwanJourneyUrp.Lit&&material.GetFloat("_Surface")==1
                        && material.GetInt("_SrcBlend")==5&&material.GetInt("_DstBlend")==10
                        && material.GetInt("_ZWrite")==0,"Soft material blends alpha: "+material.name,report);
                    Check(material.mainTexture&&material.GetTexture("_EmissionMap"),"Base color and emission both use masks: "+material.name,report);
                    textures.Add(material.mainTexture);textures.Add(material.GetTexture("_EmissionMap"));
                }
                report.materials=materials.Count;
                foreach(var texture in textures)CheckImage(texture,report);
                report.textures=textures.Count;
                Check(bridge.materials.Where(m=>m.material&&m.kind!=JourneyNativeMaterialKind.Particle&&Soft(m.kind))
                    .All(m=>m.material.GetColor("_EmissionColor").maxColorComponent>0),
                    "Runtime emissive channels have nonzero export-time values",report);
                CheckLifetimeFade(bridge,report);
                report.passed=true;
                Debug.Log("SWAN_JOURNEY_SOFT_PARTICLE_CHECKS passed="+report.checks.Count+" materials="+report.materials+" textures="+report.textures);
            }
            catch(Exception exception){report.failure=exception.ToString();throw;}
            finally
            {
                Directory.CreateDirectory("Builds/SwanJourney");
                File.WriteAllText("Builds/SwanJourney/soft-particle-checks.json",JsonUtility.ToJson(report,true));
            }
        }

        static void VerifyPersistedEmission(HashSet<Material> materials,Report report)
        {
            AssetDatabase.SaveAssets();
            var paths=materials.Select(AssetDatabase.GetAssetPath).Distinct().ToArray();
            foreach(string path in paths)
            {
                if(string.IsNullOrEmpty(path)||!path.EndsWith(".mat",StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Soft material is not a standalone saved material: "+path);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
            }
            // Persist any importer/StandardShaderGUI normalization before testing
            // the disk representation, not just the pre-import Material instance.
            AssetDatabase.SaveAssets();
            foreach(string path in paths)
            {
                var reloaded=AssetDatabase.LoadAssetAtPath<Material>(path);
                Check(reloaded&&reloaded.IsKeywordEnabled("_EMISSION")
                    &&(reloaded.globalIlluminationFlags&MaterialGlobalIlluminationFlags.EmissiveIsBlack)==0
                    &&reloaded.GetTexture("_EmissionMap"),"Emission survives synchronous material reimport: "+reloaded.name,report);
                string yaml=File.ReadAllText(path);
                bool keyword=Regex.IsMatch(yaml,@"(?m)^  - _EMISSION\s*$")
                    ||Regex.IsMatch(yaml,@"(?m)^  m_ShaderKeywords:.*\b_EMISSION\b");
                var flags=Regex.Match(yaml,@"(?m)^  m_LightmapFlags: (\d+)");
                Check(keyword&&flags.Success&&(int.Parse(flags.Groups[1].Value)&4)==0,
                    "Saved material YAML keeps emissive keyword and flags: "+reloaded.name,report);
                report.reimportedMaterials++;
            }
        }

        static bool Soft(JourneyNativeMaterialKind k)=>k==JourneyNativeMaterialKind.Particle||k==JourneyNativeMaterialKind.Steam
            ||k==JourneyNativeMaterialKind.Mist||k==JourneyNativeMaterialKind.Furnace||k==JourneyNativeMaterialKind.Beam||k==JourneyNativeMaterialKind.Halo;

        static void DiscMesh(Mesh mesh,int triangles,string label,Report report)
        {
            Check(mesh&&mesh.triangles.Length/3==triangles&&mesh.uv.Length==mesh.vertexCount,label+" uses bounded radial mesh geometry",report);
            Check(mesh.uv.All(uv=>(uv-new Vector2(.5f,.5f)).sqrMagnitude<=.2501f),label+" submits no square UV corners",report);
        }

        static void CheckImage(Texture texture,Report report)
        {
            string path=AssetDatabase.GetAssetPath(texture);
            Check(!string.IsNullOrEmpty(path)&&File.Exists(path),"Soft image is a saved asset: "+texture.name,report);
            var image=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                Check(image.LoadImage(File.ReadAllBytes(path)),"Soft image decodes: "+texture.name,report);
                bool core=false;
                for(int y=0;y<image.height;y++)for(int x=0;x<image.width;x++)
                {
                    var c=image.GetPixel(x,y);core|=c.a>.01f;
                    if(x!=0&&y!=0&&x!=image.width-1&&y!=image.height-1)continue;
                    report.borderPixels++;
                    if(c.a>.001f||c.r+c.g+c.b>.003f)throw new InvalidOperationException("Soft image has visible edge: "+path+" at "+x+","+y);
                }
                Check(core,"Soft image retains visible interior density: "+texture.name,report);
            }
            finally {UnityEngine.Object.DestroyImmediate(image);}
        }

        static void CheckLifetimeFade(SwanJourneyVisualBridge generated,Report report)
        {
            var root=new GameObject("Soft-particle check fixture"){hideFlags=HideFlags.HideAndDontSave};
            Material source=null,instance=null;
            try
            {
                var journey=root.AddComponent<SteamJourney>();
                var bridge=root.AddComponent<SwanJourneyVisualBridge>();bridge.journey=journey;bridge.workbench=root.transform;
                bridge.softVolume=generated.softVolume;
                var definition=generated.materials.First(m=>m.kind==JourneyNativeMaterialKind.Steam);
                source=new Material(definition.material);
                bridge.materials=new[]{new JourneyNativeMaterial{material=source,kind=JourneyNativeMaterialKind.Steam,color=definition.color,opacity=1}};
                var go=new GameObject("Puff",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root.transform,false);
                var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=source;go.GetComponent<MeshFilter>().sharedMesh=generated.softVolume;
                bridge.SendMessage("Awake",SendMessageOptions.RequireReceiver);instance=renderer.sharedMaterial;
                var block=new MaterialPropertyBlock();block.SetFloat("_Opacity",1);renderer.SetPropertyBlock(block);
                bridge.SendMessage("LateUpdate",SendMessageOptions.RequireReceiver);
                Check(instance!=source&&instance.color.a>0&&instance.GetColor("_EmissionColor").maxColorComponent>0,"Visible puff receives cloned masked material and live brightness",report);
                var mask=instance.GetTexture("_EmissionMap");
                block.SetFloat("_Opacity",0);renderer.SetPropertyBlock(block);bridge.SendMessage("LateUpdate",SendMessageOptions.RequireReceiver);
                Check(instance.color.a==0&&instance.GetColor("_EmissionColor").maxColorComponent==0,"Puff lifetime reaches zero alpha and zero emission together",report);
                block.SetFloat("_Opacity",.5f);renderer.SetPropertyBlock(block);bridge.SendMessage("LateUpdate",SendMessageOptions.RequireReceiver);
                Check(instance.color.a>0&&instance.GetTexture("_EmissionMap")==mask&&instance.GetFloat("_Surface")==1,"Reused puff retains its mask and transparency",report);
            }
            finally
            {
                // Dispose the private material before the runtime OnDestroy path;
                // edit-mode verification must not schedule delayed Destroy calls.
                if(instance&&instance!=source)UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(root);if(source)UnityEngine.Object.DestroyImmediate(source);
            }
        }
        static void Check(bool condition,string label,Report report)
        {if(!condition)throw new InvalidOperationException("Soft particle check: "+label);report.checks.Add(label);}
    }
}
