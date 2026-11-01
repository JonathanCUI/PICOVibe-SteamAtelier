using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SwanJourney.Editor
{
    // Offline, after metre conversion. Only the pebble drawables in the derived canyon
    // are changed. Keeping complete connected islands preserves their original corners.
    internal static class SwanJourneyPebbleBudget
    {
        const string PebbleMaterial = "Swan V23 Rock ambient - Rock ambient - Original-Rocks";
        const string SandMaterial = "Swan V23 Continuous sand";
        const string Mark = " [sparse pebbles]";
        internal const string BakedPath = "Assets/SwanJourney/Generated/CanyonPebbleAlbedo.png";

        sealed class Island
        {
            public Bounds bounds;
            public float exposure;
            public readonly List<int> faces = new List<int>();
            public float Span => Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y,bounds.size.z));
        }

        static int Find(int[] parents, int i)
        {
            while (parents[i] != i) { parents[i] = parents[parents[i]]; i = parents[i]; }
            return i;
        }

        static List<Island> Islands(Vector3[] vertices, int[] indices)
        {
            var canonical = new Dictionary<Vector3Int,int>();
            var parents = new int[vertices.Length];
            for (int i=0; i<vertices.Length; ++i)
            {
                var point = Vector3Int.RoundToInt(vertices[i]*1000000f);
                if (!canonical.TryGetValue(point,out int id)) canonical[point]=id=i;
                parents[i]=id;
            }
            for (int i=0; i<indices.Length; i+=3)
            {
                int a=Find(parents,indices[i]);
                parents[Find(parents,indices[i+1])]=a;
                parents[Find(parents,indices[i+2])]=a;
            }
            var groups=new Dictionary<int,Island>();
            for (int i=0; i<indices.Length; i+=3)
            {
                int id=Find(parents,indices[i]);
                if (!groups.TryGetValue(id,out var island))
                    groups[id]=island=new Island{bounds=new Bounds(vertices[indices[i]],Vector3.zero)};
                island.faces.Add(i);
                for (int k=0;k<3;++k) island.bounds.Encapsulate(vertices[indices[i+k]]);
            }
            return groups.Values.ToList();
        }

        static int Cell(float value,float min,float max) =>
            Mathf.Min(11,Mathf.FloorToInt(12*Mathf.InverseLerp(min,max,value)));

        static Mesh Compact(Mesh source,int[] retained)
        {
            // Remap only the vertex indices. Copy each entire vertex stream so seams,
            // tangents, UVs, colors and packed attribute formats survive unchanged.
            var used=retained.Distinct().OrderBy(i=>i).ToArray();
            var map=new int[source.vertexCount];
            for (int i=0;i<used.Length;++i) map[used[i]]=i;
            var result=new Mesh{name=source.name+Mark, indexFormat=used.Length>65535 ?
                UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16};
            result.SetVertexBufferParams(used.Length,source.GetVertexAttributes());
            using (var data=Mesh.AcquireReadOnlyMeshData(source))
            {
                for (int s=0;s<source.vertexBufferCount;++s)
                {
                    int stride=source.GetVertexBufferStride(s);
                    var bytes=data[0].GetVertexData<byte>(s).ToArray();
                    var packed=new byte[used.Length*stride];
                    for (int i=0;i<used.Length;++i) Buffer.BlockCopy(bytes,used[i]*stride,packed,i*stride,stride);
                    result.SetVertexBufferData(packed,0,0,packed.Length,s);
                }
            }
            result.SetTriangles(retained.Select(i=>map[i]).ToArray(),0,false);
            var positions=source.vertices;
            var extent=new Bounds(positions[used[0]],Vector3.zero);
            foreach(int i in used)extent.Encapsulate(positions[i]);
            // Raw vertex-stream uploads do not reliably populate either AABB in
            // the serialized mesh on 2022.3. Both are used for native culling.
            var submesh=result.GetSubMesh(0);
            submesh.firstVertex=0;submesh.vertexCount=used.Length;submesh.bounds=extent;
            result.SetSubMesh(0,submesh,UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);
            result.bounds=extent;
            return result;
        }

        public static void Apply(SwanJourneyEnvironment environment)
        {
            if (Application.isPlaying || !environment) throw new InvalidOperationException("Canyon pebble budget requires the derived environment in Edit Mode");
            var root=environment.transform;
            var rockGroup=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name.StartsWith("Fixed original rock meshes V14",StringComparison.Ordinal));
            var pebbles=rockGroup.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r=>r.enabled && r.sharedMaterial && r.sharedMaterial.name==PebbleMaterial).ToArray();
            var sand=root.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r=>r.enabled && r.sharedMaterial && r.sharedMaterial.name==SandMaterial).ToArray();
            if (pebbles.Length==0 || sand.Length==0) throw new InvalidOperationException("Missing pebble or sand inputs");
            var sources=pebbles.Select(r=>r.GetComponent<MeshFilter>().sharedMesh).ToArray();
            var ground=sand.Select(r=>r.sharedMaterial).Distinct().Single();
            if (sources.All(m=>m.name.EndsWith(Mark)) && AssetDatabase.GetAssetPath(ground.GetTexture("_BaseMap"))==BakedPath) return;
            if (sources.Any(m=>m.name.EndsWith(Mark)) || sources.Distinct().Count()!=sources.Length ||
                root.GetComponentsInChildren<MeshCollider>(true).Any(c=>sources.Contains(c.sharedMesh)) ||
                sources.Any(m=>!AssetDatabase.GetAssetPath(m).StartsWith(SwanJourneyBuild.Generated+"/CanyonMetres/",StringComparison.Ordinal)))
                throw new InvalidOperationException("Expected pristine, independent render meshes in the derived canyon");
            var changes=new Dictionary<Mesh,Mesh>();
            int keptFaces=0,originalFaces=0,islands=0,keptIslands=0;
            using (var bake=new SwanJourneyPebbleGround(root,sand,ground,pebbles[0].sharedMaterial))
            {
                try
                {
                    foreach (var renderer in pebbles)
                    {
                        var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
                        if (mesh.subMeshCount!=1 || mesh.GetTopology(0)!=MeshTopology.Triangles)
                            throw new InvalidOperationException("Unexpected pebble topology on "+renderer.name);
                        var vertices=mesh.vertices;var indices=mesh.triangles;var uv=mesh.uv;
                        var pieces=Islands(vertices,indices);var b=mesh.bounds;
                        var localToRoot=root.worldToLocalMatrix*renderer.localToWorldMatrix;
                        var points=vertices.Select(localToRoot.MultiplyPoint3x4).ToArray();
                        var relief=points.Select(bake.Relief).ToArray();
                        foreach(var island in pieces)
                            foreach(var face in island.faces)
                                for(int k=0;k<3;k++)island.exposure=Mathf.Max(island.exposure,relief[indices[face+k]]);
                        // Keep the largest silhouettes separately in each patch of the
                        // ground, so small clusters survive as well as dense ones.
                        var kept=pieces.GroupBy(i=>new Vector2Int(Cell(i.bounds.center.x,b.min.x,b.max.x),Cell(i.bounds.center.z,b.min.z,b.max.z)))
                            .SelectMany(cell=>cell.OrderByDescending(i=>i.Span+2*i.exposure).ThenBy(i=>i.faces[0])
                                .Take(Mathf.Max(1,Mathf.RoundToInt(cell.Count()*.15f)))).ToHashSet();
                        var keep=new bool[indices.Length/3];
                        foreach(var island in kept) foreach(var face in island.faces) keep[face/3]=true;
                        var retained=new List<int>();
                        for(int i=0;i<indices.Length;i+=3)
                        {
                            int a=indices[i],v=indices[i+1],c=indices[i+2];
                            if(keep[i/3]) {retained.Add(a);retained.Add(v);retained.Add(c);}
                            else bake.Imprint(points[a],points[v],points[c],uv[a],uv[v],uv[c]);
                        }
                        changes.Add(mesh,Compact(mesh,retained.ToArray()));
                        originalFaces+=indices.Length/3;keptFaces+=retained.Count/3;islands+=pieces.Count;keptIslands+=kept.Count;
                    }
                    var baked=bake.Save(BakedPath);
                    foreach(var change in changes) {EditorUtility.CopySerialized(change.Value,change.Key);EditorUtility.SetDirty(change.Key);}
                    ground.SetTexture("_BaseMap",baked);ground.SetTexture("_MainTex",baked);EditorUtility.SetDirty(ground);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"SWAN_PEBBLE_BUDGET batches={pebbles.Length} triangles={originalFaces}->{keptFaces} islands={islands}->{keptIslands}");
                }
                finally {foreach(var copy in changes.Values)Object.DestroyImmediate(copy);}
            }
        }
    }
}
