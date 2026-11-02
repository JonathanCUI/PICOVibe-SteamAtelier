using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace SwanJourney.Editor
{
    internal static class SwanJourneyValleyShellBuild
    {
        const string Folder=SwanJourneyBuild.Generated+"/ValleyShell";
        internal sealed class Part
        {
            public MeshRenderer source;
            public Mesh mesh;
            public int[][] indices,islands,faceIds;
            public int triangles,removed;public float localTolerance;
        }
        static int Find(int[] parent,int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
        static bool Opaque(Material m) => m && m.shader && m.shader.name=="Universal Render Pipeline/Lit"
            && m.renderQueue<2450 && m.GetFloat("_Surface")==0 && m.GetFloat("_AlphaClip")==0 && m.GetFloat("_Cull")!=1;

        static HashSet<Transform> MovingRoots(SteamJourney j)
        {
            var roots=new HashSet<Transform>();
            foreach(var obj in new Object[]{j,j.engine})
            using(var so=new SerializedObject(obj))
            {
                var p=so.GetIterator();
                while(p.Next(true))
                {
                    if(p.propertyType!=SerializedPropertyType.ObjectReference || !(p.objectReferenceValue is Transform t))continue;
                    if(t!=j.trainAnchor && t!=j.engine.locomotive && t.IsChildOf(j.trainAnchor))roots.Add(t);
                }
            }
            return roots;
        }
        static void Analyze(SteamJourney source,List<Part> output,out int[] seen)
        {
            var points=new List<Vector3>();var faces=new List<SwanJourneyShellVisibility.Face>();int islandCount=0;
            var preview=EditorSceneManager.NewPreviewScene();
            try
            {
                var clone=Object.Instantiate(source.transform.root.gameObject);
                SceneManager.MoveGameObjectToScene(clone,preview);
                var j=clone.GetComponentInChildren<SteamJourney>(true);
                foreach(var m in j.modules.Concat(j.finishing))m.installed=true;
                j.inSandbox=true;j.inspectionMode=0;j.cutaway=false;j.ApplyVisibility();
                foreach(var d in j.detailMeshes)if(d.filter && d.miniature)d.filter.sharedMesh=d.miniature;
                var originals=source.trainAnchor.GetComponentsInChildren<MeshRenderer>(true);
                var renderers=j.trainAnchor.GetComponentsInChildren<MeshRenderer>(true);
                if(originals.Length!=renderers.Length)throw new InvalidOperationException("Inconsistent cloned locomotive");
                var moving=MovingRoots(j);
                var bridge=j.GetComponent<SwanJourneyVisualBridge>();
                var animated=bridge.materials.Where(m=>m.material&&m.kind!=JourneyNativeMaterialKind.Surface).Select(m=>m.material).ToHashSet();
                var protectedRenderers=new HashSet<Renderer>();
                var refinement=j.GetComponent<StudioSpatialRefineV19>();
                if(refinement && refinement.passengerWindows!=null)foreach(var r in refinement.passengerWindows)if(r)protectedRenderers.Add(r);
                // Only static opaque solids occlude. Looking through an open cab,
                // a wheel gap or glazing therefore cannot rely on a moving blocker.
                for(int i=0;i<renderers.Length;i++)
                {
                    var r=renderers[i];var filter=r.GetComponent<MeshFilter>();
                    if(r.shadowCastingMode==ShadowCastingMode.ShadowsOnly || !r.enabled || !r.gameObject.activeInHierarchy || r.forceRenderingOff || !filter || !filter.sharedMesh)continue;
                    var mesh=filter.sharedMesh;var mats=r.sharedMaterials;
                    if(!mesh.isReadable || !AssetDatabase.GetAssetPath(mesh).StartsWith("Assets/",StringComparison.Ordinal)
                        || mesh.blendShapeCount!=0 || mats.Length!=mesh.subMeshCount || r.HasPropertyBlock() || protectedRenderers.Contains(r)
                        || mats.Any(m=>!Opaque(m)||animated.Contains(m)) || r.GetComponentInParent<Animator>()
                        || moving.Any(t=>r.transform.IsChildOf(t)))continue;
                    if(Enumerable.Range(0,mesh.subMeshCount).Any(k=>mesh.GetTopology(k)!=MeshTopology.Triangles))continue;
                    string materials=string.Join(" ",mats.Select(m=>m.name)).ToLowerInvariant();
                    if(materials.Contains("glass")||materials.Contains("glazing")||materials.Contains("window")||materials.Contains("玻璃"))continue;
                    var vertices=mesh.vertices;var matrix=j.trainAnchor.worldToLocalMatrix*r.localToWorldMatrix;
                    bool flipped=matrix.determinant<0;int offset=points.Count;
                    float scale=Math.Max(matrix.MultiplyVector(Vector3.right).magnitude,Math.Max(matrix.MultiplyVector(Vector3.up).magnitude,matrix.MultiplyVector(Vector3.forward).magnitude));
                    var p=new Part{source=originals[i],mesh=mesh,localTolerance=Math.Min(.018f/Math.Max(scale,1e-8f),mesh.bounds.size.magnitude*.04f),faceIds=new int[mesh.subMeshCount][],indices=new int[mesh.subMeshCount][],islands=new int[mesh.subMeshCount][]};
                    // Weld for connectivity analysis only. Output uses untouched source corner data.
                    var canonical=new Dictionary<Vector3Int,int>();var parent=new int[vertices.Length];
                    for(int k=0;k<vertices.Length;k++)
                    {
                        points.Add(matrix.MultiplyPoint3x4(vertices[k]));
                        var key=Vector3Int.RoundToInt(vertices[k]*100000f);
                        if(!canonical.TryGetValue(key,out int id))canonical[key]=id=k;
                        parent[k]=id;
                    }
                    for(int s=0;s<mesh.subMeshCount;s++)
                    {
                        var index=p.indices[s]=mesh.GetTriangles(s);p.triangles+=index.Length/3;
                        for(int k=0;k<index.Length;k+=3)
                        {
                            int root=Find(parent,index[k]);parent[Find(parent,index[k+1])]=root;parent[Find(parent,index[k+2])]=root;
                        }
                    }
                    var islandIds=new Dictionary<int,int>();
                    for(int s=0;s<mesh.subMeshCount;s++)
                    {
                        var index=p.indices[s];var faceIds=p.faceIds[s]=new int[index.Length/3];var islands=p.islands[s]=new int[index.Length/3];
                        for(int k=0;k<index.Length;k+=3)
                        {
                            int root=Find(parent,index[k]);if(!islandIds.TryGetValue(root,out int id))islandIds[root]=id=islandCount++;
                            islands[k/3]=id;faceIds[k/3]=faces.Count;
                            faces.Add(new SwanJourneyShellVisibility.Face{a=offset+index[k],b=offset+index[k+(flipped?2:1)],c=offset+index[k+(flipped?1:2)],id=faces.Count});
                        }
                    }
                    output.Add(p);
                }
            }
            finally {EditorSceneManager.ClosePreviewScene(preview);}
            if(faces.Count==0)throw new InvalidOperationException("No static opaque geometry for valley shell");
            seen=SwanJourneyShellVisibility.FindVisible(points.ToArray(),faces.ToArray());
            ProtectBoundaries(output,seen);
        }

        // Small connected details remain whole. On larger solids retain a ring of
        // neighbouring original faces behind every surface sampled from outside.
        // In particular this can drop the invisible inner side of a closed boiler wall.
        static void ProtectBoundaries(List<Part> parts,int[] seen)
        {
            foreach(var p in parts)
            {
                var groupCounts=new Dictionary<int,int>();var anySeen=new HashSet<int>();
                var points=p.mesh.vertices.Select(v=>Vector3Int.RoundToInt(v*100000f)).ToArray();
                var rim=new HashSet<Vector3Int>();
                for(int s=0;s<p.indices.Length;s++)for(int k=0;k<p.faceIds[s].Length;k++)
                {
                    int g=p.islands[s][k];groupCounts.TryGetValue(g,out int n);groupCounts[g]=n+1;
                    if(seen[p.faceIds[s][k]]==0)continue;
                    anySeen.Add(g);for(int v=0;v<3;v++)rim.Add(points[p.indices[s][3*k+v]]);
                }
                for(int s=0;s<p.indices.Length;s++)for(int k=0;k<p.faceIds[s].Length;k++)
                {
                    int g=p.islands[s][k];
                    if(groupCounts[g]<=64 && anySeen.Contains(g))seen[p.faceIds[s][k]]=1;
                    else for(int v=0;v<3;v++)if(rim.Contains(points[p.indices[s][3*k+v]]))seen[p.faceIds[s][k]]=1;
                }
            }
        }

        static Mesh Exterior(Part part,int[] seen)
        {
            var source=part.mesh;
            var triangles=part.indices.Select((arr,s)=>arr.Where((v,k)=>seen[part.faceIds[s][k/3]]!=0).ToArray()).ToArray();
            return Compact(source,triangles);
        }
        internal static Mesh Compact(Mesh source,int[][] triangles)
        {
            var used=triangles.SelectMany(a=>a).Distinct().OrderBy(i=>i).ToArray();
            var map=new int[source.vertexCount];for(int i=0;i<used.Length;i++)map[used[i]]=i;
            var result=new Mesh{name=source.name+" · valley shell",indexFormat=used.Length>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            result.SetVertexBufferParams(used.Length,source.GetVertexAttributes());
            using(var data=Mesh.AcquireReadOnlyMeshData(source))
                for(int s=0;s<source.vertexBufferCount;s++)
                {
                    int stride=source.GetVertexBufferStride(s);var input=data[0].GetVertexData<byte>(s).ToArray();var copy=new byte[used.Length*stride];
                    for(int i=0;i<used.Length;i++)Buffer.BlockCopy(input,used[i]*stride,copy,i*stride,stride);
                    result.SetVertexBufferData(copy,0,0,copy.Length,s);
                }
            result.subMeshCount=triangles.Length;var pos=source.vertices;
            var bounds=new Bounds(pos[used[0]],Vector3.zero);foreach(int i in used)bounds.Encapsulate(pos[i]);
            for(int s=0;s<triangles.Length;s++)
            {
                var retained=triangles[s];result.SetTriangles(retained.Select(i=>map[i]).ToArray(),s,false);
                if(retained.Length==0)continue;
                var b=new Bounds(pos[retained[0]],Vector3.zero);foreach(int i in retained)b.Encapsulate(pos[i]);
                var sub=result.GetSubMesh(s);sub.bounds=b;result.SetSubMesh(s,sub,MeshUpdateFlags.DontRecalculateBounds);
            }
            result.bounds=bounds;return result;
        }
        public static void Configure(SteamJourney j)
        {
            if(Application.isPlaying || j.GetComponent<SwanJourneyValleyShell>())throw new InvalidOperationException("Bake from the fresh editable locomotive");
            var candidates=new List<Part>();Analyze(j,candidates,out var seen);
            Directory.CreateDirectory(Folder);if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.Refresh();
            var replacements=new List<SwanJourneyValleyShell.Part>();
            long saved=0,occluded=0,simplified=0;int partial=0;var shapes=new Dictionary<string,Mesh>();
            var retainedSlots=new Dictionary<string,int[]>();
            foreach(var part in candidates)
            {
                part.removed=part.faceIds.Sum(a=>a.Count(id=>seen[id]==0));
                if(part.removed<part.triangles && (part.removed<80 || part.removed<part.triangles*.10f))part.removed=0;
                MeshRenderer exterior=null; int lodSaving=0;
                if(part.removed<part.triangles)
                {
                    string mask=part.removed==0?"all":string.Join("",part.faceIds.SelectMany(a=>a).Select(id=>seen[id]==0?'0':'1'));
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(part.mesh,out string guid,out long localId);
                    string key;
                    using(var hash=System.Security.Cryptography.SHA256.Create())key=guid+"_"+localId+"_"+BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.ASCII.GetBytes(mask+"/"+part.localTolerance.ToString("R",System.Globalization.CultureInfo.InvariantCulture))),0,8).Replace("-","");
                    if(!shapes.TryGetValue(key,out var mesh))
                    {
                        var input=part.removed>0?Exterior(part,seen):part.mesh;
                        var reduced=SwanJourneyShellLod.ReduceParts(input,part.localTolerance);
                        var copy=reduced?reduced:input;
                        if(reduced && input!=part.mesh)Object.DestroyImmediate(input);
                        if(copy==part.mesh){shapes[key]=null;continue;}
                        var slots=Enumerable.Range(0,copy.subMeshCount).Where(s=>copy.GetIndexCount(s)>0).ToArray();
                        if(slots.Length!=copy.subMeshCount)
                        {
                            var packed=Compact(copy,slots.Select(s=>copy.GetTriangles(s)).ToArray());
                            Object.DestroyImmediate(copy);copy=packed;retainedSlots[key]=slots;
                        }
                        string path=Folder+"/"+key+".asset";
                        mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                        if(mesh){EditorUtility.CopySerialized(copy,mesh);Object.DestroyImmediate(copy);EditorUtility.SetDirty(mesh);}
                        else{mesh=copy;AssetDatabase.CreateAsset(mesh,path);}shapes[key]=mesh;
                    }
                    if(!mesh)continue;
                    lodSaving=part.triangles-part.removed-(int)SwanJourneyWorkshopMeshBudget.Triangles(mesh);
                    var go=new GameObject("Valley shell · "+part.source.name,typeof(MeshFilter),typeof(MeshRenderer));
                    go.transform.SetParent(part.source.transform,false);go.layer=part.source.gameObject.layer;
                    go.GetComponent<MeshFilter>().sharedMesh=mesh;exterior=go.GetComponent<MeshRenderer>();
                    EditorUtility.CopySerialized(part.source,exterior);
                    if(retainedSlots.TryGetValue(key,out var materialSlots))
                    {
                        var materials=part.source.sharedMaterials;
                        exterior.sharedMaterials=materialSlots.Select(s=>materials[s]).ToArray();
                    }
                    exterior.enabled=true;go.SetActive(false);partial++;
                }
                replacements.Add(new SwanJourneyValleyShell.Part{original=part.source,exterior=exterior});occluded+=part.removed;simplified+=lodSaving;saved+=part.removed+lodSaving;
            }
            var shell=j.gameObject.AddComponent<SwanJourneyValleyShell>();shell.parts=replacements.ToArray();j.valleyShell=shell;
            EditorUtility.SetDirty(shell);EditorUtility.SetDirty(j);AssetDatabase.SaveAssets();
            Debug.Log($"VALLEY_SHELL sources={candidates.Count} trianglesConsidered={candidates.Sum(p=>p.triangles)} removed={saved} hiddenFaces={occluded} exteriorLod={simplified} hiddenRenderers={replacements.Count-partial} partialRenderers={partial} partialMeshes={shapes.Values.Count(m=>m!=null)}");
        }
    }
}
