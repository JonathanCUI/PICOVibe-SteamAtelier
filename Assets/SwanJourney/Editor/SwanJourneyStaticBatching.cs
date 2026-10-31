using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SwanJourney.Editor
{
    /// <summary>Lossless, scene-generation-only batches for explicitly static canyon scenery.</summary>
    public static class SwanJourneyStaticBatching
    {
        const int VertexBudget = 60000;
        sealed class Candidate
        {
            public MeshRenderer renderer;
            public MeshFilter filter;
            public Transform anchor;
        }
        sealed class Piece
        {
            public Candidate source;
            public int submesh;
            public Material material;
            public string key;
        }
        [Serializable] sealed class BatchEvidence
        {
            public string anchor, material, mesh;
            public int sourceRenderers, outputVertices;
            public long inputTriangles, outputTriangles;
            public float boundsError;
        }
        [Serializable] sealed class Report
        {
            public string scope = "FlatTraySourceV22 marked scenery in the current Mesa original canyon only";
            public int beforeRenderers, afterRenderers, eligibleRenderers, removedRenderers, createdRenderers;
            public long beforeTriangles, afterTriangles;
            public float boundsError;
            public bool trianglesPreserved, boundsPreserved;
            public string[] skippedReasons;
            public BatchEvidence[] batches;
        }

        public static void Optimize(SteamJourney journey, string generatedFolder)
        {
            if (!journey || !journey.sandboxRoot) throw new ArgumentException("A saved journey sandbox is required");
            var content = journey.spatialRoot ? journey.spatialRoot : journey.transform.root;
            string folder = generatedFolder + "/StaticCanyon";
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            var report = new Report();
            var vertexCache = new Dictionary<Mesh, Vector3[]>();
            var reasons = new Dictionary<string, int>();
            var evidence = new List<BatchEvidence>();
            var protectedObjects = ReferencedObjects(journey.gameObject.scene.GetRootGameObjects());
            var canyon = journey.sandboxRoot.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name.StartsWith("Mesa original canyon", StringComparison.Ordinal));
            var before = content.GetComponentsInChildren<MeshRenderer>(true);
            report.beforeRenderers = before.Length; report.beforeTriangles = TriangleCount(before);
            var beforeBounds = BoundsOf(before, content, vertexCache);
            if (!canyon) { Skip(reasons,"No current canyon root"); FinishReport(); return; }

            var candidates = new List<Candidate>();
            foreach (var r in canyon.GetComponentsInChildren<MeshRenderer>(true))
            {
                string reason = Eligible(r,canyon,protectedObjects,out var filter,out var anchor);
                if (reason != null) { Skip(reasons,reason); continue; }
                candidates.Add(new Candidate {renderer=r,filter=filter,anchor=anchor});
            }
            report.eligibleRenderers = candidates.Count;
            var groups = new Dictionary<string,List<Piece>>();
            foreach (var c in candidates)
            {
                var mats=c.renderer.sharedMaterials;
                for(int s=0;s<c.filter.sharedMesh.subMeshCount;s++)
                {
                    string key=Key(c,mats[s]);
                    if(!groups.TryGetValue(key,out var list))groups.Add(key,list=new List<Piece>());
                    list.Add(new Piece{source=c,submesh=s,material=mats[s],key=key});
                }
            }

            // All of a renderer's submeshes must be included before it can be removed.
            // Singleton material groups are retained in-place, avoiding a one-for-one copy.
            var accepted=new HashSet<Candidate>(candidates);
            bool narrowed;
            do
            {
                narrowed=false;
                foreach(var group in groups.Values)
                {
                    var live=group.Where(p=>accepted.Contains(p.source)).ToArray();
                    if(live.Select(p=>p.source).Distinct().Count()>=2)continue;
                    foreach(var p in live)if(accepted.Remove(p.source))narrowed=true;
                }
            } while(narrowed);

            int serial=0;
            foreach(var group in groups.OrderBy(p=>p.Key,StringComparer.Ordinal))
            {
                var pieces=group.Value.Where(p=>accepted.Contains(p.source)).ToArray();
                if(pieces.Length==0)continue;
                var chunk=new List<Piece>();int vertices=0;
                foreach(var p in pieces)
                {
                    int cost=p.source.filter.sharedMesh.vertexCount;
                    if(chunk.Count>0 && vertices+cost>VertexBudget){CreateBatch(chunk,serial++);chunk.Clear();vertices=0;}
                    chunk.Add(p);vertices+=cost;
                }
                if(chunk.Count>0)CreateBatch(chunk,serial++);
            }
            // Mutation happens only after every generated batch passed geometry validation.
            foreach(var c in accepted)
            {
                UnityEngine.Object.DestroyImmediate(c.renderer);
                UnityEngine.Object.DestroyImmediate(c.filter);
            }
            report.removedRenderers=accepted.Count;report.createdRenderers=serial;
            FinishReport();

            void CreateBatch(List<Piece> pieces,int index)
            {
                var sample=pieces[0].source;var anchor=sample.anchor;
                var combine=new CombineInstance[pieces.Count];long expected=0;
                Bounds expectedBounds=new Bounds();bool first=true;
                for(int i=0;i<pieces.Count;i++)
                {
                    var p=pieces[i];var c=p.source;var mesh=c.filter.sharedMesh;
                    combine[i]=new CombineInstance{mesh=mesh,subMeshIndex=p.submesh,transform=anchor.worldToLocalMatrix*c.filter.transform.localToWorldMatrix,
                        lightmapScaleOffset=c.renderer.lightmapScaleOffset,realtimeLightmapScaleOffset=c.renderer.realtimeLightmapScaleOffset};
                    expected+=(long)mesh.GetIndexCount(p.submesh)/3;
                    // A material batch contains one submesh, not the source mesh's entire
                    // vertex buffer. Other materials may have vertices outside this part.
                    var bounds=GeometryBounds(mesh,combine[i].transform,vertexCache,p.submesh);
                    if(first){expectedBounds=bounds;first=false;}else expectedBounds.Encapsulate(bounds);
                }
                var output=new Mesh{name="Swan static canyon "+index.ToString("0000"),indexFormat=IndexFormat.UInt32};
                bool lightmapped=sample.renderer.lightmapIndex>=0&&sample.renderer.lightmapIndex<65534;
                output.CombineMeshes(combine,true,true,lightmapped);
                output.bounds=GeometryBounds(output,Matrix4x4.identity,vertexCache);
                long actual=(long)output.GetIndexCount(0)/3;
                float error=BoundsError(expectedBounds,output.bounds);
                float tolerance=Mathf.Max(.0005f,expectedBounds.size.magnitude*.00002f);
                if(expected!=actual||error>tolerance)
                {
                    UnityEngine.Object.DestroyImmediate(output);
                    throw new InvalidOperationException("Static batch geometry changed: "+PathOf(anchor)+" triangles="+expected+"/"+actual+" boundsError="+error);
                }
                string path=folder+"/Canyon_"+index.ToString("0000")+".asset";
                var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(saved){EditorUtility.CopySerialized(output,saved);UnityEngine.Object.DestroyImmediate(output);EditorUtility.SetDirty(saved);output=saved;}
                else AssetDatabase.CreateAsset(output,path);
                var go=new GameObject("Swan static scenery "+index.ToString("0000"),typeof(MeshFilter),typeof(MeshRenderer));
                go.transform.SetParent(anchor,false);go.layer=sample.renderer.gameObject.layer;
                go.GetComponent<MeshFilter>().sharedMesh=output;
                var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=pieces[0].material;
                renderer.shadowCastingMode=sample.renderer.shadowCastingMode;renderer.receiveShadows=sample.renderer.receiveShadows;
                renderer.lightProbeUsage=sample.renderer.lightProbeUsage;renderer.reflectionProbeUsage=sample.renderer.reflectionProbeUsage;
                renderer.probeAnchor=sample.renderer.probeAnchor;renderer.lightProbeProxyVolumeOverride=sample.renderer.lightProbeProxyVolumeOverride;
                renderer.lightmapIndex=sample.renderer.lightmapIndex;renderer.realtimeLightmapIndex=sample.renderer.realtimeLightmapIndex;
                renderer.lightmapScaleOffset=lightmapped?new Vector4(1,1,0,0):sample.renderer.lightmapScaleOffset;
                renderer.realtimeLightmapScaleOffset=lightmapped?new Vector4(1,1,0,0):sample.renderer.realtimeLightmapScaleOffset;
                renderer.sortingLayerID=sample.renderer.sortingLayerID;renderer.sortingOrder=sample.renderer.sortingOrder;
                renderer.renderingLayerMask=sample.renderer.renderingLayerMask;renderer.motionVectorGenerationMode=sample.renderer.motionVectorGenerationMode;
                renderer.allowOcclusionWhenDynamic=sample.renderer.allowOcclusionWhenDynamic;
                evidence.Add(new BatchEvidence{anchor=PathOf(anchor),material=pieces[0].material.name,mesh=path,
                    sourceRenderers=pieces.Select(p=>p.source).Distinct().Count(),outputVertices=output.vertexCount,inputTriangles=expected,outputTriangles=actual,boundsError=error});
            }
            void FinishReport()
            {
                var after=content.GetComponentsInChildren<MeshRenderer>(true);
                report.afterRenderers=after.Length;report.afterTriangles=TriangleCount(after);
                report.boundsError=BoundsError(beforeBounds,BoundsOf(after,content,vertexCache));
                report.trianglesPreserved=report.beforeTriangles==report.afterTriangles;
                report.boundsPreserved=report.boundsError<Mathf.Max(.001f,beforeBounds.size.magnitude*.00002f);
                report.skippedReasons=reasons.Select(p=>p.Key+": "+p.Value).ToArray();report.batches=evidence.ToArray();
                Directory.CreateDirectory("Builds/SwanJourney");
                File.WriteAllText("Builds/SwanJourney/static-batching.json",JsonUtility.ToJson(report,true));
                if(!report.trianglesPreserved||!report.boundsPreserved)throw new InvalidOperationException("Canyon static batching changed geometry; see static-batching.json");
                AssetDatabase.SaveAssets();
                Debug.Log("SWAN_JOURNEY_STATIC_BATCHES renderers="+report.beforeRenderers+"->"+report.afterRenderers+" removed="+report.removedRenderers+" batches="+report.createdRenderers+" triangles="+report.afterTriangles+" preserved=true boundsError="+report.boundsError);
            }
        }

        static string Eligible(MeshRenderer r,Transform canyon,HashSet<UnityEngine.Object> protectedObjects,out MeshFilter filter,out Transform anchor)
        {
            filter=r.GetComponent<MeshFilter>();anchor=null;
            if(!r.enabled||!ActiveTo(r.transform,canyon))return "Disabled or preserved historical geometry";
            if(!filter||!filter.sharedMesh)return "No mesh";
            if(!r.GetComponent<FlatTraySourceV22>())return "No static provenance marker";
            if(protectedObjects.Contains(r)||protectedObjects.Contains(filter)||protectedObjects.Contains(r.gameObject)||protectedObjects.Contains(r.transform))return "Runtime serialized reference";
            if(r.HasPropertyBlock())return "Material property block";
            if(r.additionalVertexStreams)return "Additional vertex stream";
            if(r.GetComponent<Animator>()||r.GetComponent<Animation>()||r.GetComponent<SkinnedMeshRenderer>())return "Animated component";
            if(r.GetComponent<TMPro.TMP_Text>()||r.GetComponent<ParticleSystem>())return "Text or particles";
            if(!filter.sharedMesh.isReadable)return "Unreadable mesh";
            if(filter.sharedMesh.vertexCount>VertexBudget)return "Already large static mesh";
            if(r.transform.localToWorldMatrix.determinant<=.00000001f)return "Mirrored or singular transform";
            var mats=r.sharedMaterials;
            if(mats.Length!=filter.sharedMesh.subMeshCount||mats.Any(m=>!m||m.renderQueue>=2500))return "Transparent or ambiguous submesh materials";
            for(int i=0;i<filter.sharedMesh.subMeshCount;i++)if(filter.sharedMesh.GetTopology(i)!=MeshTopology.Triangles)return "Non-triangle topology";
            for(var t=r.transform;t&&t!=canyon;t=t.parent)
            {
                if(t.GetComponents<MonoBehaviour>().Any(b=>b&&!(b is FlatTraySourceV22)))return "Behaviour in static candidate hierarchy";
                if(t.GetComponent<Animator>()||t.GetComponent<Animation>()||t.GetComponent<LODGroup>())return "Animation or LOD ancestor";
                if(IsStaticGroup(t.name)){anchor=t;break;}
            }
            if(!anchor)return "Outside explicit static scenery groups";
            // Do not hoist geometry across a referenced transform that could be moved separately.
            for(var t=r.transform.parent;t&&t!=anchor;t=t.parent)
                if(protectedObjects.Contains(t)||protectedObjects.Contains(t.gameObject)){anchor=t;break;}
            return null;
        }
        static bool IsStaticGroup(string name)=>name.StartsWith("Fixed original rock meshes",StringComparison.Ordinal)||name.StartsWith("Continuous terrain",StringComparison.Ordinal)||name.StartsWith("Original vegetation full meshes",StringComparison.Ordinal)||name.StartsWith("Original dry grass and wildflowers",StringComparison.Ordinal);
        static HashSet<UnityEngine.Object> ReferencedObjects(GameObject[] roots)
        {
            var result=new HashSet<UnityEngine.Object>();
            foreach(var component in roots.SelectMany(r=>r.GetComponentsInChildren<MonoBehaviour>(true)))
            {
                if(!component||component is FlatTraySourceV22)continue;
                var so=new SerializedObject(component);var p=so.GetIterator();
                while(p.Next(true))
                {
                    if(p.propertyType!=SerializedPropertyType.ObjectReference||p.propertyPath=="m_GameObject"||p.propertyPath=="m_Script")continue;
                    var value=p.objectReferenceValue;
                    if(value is GameObject go&&go.scene.IsValid())result.Add(go);
                    else if(value is Component c&&c.gameObject.scene.IsValid())result.Add(c);
                }
            }
            return result;
        }
        static string Key(Candidate c,Material material)
        {
            var r=c.renderer;var layout=string.Join(";",c.filter.sharedMesh.GetVertexAttributes().Select(a=>a.attribute+":"+a.format+":"+a.dimension+":"+a.stream));
            var centre=c.anchor.InverseTransformPoint(c.filter.transform.TransformPoint(c.filter.sharedMesh.bounds.center));
            // Keep distant rocks in separate batches for coarse culling and rising reveal feedback.
            string cell=Mathf.FloorToInt(centre.x/8)+","+Mathf.FloorToInt(centre.z/8);
            return c.anchor.GetInstanceID()+"|"+cell+"|"+material.GetInstanceID()+"|"+r.gameObject.layer+"|"+r.shadowCastingMode+"|"+r.receiveShadows+"|"+r.lightmapIndex+"|"+r.realtimeLightmapIndex+"|"+r.lightProbeUsage+"|"+r.reflectionProbeUsage+"|"+(r.probeAnchor?r.probeAnchor.GetInstanceID():0)+"|"+(r.lightProbeProxyVolumeOverride?r.lightProbeProxyVolumeOverride.GetInstanceID():0)+"|"+r.sortingLayerID+"|"+r.sortingOrder+"|"+r.renderingLayerMask+"|"+r.motionVectorGenerationMode+"|"+layout;
        }
        static bool ActiveTo(Transform node,Transform root){for(var t=node;t;t=t.parent){if(!t.gameObject.activeSelf)return false;if(t==root)return true;}return false;}
        static long TriangleCount(IEnumerable<MeshRenderer> renderers)
        {
            long count=0;foreach(var r in renderers){var f=r.GetComponent<MeshFilter>();if(!f||!f.sharedMesh)continue;for(int i=0;i<f.sharedMesh.subMeshCount;i++)if(f.sharedMesh.GetTopology(i)==MeshTopology.Triangles)count+=(long)f.sharedMesh.GetIndexCount(i)/3;}return count;
        }
        static Bounds BoundsOf(IEnumerable<MeshRenderer> renderers,Transform root,Dictionary<Mesh,Vector3[]> cache)
        {
            var bounds=new Bounds();bool first=true;foreach(var r in renderers){var f=r.GetComponent<MeshFilter>();if(!f||!f.sharedMesh)continue;var b=GeometryBounds(f.sharedMesh,root.worldToLocalMatrix*f.transform.localToWorldMatrix,cache);if(first){bounds=b;first=false;}else bounds.Encapsulate(b);}return bounds;
        }
        static Bounds GeometryBounds(Mesh mesh,Matrix4x4 matrix,Dictionary<Mesh,Vector3[]> cache,int submesh=-1)
        {
            if(!mesh.isReadable)return TransformBounds(mesh.bounds,matrix);
            if(!cache.TryGetValue(mesh,out var vertices)){vertices=mesh.vertices;cache.Add(mesh,vertices);}
            var bounds=new Bounds(matrix.MultiplyPoint3x4(Vector3.zero),Vector3.zero);bool first=true;
            int start=submesh<0?0:submesh,end=submesh<0?mesh.subMeshCount:submesh+1;
            for(int s=start;s<end;s++)foreach(int index in mesh.GetIndices(s))
            {
                var position=matrix.MultiplyPoint3x4(vertices[index]);
                if(first){bounds=new Bounds(position,Vector3.zero);first=false;}else bounds.Encapsulate(position);
            }
            return bounds;
        }
        static Bounds TransformBounds(Bounds b,Matrix4x4 matrix){var result=new Bounds(matrix.MultiplyPoint3x4(b.center),Vector3.zero);for(int i=0;i<8;i++)result.Encapsulate(matrix.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));return result;}
        static float BoundsError(Bounds a,Bounds b)=>Mathf.Max((a.min-b.min).magnitude,(a.max-b.max).magnitude);
        static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
        static void Skip(Dictionary<string,int> reasons,string reason){reasons.TryGetValue(reason,out int count);reasons[reason]=count+1;}
    }
}
