using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SwanJourney.Editor
{
    // Verified in APK pixels: when metal and AO share one Unity texture,
    // this exporter writes metallic R into the final ORM's occlusion R (zero
    // for wood/stone). Give AO its own texture identity. The exporter then
    // combines AO and metal correctly. Preserve the original metallic map.
    public static class SwanJourneyPbrPacking
    {
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
                if(m.shader.name!="Standard" || !AssetDatabase.GetAssetPath(m).StartsWith(SwanJourneyBuild.Generated+"/NativeVisuals/",StringComparison.Ordinal))continue;
                report.materials++;
                if(m.GetTexture("_MetallicGlossMap") is Texture2D)report.metalMaps++;
                if(m.GetTexture("_OcclusionMap") is Texture2D ao)
                { m.SetTexture("_OcclusionMap",Pack(ao,true));report.occlusionMaps++; }
                EditorUtility.SetDirty(m);
            }
            AssetDatabase.SaveAssets();report.passed=true;
            File.WriteAllText(SwanJourneyBuild.Output+"/pbr-packing.json",JsonUtility.ToJson(report,true));
            Debug.Log("SWAN_JOURNEY_PBR_PACKING metalMaps="+report.metalMaps+" aoMaps="+report.occlusionMaps+" nativeVisuals=unverified");
            Texture2D Pack(Texture2D source,bool ao)
            {
                string src=AssetDatabase.GetAssetPath(source);
                if(src.StartsWith(folder+"/",StringComparison.Ordinal))return source;
                string key=AssetDatabase.AssetPathToGUID(src)+(ao?"_ao":"_metal");
                if(cache.TryGetValue(key,out var cached))return cached;
                // TextureBudget already creates canonical, <=512px linear PNGs.
                if(!src.EndsWith(".png",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Data packing requires budgeted PNG: "+src);
                var pixels=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
                if(!pixels.LoadImage(File.ReadAllBytes(src)))throw new InvalidOperationException("Invalid data PNG: "+src);
                var values=pixels.GetPixels32();
                for(int i=0;i<values.Length;i++)
                {
                    var c=values[i];values[i]=ao?new Color32(c.g,c.g,c.g,255):new Color32(c.r,(byte)(255-c.a),c.r,c.a);
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
