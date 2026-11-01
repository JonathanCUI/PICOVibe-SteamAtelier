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
                var ps=journey.furnaceParticles;
                Check(journey.bakedFirebox && ps && journey.pausedFurnace, "Native particles and paused sprite mesh bound",report);
                Check(!journey.flameVolume.enabled && !journey.flameVolume.gameObject.activeSelf,"Replaced polygonal fire hidden",report);
                var layers=ps.GetComponentsInChildren<ParticleSystem>(true);
                Check(layers.Length==4 && layers.Sum(p=>p.main.maxParticles)==108,"Original fire/core/base/ember layers within 108-particle ceiling",report);
                Check(layers.All(p=>p.main.cullingMode==ParticleSystemCullingMode.AlwaysSimulate && p.main.simulationSpace==ParticleSystemSimulationSpace.Local),"Native camera and miniature-transform compatibility",report);
                Check(layers.All(p=>!p.collision.enabled&&!p.trails.enabled&&!p.lights.enabled&&!p.noise.enabled&&!p.subEmitters.enabled),"No expensive particle modules",report);
                var material=ps.GetComponent<ParticleSystemRenderer>().sharedMaterial;
                Check(material && material.shader.name=="Universal Render Pipeline/Particles/Unlit" && material.GetFloat("_Surface")==1 && material.GetFloat("_Blend")==2,"Original additive campfire uses native-supported unlit material",report);
                var texture=material.GetTexture("_BaseMap");
                Check(texture&&texture.width<=512&&texture.height<=512&&AssetDatabase.GetAssetPath(texture).EndsWith("CampfireSource/FX_fire.png"),"Original campfire texture retained within 512 budget",report);
                Check(layers.All(p=>p.GetComponent<ParticleSystemRenderer>().sharedMaterial==material),"All four layers share one material",report);
                Check(!ps.gameObject.activeSelf && !journey.pausedFurnace.gameObject.activeSelf
                    && layers.All(p=>p.GetComponent<ParticleSystemRenderer>().shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.Off),"Cold fire hidden without shadows",report);
                var mesh=journey.pausedFurnace.GetComponent<MeshFilter>().sharedMesh;report.flameTriangles=mesh.triangles.Length/3;
                Check(mesh.subMeshCount==1&&report.flameTriangles>0&&report.flameTriangles<=432&&mesh.uv.Length==mesh.vertexCount&&mesh.colors32.Length==mesh.vertexCount,"Paused sample preserves original particle colors and UVs in one bounded mesh",report);
                Check(mesh.bounds.size.y<1.5f,"Original campfire gravity adapted to the miniature train scale",report);
                Check(!journey.furnaceLight || (!journey.furnaceLight.enabled && !journey.furnaceLight.gameObject.activeSelf),
                    "Furnace illumination is disabled in the exported scene",report);
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
