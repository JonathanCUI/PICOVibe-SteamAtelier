using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SwanJourney.Editor
{
    internal static class SwanXrConsoleFinish
    {
        const string Native="Assets/SwanJourney/Generated/NativeVisuals/";
        static Material Source(int id)=>AssetDatabase.LoadAssetAtPath<Material>(Native+$"Material_{id:D4}.mat");

        static Material Finish(int id,string name,Color color,float metal,float smooth)
        {
            var source=Source(id);
            var result=SwanXrWorkshopSurfaces.MaterialAsset("Desk "+name,source.shader);
            result.CopyPropertiesFromMaterial(source);
            result.SetColor("_BaseColor",color);
            result.SetColor("_Color",color);
            result.SetFloat("_Metallic",metal);
            result.SetFloat("_Smoothness",smooth);
            result.SetTexture("_MetallicGlossMap",null);
            result.DisableKeyword("_METALLICSPECGLOSSMAP");
            result.DisableKeyword("_EMISSION");
            return result;
        }

        static void Replace(Transform root,Dictionary<Material,Material> finishes)
        {
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var slots=renderer.sharedMaterials;
                for(int i=0;i<slots.Length;i++)
                    if(slots[i] && finishes.TryGetValue(slots[i],out var finish))slots[i]=finish;
                renderer.sharedMaterials=slots;
            }
        }

        internal static void Apply(SteamJourney j)
        {
            var brass=Finish(218,"brushed brass",new Color(.62f,.48f,.29f),.65f,.40f);
            var steel=Finish(13,"satin nickel",new Color(.56f,.60f,.59f),.72f,.37f);
            var black=Finish(217,"graphite enamel",new Color(.17f,.19f,.20f),.18f,.34f);
            var ivory=Finish(223,"ivory",new Color(.85f,.78f,.61f),.05f,.38f);
            var guard=Finish(222,"smoked safety glass",new Color(.55f,.63f,.59f,.16f),.03f,.55f);
            Replace(j.consoleRoot.transform,new Dictionary<Material,Material>{
                {Source(218),brass},{Source(13),steel},{Source(217),black},{Source(222),guard},{Source(223),ivory},
                {Source(219),Finish(219,"oiled walnut",new Color(.78f,.70f,.59f),.01f,.32f)},
                {Source(220),Finish(220,"bone handle",new Color(.78f,.73f,.58f),.03f,.41f)},
                {Source(221),Finish(221,"oxblood handle",new Color(.50f,.18f,.12f),.04f,.43f)}});
            var controls=j.GetComponent<StudioSpatialConsoleV18>();
            Replace(controls.introLever.parent,new Dictionary<Material,Material>{{Source(142),brass}});
            Replace(controls.introLever,new Dictionary<Material,Material>{{brass,steel}});
            Replace(controls.rotary.parent,new Dictionary<Material,Material>{{Source(142),brass},{Source(143),black}});
            Replace(controls.rotary,new Dictionary<Material,Material>{{brass,black},{black,ivory}});
            BevelControl(controls.introLever.parent.Find("Brass foot").GetComponent<MeshFilter>(),"toggle foot",false);
            BevelControl(controls.rotary.GetComponentInChildren<MeshFilter>(true),"selector crown",true);
            var panel=j.GetComponent<StudioSpatialRefineV19>();
            RoundGrip(panel.directionPivot,"direction");
            RoundGrip(panel.brakePivot,"brake");
            SideHinge(j.consoleRoot.transform,panel.returnCover);
        }

        static Mesh SaveMesh(string name,Mesh mesh)
        {
            string path=SwanXrWorkshopSurfaces.Folder+name+".asset";
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            mesh.name=name;
            if(!saved){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,saved);EditorUtility.SetDirty(saved);
            UnityEngine.Object.DestroyImmediate(mesh);
            return saved;
        }
        static void BevelControl(MeshFilter filter,string name,bool fluted)
        {
            var old=filter.sharedMesh;var verts=old.vertices;var tris=old.GetTriangles(0);
            var b=new Bounds(verts[tris[0]],Vector3.zero);
            foreach(int i in tris)b.Encapsulate(verts[i]);
            const int sides=40;
            float[] height={-1,-.88f,-.68f,.52f,.78f,.95f,1};
            float[] radius={.88f,.96f,1,1,.97f,.91f,.85f};
            float[] bevel={-1,-.65f,-.14f,.08f,.55f,.92f,1};
            var v=new List<Vector3>();var n=new List<Vector3>();var idx=new List<int>();
            for(int row=0;row<height.Length;row++)
                for(int i=0;i<sides;i++){
                    float a=i*2*Mathf.PI/sides;var axis=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                    bool notch=fluted && (i&1)!=0 && (row==2 || row==3);
                    float r=radius[row]*(notch?.977f:1f);
                    v.Add(b.center+Vector3.Scale(b.extents,axis*r+Vector3.up*height[row]));
                    n.Add(axis*Mathf.Sqrt(1-bevel[row]*bevel[row])+Vector3.up*bevel[row]);
                    if(row==0)continue;
                    int p=(row-1)*sides+i,q=(row-1)*sides+(i+1)%sides;
                    idx.Add(p);idx.Add(p+sides);idx.Add(q);
                    idx.Add(q);idx.Add(p+sides);idx.Add(q+sides);
                }
            for(int side=0;side<2;side++){
                int centre=v.Count,row=side*(height.Length-1)*sides;
                v.Add(b.center+Vector3.up*b.extents.y*(2*side-1));n.Add(side==1?Vector3.up:Vector3.down);
                for(int i=0;i<sides;i++){idx.Add(centre);idx.Add(row+(side==1?(i+1)%sides:i));idx.Add(row+(side==1?i:(i+1)%sides));}
            }
            var shape=new Mesh();shape.SetVertices(v);shape.SetNormals(n);shape.SetTriangles(idx,0);
            var slots=new CombineInstance[old.subMeshCount];slots[0]=new CombineInstance{mesh=shape};
            for(int i=1;i<slots.Length;i++)slots[i]=new CombineInstance{mesh=old,subMeshIndex=i};
            var mesh=new Mesh();mesh.CombineMeshes(slots,false,false);
            filter.sharedMesh=SaveMesh("Desk "+name,mesh);UnityEngine.Object.DestroyImmediate(shape);
        }

        static void RoundGrip(Transform pivot,string name)
        {
            var filter=pivot.GetComponentInChildren<MeshFilter>(true);
            var source=filter.sharedMesh;
            if(source.subMeshCount!=2)throw new InvalidOperationException("Unexpected "+name+" lever mesh");
            var vertices=source.vertices;var indices=source.GetTriangles(1);
            var bounds=new Bounds(vertices[indices[0]],Vector3.zero);
            foreach(int i in indices)bounds.Encapsulate(vertices[i]);
            // Build a softly rounded grip inside the authored collider, keeping the original stem submesh.
            const int sides=16,rings=7;
            var points=new List<Vector3>();var normals=new List<Vector3>();var triangles=new List<int>();
            for(int row=0;row<=rings;row++){
                float latitude=(float)row/rings*Mathf.PI;
                for(int col=0;col<sides;col++){
                    float a=2*Mathf.PI*col/sides;
                    var n=new Vector3(Mathf.Sin(latitude)*Mathf.Cos(a),Mathf.Cos(latitude),Mathf.Sin(latitude)*Mathf.Sin(a));
                    points.Add(bounds.center+Vector3.Scale(bounds.extents,n));
                    normals.Add(new Vector3(n.x/bounds.extents.x,n.y/bounds.extents.y,n.z/bounds.extents.z).normalized);
                    if(row==rings)continue;
                    int p=row*sides+col,q=row*sides+(col+1)%sides;
                    if(row>0){triangles.Add(p);triangles.Add(q);triangles.Add(p+sides);}
                    if(row<rings-1){triangles.Add(q);triangles.Add(q+sides);triangles.Add(p+sides);}
                }
            }
            var knob=new Mesh();knob.SetVertices(points);knob.SetNormals(normals);knob.SetTriangles(triangles,0);
            var mesh=new Mesh();
            mesh.CombineMeshes(new[]{new CombineInstance{mesh=source,subMeshIndex=0},new CombineInstance{mesh=knob}},false,false);
            filter.sharedMesh=SaveMesh("Desk "+name+" lever",mesh);UnityEngine.Object.DestroyImmediate(knob);
        }

        static void SideHinge(Transform console,Transform lid)
        {
            // The fixed pin was merged into the desk. Move that portion of the existing mesh along with the pivot.
            var bay=lid.parent;
            var oldPin=lid.localPosition;
            var sidePin=new Vector3(-.43f,oldPin.y,oldPin.z+.49f);
            var pivotRotation=Quaternion.Euler(0,90,0);
            int moved=0;
            foreach(var filter in console.GetComponentsInChildren<MeshFilter>(true))
            {
                if(filter.transform.parent!=console || !filter.sharedMesh)continue;
                var src=filter.sharedMesh;
                var v=src.vertices;var normals=src.normals;var tangents=src.tangents;
                var toBay=bay.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                bool changed=false;
                for(int i=0;i<v.Length;i++){
                    var offset=toBay.MultiplyPoint3x4(v[i])-oldPin;
                    if(Mathf.Abs(offset.x)>.47f || Mathf.Abs(offset.y)>.065f || Mathf.Abs(offset.z)>.065f)continue;
                    var rotation=Quaternion.Inverse(filter.transform.rotation)*bay.rotation*pivotRotation*Quaternion.Inverse(bay.rotation)*filter.transform.rotation;
                    v[i]=toBay.inverse.MultiplyPoint3x4(sidePin+pivotRotation*offset);
                    if(normals.Length==v.Length)normals[i]=rotation*normals[i];
                    if(tangents.Length==v.Length){var t=tangents[i];Vector3 xyz=rotation*(Vector3)t;tangents[i]=new Vector4(xyz.x,xyz.y,xyz.z,t.w);}
                    changed=true;moved++;
                }
                if(!changed)continue;
                var mesh=UnityEngine.Object.Instantiate(src);mesh.vertices=v;mesh.normals=normals;mesh.tangents=tangents;
                var materials=filter.GetComponent<MeshRenderer>().sharedMaterials;
                int brass=Array.FindIndex(materials,m=>m.name=="Desk brushed brass");
                int enamel=Array.FindIndex(materials,m=>m.name=="Desk graphite enamel");
                if(brass<0 || enamel<0)throw new InvalidOperationException("Missing fixed console finishes");
                foreach(float along in new[]{-.34f,.34f})
                    AddBlock(mesh,brass,toBay.inverse,sidePin+pivotRotation*new Vector3(along,-.13f,0),new Vector3(.115f,.26f,.078f));
                AddBlock(mesh,enamel,toBay.inverse,new Vector3(0,-.035f,0),new Vector3(.78f,.115f,.88f));
                mesh.RecalculateBounds();
                filter.sharedMesh=SaveMesh("Desk side pin "+src.name,mesh);
            }
            if(moved==0)throw new InvalidOperationException("Missing fixed return lid pin");
            var mount=new GameObject("Outboard safety hinge").transform;
            mount.SetParent(bay,false);mount.localPosition=sidePin;mount.localRotation=pivotRotation;
            lid.SetParent(mount,false);lid.localPosition=Vector3.zero;lid.localRotation=Quaternion.identity;
            lid.localScale=new Vector3(1/.86f,1,.86f);
        }
        static void AddBlock(Mesh mesh,int slot,Matrix4x4 fromBay,Vector3 centre,Vector3 size)
        {
            var cube=Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            int first=mesh.vertexCount;
            var v=new List<Vector3>(mesh.vertices);var n=new List<Vector3>(mesh.normals);
            var t=new List<Vector4>(mesh.tangents);var uv=new List<Vector2>(mesh.uv);
            foreach(var p in cube.vertices)v.Add(fromBay.MultiplyPoint3x4(centre+Vector3.Scale(p,size)));
            foreach(var normal in cube.normals)n.Add(fromBay.inverse.transpose.MultiplyVector(normal).normalized);
            foreach(var tangent in cube.tangents){
                Vector3 dir=fromBay.MultiplyVector((Vector3)tangent).normalized;
                t.Add(new Vector4(dir.x,dir.y,dir.z,tangent.w));
            }
            uv.AddRange(cube.uv);var indices=new List<int>(mesh.GetTriangles(slot));
            foreach(int index in cube.triangles)indices.Add(first+index);
            mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetTangents(t);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,slot);
        }
    }
}
