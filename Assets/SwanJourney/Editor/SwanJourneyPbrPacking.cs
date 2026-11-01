using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SwanJourney.Editor
{
    // Give AO its own texture identity. Metal maps carry both Unity Lit's R/A
    // and glTF's G/B layout so the editor and the exported native models agree.
    public static class SwanJourneyPbrPacking
    {
        enum Finish { Original, Paint, Steel, Brass, Rock }
        static Finish Surface(string name)
        {
            string n=name.ToLowerInvariant();
            if(n.StartsWith("swan v23 rock ambient -",StringComparison.Ordinal))return Finish.Rock;
            if(n.Contains("enamel")||n.Contains("rolled roof"))return Finish.Paint;
            if(n.Contains("brass"))return Finish.Brass;
            if(n.Contains("machined")||n.Contains("ground linkage")||n.Contains("honed liner")||n.Contains("brushedsteel")||n.Contains("structuralsteel"))return Finish.Steel;
            return Finish.Original;
        }
        static Vector2 Response(Finish finish)
        {
            switch(finish)
            {
                case Finish.Paint:return new Vector2(.14f,.58f);
                case Finish.Steel:return new Vector2(.95f,.78f);
                case Finish.Brass:return new Vector2(.96f,.74f);
                case Finish.Rock:return new Vector2(0,.22f);
                default:return Vector2.one;
            }
        }
        [Serializable] sealed class Report
        {
            public bool passed, nativeRenderingValidated;
            public string scope = "Derived PNG pixel packing and material bindings; native visual acceptance separate";
            public int materials, metalMaps, occlusionMaps;
            public List<string> outputs = new List<string>();
        }
        public static void Apply(Transform content)
        {
            string folder=SwanJourneyBuild.Generated+"/NativePbr";
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            var report=new Report(); var cache=new Dictionary<string,Texture2D>();
            var materials=content.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials)
                .Concat(content.GetComponentInChildren<SwanJourneyVisualBridge>(true).materials.Select(m=>m.material)).Where(m=>m).Distinct();
            foreach(var m in materials)
            {
                if(m.shader.name!=SwanJourneyUrp.Lit || !AssetDatabase.GetAssetPath(m).StartsWith(SwanJourneyBuild.Generated+"/NativeVisuals/",StringComparison.Ordinal))continue;
                report.materials++;
                var finish=Surface(m.name);
                if(m.GetTexture("_MetallicGlossMap") is Texture2D metal)
                {
                    report.metalMaps++;
                    // The packed texture already contains the authored metal and finish response.
                    // The URP exporter multiplies by these scalars when writing glTF.
                    m.SetFloat("_Metallic",.99f);
                    m.SetFloat("_Smoothness",1f);
                    m.SetTexture("_MetallicGlossMap",Pack(metal,false,finish));
                }
                else if(finish!=Finish.Original)
                {
                    var response=Response(finish);
                    m.SetFloat("_Metallic",m.GetFloat("_Metallic")*response.x);
                    m.SetFloat("_Smoothness",m.GetFloat("_Smoothness")*response.y);
                }
                if(m.GetTexture("_OcclusionMap") is Texture2D ao)
                { m.SetTexture("_OcclusionMap",Pack(ao,true,finish==Finish.Rock?finish:Finish.Original));report.occlusionMaps++; }
                EditorUtility.SetDirty(m);
            }
            AssetDatabase.SaveAssets();report.passed=true;
            File.WriteAllText(SwanJourneyBuild.Output+"/pbr-packing.json",JsonUtility.ToJson(report,true));
            Debug.Log("SWAN_JOURNEY_PBR_PACKING metalMaps="+report.metalMaps+" aoMaps="+report.occlusionMaps+" nativeVisuals=unverified");
            Texture2D Pack(Texture2D source,bool ao,Finish finish)
            {
                string src=AssetDatabase.GetAssetPath(source);
                if(src.StartsWith(folder+"/",StringComparison.Ordinal))return source;
                string key=AssetDatabase.AssetPathToGUID(src)+(ao?"_ao":"_metal")+(finish==Finish.Original?"":"_"+finish);
                if(cache.TryGetValue(key,out var cached))return cached;
                // TextureBudget already creates canonical, <=512px linear PNGs.
                if(!src.EndsWith(".png",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Data packing requires budgeted PNG: "+src);
                var pixels=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
                if(!pixels.LoadImage(File.ReadAllBytes(src)))throw new InvalidOperationException("Invalid data PNG: "+src);
                var values=pixels.GetPixels32();var response=Response(finish);
                for(int i=0;i<values.Length;i++)
                {
                    var c=values[i];
                    if(ao)
                    {
                        byte occlusion=finish==Finish.Rock?(byte)(150+(105*c.g+127)/255):c.g;
                        values[i]=new Color32(occlusion,occlusion,occlusion,255);
                    }
                    else
                    {
                        byte metal=(byte)Mathf.RoundToInt(c.r*response.x);
                        byte smooth=(byte)Mathf.RoundToInt(c.a*response.y);
                        // URP Lit samples R/A. The local URP glTF exporter passes G/B through.
                        // Match the old exporter's linear-to-sRGB conversion of the metal channel.
                        byte exportedMetal=(byte)Mathf.RoundToInt(255f*Mathf.LinearToGammaSpace(metal/255f));
                        values[i]=new Color32(metal,(byte)(255-smooth),exportedMetal,smooth);
                    }
                }
                pixels.SetPixels32(values);pixels.Apply();string path=folder+"/"+key+".png";
                File.WriteAllBytes(path,pixels.EncodeToPNG());UnityEngine.Object.DestroyImmediate(pixels);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.sRGBTexture=false;importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=false;
                importer.mipmapEnabled=true;importer.isReadable=false;importer.maxTextureSize=512;importer.SaveAndReimport();
                var output=AssetDatabase.LoadAssetAtPath<Texture2D>(path);cache.Add(key,output);report.outputs.Add(path);return output;
            }
        }
    }
}
