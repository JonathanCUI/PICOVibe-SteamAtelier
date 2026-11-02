using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityMeshSimplifier;
using Object=UnityEngine.Object;

namespace SwanJourney.Editor
{
    // Limit LOD error in both directions, for each material separately: a missing
    // feature and an edge bridging a hole are different errors. Sampling vertices
    // and face interiors is supplemented by rendered multi-angle checks.
    internal static class SwanJourneyShellLod
    {
        struct Tri { public Vector3 a,b,c,center; public Bounds bounds; }
        sealed class Node { public Bounds box;public int first,count;public Node left,right; }
        sealed class Surface
        {
            readonly Tri[] tris;readonly Node root;
            public Surface(Vector3[] v,int[] index)
            {
                tris=new Tri[index.Length/3];
                for(int i=0;i<tris.Length;i++)
                {
                    var a=v[index[3*i]];var b=v[index[3*i+1]];var c=v[index[3*i+2]];
                    var box=new Bounds(a,Vector3.zero);box.Encapsulate(b);box.Encapsulate(c);
                    tris[i]=new Tri{a=a,b=b,c=c,center=(a+b+c)/3, bounds=box};
                }
                root=Build(0,tris.Length);
            }
            Node Build(int first,int count)
            {
                var n=new Node{first=first,count=count,box=tris[first].bounds};
                for(int i=first+1;i<first+count;i++)n.box.Encapsulate(tris[i].bounds);
                if(count<=12)return n;
                var size=n.box.size;int axis=size.x>size.y?(size.x>size.z?0:2):(size.y>size.z?1:2);
                Array.Sort(tris,first,count,Comparer<Tri>.Create((a,b)=>a.center[axis].CompareTo(b.center[axis])));
                int left=count/2;n.left=Build(first,left);n.right=Build(first+left,count-left);return n;
            }
            static float Edge(Vector3 p,Vector3 a,Vector3 edge)
            {
                float t=Mathf.Clamp01(Vector3.Dot(p-a,edge)/Math.Max(edge.sqrMagnitude,1e-24f));
                return (p-a-t*edge).sqrMagnitude;
            }
            static bool Close(Vector3 p,Tri t,float squared)
            {
                var ab=t.b-t.a;var bc=t.c-t.b;var ca=t.a-t.c;var normal=Vector3.Cross(ab,-ca);var d=p-t.a;
                float dot=Vector3.Dot(d,normal);
                if(dot*dot>squared*normal.sqrMagnitude)return false;
                if(normal.sqrMagnitude>1e-24f && Vector3.Dot(Vector3.Cross(ab,d),normal)>=0
                    && Vector3.Dot(Vector3.Cross(bc,p-t.b),normal)>=0 && Vector3.Dot(Vector3.Cross(ca,p-t.c),normal)>=0)return true;
                return Math.Min(Edge(p,t.a,ab),Math.Min(Edge(p,t.b,bc),Edge(p,t.c,ca)))<=squared;
            }
            bool Contains(Node node,Vector3 p,float squared)
            {
                if(node.box.SqrDistance(p)>squared)return false;
                if(node.left!=null)return Contains(node.left,p,squared)||Contains(node.right,p,squared);
                for(int i=node.first;i<node.first+node.count;i++)if(Close(p,tris[i],squared))return true;
                return false;
            }
            public bool CoveredBy(Surface other,float squared)
            {
                foreach(var t in tris)
                    if(!other.Contains(other.root,t.a,squared)||!other.Contains(other.root,t.b,squared)||
                       !other.Contains(other.root,t.c,squared)||!other.Contains(other.root,t.center,squared)||
                       !other.Contains(other.root,(t.a+t.b)*.5f,squared)||!other.Contains(other.root,(t.b+t.c)*.5f,squared)||
                       !other.Contains(other.root,(t.c+t.a)*.5f,squared))return false;
                return true;
            }
        }

        static int Root(int[] parent,int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
        public static Mesh ReduceParts(Mesh source,float tolerance)
        {
            if(SwanJourneyWorkshopMeshBudget.Triangles(source)<220)return null;
            var v=source.vertices;var parent=new int[v.Length];var ids=new Dictionary<Vector3Int,int>();
            for(int i=0;i<v.Length;i++){
                var key=Vector3Int.RoundToInt(v[i]*100000f);
                if(!ids.TryGetValue(key,out int id))ids[key]=id=i;parent[i]=id;
            }
            var subs=Enumerable.Range(0,source.subMeshCount).Select(i=>source.GetTriangles(i)).ToArray();
            foreach(var arr in subs)for(int k=0;k<arr.Length;k+=3){
                int id=Root(parent,arr[k]);parent[Root(parent,arr[k+1])]=id;parent[Root(parent,arr[k+2])]=id;
            }
            var groups=new Dictionary<int,List<KeyValuePair<int,int>>>();
            for(int s=0;s<subs.Length;s++)for(int k=0;k<subs[s].Length;k+=3){
                int id=Root(parent,subs[s][k]);
                if(!groups.TryGetValue(id,out var group))groups[id]=group=new List<KeyValuePair<int,int>>();
                group.Add(new KeyValuePair<int,int>(s,k));
            }
            if(groups.Count<=1)return Reduce(source,tolerance);
            var untouched=Enumerable.Range(0,subs.Length).Select(_=>new List<int>()).ToArray();
            var pieces=new List<Mesh>();var combine=new List<CombineInstance>();var slots=new List<int>();
            void Add(Mesh m){
                pieces.Add(m);
                for(int s=0;s<m.subMeshCount;s++)if(m.GetIndexCount(s)>0){
                    combine.Add(new CombineInstance{mesh=m,subMeshIndex=s});slots.Add(s);
                }
            }
            try
            {
                int saving=0;
                foreach(var group in groups.Values)
                {
                    Mesh lower=null;
                    if(group.Count>=48)
                    {
                        var chunks=Enumerable.Range(0,subs.Length).Select(_=>new List<int>()).ToArray();
                        foreach(var f in group)for(int k=0;k<3;k++)chunks[f.Key].Add(subs[f.Key][f.Value+k]);
                        var original=SwanJourneyValleyShellBuild.Compact(source,chunks.Select(a=>a.ToArray()).ToArray());
                        try{lower=Reduce(original,Math.Min(tolerance,original.bounds.size.magnitude*.06f));}
                        finally{Object.DestroyImmediate(original);}
                    }
                    if(lower){saving+=group.Count-(int)SwanJourneyWorkshopMeshBudget.Triangles(lower);Add(lower);}
                    else foreach(var f in group)for(int k=0;k<3;k++)untouched[f.Key].Add(subs[f.Key][f.Value+k]);
                }
                if(saving<100)return null;
                if(untouched.Any(a=>a.Count>0))Add(SwanJourneyValleyShellBuild.Compact(source,untouched.Select(a=>a.ToArray()).ToArray()));
                var result=new Mesh{name=source.name+" · distant exterior",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
                result.CombineMeshes(combine.ToArray(),false,false);
                var rebuilt=Enumerable.Range(0,subs.Length).Select(_=>new List<int>()).ToArray();
                if(result.subMeshCount!=slots.Count)throw new InvalidOperationException("Shell material slots changed");
                for(int i=0;i<slots.Count;i++)rebuilt[slots[i]].AddRange(result.GetTriangles(i));
                result.subMeshCount=subs.Length;
                for(int s=0;s<subs.Length;s++)result.SetTriangles(rebuilt[s],s,false);
                result.RecalculateBounds();
                var positions=result.vertices;
                for(int s=0;s<subs.Length;s++){var idx=rebuilt[s];if(idx.Count==0)continue;var b=new Bounds(positions[idx[0]],Vector3.zero);foreach(int j in idx)b.Encapsulate(positions[j]);var desc=result.GetSubMesh(s);desc.bounds=b;result.SetSubMesh(s,desc,UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);}
                return result;
            }
            finally {foreach(var piece in pieces)Object.DestroyImmediate(piece);}
        }
        static Mesh Reduce(Mesh source,float tolerance)
        {
            var oldPos=source.vertices;
            var old=Enumerable.Range(0,source.subMeshCount).Select(i=>source.GetTriangles(i)).ToArray();
            int before=old.Sum(i=>i.Length);
            if(before<48*3)return null;
            var originals=new Surface[old.Length];
            foreach(float ratio in new[]{.24f,.46f,.72f})
            {
                var solver=new MeshSimplifier();
                var opt=SimplificationOptions.Default;
                opt.PreserveBorderEdges=true;opt.PreserveUVSeamEdges=true;opt.PreserveUVFoldoverEdges=false;
                opt.VertexLinkDistance=Math.Max(1e-9,source.bounds.size.magnitude*1e-7);
                solver.SimplificationOptions=opt;solver.Initialize(source);solver.SimplifyMesh(ratio);
                var candidate=solver.ToMesh();candidate.name=source.name+" · distant exterior";
                bool valid=candidate.subMeshCount==old.Length && candidate.vertexCount>0;
                var v=candidate.vertices;
                int after=valid?Enumerable.Range(0,old.Length).Sum(i=>(int)candidate.GetIndexCount(i)):before;
                if(before-after<12*3 || after>before*.87f){Object.DestroyImmediate(candidate);break;}
                for(int i=0;valid && i<old.Length;i++)
                {
                    var idx=candidate.GetTriangles(i);
                    if(old[i].Length==0||idx.Length==0){valid=idx.Length==old[i].Length;continue;}
                    if(originals[i]==null)originals[i]=new Surface(oldPos,old[i]);
                    var surface=new Surface(v,idx);
                    valid=originals[i].CoveredBy(surface,tolerance*tolerance)&&surface.CoveredBy(originals[i],tolerance*tolerance);
                }
                if(valid)
                {
                    var normals=candidate.normals;
                    if(normals.Length>0){for(int i=0;i<normals.Length;i++)normals[i]=normals[i].normalized;candidate.normals=normals;}
                    if(source.tangents.Length>0 && candidate.uv.Length==v.Length)candidate.RecalculateTangents();
                    candidate.RecalculateBounds();
                    for(int i=0;i<candidate.subMeshCount;i++){
                        var idx=candidate.GetTriangles(i);if(idx.Length==0)continue;
                        var b=new Bounds(v[idx[0]],Vector3.zero);foreach(int ix in idx)b.Encapsulate(v[ix]);
                        var sub=candidate.GetSubMesh(i);sub.bounds=b;candidate.SetSubMesh(i,sub,UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);
                    }
                    return candidate;
                }
                Object.DestroyImmediate(candidate);
            }
            return null;
        }
    }
}
