using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using ByteDance.PICO.SpatialAdapter.Component;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SwanJourney.Editor
{
    internal static class SwanJourneyValleyTrainBuild
    {
        const string Folder = SwanJourneyBuild.Generated + "/DrivingTrain";

        internal static void RetireSourceShell(SteamJourney source)
        {
            if (!source.valleyTrain || !source.valleyShell) throw new InvalidOperationException("Expected both baked trains.");
            var shell=source.valleyShell;
            source.valleyShell=null;
            foreach (var part in shell.parts) if (part.exterior) Object.DestroyImmediate(part.exterior.gameObject);
            Object.DestroyImmediate(shell);
            EditorUtility.SetDirty(source);
        }

        internal static void Configure(SteamJourney source)
        {
            if (!source.valleyShell || source.valleyTrain || !source.fixedValleyDaylight)
                throw new InvalidOperationException("Build the independent train after the exterior and daylight passes.");
            Directory.CreateDirectory(Folder);
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.Refresh();
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var originalRoot = source.transform.root;
                var copy = Object.Instantiate(originalRoot.gameObject);
                SceneManager.MoveGameObjectToScene(copy, preview);
                var j = copy.GetComponentInChildren<SteamJourney>(true);
                var live = originalRoot.GetComponentsInChildren<Transform>(true);
                var cloned = copy.GetComponentsInChildren<Transform>(true);
                if (live.Length != cloned.Length) throw new InvalidOperationException("Source clone changed hierarchy.");
                var original = cloned.Select((t,i)=>(t, live[i])).ToDictionary(x=>x.t, x=>x.Item2);
                foreach (var part in j.modules.Concat(j.finishing)) part.installed = true;
                j.inSandbox = true; j.cutaway = false; j.inspectionMode = 0;
                j.trainAnchor.SetParent(j.sandboxRoot.transform, false);
                j.trainAnchor.localScale = Vector3.one * .255f;
                j.sandboxRoot.SetActive(true); j.workshopRoot.SetActive(false);
                foreach (var d in j.detailMeshes) if (d.filter && d.miniature) d.filter.sharedMesh = d.miniature;
                j.ApplyVisibility();
                var roots = new[] {j.trainAnchor}.Concat(j.coaches).ToArray();
                var pivots = new HashSet<Transform>(roots);
                foreach (var component in new Component[]{j,j.engine})
                using (var so = new SerializedObject(component))
                {
                    var property = so.GetIterator();
                    while (property.Next(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue is Transform t
                            && t!=j.engine.locomotive && roots.Any(root=>t.IsChildOf(root))) pivots.Add(t);
                }
                var fleet = source.gameObject.AddComponent<SwanJourneyValleyTrain>();
                fleet.journey = source;
                fleet.originals = roots.Select(t=>original[t]).ToArray();
                fleet.vehicles = new Transform[roots.Length];
                var links = new List<SwanJourneyValleyTrain.PoseLink>();
                for (int i=0; i<roots.Length; i++)
                {
                    var vehicle = new Vehicle(i, roots[i], source.sandboxRoot.transform, pivots, original, links);
                    fleet.vehicles[i] = vehicle.root;
                    vehicle.Build(j);
                    if (i == 0)
                    {
                        fleet.drivingChimney = vehicle.CopyEffect(j.bakedChimney).GetComponent<SwanJourneyChimneySmoke>();
                        fleet.drivingChimney.journey = source;
                        fleet.drivingChimney.tunnel = source.bakedChimney.tunnel;
                        fleet.drivingSparks = vehicle.CopyEffect(j.sparks).GetComponent<ParticleSystem>();
                    }
                    vehicle.root.gameObject.SetActive(false);
                }
                fleet.pose = links.ToArray();
                source.valleyTrain = fleet;
                EditorUtility.SetDirty(source); EditorUtility.SetDirty(fleet);
                AssetDatabase.SaveAssets();
            }
            finally {EditorSceneManager.ClosePreviewScene(preview);}
        }

        sealed class Vehicle
        {
            public readonly Transform root;
            readonly int number;
            readonly Transform sourceRoot;
            readonly HashSet<Transform> pivots;
            readonly Dictionary<Transform,Transform> original;
            readonly Dictionary<Transform,Transform> copied = new Dictionary<Transform,Transform>();
            readonly List<SwanJourneyValleyTrain.PoseLink> links;
            readonly List<MeshRenderer> body = new List<MeshRenderer>();
            int batch;

            public Vehicle(int index, Transform source, Transform parent, HashSet<Transform> pivots,
                Dictionary<Transform,Transform> original, List<SwanJourneyValleyTrain.PoseLink> links)
            {
                number = index; sourceRoot = source; this.pivots = pivots; this.original = original; this.links = links;
                root = new GameObject(index==0 ? "Driving locomotive" : "Driving coach "+index).transform;
                root.SetParent(parent,false); CopyLocal(source,root); copied[source]=root;
                root.gameObject.AddComponent<SwanJourneyTransformSyncBudget>();
                Link(source,root);
            }
            static void CopyLocal(Transform from, Transform to)
            {
                to.SetLocalPositionAndRotation(from.localPosition,from.localRotation);
                to.localScale=from.localScale; to.gameObject.layer=from.gameObject.layer;
            }
            void Link(Transform src,Transform dst) => links.Add(new SwanJourneyValleyTrain.PoseLink{source=original[src],display=dst});
            Transform Node(Transform t)
            {
                if(copied.TryGetValue(t,out var node))return node;
                var parent=Node(t.parent);
                node=new GameObject(t.name).transform;node.SetParent(parent,false);CopyLocal(t,node);
                copied[t]=node;if(pivots.Contains(t))Link(t,node);return node;
            }
            Transform Owner(Transform t){while(!pivots.Contains(t))t=t.parent;return t;}
            public GameObject CopyEffect(Component effect)
            {
                var source=original[effect.transform];
                var clone=Object.Instantiate(source.gameObject,Node(effect.transform.parent),false);
                clone.name=source.name;
                return clone;
            }
            static string Layout(MeshRenderer r)
            {
                var mesh=r.GetComponent<MeshFilter>().sharedMesh;
                return string.Join("/",r.gameObject.layer,r.shadowCastingMode,r.receiveShadows,r.renderingLayerMask,r.lightProbeUsage,
                    r.reflectionProbeUsage,r.sortingLayerID,r.sortingOrder,r.motionVectorGenerationMode,
                    string.Join(",",mesh.GetVertexAttributes().Select(a=>a.attribute+"-"+a.format+"-"+a.dimension)));
            }
            public void Build(SteamJourney j)
            {
                var opaque=new List<MeshRenderer>();
                foreach(var r in sourceRoot.GetComponentsInChildren<MeshRenderer>())
                {
                    if (!r.enabled || r.forceRenderingOff || r == j.fireJacket || r.transform.IsChildOf(j.bakedChimney.transform)
                        || (j.pausedFurnace && r.transform.IsChildOf(j.pausedFurnace.transform)))continue;
                    if(r.TryGetComponent<TMP_Text>(out var label))
                    {
                        var copy=Object.Instantiate(original[label.transform].gameObject,Node(label.transform.parent),false);
                        copy.name=label.name; continue;
                    }
                    if(r.GetComponentInParent<TMP_Text>())continue;
                    if(!r.TryGetComponent<MeshFilter>(out var f)||!f.sharedMesh)continue;
                    var mesh=f.sharedMesh;var materials=r.sharedMaterials;
                    if(materials.Length==mesh.subMeshCount && materials.Length>0
                        && materials.All(m=>m && m.renderQueue<2450 && m.shader.name=="Universal Render Pipeline/Lit")
                        && mesh.isReadable && r.localToWorldMatrix.determinant>0 && !r.HasPropertyBlock()
                        && !r.additionalVertexStreams && !r.probeAnchor && r.lightmapIndex<0
                        && Enumerable.Range(0,mesh.subMeshCount).All(s=>mesh.GetTopology(s)==MeshTopology.Triangles))
                        opaque.Add(r);
                    else CopyRenderer(r);
                }
                foreach(var group in opaque.GroupBy(r=>(owner:Owner(r.transform),settings:Layout(r))))
                    MakeBatch(group.Key.owner,group.ToList());
                // One hit surface per vehicle supports two-hand stage manipulation.
                // It shares the exported renderer entity, as required by the native input path.
                var hit=body.OrderByDescending(r=>r.bounds.size.sqrMagnitude).FirstOrDefault();
                if(!hit)throw new InvalidOperationException("No exterior hit surface for "+root.name);
                var bounds=hit.GetComponent<MeshFilter>().sharedMesh.bounds;
                SwanJourneyMeshAudit.EnsurePositiveCollider(hit,bounds);
                hit.gameObject.AddComponent<SpatialAdapterHoverEffect>();
            }
            void CopyRenderer(MeshRenderer source)
            {
                var t=Node(source.transform);var r=t.gameObject.AddComponent<MeshRenderer>();
                EditorUtility.CopySerialized(source,r);
                t.gameObject.AddComponent<MeshFilter>().sharedMesh=source.GetComponent<MeshFilter>().sharedMesh;
                body.Add(r);
            }
            void MakeBatch(Transform owner,List<MeshRenderer> renderers)
            {
                if(renderers.Count==1){CopyRenderer(renderers[0]);return;}
                var materials=new List<Material>();var entries=new List<List<CombineInstance>>();
                long expected=0;
                foreach(var r in renderers)
                {
                    var mesh=r.GetComponent<MeshFilter>().sharedMesh;var slots=r.sharedMaterials;
                    var matrix=owner.worldToLocalMatrix*r.localToWorldMatrix;
                    for(int s=0;s<slots.Length;s++)
                    {
                        if(mesh.GetIndexCount(s)==0)continue;
                        int id=materials.IndexOf(slots[s]);if(id<0){id=materials.Count;materials.Add(slots[s]);entries.Add(new List<CombineInstance>());}
                        entries[id].Add(new CombineInstance{mesh=mesh,subMeshIndex=s,transform=matrix});
                        expected+=mesh.GetIndexCount(s);
                    }
                }
                if(expected==0)return;
                var temporary=new List<Mesh>();
                try
                {
                    foreach(var inputs in entries)
                    {
                        var piece=new Mesh{indexFormat=IndexFormat.UInt32};temporary.Add(piece);
                        piece.CombineMeshes(inputs.ToArray(),true,true,false);
                    }
                    var name="Car"+number+"_Batch"+batch++.ToString("D3");
                    var combined=new Mesh{name=name,indexFormat=IndexFormat.UInt32};
                    combined.CombineMeshes(temporary.Select(m=>new CombineInstance{mesh=m}).ToArray(),false,false);
                    combined.RecalculateBounds();
                    if(materials.Count!=combined.subMeshCount || Enumerable.Range(0,materials.Count).Sum(s=>(long)combined.GetIndexCount(s))!=expected)
                        throw new InvalidOperationException("Material batch lost triangles: "+name);
                    var indices=Enumerable.Range(0,combined.subMeshCount).Select(i=>combined.GetTriangles(i)).ToArray();
                    // CombineMeshes may copy a whole vertex stream for each material slot.
                    // Drop only unused vertices. Keep all original triangle corners and attributes.
                    if(indices.SelectMany(a=>a).Distinct().Count()<combined.vertexCount)
                    {
                        var packed=SwanJourneyValleyShellBuild.Compact(combined,indices);
                        Object.DestroyImmediate(combined);combined=packed;combined.name=name;
                    }
                    string path=Folder+"/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if(mesh){EditorUtility.CopySerialized(combined,mesh);Object.DestroyImmediate(combined);EditorUtility.SetDirty(mesh);}
                    else{mesh=combined;AssetDatabase.CreateAsset(mesh,path);}
                    var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
                    go.transform.SetParent(Node(owner),false);go.layer=renderers[0].gameObject.layer;
                    go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();
                    EditorUtility.CopySerialized(renderers[0],renderer);renderer.sharedMaterials=materials.ToArray();
                    body.Add(renderer);
                }
                finally {foreach(var m in temporary)Object.DestroyImmediate(m);}
            }
        }
    }
}
