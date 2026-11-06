using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SwanJourney.Editor
{
    public static class SwanCanyonPortalStudy
    {
        public static readonly string Output=Path.GetFullPath("Builds/Evidence/CanyonPortalStudy");
        const string Folder="Assets/SwanJourney/Branding/CanyonPortal";
        static void EnsureFolder()
        {
            Directory.CreateDirectory(Output);
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/SwanJourney/Branding","CanyonPortal");
        }
        static Mesh SaveMesh(Mesh mesh,string name)
        {
            mesh.name=name;var path=Folder+"/"+name+".asset";
            var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old){EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);return old;}
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static Material RockMaterial(string name,Material source,Color tint,float scale)
        {
            var path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(source);AssetDatabase.CreateAsset(m,path);}else m.CopyPropertiesFromMaterial(source);
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SwanJourney/Branding/XrSurfaceArt/Cliff recess 0169.png");
            m.SetTexture("_BaseMap",texture);m.SetTexture("_MainTex",texture);
            m.SetTextureScale("_BaseMap",Vector2.one*scale);m.SetTextureScale("_MainTex",Vector2.one*scale);
            m.SetColor("_BaseColor",tint);m.SetColor("_Color",tint);
            m.SetFloat("_Smoothness",.14f);m.SetFloat("_Metallic",0);
            m.SetTexture("_BumpMap",null);m.DisableKeyword("_NORMALMAP");
            m.SetColor("_EmissionColor",Color.black);m.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(m);return m;
        }
        public static void Prepare()
        {
            EnsureFolder();
            var root=PrefabUtility.LoadPrefabContents(SwanCanyonLightingStudy.PrefabPath);
            try
            {
                var all=root.GetComponentsInChildren<MeshRenderer>(true);
                // Static scenery uses its precomputed contact information, never the train's shadow atlas.
                foreach(var r in all)r.shadowCastingMode=ShadowCastingMode.Off;
                var approaches=all.Where(r=>r.name.StartsWith("Solid sandstone approach",StringComparison.Ordinal)).ToArray();
                if(approaches.Length!=4)throw new Exception("Expected four original rock approach faces");
                int corrected=0;
                foreach(var r in approaches)
                {
                    var f=r.GetComponent<MeshFilter>();var source=f.sharedMesh;
                    var vertices=source.vertices;var normals=source.normals;var tri=source.triangles;
                    var matrix=root.transform.worldToLocalMatrix*r.localToWorldMatrix;
                    var vv=new List<Vector3>();var nn=new List<Vector3>();var uv=new List<Vector2>();
                    for(int i=0;i<tri.Length;i+=3)
                    {
                        var p=matrix.MultiplyPoint3x4(vertices[tri[i]]);var q=matrix.MultiplyPoint3x4(vertices[tri[i+1]]);
                        var z=matrix.MultiplyPoint3x4(vertices[tri[i+2]]);var normal=Vector3.Cross(q-p,z-p).normalized;
                        int axis=Mathf.Abs(normal.y)>Mathf.Max(Mathf.Abs(normal.x),Mathf.Abs(normal.z))?1:Mathf.Abs(normal.x)>Mathf.Abs(normal.z)?0:2;
                        for(int k=0;k<3;k++)
                        {
                            int idx=tri[i+k];var v=matrix.MultiplyPoint3x4(vertices[idx]);
                            vv.Add(vertices[idx]);nn.Add(normals[idx]);
                            uv.Add((axis==0?new Vector2(v.z,v.y):axis==1?new Vector2(v.x,v.z):new Vector2(v.x,v.y))*8);
                        }
                    }
                    var mesh=new Mesh();mesh.SetVertices(vv);mesh.SetNormals(nn);mesh.SetUVs(0,uv);
                    mesh.SetTriangles(Enumerable.Range(0,vv.Count).ToArray(),0);mesh.RecalculateTangents();mesh.bounds=source.bounds;
                    if(mesh.triangles.Length!=tri.Length || mesh.bounds!=source.bounds)throw new Exception("Approach geometry changed");
                    // The same positions and normals in original triangle order, only UV seams are split.
                    for(int i=0;i<tri.Length;i++)if(vv[i]!=vertices[tri[i]] || nn[i]!=normals[tri[i]])throw new Exception("Approach surface changed");
                    f.sharedMesh=SaveMesh(mesh,r.name+" mapped");corrected+=tri.Length/3;
                }
                var bore=all.Single(r=>r.name=="Closed mountain bore V13");var bm=bore.sharedMaterials;
                var exterior=RockMaterial("Bore sandstone",bm[0],new Color(.86f,.86f,.88f),.15f);
                var interior=RockMaterial("Bore inner sandstone",bm[1],new Color(.62f,.65f,.69f),.15f);
                bore.sharedMaterials=new[]{exterior,interior,exterior};
                var stones=all.Where(r=>r.name=="Portal fitted voussoir").ToArray();
                if(stones.Length!=34)throw new Exception("Expected two 17-stone portal arches");
                var archMat=RockMaterial("Fitted sandstone arch",exterior,new Color(.93f,.88f,.83f),1);
                for(int end=0;end<2;end++)
                {
                    var group=stones.Skip(end*17).Take(17).ToArray();
                    Vector3 Centre(MeshRenderer r)=>root.transform.InverseTransformPoint(r.transform.TransformPoint(r.localBounds.center));
                    var first=Centre(group[0]);var last=Centre(group[16]);var top=Centre(group[8]);
                    var right=(first-last).normalized;right.y=0;right.Normalize();var forward=Vector3.Cross(right,Vector3.up).normalized;
                    float theta=Mathf.PI/34,rx=Vector3.Distance(first,last)*.5f/Mathf.Cos(theta);
                    float ry=(top.y-first.y)/(1-Mathf.Sin(theta));var centre=top-Vector3.up*ry;
                    float halfDepth=0,halfThickness=0;
                    foreach(var r in group)
                    {
                        var c=Centre(r);var radial=(c-centre).normalized;
                        foreach(var v in r.GetComponent<MeshFilter>().sharedMesh.vertices)
                        {
                            var d=root.transform.InverseTransformPoint(r.transform.TransformPoint(v))-c;
                            halfDepth=Mathf.Max(halfDepth,Mathf.Abs(Vector3.Dot(d,forward)));
                            halfThickness=Mathf.Max(halfThickness,Mathf.Abs(Vector3.Dot(d,radial)));
                        }
                    }
                    // Continuous, fitted wedges remove the square blocks' protruding corners.
                    var vtx=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
                    Vector3 P(float angle,bool outer,float depth)=>centre+right*(Mathf.Cos(angle)*(rx+(outer?halfThickness:-halfThickness)))+
                        Vector3.up*(Mathf.Sin(angle)*(ry+(outer?halfThickness:-halfThickness)))+forward*depth;
                    void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 outward)
                    {
                        if(Vector3.Dot(Vector3.Cross(b-a,c-a),outward)<0){var tmp=b;b=d;d=tmp;}
                        int n=vtx.Count;vtx.AddRange(new[]{a,b,c,d});
                        var u=(b-a).normalized;var v=Vector3.Cross(outward.normalized,u);
                        foreach(var p in new[]{a,b,c,d})uv.Add(new Vector2(Vector3.Dot(p,u),Vector3.Dot(p,v))*8);
                        indices.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
                    }
                    const int segments=34;
                    for(int i=0;i<segments;i++)
                    {
                        float a=i*Mathf.PI/segments,b=(i+1)*Mathf.PI/segments;
                        var radial=right*Mathf.Cos((a+b)*.5f)+Vector3.up*Mathf.Sin((a+b)*.5f);
                        Quad(P(a,false,-halfDepth),P(b,false,-halfDepth),P(b,true,-halfDepth),P(a,true,-halfDepth),-forward);
                        Quad(P(a,false,halfDepth),P(a,true,halfDepth),P(b,true,halfDepth),P(b,false,halfDepth),forward);
                        Quad(P(a,false,-halfDepth),P(a,false,halfDepth),P(b,false,halfDepth),P(b,false,-halfDepth),-radial);
                        Quad(P(a,true,-halfDepth),P(b,true,-halfDepth),P(b,true,halfDepth),P(a,true,halfDepth),radial);
                    }
                    Quad(P(0,false,-halfDepth),P(0,true,-halfDepth),P(0,true,halfDepth),P(0,false,halfDepth),Vector3.down);
                    Quad(P(Mathf.PI,false,-halfDepth),P(Mathf.PI,false,halfDepth),P(Mathf.PI,true,halfDepth),P(Mathf.PI,true,-halfDepth),Vector3.down);
                    var mesh=new Mesh();mesh.SetVertices(vtx);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
                    string name="Fitted continuous portal "+end;var t=root.transform.Find(name);
                    if(!t){t=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)).transform;t.SetParent(root.transform,false);}
                    t.GetComponent<MeshFilter>().sharedMesh=SaveMesh(mesh,name);var mr=t.GetComponent<MeshRenderer>();mr.sharedMaterial=archMat;
                    mr.shadowCastingMode=ShadowCastingMode.Off;
                    foreach(var r in group)r.enabled=false;
                    Debug.Log($"PORTAL_ARCH end={end} triangles={indices.Count/3} innerRadius={rx-halfThickness:F5}/{ry-halfThickness:F5}");
                }
                PrefabUtility.SaveAsPrefabAsset(root,SwanCanyonLightingStudy.PrefabPath);
                Debug.Log($"PORTAL_REPAIRED approachTriangles={corrected} geometryPreserved=true arches=2 originalArchRenderers=34");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();
        }
        public static void Preview()
        {
            Prepare();SwanCanyonLightingStudy.ApplyBindings();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SwanCanyonLightingStudy.PrefabPath));
            root.GetComponent<SwanCanyonBakedLighting>().SetReview(true);
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.44f,.48f,.54f);
            RenderSettings.ambientEquatorColor=new Color(.30f,.28f,.25f);RenderSettings.ambientGroundColor=new Color(.17f,.15f,.13f);RenderSettings.fog=false;
            var light=new GameObject("Daylight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.35f;light.transform.rotation=Quaternion.Euler(48,-32,0);RenderSettings.sun=light;
            var c=new GameObject("Camera").AddComponent<Camera>();c.fieldOfView=65;c.nearClipPlane=.001f;c.farClipPlane=4;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.18f,.19f,.20f);
            root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var centre=new Vector3(.028677f,.0929f,-.000683f);var forward=new Vector3(-.593f,0,.805f);
            c.transform.position=centre-forward*.13f+Vector3.up*.018f;c.transform.LookAt(centre+forward*.03f);
            var rt=new RenderTexture(1200,1000,24);c.targetTexture=rt;c.Render();var old=RenderTexture.active;RenderTexture.active=rt;
            var tex=new Texture2D(1200,1000,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1200,1000),0,0);tex.Apply();File.WriteAllBytes(Output+"/portal-after.png",tex.EncodeToPNG());
            RenderTexture.active=old;c.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);
            Debug.Log("PORTAL_PREVIEW_DONE");
        }
        [Serializable] class Entry
        {
            public string name,mesh; public Vector3[] vertices,normals; public Vector2[] uv;
            public int[] triangles; public int[] submeshCounts; public string[] materials,textures;
            public Vector2[] textureScales; public Color[] colors;
        }
        [Serializable] class Report { public Entry[] entries; }
        public static void Inspect()
        {
            Directory.CreateDirectory(Output);
            var root=PrefabUtility.LoadPrefabContents(SwanCanyonLightingStudy.PrefabPath);
            try
            {
                var entries=new List<Entry>();
                foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if(!r.enabled || !r.gameObject.activeInHierarchy ||
                        !(r.name.Contains("bore") || r.name.Contains("approach") || r.name.Contains("voussoir") || r.name.Contains("Fixed rock") || r.name.Contains("0029")))continue;
                    var m=r.GetComponent<MeshFilter>().sharedMesh;
                    if(!m)continue;
                    var matrix=root.transform.worldToLocalMatrix*r.localToWorldMatrix;
                    entries.Add(new Entry{name=r.name,mesh=AssetDatabase.GetAssetPath(m),
                        vertices=m.vertices.Select(matrix.MultiplyPoint3x4).ToArray(),
                        normals=m.normals.Select(v=>matrix.MultiplyVector(v).normalized).ToArray(),uv=m.uv,triangles=m.triangles,
                        submeshCounts=Enumerable.Range(0,m.subMeshCount).Select(s=>(int)m.GetIndexCount(s)).ToArray(),
                        materials=r.sharedMaterials.Select(AssetDatabase.GetAssetPath).ToArray(),
                        textures=r.sharedMaterials.Select(x=>AssetDatabase.GetAssetPath(x.GetTexture("_BaseMap"))).ToArray(),
                        textureScales=r.sharedMaterials.Select(x=>x.GetTextureScale("_BaseMap")).ToArray(),
                        colors=r.sharedMaterials.Select(x=>x.GetColor("_BaseColor")).ToArray()});
                }
                File.WriteAllText(Output+"/geometry.json",JsonUtility.ToJson(new Report{entries=entries.ToArray()}));
                Debug.Log("PORTAL_INSPECT_DONE objects="+entries.Count);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
