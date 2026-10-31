using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SwanJourney.Editor
{
    // ConfigureTargets already creates primitive hit volumes. The partner's
    // detailed MeshColliders were still alongside them, so every scale/move
    // updated both representations. Remove only demonstrably redundant ones.
    public static class SwanJourneyColliderBudget
    {
        [Serializable] public sealed class Report
        {
            public bool passed, nativePerformanceValidated;
            public int meshCollidersBefore, meshCollidersAfter, removed, referenceProtected, uncovered;
            public long collisionTrianglesRemoved;
            public List<string> removedPaths = new List<string>();
        }

        public static Report Apply(Transform root)
        {
            if (Application.isPlaying || !root) throw new InvalidOperationException("Editor derived content required");
            var meshes = root.GetComponentsInChildren<MeshCollider>(true);
            var references = new HashSet<Object>();
            foreach (var component in root.gameObject.scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<Component>(true)))
            {
                if (!component || component is Transform || component is Renderer || component is MeshFilter || component is Collider) continue;
                using (var so = new SerializedObject(component))
                {
                    var p = so.GetIterator();
                    while (p.Next(true))
                        if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue is MeshCollider)
                            references.Add(p.objectReferenceValue);
                }
            }
            var report = new Report { meshCollidersBefore = meshes.Length };
            foreach (var mesh in meshes)
            {
                if (references.Contains(mesh)) { report.referenceProtected++; continue; }
                var boxes = mesh.GetComponents<BoxCollider>().AsEnumerable();
                var proxy = mesh.transform.Find("Swan positive-scale hit proxy");
                if (proxy) boxes = boxes.Concat(proxy.GetComponents<BoxCollider>());
                // Match enabled/trigger/material/layer, including currently hidden
                // lesson pieces. Never replace a larger or specialized volume.
                if (!boxes.Any(box => Covers(mesh, box))) { report.uncovered++; continue; }
                var visible = mesh.GetComponent<MeshFilter>();
                var visibleMesh = visible ? visible.sharedMesh : null;
                var renderers = mesh.GetComponents<Renderer>();
                int nodesBefore = mesh.transform.childCount;
                if (mesh.sharedMesh)
                    for (int s = 0; s < mesh.sharedMesh.subMeshCount; s++)
                        report.collisionTrianglesRemoved += (long)mesh.sharedMesh.GetIndexCount(s) / 3;
                report.removedPaths.Add(PathOf(mesh.transform));
                var go = mesh.gameObject;
                Object.DestroyImmediate(mesh);
                if ((visible && visible.sharedMesh != visibleMesh) || go.transform.childCount != nodesBefore ||
                    renderers.Any(r => !r)) throw new InvalidOperationException("Collision cleanup changed visible geometry");
                report.removed++;
            }
            report.meshCollidersAfter = root.GetComponentsInChildren<MeshCollider>(true).Length;
            report.passed = report.meshCollidersBefore - report.meshCollidersAfter == report.removed;
            Directory.CreateDirectory(SwanJourneyBuild.Output);
            File.WriteAllText(SwanJourneyBuild.Output + "/collider-budget.json", JsonUtility.ToJson(report, true));
            if (!report.passed) throw new InvalidOperationException("Collider accounting mismatch");
            Debug.Log("SWAN_JOURNEY_COLLIDER_BUDGET redundantMeshesRemoved=" + report.removed +
                " collisionTrianglesRemoved=" + report.collisionTrianglesRemoved + " protected=" + report.referenceProtected +
                " visibleGeometryUnchanged=true nativeBenefit=unmeasured");
            return report;
        }

        public static bool Covers(MeshCollider mesh, BoxCollider box)
        {
            if (!mesh || !box || !mesh.sharedMesh || mesh.enabled != box.enabled || mesh.isTrigger != box.isTrigger ||
                mesh.sharedMaterial != box.sharedMaterial || mesh.gameObject.layer != box.gameObject.layer) return false;
            if (box.transform != mesh.transform && (box.transform.parent != mesh.transform || !box.gameObject.activeSelf)) return false;
            var bounds = mesh.sharedMesh.bounds;
            var matrix = box.transform.worldToLocalMatrix * mesh.transform.localToWorldMatrix;
            var half = box.size * .5f;
            for (int corner = 0; corner < 8; corner++)
            {
                var signs = new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1);
                var point = matrix.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents, signs)) - box.center;
                if (Mathf.Abs(point.x) > half.x + .0001f || Mathf.Abs(point.y) > half.y + .0001f || Mathf.Abs(point.z) > half.z + .0001f)
                    return false;
            }
            return true;
        }
        static string PathOf(Transform t) => t.parent ? PathOf(t.parent) + "/" + t.name : t.name;
    }
}
