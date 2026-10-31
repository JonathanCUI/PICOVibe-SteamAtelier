using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SwanJourney.Editor
{
    // Call after SwanJourneyMaterials.Configure in the generated scene. No Play mode,
    // device, simulation advancement or source-asset mutation is required.
    public static class SwanJourneyFireChecks
    {
        [Serializable] sealed class Report
        {
            public string scope="Generated scene and exported-material prerequisites; native visual validation pending";
            public bool passed, nativeRenderingValidated;
            public int flameTriangles, checkedBorderPixels;
            public List<string> checks=new List<string>();
            public string failure;
        }

        public static void Validate(SteamJourney journey)
        {
            var report=new Report();
            try
            {
                Check(journey && journey.fire && journey.flameVolume,"Authored fire source and replacement flame volume remain bound",report);
                var bridge=journey.GetComponent<SwanJourneyVisualBridge>();
                Check(bridge && bridge.flameVolume,"Native flame mesh is generated and bound",report);
                Check(!journey.fire.emission.enabled && journey.fire.emission.rateOverTime.constantMax==0,
                    "Replaced legacy fire cannot emit in exported native particle state",report);
                Check(journey.fire.gameObject.activeSelf,"Legacy source object remains intact for authored references",report);
                var mesh=journey.flameVolume.GetComponent<MeshFilter>().sharedMesh;
                Check(mesh==bridge.flameVolume && AssetDatabase.Contains(mesh),"Flame renderer uses its saved tapered mesh",report);
                report.flameTriangles=mesh.triangles.Length/3;
                Check(mesh.vertexCount>0 && report.flameTriangles==224 && mesh.uv.Length==mesh.vertexCount,
                    "Tapered flame has 224 triangles and complete UVs",report);
                Check(mesh.bounds.min.y>=-.501f && mesh.bounds.max.y<=.501f && mesh.bounds.size.x<1 && mesh.bounds.size.z<1,
                    "Flame stays within original unit volume",report);
                var material=journey.flameVolume.sharedMaterial;
                Check(material && material.shader.name=="Standard" && bridge.materials.Any(m=>m.material==material&&m.kind==JourneyNativeMaterialKind.Furnace),
                    "Flame uses a native-exportable furnace material",report);
                Check(material.GetFloat("_Mode")==2 && material.GetInt("_ZWrite")==0 && material.renderQueue==3000
                    && material.IsKeywordEnabled("_ALPHABLEND_ON") && !material.IsKeywordEnabled("_ALPHATEST_ON"),
                    "Flame blends alpha without opaque depth writes",report);
                var texture=material.mainTexture;
                Check(texture && material.GetTexture("_EmissionMap")==texture,"Alpha and emissive RGB share the saved soft flame mask",report);
                string path=AssetDatabase.GetAssetPath(texture);
                Check(!string.IsNullOrEmpty(path)&&File.Exists(path),"Soft flame texture is a real exported PNG asset",report);
                var pixels=new Texture2D(2,2,TextureFormat.RGBA32,false);
                try
                {
                    Check(pixels.LoadImage(File.ReadAllBytes(path)),"Saved flame mask decodes",report);
                    for(int y=0;y<pixels.height;y++)for(int x=0;x<pixels.width;x++)
                    {
                        if(x!=0&&y!=0&&x!=pixels.width-1&&y!=pixels.height-1)continue;
                        Color color=pixels.GetPixel(x,y);report.checkedBorderPixels++;
                        if(color.a>.001f||color.r+color.g+color.b>.003f)
                            throw new InvalidOperationException("Flame texture edge is opaque or emissive at "+x+","+y);
                    }
                    Check(pixels.GetPixel(pixels.width/2,pixels.height/2).a>.15f,"Flame mask keeps a visible soft core",report);
                    Check(report.checkedBorderPixels>0,"All border pixels are transparent and non-emissive",report);
                }
                finally {UnityEngine.Object.DestroyImmediate(pixels);}
                Check(bridge.materials.All(m=>!m.material||!m.material.shader.name.StartsWith("TextMeshPro/",StringComparison.OrdinalIgnoreCase)),
                    "Native visual animation table excludes SDF font materials",report);
                report.passed=true;
                Debug.Log("SWAN_JOURNEY_FIRE_CHECKS passed="+report.checks.Count+" triangles="+report.flameTriangles+" legacyEmission=false");
            }
            catch(Exception exception){report.failure=exception.ToString();throw;}
            finally
            {
                Directory.CreateDirectory("Builds/SwanJourney");
                File.WriteAllText("Builds/SwanJourney/fire-checks.json",JsonUtility.ToJson(report,true));
            }
        }
        static void Check(bool condition,string message,Report report)
        {
            if(!condition)throw new InvalidOperationException("Swan fire validation: "+message);
            report.checks.Add(message);
        }
    }
}
