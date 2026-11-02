using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ByteDance.PICO.SpatialAdapter.Component;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using View = SwanJourney.SwanJourneyValveViews.View;

namespace SwanJourney.Editor
{
    internal static class SwanJourneyValveGroupBuild
    {
        const string Folder = SwanJourneyBuild.Generated + "/ValveGroup";
        sealed class Piece
        {
            public MeshRenderer renderer;
            public MeshFilter filter;
            public BoxCollider box;
            public JourneyDetail detail;
            public View view;
            public bool sleepInSolid, sleepInXray, sleepInSection;
        }
        sealed class Cluster
        {
            public Piece owner;
            public readonly List<Piece> pieces = new List<Piece>();
            public readonly List<BoxCollider> boxes = new List<BoxCollider>();
            public MeshRenderer output;
        }

        internal static void Apply(SteamJourney j)
        {
            if (Application.isPlaying || !j.SwanInputActive || j.valveGroupViews ||
                j.modules.Length != 7 || !j.modules[4].title.Contains("配汽"))
                throw new InvalidOperationException("Expected the editable Swan valve assembly.");
            var roots = j.modules[4].objects;
            var toRemove = new List<Transform>();
            var shells = new HashSet<Renderer>(j.shellRenderers);
            var ghosts = new HashSet<Renderer>(j.inspectionGhosts.SelectMany(p=>new[]{p.hull,p.edges}).Where(r=>r));
            var detail = j.detailMeshes.ToDictionary(d=>d.filter);
            var refs = SwanJourneyBaselineAudit.References(j.gameObject.scene.GetRootGameObjects());
            var removable = new HashSet<MeshRenderer>();
            var bindings = new List<SwanJourneyValveViews.Surface>();
            var details = new List<JourneyDetail>();
            if (!AssetDatabase.IsValidFolder(Folder)) { Directory.CreateDirectory(Folder); AssetDatabase.Refresh(); }
            int batches=0, removed=0, boxes=0;
            foreach (var go in roots)
            {
                var part = go.transform;
                // Each immediate branch is accepted as a whole. This keeps fine native
                // hit owners, independently activated children and referenced pivots intact.
                var branches = part.Cast<Transform>().Where(branch =>
                    branch.GetComponentsInChildren<Transform>(true).All(t=>Eligible(t,part))).ToArray();
                var sources = new HashSet<MeshRenderer>(branches.SelectMany(t=>t.GetComponentsInChildren<MeshRenderer>(true)));
                foreach (var pair in j.inspectionGhosts)
                {
                    var members=new[]{pair.source,pair.hull,pair.edges}.Where(r=>r).ToArray();
                    if(members.Any(r=>sources.Contains(r as MeshRenderer)) && members.Any(r=>!sources.Contains(r as MeshRenderer)))
                        throw new InvalidOperationException("Inspection pair crosses a valve batch boundary.");
                }
                if (sources.Count==0) continue;
                var pieces = branches.SelectMany(t=>t.GetComponentsInChildren<MeshRenderer>(true)).Select(r=>{
                    var f=r.GetComponent<MeshFilter>();
                    detail.TryGetValue(f,out var d);
                    return new Piece{renderer=r,filter=f,box=r.GetComponent<BoxCollider>(),detail=d,
                        view=ghosts.Contains(r)?View.Inspection:shells.Contains(r)?View.Shell:View.Fixed};
                }).ToList();
                foreach(var p in pieces)
                {
                    bool Covered(View by)=>pieces.Any(other=>other!=p && other.view==by && Contains(other.box,p.box));
                    bool permanent=Covered(View.Fixed);
                    p.sleepInSolid=p.view==View.Inspection && (permanent||Covered(View.Shell));
                    p.sleepInXray=p.view==View.Shell && (permanent||Covered(View.Inspection));
                    p.sleepInSection=permanent || (p.view==View.Inspection && Covered(View.Shell));
                }
                var groups = new List<Cluster>();
                foreach(var signature in pieces.GroupBy(p=>Layout(p)))
                {
                    // Keep the oriented box of a real source. A box is redundant only
                    // when every corner lies inside that retained box, in its local frame.
                    var retained = new List<Cluster>();
                    foreach(var p in signature.OrderByDescending(x=>Volume(x.box)))
                    {
                        var group=retained.FirstOrDefault(g=>g.boxes.Any(b=>Contains(b,p.box)))
                            ?? retained.FirstOrDefault(g=>AxisAligned(g.owner.box.transform,p.box.transform));
                        if(group==null){group=new Cluster{owner=p};retained.Add(group);}
                        group.pieces.Add(p);
                        if(!group.boxes.Any(b=>Contains(b,p.box)))group.boxes.Add(p.box);
                    }
                    groups.AddRange(retained);
                }
                foreach (var group in groups)
                {
                    var p=group.owner;
                    var host=new GameObject(p.renderer.name);
                    host.transform.SetParent(part,false);
                    SetRelativeTransform(host.transform,part.worldToLocalMatrix*p.renderer.localToWorldMatrix);
                    host.layer=p.renderer.gameObject.layer;
                    var f=host.AddComponent<MeshFilter>();
                    var r=host.AddComponent<MeshRenderer>();EditorUtility.CopySerialized(p.renderer,r);
                    foreach(var old in group.boxes)
                    {
                        var hit=host.AddComponent<BoxCollider>();EditorUtility.CopySerialized(old,hit);
                        if(old==p.box)continue;
                        var m=host.transform.worldToLocalMatrix*old.transform.localToWorldMatrix;
                        hit.center=m.MultiplyPoint3x4(old.center);
                        var x=m.MultiplyVector(Vector3.right*old.size.x);
                        var y=m.MultiplyVector(Vector3.up*old.size.y);
                        var z=m.MultiplyVector(Vector3.forward*old.size.z);
                        hit.size=new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),
                            Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z));
                    }
                    var hover=host.AddComponent<SpatialAdapterHoverEffect>();EditorUtility.CopySerialized(p.renderer.GetComponent<SpatialAdapterHoverEffect>(),hover);
                    group.output=r;
                    if(group.pieces.Count==1)
                    {
                        f.sharedMesh=p.filter.sharedMesh;
                        if(p.detail!=null)details.Add(new JourneyDetail{filter=f,inspection=p.detail.inspection,miniature=p.detail.miniature});
                    }
                    else
                    {
                        string name="Valve_"+batches++.ToString("D3");
                        var materials=group.pieces.SelectMany(x=>x.renderer.sharedMaterials).Distinct().ToArray();
                        f.sharedMesh=Combine(group,host.transform,materials,false,name);
                        r.sharedMaterials=materials;
                        if(group.pieces.Any(x=>x.detail!=null))
                        {
                            var mini=group.pieces.All(x=>x.detail==null || x.detail.miniature==x.filter.sharedMesh)
                                ? f.sharedMesh : Combine(group,host.transform,materials,true,name+"_Miniature");
                            details.Add(new JourneyDetail{filter=f,inspection=f.sharedMesh,miniature=mini});
                        }
                        boxes+=group.pieces.Count-group.boxes.Count;
                    }
                }
                foreach (var g in groups)
                {
                    var p=g.owner;
                    bindings.Add(new SwanJourneyValveViews.Surface{renderer=g.output,view=p.view,
                        sleepInSolid=p.sleepInSolid,sleepInXray=p.sleepInXray,sleepInSection=p.sleepInSection});
                }
                foreach(var r in sources)removable.Add(r);
                removed+=branches.Sum(t=>t.GetComponentsInChildren<Transform>(true).Length)-groups.Count;
                // Defer destruction until serialized arrays have been remapped.
                toRemove.AddRange(branches);
            }
            if(bindings.Count==0)throw new InvalidOperationException("No valve geometry selected.");
            j.detailMeshes=j.detailMeshes.Where(d=>!removable.Contains(d.filter.GetComponent<MeshRenderer>())).Concat(details).ToArray();
            j.shellRenderers=j.shellRenderers.Where(r=>!removable.Contains(r as MeshRenderer)).ToArray();
            j.inspectionGhosts=j.inspectionGhosts.Where(p=>!removable.Contains(p.source as MeshRenderer)).ToArray();
            var views=j.gameObject.AddComponent<SwanJourneyValveViews>();views.surfaces=bindings.ToArray();j.valveGroupViews=views;
            foreach(var t in toRemove)Object.DestroyImmediate(t.gameObject);
            views.Apply(!j.inSandbox&&j.cutaway,!j.inSandbox&&j.cutaway&&j.inspectionMode==1);
            EditorUtility.SetDirty(j);EditorUtility.SetDirty(views);AssetDatabase.SaveAssets();
            Debug.Log($"VALVE_GROUP_BUILD partRoots={roots.Length} combined={batches} fewerNodes={removed} fewerBoxes={boxes}");

            bool Eligible(Transform t,Transform part)
            {
                if(!t.gameObject.activeSelf || t is RectTransform || t.gameObject.tag!="Untagged" ||
                    t.GetComponents<Component>().Any(c=>!(c is Transform || c is MeshRenderer || c is MeshFilter || c is BoxCollider || c is SpatialAdapterHoverEffect)))return false;
                foreach(var o in t.GetComponents<Component>().Cast<Object>().Append(t.gameObject))
                    if(refs.TryGetValue(o,out var users) && users.Any(s=>!s.StartsWith("SteamJourney.inspectionGhosts.Array.",StringComparison.Ordinal)
                        && !s.StartsWith("SteamJourney.shellRenderers.Array.",StringComparison.Ordinal)
                        && !s.StartsWith("SteamJourney.detailMeshes.Array.",StringComparison.Ordinal)))return false;
                var r=t.GetComponent<MeshRenderer>();if(!r)return t.GetComponents<Component>().Length==1;
                var f=t.GetComponent<MeshFilter>();var colliders=t.GetComponents<Collider>();
                if(!f || !f.sharedMesh || !f.sharedMesh.isReadable || colliders.Length!=1 || !(colliders[0] is BoxCollider cbox)
                    || !cbox.enabled || cbox.isTrigger || cbox.sharedMaterial || !t.GetComponent<SpatialAdapterHoverEffect>()
                    || r.HasPropertyBlock() || r.additionalVertexStreams || r.probeAnchor || r.forceRenderingOff
                    || (!shells.Contains(r)&&!ghosts.Contains(r)&&!r.enabled))return false;
                if(r.sharedMaterials.Length!=f.sharedMesh.subMeshCount || r.sharedMaterials.Any(m=>!m) ||
                    Enumerable.Range(0,f.sharedMesh.subMeshCount).Any(s=>f.sharedMesh.GetTopology(s)!=MeshTopology.Triangles))return false;
                if(detail.TryGetValue(f,out var d) && (!d.inspection||d.inspection!=f.sharedMesh ||!d.miniature||!d.miniature.isReadable||d.miniature.subMeshCount!=f.sharedMesh.subMeshCount))return false;
                var mtx=part.worldToLocalMatrix*t.localToWorldMatrix;
                return Decompose(mtx,out _,out _,out _);
            }
        }
        static string Layout(Piece p)
        {
            var r=p.renderer;
            return string.Join("/",p.view,p.sleepInSolid,p.sleepInXray,p.sleepInSection,r.gameObject.layer,r.renderingLayerMask,r.shadowCastingMode,r.receiveShadows,r.lightProbeUsage,
                r.reflectionProbeUsage,r.sortingLayerID,r.sortingOrder,r.motionVectorGenerationMode,
                string.Join(",",p.filter.sharedMesh.GetVertexAttributes().Select(a=>a.attribute+"-"+a.format+"-"+a.dimension)),
                string.Join(",",r.sharedMaterials.Select(m=>p.view==View.Inspection ? m.GetInstanceID() : m.renderQueue).Distinct()));
        }
        static bool Decompose(Matrix4x4 matrix,out Vector3 p,out Quaternion q,out Vector3 scale)
        {
            p=matrix.GetColumn(3);Vector3 x=matrix.GetColumn(0),y=matrix.GetColumn(1),z=matrix.GetColumn(2);
            scale=new Vector3(x.magnitude,y.magnitude,z.magnitude);q=Quaternion.identity;
            if(matrix.determinant<=0 || Mathf.Min(scale.x,Mathf.Min(scale.y,scale.z))<1e-7f)return false;
            q=Quaternion.LookRotation(z,y);var check=Matrix4x4.TRS(p,q,scale);
            for(int i=0;i<3;i++)for(int k=0;k<3;k++)if(Mathf.Abs(matrix[i,k]-check[i,k])>1e-5f*scale[k])return false;
            return true;
        }
        static void SetRelativeTransform(Transform t,Matrix4x4 m)
        {
            if(!Decompose(m,out var p,out var q,out var s))throw new InvalidOperationException("Non-rigid valve batch.");
            t.SetLocalPositionAndRotation(p,q);t.localScale=s;
        }
        static double Volume(BoxCollider c)=>Math.Abs(c.transform.localToWorldMatrix.determinant)*c.size.x*c.size.y*c.size.z;
        // One native entity can carry multiple box shapes. Only axis permutations
        // preserve an oriented Unity box exactly when transferring it to this host.
        static bool AxisAligned(Transform host,Transform t)
        {
            var m=host.worldToLocalMatrix*t.localToWorldMatrix;
            int mask=0;
            for(int c=0;c<3;c++){
                Vector3 a=m.GetColumn(c);if(a.sqrMagnitude<=1e-16f)return false;a.Normalize();
                int i=Mathf.Abs(a.x)>Mathf.Abs(a.y)?0:1;if(Mathf.Abs(a.z)>Mathf.Abs(a[i]))i=2;
                if(Mathf.Abs(a[(i+1)%3])>1e-5f||Mathf.Abs(a[(i+2)%3])>1e-5f)return false;
                mask|=1<<i;
            }
            return mask==7;
        }
        static bool Contains(BoxCollider outer,BoxCollider inner)
        {
            if(outer.gameObject.layer!=inner.gameObject.layer)return false;
            var m=outer.transform.worldToLocalMatrix*inner.transform.localToWorldMatrix;
            var half=outer.size*.5f;
            for(int i=0;i<8;i++){
                var signs=new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1);
                var p=m.MultiplyPoint3x4(inner.center+Vector3.Scale(inner.size*.5f,signs))-outer.center;
                for(int k=0;k<3;k++)if(Mathf.Abs(p[k])>half[k]+1e-5f*Mathf.Max(1,half[k]))return false;
            }
            return true;
        }
        static Mesh Combine(Cluster group,Transform dest,Material[] materials,bool miniature,string name)
        {
            var inputs=new List<CombineInstance>();var slots=new List<int>();long expected=0;
            foreach(var p in group.pieces){
                var m=miniature && p.detail!=null?p.detail.miniature:p.filter.sharedMesh;
                var mats=p.renderer.sharedMaterials;
                for(int i=0;i<m.subMeshCount;i++){
                    if(m.GetIndexCount(i)==0)continue;
                    inputs.Add(new CombineInstance{mesh=m,subMeshIndex=i,transform=dest.worldToLocalMatrix*p.renderer.localToWorldMatrix});
                    slots.Add(Array.IndexOf(materials,mats[i]));expected+=m.GetIndexCount(i);
                }
            }
            var tmp=new Mesh{indexFormat=IndexFormat.UInt32};
            try{
                tmp.CombineMeshes(inputs.ToArray(),false,true);
                if(tmp.subMeshCount!=slots.Count)throw new InvalidOperationException("Valve material slots changed.");
                var byMat=materials.Select(_=>new List<int>()).ToArray();
                for(int i=0;i<slots.Count;i++)byMat[slots[i]].AddRange(tmp.GetTriangles(i));
                if(byMat.Any(x=>x.Count==0)||byMat.Sum(x=>(long)x.Count)!=expected)throw new InvalidOperationException("Valve triangle or material loss.");
                var mesh=SwanJourneyValleyShellBuild.Compact(tmp,byMat.Select(x=>x.ToArray()).ToArray());mesh.name=name;
                var path=Folder+"/"+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(saved){EditorUtility.CopySerialized(mesh,saved);EditorUtility.SetDirty(saved);Object.DestroyImmediate(mesh);return saved;}
                AssetDatabase.CreateAsset(mesh,path);return mesh;
            }finally{Object.DestroyImmediate(tmp);}
        }
    }
}
