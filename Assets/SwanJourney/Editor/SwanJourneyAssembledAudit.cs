using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SwanJourney.Editor
{
    // Read-only scene analysis. Authored enable/active state is not native GPU visibility.
    public static class SwanJourneyAssembledAudit
    {
        [Serializable] sealed class Row
        {
            public string name, parent, material;
            public long triangles;
            public string[] references;
        }
        [Serializable] sealed class State
        {
            public int inspectionMode, enabledRenderers, materialInstances;
            public long enabledMeshTriangles;
            public Row[] renderers;
        }
        [Serializable] sealed class Report
        {
            public string scope="Editor all-installed visibility only; no native FPS/draw calls or runtime material-clone measurement";
            public int editorDiscoveredAnimatedBindings,editorOwnedLiveMaterials,editorSharedGhostDefinitions;
            public State[] states;
        }
        public static void Run()
        {
            SwanJourneyBaselineAudit.Run();
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var roots=scene.GetRootGameObjects();
            var journey=UnityEngine.Object.FindObjectOfType<SteamJourney>(true);
            journey.suppressPersistence=true;
            foreach(var m in journey.modules.Concat(journey.finishing))
            {
                m.installed=true;
                if(m.preview)m.preview.SetActive(false);
            }
            journey.stage=journey.modules.Length;
            var refs=SwanJourneyBaselineAudit.References(roots);
            var states=new List<State>();
            for(int mode=0;mode<3;mode++)
            {
                journey.inspectionMode=mode;journey.cutaway=mode!=0;
                journey.ApplyVisibility();
                var renderers=roots.SelectMany(r=>r.GetComponentsInChildren<Renderer>(true))
                    .Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();
                var rows=new List<Row>();
                foreach(var r in renderers)
                {
                    var owners=new List<string>();
                    foreach(var o in r.GetComponents<Component>().Cast<UnityEngine.Object>().Append(r.gameObject))
                        if(o&&refs.TryGetValue(o,out var names))owners.AddRange(names);
                    var f=r.GetComponent<MeshFilter>();
                    rows.Add(new Row{name=r.name,parent=PathOf(r.transform.parent),
                        material=string.Join(";",r.sharedMaterials.Select(m=>m?m.name:"null")),
                        triangles=f&&f.sharedMesh?SwanJourneyWorkshopMeshBudget.Triangles(f.sharedMesh):0,
                        references=owners.Distinct().ToArray()});
                }
                states.Add(new State{inspectionMode=mode,enabledRenderers=renderers.Length,
                    materialInstances=renderers.SelectMany(r=>r.sharedMaterials).Where(m=>m).Distinct().Count(),
                    enabledMeshTriangles=rows.Sum(r=>r.triangles),renderers=rows.ToArray()});
            }
            var bridge=journey.GetComponent<SwanJourneyVisualBridge>();
            // The actual project bridge uses no native calls in Awake. This measures
            // its material ownership in EditMode, not startup producers or device FPS.
            bridge.SendMessage("Awake",SendMessageOptions.RequireReceiver);
            var report=new Report{states=states.ToArray(),editorDiscoveredAnimatedBindings=bridge.NativeAnimatedRenderers,
                editorOwnedLiveMaterials=bridge.NativeOwnedMaterialCount,editorSharedGhostDefinitions=bridge.NativeSharedGhostCount};
            File.WriteAllText(SwanJourneyBuild.Output+"/assembled-scene-audit.json",JsonUtility.ToJson(report,true));
            bridge.SendMessage("OnDestroy",SendMessageOptions.RequireReceiver);
            // Discard this editor-only scenario; never write player saves or the scene.
            EditorSceneManager.OpenScene(SwanJourneyBuild.ScenePath);
            Debug.Log("SWAN_ASSEMBLED_AUDIT "+string.Join(";",states.Select(s=>"mode="+s.inspectionMode+" renderers="+s.enabledRenderers+" materials="+s.materialInstances+" triangles="+s.enabledMeshTriangles)));
        }
        static string PathOf(Transform t)=>!t?"":(t.parent?PathOf(t.parent)+"/":"")+t.name;
    }
}
