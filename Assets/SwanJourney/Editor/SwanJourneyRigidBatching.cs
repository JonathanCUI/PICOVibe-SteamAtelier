using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ByteDance.PICO.SpatialAdapter.Component;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SwanJourney.Editor
{
    // Merge only unreferenced geometry within proven rigid branches. Moving parents, lesson
    // objects, controls, renderer references and animated materials stay intact.
    public static class SwanJourneyRigidBatching
    {
        [Serializable] sealed class Batch
        {
            public string parent, material;
            public int sources, vertices;
            public long trianglesBefore, trianglesAfter;
            public float boundsError;
            public bool hover;
        }
        [Serializable] sealed class Report
        {
            public bool passed, nativePerformanceValidated;
            public string scope = "Opaque unreferenced leaves within reference/component/activation boundaries; geometry unchanged, compound part hover box";
            public int candidates, removedRenderers, createdRenderers, removedColliders, liftedLeaves;
            public List<Batch> batches = new List<Batch>();
        }
        public static void Apply(SteamJourney journey, SwanJourneyVisualBridge visuals)
        {
            if (Application.isPlaying || !journey.SwanInputActive) throw new InvalidOperationException("Derived Swan scene required");
            var roots = journey.gameObject.scene.GetRootGameObjects();
            var protectedObjects = new HashSet<UnityEngine.Object>();
            foreach (var c in roots.SelectMany(r => r.GetComponentsInChildren<Component>(true)))
            {
                if (!c || c is Transform || c is MeshFilter || c is MeshRenderer || c is Collider || c is SpatialAdapterHoverEffect) continue;
                using (var so = new SerializedObject(c))
                {
                    var p = so.GetIterator();
                    while (p.Next(true))
                        if (p.propertyType == SerializedPropertyType.ObjectReference && p.propertyPath != "m_GameObject" && p.propertyPath != "m_Script" && p.objectReferenceValue)
                            protectedObjects.Add(p.objectReferenceValue);
                }
            }
            var grateShafts = new HashSet<Transform>(journey.rockingBars ?? Array.Empty<Transform>());
            var surfaces = new HashSet<Material>(visuals.materials.Where(m => m.kind == JourneyNativeMaterialKind.Surface).Select(m => m.material));
            var candidates = journey.spatialRoot.GetComponentsInChildren<MeshRenderer>(true).Where(r => Eligible(r))
                // Keep the previously generated serial asset names stable when appending moving grate batches.
                .OrderBy(r => grateShafts.Contains(r.transform.parent) ? 1 : 0).ToArray();
            var report = new Report { candidates = candidates.Length };
            var anchors=candidates.ToDictionary(r=>r,r=>BatchAnchor(r.transform.parent));
            string folder = SwanJourneyBuild.Generated + "/RigidBatches";
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            int serial = 0;
            foreach (var group in candidates.GroupBy(r => anchors[r].GetInstanceID() + ":" + r.sharedMaterial.GetInstanceID() + ":" + r.gameObject.layer + ":" + r.shadowCastingMode + ":" + r.receiveShadows + ":" + (r.GetComponent<SpatialAdapterHoverEffect>() != null)))
            {
                var chunk = new List<MeshRenderer>(); int vertices = 0;
                foreach (var r in group)
                {
                    int cost = r.GetComponent<MeshFilter>().sharedMesh.vertexCount;
                    if (chunk.Count > 0 && vertices + cost > 60000) { Combine(chunk); chunk.Clear(); vertices = 0; }
                    chunk.Add(r); vertices += cost;
                }
                Combine(chunk);
            }
            report.passed = true;
            File.WriteAllText(SwanJourneyBuild.Output + "/rigid-batching.json", JsonUtility.ToJson(report, true));
            Debug.Log("SWAN_JOURNEY_RIGID_BATCHES candidates=" + report.candidates + " renderers=" + report.removedRenderers + "->" + report.createdRenderers + " geometryPreserved=true nativeBenefit=unmeasured");

            bool Eligible(MeshRenderer r)
            {
                if (!r.enabled || !r.gameObject.activeSelf || !r.transform.parent || r.transform.childCount != 0 || r.HasPropertyBlock()) return false;
                // The canyon has a separate spatial-cell batching and streaming policy.
                bool underGrateShaft = grateShafts.Contains(r.transform.parent);
                for (var p = r.transform; p; p = p.parent)
                    if (p.name.StartsWith("Mesa original canyon", StringComparison.Ordinal) || p.GetComponent<Animator>() || p.GetComponent<Animation>() || (p.GetComponent<JourneyControl>() && (!underGrateShaft || p == r.transform))) return false;
                var f = r.GetComponent<MeshFilter>();
                if (!f || !f.sharedMesh || !f.sharedMesh.isReadable || f.sharedMesh.subMeshCount != 1 || f.sharedMesh.vertexCount > 20000 || f.sharedMesh.GetTopology(0) != MeshTopology.Triangles) return false;
                if (r.sharedMaterials.Length != 1 || !surfaces.Contains(r.sharedMaterial) || r.sharedMaterial.renderQueue >= 2500 || r.additionalVertexStreams) return false;
                if (r.transform.localToWorldMatrix.determinant <= 0) return false;
                if (r.GetComponents<Collider>().Length > 0 && !r.GetComponent<SpatialAdapterHoverEffect>()) return false;
                if (protectedObjects.Contains(r) || protectedObjects.Contains(f) || protectedObjects.Contains(r.transform) || protectedObjects.Contains(r.gameObject)) return false;
                foreach (var c in r.GetComponents<Component>())
                    if (!c || protectedObjects.Contains(c) || !(c is Transform || c is MeshRenderer || c is MeshFilter || c is BoxCollider || c is SphereCollider || c is CapsuleCollider || c is SpatialAdapterHoverEffect)) return false;
                // Preserve nonstandard triggers and disabled collider state.
                return r.GetComponents<Collider>().All(c => c.enabled && !c.isTrigger && !c.sharedMaterial);
            }
            Transform BatchAnchor(Transform parent)
            {
                // Never cross a serialized reference, custom behavior, activation
                // boundary or reflected coordinate space. In particular each
                // locomotive mechanism and assembly preview stays independently movable.
                var anchor=parent;
                while(anchor!=journey.spatialRoot && anchor.parent && anchor.gameObject.activeSelf &&
                    !protectedObjects.Contains(anchor) && !protectedObjects.Contains(anchor.gameObject) &&
                    anchor.GetComponents<Component>().All(c=>c is Transform) &&
                    anchor.localToWorldMatrix.determinant>0 && anchor.parent.localToWorldMatrix.determinant>0)
                    anchor=anchor.parent;
                return anchor;
            }
            void Combine(List<MeshRenderer> sources)
            {
                if (sources.Count < 2) return;
                var first = sources[0]; var parent = anchors[first];
                var instances = new CombineInstance[sources.Count];
                Bounds bounds = default; bool initialized = false; long triangles = 0;
                for (int i = 0; i < sources.Count; i++)
                {
                    var f = sources[i].GetComponent<MeshFilter>(); var m = f.sharedMesh;
                    var matrix = parent.worldToLocalMatrix * f.transform.localToWorldMatrix;
                    instances[i] = new CombineInstance { mesh = m, subMeshIndex = 0, transform = matrix };
                    triangles += m.GetIndexCount(0) / 3;
                    foreach (var v in m.vertices) { var p = matrix.MultiplyPoint3x4(v); if (!initialized) { bounds = new Bounds(p, Vector3.zero); initialized = true; } else bounds.Encapsulate(p); }
                }
                var mesh = new Mesh { name = "Swan rigid detail " + serial, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(instances, true, true); mesh.RecalculateBounds();
                float error = Mathf.Max((bounds.min - mesh.bounds.min).magnitude, (bounds.max - mesh.bounds.max).magnitude);
                long after = mesh.GetIndexCount(0) / 3;
                if (after != triangles || error > .001f) throw new InvalidOperationException("Rigid batch changed geometry");
                string path = folder + "/Rigid_" + serial.ToString("0000") + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing) { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; EditorUtility.SetDirty(mesh); }
                else AssetDatabase.CreateAsset(mesh, path);
                var go = new GameObject("Swan rigid detail batch " + serial++, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(parent, false); go.layer = first.gameObject.layer;
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = first.sharedMaterial;
                renderer.shadowCastingMode = first.shadowCastingMode; renderer.receiveShadows = first.receiveShadows;
                bool hover = sources.Any(r => r.GetComponent<SpatialAdapterHoverEffect>());
                if (hover)
                {
                    var collider = go.AddComponent<BoxCollider>(); collider.center = bounds.center; collider.size = bounds.size;
                    go.AddComponent<SpatialAdapterHoverEffect>();
                }
                report.batches.Add(new Batch { parent = PathOf(parent), material = first.sharedMaterial.name, sources = sources.Count, vertices = mesh.vertexCount, trianglesBefore = triangles, trianglesAfter = after, boundsError = error, hover = hover });
                foreach (var r in sources)
                {
                    if(r.transform.parent!=parent)report.liftedLeaves++;
                    // Keep empty leaves/names; only unreferenced rendering and hit-test
                    // components migrate to the sibling compound geometry.
                    foreach (var c in r.GetComponents<Collider>()) { UnityEngine.Object.DestroyImmediate(c); report.removedColliders++; }
                    var h = r.GetComponent<SpatialAdapterHoverEffect>(); if (h) UnityEngine.Object.DestroyImmediate(h);
                    // Eligibility proved this leaf and all components have no
                    // serialized references. Remove the now-empty leaf too;
                    // otherwise the adapter still visits it on every frame.
                    UnityEngine.Object.DestroyImmediate(r.gameObject);
                }
                report.removedRenderers += sources.Count; report.createdRenderers++;
            }
        }
        static string PathOf(Transform t) => t.parent ? PathOf(t.parent) + "/" + t.name : t.name;
    }
}
