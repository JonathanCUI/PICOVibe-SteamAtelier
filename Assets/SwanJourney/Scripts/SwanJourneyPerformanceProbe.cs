using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SwanJourney
{
    // Opt-in, development-build-only isolation, controlled through this app's
    // private files directory. No saved gameplay settings or progress changes.
    [DefaultExecutionOrder(90000)]
    public sealed class SwanJourneyPerformanceProbe : MonoBehaviour
    {
        [Serializable] public sealed class Surface { public Material original, simple; }
        public SteamJourney journey;
        public SwanJourneyInteraction interaction;
        public Transform content;
        public Surface[] surfaces=Array.Empty<Surface>();
        public string ActiveProfile {get;private set;}="normal";
        sealed class Saved { public Renderer renderer; public bool enabled; public Material[] original, replacement; }
        readonly List<Saved> saved=new List<Saved>();
        double nextPoll,expires;
        int installed;
        string censusKey;
        [Serializable] sealed class Census
        {
            public string mode,scope="Unity enabled/active state, not native draw calls or GPU visibility";
            public int installed,inspection,renderers,enabledRenderers,enabledMeshRenderers,enabledParticleRenderers,uniqueEnabledMaterials;
            public int animatedBindings,ownedLiveMaterials,sharedGhostDefinitions;
            public long enabledMeshTriangles;
        }
        public int AffectedRenderers=>saved.Count;
        public static bool IsProfile(string profile)=>profile=="normal"||profile=="surface-simple"||profile=="render-off";
        public bool TryApply(string profile)
        {
            if(!IsProfile(profile))return false;
            Restore();
            if(profile=="normal")return true;
            if(!journey||!journey.SwanInputActive||journey.inSandbox||journey.SwanSceneLoading||!content||Busy())return false;
            var lookup=surfaces.Where(s=>s!=null&&s.original&&s.simple).ToDictionary(s=>s.original,s=>s.simple);
            foreach(var r in content.GetComponentsInChildren<Renderer>(true))
            {
                if(!r)continue;
                if(profile=="render-off") {saved.Add(new Saved{renderer=r,enabled=r.enabled});continue;}
                var originals=r.sharedMaterials;
                var replacements=originals.Select(m=>m&&lookup.TryGetValue(m,out var simple)?simple:m).ToArray();
                if(originals.SequenceEqual(replacements))continue;
                saved.Add(new Saved{renderer=r,enabled=r.enabled,original=originals,replacement=replacements});
                r.sharedMaterials=replacements;
            }
            ActiveProfile=profile;installed=Installed();expires=Time.realtimeSinceStartupAsDouble+20;
            EnforceRenderMask();
            Debug.Log("SWAN_PERFORMANCE_PROBE profile="+profile+" affected="+saved.Count+" timeoutSeconds=20 savesUnchanged=true");
            return true;
        }
        bool Busy()=>journey.AutoAssembling||journey.CompletionActive||journey.SwanHeldModule!=null||journey.running||
            (interaction&&(interaction.TwoHandActive||interaction.Exploding||interaction.QuickReturning))||
            (journey.GetComponent<JourneySceneArrival>()?.Active??false);
        int Installed()=>journey.modules.Concat(journey.finishing).Count(m=>m.installed);
        void LateUpdate()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if(!Debug.isDebugBuild)return;
            double now=Time.realtimeSinceStartupAsDouble;
            if(ActiveProfile!="normal" && (!Application.isFocused||now>=expires||!journey||journey.inSandbox||
                journey.SwanSceneLoading||Busy()||Installed()!=installed))Restore();
            if(now>=nextPoll)
            {
                nextPoll=now+1;
                if(now>5&&Application.isFocused&&ActiveProfile=="normal"&&journey&&journey.SwanInputActive&&
                    !journey.SwanSceneLoading&&content&&!Busy())
                {
                    string key=(journey.inSandbox?"canyon":"workshop")+":"+journey.inspectionMode+":"+Installed();
                    if(key!=censusKey){censusKey=key;LogSceneCensus();}
                }
                var path=Path.Combine(Application.persistentDataPath,"swan-performance-probe.txt");
                try
                {
                    if(File.Exists(path))
                    {
                        var profile=File.ReadAllText(path).Trim();File.Delete(path);
                        bool applied=Application.isFocused&&TryApply(profile);
                        Debug.Log("SWAN_PERFORMANCE_PROBE_COMMAND requested="+profile+" applied="+applied);
                    }
                }
                catch(IOException e){Debug.LogWarning("SWAN_PERFORMANCE_PROBE_IO "+e.GetType().Name);}
                catch(UnauthorizedAccessException e){Debug.LogWarning("SWAN_PERFORMANCE_PROBE_IO "+e.GetType().Name);}
            }
            EnforceRenderMask();
#endif
        }
        void LogSceneCensus()
        {
            var all=content.GetComponentsInChildren<Renderer>(true);
            var visible=all.Where(r=>r&&r.enabled&&r.gameObject.activeInHierarchy).ToArray();
            var bridge=journey.GetComponent<SwanJourneyVisualBridge>();
            var census=new Census{mode=journey.inSandbox?"canyon":"workshop",installed=Installed(),inspection=journey.inspectionMode,
                renderers=all.Length,enabledRenderers=visible.Length,enabledMeshRenderers=visible.Count(r=>r is MeshRenderer),
                enabledParticleRenderers=visible.Count(r=>r is ParticleSystemRenderer),
                uniqueEnabledMaterials=visible.SelectMany(r=>r.sharedMaterials).Where(m=>m).Distinct().Count(),
                animatedBindings=bridge?bridge.NativeAnimatedRenderers:0,ownedLiveMaterials=bridge?bridge.NativeOwnedMaterialCount:0,
                sharedGhostDefinitions=bridge?bridge.NativeSharedGhostCount:0};
            foreach(var r in visible)
            {
                var filter=r.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh)continue;
                var mesh=filter.sharedMesh;
                for(int i=0;i<mesh.subMeshCount;i++)if(mesh.GetTopology(i)==MeshTopology.Triangles)census.enabledMeshTriangles+=mesh.GetIndexCount(i)/3;
            }
            Debug.Log("SWAN_JOURNEY_RENDER_CENSUS "+JsonUtility.ToJson(census));
        }
        void EnforceRenderMask()
        {
            if(ActiveProfile!="render-off")return;
            // Only during the bounded diagnostic: author scripts still tick,
            // collision/input stay live and the independent FPS HUD stays visible.
            foreach(var s in saved)if(s.renderer&&s.renderer.enabled)s.renderer.enabled=false;
        }
        public void Restore()
        {
            foreach(var s in saved)
            {
                if(!s.renderer)continue;
                if(ActiveProfile=="render-off")s.renderer.enabled=s.enabled;
                else if(s.original!=null && s.renderer.sharedMaterials.SequenceEqual(s.replacement))s.renderer.sharedMaterials=s.original;
            }
            saved.Clear();
            if(ActiveProfile!="normal")Debug.Log("SWAN_PERFORMANCE_PROBE profile=normal restored=true");
            ActiveProfile="normal";
        }
        void OnApplicationPause(bool paused){if(paused)Restore();}
        void OnDisable()=>Restore();
    }
}
