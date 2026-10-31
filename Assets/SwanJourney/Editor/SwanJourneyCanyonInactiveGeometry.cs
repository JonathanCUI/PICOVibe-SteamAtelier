using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SwanJourney.Editor
{
    public static class SwanJourneyCanyonInactiveGeometry
    {
        [Serializable] sealed class Report
        {
            public int removedRenderers, removedFilters;
            public long excludedTriangles;
            public bool activeGeometryUnchanged;
            public List<string> removedPaths = new List<string>();
        }
        public static void Apply(Transform root)
        {
            var protectedObjects = new HashSet<UnityEngine.Object>();
            foreach (var c in root.gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Component>(true)))
            {
                if (!c || c is Transform || c is Renderer || c is MeshFilter || c is Collider || c is FlatTraySourceV22) continue;
                using (var so = new SerializedObject(c))
                {
                    var p = so.GetIterator();
                    while (p.Next(true)) if (p.propertyType == SerializedPropertyType.ObjectReference && p.propertyPath != "m_GameObject" && p.propertyPath != "m_Script" && p.objectReferenceValue) protectedObjects.Add(p.objectReferenceValue);
                }
            }
            var report = new Report();
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var f = r.GetComponent<MeshFilter>();
                if (!f || !f.sharedMesh || protectedObjects.Contains(r) || protectedObjects.Contains(f)) continue;
                bool hidden = !r.enabled, safe = true;
                for (var t = r.transform; t && t != root; t = t.parent)
                {
                    hidden |= !t.gameObject.activeSelf;
                    if (protectedObjects.Contains(t) || protectedObjects.Contains(t.gameObject) || t.GetComponent<Animation>() || t.GetComponent<Animator>() || t.GetComponent<LODGroup>() || t.GetComponents<MonoBehaviour>().Any(b => b && !(b is FlatTraySourceV22))) { safe = false; break; }
                }
                if (!hidden || !safe) continue;
                report.removedPaths.Add(PathOf(r.transform));
                for (int i=0;i<f.sharedMesh.subMeshCount;i++) report.excludedTriangles += f.sharedMesh.GetIndexCount(i)/3;
                UnityEngine.Object.DestroyImmediate(r); UnityEngine.Object.DestroyImmediate(f);
                report.removedRenderers++; report.removedFilters++;
            }
            report.activeGeometryUnchanged = true;
            File.WriteAllText(SwanJourneyBuild.Output + "/canyon-inactive-geometry.json",JsonUtility.ToJson(report,true));
            Debug.Log("SWAN_JOURNEY_CANYON_INACTIVE removed="+report.removedRenderers+" excludedTriangles="+report.excludedTriangles+" activeGeometryUnchanged=true");
        }
        static string PathOf(Transform t) => t.parent ? PathOf(t.parent)+"/"+t.name : t.name;
    }
}
