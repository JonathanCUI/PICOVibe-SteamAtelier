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
    // Merge geometry only within proven rigid branches. Shell visibility and
    // paired mesh bindings are remapped explicitly; other references stay protected.
    public static class SwanJourneyRigidBatching
    {
        [Serializable] sealed class Batch
        {
            public string parent, material;
            public int sources, vertices;
            public long trianglesBefore, trianglesAfter;
            public long miniatureTrianglesBefore, miniatureTrianglesAfter;
            public float boundsError;
            public bool hover, shell, detail;
            public int materialSlots;
        }
        [Serializable] sealed class Report
        {
            public bool passed, nativePerformanceValidated;
            public string scope = "Rigid opaque leaves within reference/component/activation boundaries; multi-material submeshes and both geometry levels preserved; fine collider mesh/filter/renderer ownership remains intact";
            public int candidates, removedRenderers, createdRenderers, removedColliders, liftedLeaves;
            public int protectedFineColliderRenderers;
            public List<Batch> batches = new List<Batch>();
        }
        public static void Apply(SteamJourney journey, SwanJourneyVisualBridge visuals)
        {
            if (Application.isPlaying || !journey.SwanInputActive) throw new InvalidOperationException("Derived Swan scene required");
            var roots = journey.gameObject.scene.GetRootGameObjects();
            var shells=new HashSet<Renderer>(journey.shellRenderers??Array.Empty<Renderer>());
            var details=(journey.detailMeshes??Array.Empty<JourneyDetail>()).Where(d=>d!=null&&d.filter)
                .GroupBy(d=>d.filter).Where(g=>g.Count()==1).ToDictionary(g=>g.Key,g=>g.First());
            var protectedObjects = new HashSet<UnityEngine.Object>();
            foreach (var c in roots.SelectMany(r => r.GetComponentsInChildren<Component>(true)))
            {
                if (!c || c is Transform || c is MeshFilter || c is MeshRenderer || c is Collider || c is SpatialAdapterHoverEffect) continue;
                using (var so = new SerializedObject(c))
                {
                    var p = so.GetIterator();
                    while (p.Next(true))
                        if (p.propertyType == SerializedPropertyType.ObjectReference && p.propertyPath != "m_GameObject" && p.propertyPath != "m_Script" && p.objectReferenceValue && !Remappable(c,p))
                            protectedObjects.Add(p.objectReferenceValue);
                }
            }
            var grateShafts = new HashSet<Transform>(journey.rockingBars ?? Array.Empty<Transform>());
            var surfaces = new HashSet<Material>(visuals.materials.Where(m => m.kind == JourneyNativeMaterialKind.Surface).Select(m => m.material));
            // Unity serialization alone is insufficient for the native hit-test
            // exporter: do not leave a MeshCollider behind after moving its
            // visible mesh to another object (0.23.31 failed device input).
            var fineCollisionOwners=journey.spatialRoot.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r=>r.GetComponent<MeshCollider>() || (r.GetComponents<Collider>().Length>0&&!r.GetComponent<SpatialAdapterHoverEffect>()))
                .Select(r=>new {renderer=r,filter=r.GetComponent<MeshFilter>(),colliders=r.GetComponents<Collider>()})
                .Where(x=>x.filter).ToArray();
            var candidates = journey.spatialRoot.GetComponentsInChildren<MeshRenderer>(true).Where(r => Eligible(r))
                // Keep the previously generated serial asset names stable when appending moving grate batches.
                .OrderBy(r => grateShafts.Contains(r.transform.parent) ? 1 : 0).ToArray();
            var report = new Report { candidates = candidates.Length };
            var anchors=candidates.ToDictionary(r=>r,r=>BatchAnchor(r.transform.parent));
            string folder = SwanJourneyBuild.Generated + "/RigidBatches";
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            int serial = 0;
            // Like the earlier 90 Hz workshop, one rigid owner may carry multiple
            // material submeshes. Keep independent motion, visibility and hover
            // boundaries; material identity alone no longer requires another node.
            foreach (var group in candidates.GroupBy(r => anchors[r].GetInstanceID() + ":" + r.gameObject.layer + ":" + r.shadowCastingMode + ":" + r.receiveShadows + ":" + (r.GetComponent<SpatialAdapterHoverEffect>() != null)+":"+r.enabled+":"+shells.Contains(r)+":"+details.ContainsKey(r.GetComponent<MeshFilter>())))
            {
                var chunk = new List<MeshRenderer>(); int vertices = 0;
                foreach (var r in group)
                {
                    var filter=r.GetComponent<MeshFilter>();
                    int cost = Math.Max(filter.sharedMesh.vertexCount,details.TryGetValue(filter,out var detail)?detail.miniature.vertexCount:0);
                    if (chunk.Count > 0 && vertices + cost > 60000) { Combine(chunk); chunk.Clear(); vertices = 0; }
                    chunk.Add(r); vertices += cost;
                }
                Combine(chunk);
            }
            foreach(var item in fineCollisionOwners)
                if(!item.renderer||!item.filter||item.renderer.GetComponent<MeshFilter>()!=item.filter||item.colliders.Any(c=>!c||c.gameObject!=item.renderer.gameObject))
                    throw new InvalidOperationException("A fine collision owner lost its original visible mesh components");
            report.protectedFineColliderRenderers=fineCollisionOwners.Length;
            report.passed = true;
            File.WriteAllText(SwanJourneyBuild.Output + "/rigid-batching.json", JsonUtility.ToJson(report, true));
            Debug.Log("SWAN_JOURNEY_RIGID_BATCHES candidates=" + report.candidates + " renderers=" + report.removedRenderers + "->" + report.createdRenderers + " geometryPreserved=true nativeBenefit=unmeasured");

            bool Remappable(Component owner,SerializedProperty property)
            {
                if(owner!=journey)return false;
                // These two fields only toggle a common shell state or swap the
                // two mesh levels. Any additional reference still protects a leaf.
                if(property.propertyPath.StartsWith("shellRenderers.Array.data[",StringComparison.Ordinal))
                    return property.objectReferenceValue is Renderer renderer&&shells.Contains(renderer);
                return property.propertyPath.StartsWith("detailMeshes.Array.data[",StringComparison.Ordinal)&&
                    property.propertyPath.EndsWith("].filter",StringComparison.Ordinal)&&
                    property.objectReferenceValue is MeshFilter filter&&details.ContainsKey(filter);
            }

            bool Eligible(MeshRenderer r)
            {
                if ((!r.enabled&&!shells.Contains(r)) || !r.gameObject.activeSelf || !r.transform.parent || r.transform.childCount != 0 || r.HasPropertyBlock()) return false;
                // The canyon has a separate spatial-cell batching and streaming policy.
                for (var p = r.transform; p; p = p.parent)
                    // A control's unreferenced decorative leaves may batch inside
                    // that control. BatchAnchor stops at its behavior/reference
                    // boundary, preserving independent lever/button movement.
                    if (p.name.StartsWith("Mesa original canyon", StringComparison.Ordinal) || p.GetComponent<Animator>() || p.GetComponent<Animation>() || (p == r.transform && p.GetComponent<JourneyControl>())) return false;
                var f = r.GetComponent<MeshFilter>();
                if (!f || !f.sharedMesh || !f.sharedMesh.isReadable || f.sharedMesh.subMeshCount != 1 || f.sharedMesh.vertexCount > 20000 || f.sharedMesh.GetTopology(0) != MeshTopology.Triangles) return false;
                if(details.TryGetValue(f,out var d)&&(!d.inspection||!d.miniature||!d.miniature.isReadable||d.miniature.subMeshCount!=1||d.miniature.vertexCount>20000||d.miniature.GetTopology(0)!=MeshTopology.Triangles))return false;
                if (r.sharedMaterials.Length != 1 || !surfaces.Contains(r.sharedMaterial) || r.sharedMaterial.renderQueue >= 2500 || r.additionalVertexStreams) return false;
                if (r.transform.localToWorldMatrix.determinant <= 0) return false;
                if(r.GetComponent<MeshCollider>())return false;
                if(r.GetComponents<Collider>().Length>0&&!r.GetComponent<SpatialAdapterHoverEffect>())return false;
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
                var materials=sources.Select(r=>r.sharedMaterial).Distinct().ToArray();
                var mesh = CombineByMaterial(instances,sources,materials,"Swan rigid detail " + serial);
                float error = Mathf.Max((bounds.min - mesh.bounds.min).magnitude, (bounds.max - mesh.bounds.max).magnitude);
                long after = Triangles(mesh);
                if (after != triangles || error > .001f) throw new InvalidOperationException("Rigid batch changed geometry");
                string path = folder + "/Rigid_" + serial.ToString("0000") + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing) { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; EditorUtility.SetDirty(mesh); }
                else AssetDatabase.CreateAsset(mesh, path);
                var go = new GameObject("Swan rigid detail batch " + serial++, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(parent, false); go.layer = first.gameObject.layer;
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = first.shadowCastingMode; renderer.receiveShadows = first.receiveShadows;renderer.enabled=first.enabled;
                bool hover = sources.Any(r => r.GetComponent<SpatialAdapterHoverEffect>());
                if (hover)
                {
                    var collider = go.AddComponent<BoxCollider>(); collider.center = bounds.center; collider.size = bounds.size;
                    go.AddComponent<SpatialAdapterHoverEffect>();
                }
                bool shell=shells.Contains(first),hasDetail=details.ContainsKey(first.GetComponent<MeshFilter>());
                long miniBefore=0,miniAfter=0;
                if(hasDetail)
                {
                    var miniInstances=new CombineInstance[sources.Count];
                    Bounds miniBounds=default;bool miniInitialized=false;
                    for(int i=0;i<sources.Count;i++)
                    {
                        var filter=sources[i].GetComponent<MeshFilter>();var miniature=details[filter].miniature;
                        var matrix=instances[i].transform;
                        miniInstances[i]=new CombineInstance{mesh=miniature,subMeshIndex=0,transform=matrix};
                        miniBefore+=miniature.GetIndexCount(0)/3;
                        foreach(var v in miniature.vertices){var p=matrix.MultiplyPoint3x4(v);if(!miniInitialized){miniBounds=new Bounds(p,Vector3.zero);miniInitialized=true;}else miniBounds.Encapsulate(p);}
                    }
                    var miniMesh=CombineByMaterial(miniInstances,sources,materials,mesh.name+" miniature");
                    miniAfter=Triangles(miniMesh);
                    if(miniBefore!=miniAfter||(miniMesh.bounds.min-miniBounds.min).magnitude>.001f||(miniMesh.bounds.max-miniBounds.max).magnitude>.001f)
                        throw new InvalidOperationException("Rigid miniature batch changed geometry");
                    string miniPath=folder+"/Rigid_"+(serial-1).ToString("0000")+"_Miniature.asset";
                    var existingMini=AssetDatabase.LoadAssetAtPath<Mesh>(miniPath);
                    if(existingMini){EditorUtility.CopySerialized(miniMesh,existingMini);UnityEngine.Object.DestroyImmediate(miniMesh);miniMesh=existingMini;EditorUtility.SetDirty(miniMesh);}
                    else AssetDatabase.CreateAsset(miniMesh,miniPath);
                    var oldFilters=new HashSet<MeshFilter>(sources.Select(r=>r.GetComponent<MeshFilter>()));
                    journey.detailMeshes=journey.detailMeshes.Where(d=>!oldFilters.Contains(d.filter))
                        .Append(new JourneyDetail{filter=go.GetComponent<MeshFilter>(),inspection=mesh,miniature=miniMesh}).ToArray();
                }
                if(shell)
                {
                    var oldRenderers=new HashSet<Renderer>(sources);
                    journey.shellRenderers=journey.shellRenderers.Where(r=>!oldRenderers.Contains(r)).Append(renderer).ToArray();
                }
                foreach (var r in sources)
                {
                    if(r.transform.parent!=parent)report.liftedLeaves++;
                    // Keep empty leaves/names; only unreferenced rendering and hit-test
                    // components migrate to the sibling compound geometry.
                    foreach (var c in r.GetComponents<Collider>()) { UnityEngine.Object.DestroyImmediate(c); report.removedColliders++; }
                    var h = r.GetComponent<SpatialAdapterHoverEffect>(); if (h) UnityEngine.Object.DestroyImmediate(h);
                    // Known shell/detail references have already been remapped;
                    // eligibility protects every other serialized reference.
                    // Remove the now-empty leaf too;
                    // otherwise the adapter still visits it on every frame.
                    UnityEngine.Object.DestroyImmediate(r.gameObject);
                }
                report.batches.Add(new Batch { parent = PathOf(parent), material = string.Join(";",materials.Select(m=>m.name)), materialSlots=materials.Length,sources = sources.Count, vertices = mesh.vertexCount, trianglesBefore = triangles, trianglesAfter = after, boundsError = error, hover = hover,shell=shell,detail=hasDetail,miniatureTrianglesBefore=miniBefore,miniatureTrianglesAfter=miniAfter });
                report.removedRenderers += sources.Count; report.createdRenderers++;
            }
        }
        static long Triangles(Mesh mesh)=>Enumerable.Range(0,mesh.subMeshCount).Sum(s=>(long)mesh.GetIndexCount(s)/3);
        // The individual sleeper assets already contain wood and hardware submeshes.
        // They share one rigid workshop parent; keep their two material passes.
        internal static void CombineWorkshopSleepers(SteamJourney journey)
        {
            var root = journey.workshopRoot.transform;
            var sleepers = root.Cast<Transform>().Where(t => t.name == "Timber sleeper")
                .Select(t => t.GetComponent<MeshRenderer>()).Where(r => r).ToArray();
            if (sleepers.Length < 2) return;
            var first = sleepers[0];
            var materials = first.sharedMaterials;
            if (materials.Length != 2 || materials.Any(m => !m || m.renderQueue >= 2500)) return;
            var sources = new List<Mesh>();
            var worldToOwner = first.transform.worldToLocalMatrix;
            var tracked = new HashSet<UnityEngine.Object>();
            foreach (var r in sleepers)
            {
                var t = r.transform;
                if (!r.enabled || !t.gameObject.activeSelf || t.childCount != 0 ||
                    t.GetComponents<Component>().Any(c => !(c is Transform || c is MeshFilter || c is MeshRenderer)) ||
                    !r.TryGetComponent<MeshFilter>(out var f) || !f.sharedMesh || !f.sharedMesh.isReadable ||
                    f.sharedMesh.subMeshCount != materials.Length || !r.sharedMaterials.SequenceEqual(materials) ||
                    t.localToWorldMatrix.determinant <= 0 || r.HasPropertyBlock() || r.additionalVertexStreams ||
                    r.gameObject.layer != first.gameObject.layer || r.renderingLayerMask != first.renderingLayerMask ||
                    r.shadowCastingMode != first.shadowCastingMode || r.receiveShadows != first.receiveShadows ||
                    r.sortingLayerID != first.sortingLayerID || r.sortingOrder != first.sortingOrder ||
                    r.probeAnchor != first.probeAnchor || r.reflectionProbeUsage != first.reflectionProbeUsage ||
                    r.lightProbeUsage != first.lightProbeUsage || r.lightmapIndex >= 0 || r.realtimeLightmapIndex >= 0 ||
                    GameObjectUtility.GetStaticEditorFlags(r.gameObject) != GameObjectUtility.GetStaticEditorFlags(first.gameObject)) return;
                if (Enumerable.Range(0,materials.Length).Any(i=>f.sharedMesh.GetTopology(i)!=MeshTopology.Triangles)) return;
                if (sources.Count>0 && !sources[0].GetVertexAttributes().SequenceEqual(f.sharedMesh.GetVertexAttributes())) return;
                sources.Add(f.sharedMesh);
                tracked.UnionWith(new UnityEngine.Object[]{t,t.gameObject,r,f});
            }
            foreach (var component in journey.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)))
            {
                if (!component || component is Transform || tracked.Contains(component)) continue;
                using (var serialized = new SerializedObject(component))
                {
                    var field=serialized.GetIterator();
                    while (field.Next(true))
                        if (field.propertyType==SerializedPropertyType.ObjectReference && tracked.Contains(field.objectReferenceValue)) return;
                }
            }
            var passes = new List<Mesh>();
            Mesh combined = null;
            try
            {
                for (int slot=0;slot<materials.Length;slot++)
                {
                    var pass=new Mesh{indexFormat=IndexFormat.UInt32};passes.Add(pass);
                    var inputs=sleepers.Select((r,i)=>new CombineInstance {mesh=sources[i],subMeshIndex=slot,
                        transform=worldToOwner*r.transform.localToWorldMatrix}).ToArray();
                    pass.CombineMeshes(inputs,true,true,false);
                }
                combined=new Mesh{name="Timber sleepers",indexFormat=IndexFormat.UInt32};
                combined.CombineMeshes(passes.Select(p=>new CombineInstance{mesh=p}).ToArray(),false,false,false);
                combined.RecalculateBounds();
                for(int s=0;s<materials.Length;s++)
                    if(sources.Sum(m=>(long)m.GetIndexCount(s))!=combined.GetIndexCount(s))
                        throw new InvalidOperationException("Timber sleeper batch lost geometry");
                string path=SwanJourneyBuild.Generated+"/RigidBatches/TimberSleepers.asset";
                var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(saved){EditorUtility.CopySerialized(combined,saved);EditorUtility.SetDirty(saved);}
                else{AssetDatabase.CreateAsset(combined,path);saved=combined;combined=null;}
                first.GetComponent<MeshFilter>().sharedMesh=saved;
                foreach(var r in sleepers.Skip(1)){
                    UnityEngine.Object.DestroyImmediate(r.GetComponent<MeshFilter>());
                    UnityEngine.Object.DestroyImmediate(r);
                }
                EditorUtility.SetDirty(first.GetComponent<MeshFilter>());
                Debug.Log("SWAN_SLEEPER_BATCH "+sleepers.Length+" renderers -> 1, material slots "+sleepers.Length*materials.Length+" -> "+materials.Length);
            }
            finally{
                if(combined)UnityEngine.Object.DestroyImmediate(combined);
                foreach(var pass in passes)UnityEngine.Object.DestroyImmediate(pass);
            }
        }
        static Mesh CombineByMaterial(CombineInstance[] instances,List<MeshRenderer> sources,Material[] materials,string name)
        {
            var temporary=new List<Mesh>();
            try
            {
                var submeshes=new List<CombineInstance>();
                foreach(var material in materials)
                {
                    var selected=instances.Where((_,i)=>sources[i].sharedMaterial==material).ToArray();
                    var sub=new Mesh{indexFormat=IndexFormat.UInt32};temporary.Add(sub);
                    sub.CombineMeshes(selected,true,true);
                    long expected=selected.Sum(x=>(long)x.mesh.GetIndexCount(x.subMeshIndex)/3);
                    if(Triangles(sub)!=expected)throw new InvalidOperationException("Material batch lost triangles: "+material.name);
                    submeshes.Add(new CombineInstance{mesh=sub,transform=Matrix4x4.identity});
                }
                var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};
                mesh.CombineMeshes(submeshes.ToArray(),false,false);mesh.RecalculateBounds();
                if(mesh.subMeshCount!=materials.Length)throw new InvalidOperationException("Material slot order changed");
                return mesh;
            }
            finally{foreach(var mesh in temporary)UnityEngine.Object.DestroyImmediate(mesh);}
        }
        static string PathOf(Transform t) => t.parent ? PathOf(t.parent) + "/" + t.name : t.name;
    }
}
