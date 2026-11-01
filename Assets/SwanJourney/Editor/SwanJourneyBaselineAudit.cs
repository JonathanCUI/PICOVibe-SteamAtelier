using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SwanJourney.Editor
{
    // Counts exported candidates and their actual serialized owners. This is a
    // scene census, not a draw-call counter or a claim of native frame time.
    public static class SwanJourneyBaselineAudit
    {
        [Serializable] public sealed class Row
        {
            public string name, path, material;
            public bool active, rendererEnabled;
            public long triangles;
            public string[] components, references;
        }
        [Serializable] public sealed class Report
        {
            public int nodes, meshNodes, activeRenderers, emptyUnreferencedLeaves;
            public Row[] meshes;
        }
        public static Dictionary<UnityEngine.Object, List<string>> References(GameObject[] roots)
        {
            var references = new Dictionary<UnityEngine.Object, List<string>>();
            foreach (var c in roots.SelectMany(r => r.GetComponentsInChildren<Component>(true)))
            {
                // Renderer probe anchors and joint/particle references also protect
                // otherwise empty transforms. Only hierarchy links are excluded.
                if (!c || c is Transform) continue;
                using (var so = new SerializedObject(c))
                {
                    var p = so.GetIterator();
                    while (p.Next(true))
                    {
                        if (p.propertyType != SerializedPropertyType.ObjectReference ||
                            p.propertyPath == "m_GameObject" || p.propertyPath == "m_Script" || !p.objectReferenceValue) continue;
                        if (!references.TryGetValue(p.objectReferenceValue, out var owners))
                            references.Add(p.objectReferenceValue, owners = new List<string>());
                        owners.Add(c.GetType().Name + "." + p.propertyPath);
                    }
                }
            }
            return references;
        }
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(SwanJourneyBuild.ScenePath);
            var roots = scene.GetRootGameObjects();
            var refs = References(roots);
            var transforms = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var rows = new List<Row>();
            int empty = 0;
            foreach (var t in transforms)
            {
                var components = t.GetComponents<Component>();
                if (t.childCount == 0 && components.Length == 1 && !refs.ContainsKey(t) && !refs.ContainsKey(t.gameObject)) empty++;
                var f = t.GetComponent<MeshFilter>(); var r = t.GetComponent<MeshRenderer>();
                if (!f || !f.sharedMesh || !r) continue;
                var owners = new List<string>();
                foreach (var obj in components.Cast<UnityEngine.Object>().Append(t.gameObject))
                    if (obj && refs.TryGetValue(obj, out var names)) owners.AddRange(names);
                rows.Add(new Row { name=t.name, path=PathOf(t), active=t.gameObject.activeInHierarchy,
                    rendererEnabled=r.enabled, triangles=SwanJourneyWorkshopMeshBudget.Triangles(f.sharedMesh),
                    material=string.Join(";",r.sharedMaterials.Select(m=>m?m.name:"null")),
                    components=components.Select(c=>c?c.GetType().Name:"missing").ToArray(), references=owners.Distinct().ToArray() });
            }
            var report = new Report { nodes=transforms.Length, meshNodes=rows.Count,
                activeRenderers=rows.Count(r=>r.active&&r.rendererEnabled), emptyUnreferencedLeaves=empty, meshes=rows.ToArray() };
            File.WriteAllText(SwanJourneyBuild.Output+"/baseline-scene-audit.json",JsonUtility.ToJson(report,true));
            Debug.Log("SWAN_BASELINE_AUDIT nodes="+report.nodes+" meshes="+report.meshNodes+" emptyUnreferencedLeaves="+empty);
        }
        static string PathOf(Transform t) => t.parent ? PathOf(t.parent)+"/"+t.name : t.name;
    }
}
