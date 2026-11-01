using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SwanJourney.Editor
{
    public static class SwanJourneyPerformanceProbeBuild
    {
        public static void Configure(SteamJourney journey,SwanJourneyInteraction interaction)
        {
            var probe=interaction.gameObject.AddComponent<SwanJourneyPerformanceProbe>();
            probe.journey=journey;probe.interaction=interaction;probe.content=journey.spatialRoot;
            var definitions=journey.gameObject.scene.GetRootGameObjects()
                .SelectMany(r=>r.GetComponentsInChildren<SwanJourneyVisualBridge>(true))
                .SelectMany(b=>b.materials).Where(m=>m.kind==JourneyNativeMaterialKind.Surface).Select(m=>m.material).ToHashSet();
            string folder=SwanJourneyBuild.Generated+"/PerformanceProbe";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            var pairs=new List<SwanJourneyPerformanceProbe.Surface>();
            foreach(var original in probe.content.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Distinct())
            {
                if(!original||!definitions.Contains(original)||original.renderQueue>=2450||!original.shader||original.shader.name!="Standard")continue;
                var simple=new Material(original){name=original.name+" · diagnostic scalar surface"};
                foreach(var property in simple.GetTexturePropertyNames())simple.SetTexture(property,null);
                foreach(var keyword in new[]{"_NORMALMAP","_METALLICGLOSSMAP","_PARALLAXMAP","_DETAIL_MULX2","_SPECGLOSSMAP","_OCCLUSIONMAP"})simple.DisableKeyword(keyword);
                // Keep base color, scalar metallic/smoothness, emission and alpha.
                // Text/transparent particles and native gesture materials are excluded.
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(original,out string guid,out long id);
                string path=folder+"/"+guid+"_"+id+".mat";
                var existing=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(existing){EditorUtility.CopySerialized(simple,existing);Object.DestroyImmediate(simple);simple=existing;EditorUtility.SetDirty(simple);}
                else AssetDatabase.CreateAsset(simple,path);
                pairs.Add(new SwanJourneyPerformanceProbe.Surface{original=original,simple=simple});
            }
            probe.surfaces=pairs.ToArray();
            Debug.Log("SWAN_PERFORMANCE_PROBE_PREPARED surfacePairs="+pairs.Count+" default=normal timeoutSeconds=20");
        }
    }
}
