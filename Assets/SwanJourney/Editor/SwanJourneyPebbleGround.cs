using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SwanJourney.Editor
{
    // Bake the deleted surfaces into the sand's existing continuous UV atlas.
    // Depth-test against the actual sand and previously stamped pebbles. No extra
    // mesh, transparent decal or runtime texture sampling pass is needed.
    internal sealed class SwanJourneyPebbleGround : IDisposable
    {
        const int Size=2048, Raster=Size*2;
        readonly Texture2D sandTexture,stoneTexture;
        readonly Color32[] pixels=new Color32[Raster*Raster];
        readonly float[] heights=new float[Raster*Raster];
        const int Cells=96;
        readonly System.Collections.Generic.List<SandFace>[] bins=new System.Collections.Generic.List<SandFace>[Cells*Cells];
        readonly Vector2 min,max;
        struct SandFace {public Vector2 a,ab,ac,ua,ub,uc;public float area,ya,yb,yc;}
        readonly Color stoneTint;
        int touched;

        static Vector2 XZ(Vector3 p)=>new Vector2(p.x,p.z);
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        int Cell(float p,float lo,float hi)=>Mathf.Clamp((int)(Mathf.InverseLerp(lo,hi,p)*Cells),0,Cells-1);
        bool GroundUv(Vector3 point,out Vector2 uv,out float surface)
        {
            uv=Vector2.zero;surface=float.NegativeInfinity;
            var p=XZ(point);
            var list=bins[Cell(p.y,min.y,max.y)*Cells+Cell(p.x,min.x,max.x)];
            if(list==null)return false;
            foreach(var t in list)
            {
                var d=p-t.a;float wb=Cross(d,t.ac)/t.area,wc=Cross(t.ab,d)/t.area,wa=1-wb-wc;
                if(wa<-.00001f||wb<-.00001f||wc<-.00001f)continue;
                float h=wa*t.ya+wb*t.yb+wc*t.yc;
                if(h>surface){surface=h;uv=wa*t.ua+wb*t.ub+wc*t.uc;}
            }
            return !float.IsNegativeInfinity(surface);
        }
        public float Relief(Vector3 point) => GroundUv(point,out _,out var height) ? point.y-height : 0f;
        static Texture2D ReadAlbedo(Material material)
        {
            var original=(Texture2D)material.GetTexture("_BaseMap");
            string path=AssetDatabase.GetAssetPath(original);
            if(!File.Exists(path) || path==SwanJourneyPebbleBudget.BakedPath ||
                material.GetTextureScale("_BaseMap")!=Vector2.one || material.GetTextureOffset("_BaseMap")!=Vector2.zero)
                throw new InvalidOperationException("Missing or transformed source albedo: "+material.name);
            var t=new Texture2D(2,2,TextureFormat.RGBA32,false,true){wrapMode=original.wrapMode};
            if(!t.LoadImage(File.ReadAllBytes(path)))throw new InvalidOperationException("Could not read "+path);
            return t;
        }

        public SwanJourneyPebbleGround(Transform root,MeshRenderer[] sand,Material ground,Material stone)
        {
            sandTexture=ReadAlbedo(ground);stoneTexture=ReadAlbedo(stone);stoneTint=stone.GetColor("_BaseColor").gamma;
            var patches=sand.Select(r=>{
                var mesh=r.GetComponent<MeshFilter>().sharedMesh;
                var toRoot=root.worldToLocalMatrix*r.localToWorldMatrix;
                return new {points=mesh.vertices.Select(toRoot.MultiplyPoint3x4).ToArray(),uv=mesh.uv,faces=mesh.triangles};
            }).ToArray();
            var groundPoints=patches.SelectMany(p=>p.points).Select(XZ).ToArray();
            min=groundPoints.Aggregate(Vector2.Min);max=groundPoints.Aggregate(Vector2.Max);
            // Sample once per final texel. The four subpixel samples only resolve
            // stone coverage; the source sand keeps its authored color underneath.
            for(int y=0;y<Size;y++)for(int x=0;x<Size;x++)
            {
                Color32 c=sandTexture.GetPixelBilinear((x+.5f)/Size,(y+.5f)/Size);
                int j=2*y*Raster+2*x;
                pixels[j]=pixels[j+1]=pixels[j+Raster]=pixels[j+Raster+1]=c;
            }
            for(int i=0;i<heights.Length;i++)heights[i]=float.NegativeInfinity;
            foreach(var patch in patches)
            {
                for(int i=0;i<patch.faces.Length;i+=3)
                {
                    int a=patch.faces[i],b=patch.faces[i+1],c=patch.faces[i+2];
                    var pa=patch.points[a];var pb=patch.points[b];var pc=patch.points[c];
                    var t=new SandFace{a=XZ(pa),ab=XZ(pb-pa),ac=XZ(pc-pa),ua=patch.uv[a],ub=patch.uv[b],uc=patch.uv[c],ya=pa.y,yb=pb.y,yc=pc.y};
                    t.area=Cross(t.ab,t.ac);
                    if(Mathf.Abs(t.area)<1e-12f)continue;
                    var lo=Vector2.Min(t.a,Vector2.Min(XZ(pb),XZ(pc)));var hi=Vector2.Max(t.a,Vector2.Max(XZ(pb),XZ(pc)));
                    for(int y=Cell(lo.y,min.y,max.y);y<=Cell(hi.y,min.y,max.y);y++)
                    for(int x=Cell(lo.x,min.x,max.x);x<=Cell(hi.x,min.x,max.x);x++)
                    {
                        int j=y*Cells+x;
                        if(bins[j]==null)bins[j]=new System.Collections.Generic.List<SandFace>();
                        bins[j].Add(t);
                    }
                    Rasterize(patch.uv[a],patch.uv[b],patch.uv[c],(j,wa,wb,wc)=>{
                        heights[j]=Mathf.Max(heights[j],wa*patch.points[a].y+wb*patch.points[b].y+wc*patch.points[c].y);
                    });
                }
            }
        }

        static void Rasterize(Vector2 a,Vector2 b,Vector2 c,Action<int,float,float,float> pixel)
        {
            a*=Raster;b*=Raster;c*=Raster;
            var ab=b-a;var ac=c-a;float area=Cross(ab,ac);
            if(Mathf.Abs(area)<1e-7f)return;
            var lo=Vector2.Min(a,Vector2.Min(b,c));var hi=Vector2.Max(a,Vector2.Max(b,c));
            int x0=Mathf.Max(0,Mathf.CeilToInt(lo.x-.5f)),x1=Mathf.Min(Raster-1,Mathf.FloorToInt(hi.x-.5f));
            int y0=Mathf.Max(0,Mathf.CeilToInt(lo.y-.5f)),y1=Mathf.Min(Raster-1,Mathf.FloorToInt(hi.y-.5f));
            for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
            {
                var p=new Vector2(x+.5f,y+.5f)-a;
                float wb=Cross(p,ac)/area,wc=Cross(ab,p)/area,wa=1-wb-wc;
                if(wa>=0 && wb>=0 && wc>=0)pixel(y*Raster+x,wa,wb,wc);
            }
        }
        public void Imprint(Vector3 a,Vector3 b,Vector3 c,Vector2 ua,Vector2 ub,Vector2 uc)
        {
            float shade=.78f+.22f*Mathf.Abs(Vector3.Cross(b-a,c-a).normalized.y);
            if(!GroundUv(a,out var ga,out _)||!GroundUv(b,out var gb,out _)||!GroundUv(c,out var gc,out _))return;
            Rasterize(ga,gb,gc,(j,wa,wb,wc)=>{
                float h=wa*a.y+wb*b.y+wc*c.y;
                if(float.IsNegativeInfinity(heights[j]) || h+1e-6f<heights[j])return;
                heights[j]=h;
                var uv=wa*ua+wb*ub+wc*uc;var color=stoneTexture.GetPixelBilinear(uv.x,uv.y)*stoneTint*shade;
                color.a=1;pixels[j]=color;touched++;
            });
        }
        public Texture2D Save(string path)
        {
            if(touched==0)throw new InvalidOperationException("No exposed pebble fragments baked onto sand");
            var result=new Texture2D(Size,Size,TextureFormat.RGB24,false,true);
            try
            {
                var down=new Color32[Size*Size];
                for(int y=0;y<Size;y++)for(int x=0;x<Size;x++)
                {
                    int j=2*y*Raster+2*x;
                    var a=pixels[j];var b=pixels[j+1];var c=pixels[j+Raster];var d=pixels[j+Raster+1];
                    down[y*Size+x]=new Color32((byte)((a.r+b.r+c.r+d.r+2)/4),(byte)((a.g+b.g+c.g+d.g+2)/4),(byte)((a.b+b.b+c.b+d.b+2)/4),255);
                }
                result.SetPixels32(down);result.Apply();File.WriteAllBytes(path,result.EncodeToPNG());
            }
            finally{Object.DestroyImmediate(result);}
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var imp=(TextureImporter)AssetImporter.GetAtPath(path);
            imp.sRGBTexture=true;imp.mipmapEnabled=true;imp.wrapMode=TextureWrapMode.Clamp;imp.filterMode=FilterMode.Trilinear;
            imp.maxTextureSize=Size;imp.alphaSource=TextureImporterAlphaSource.None;
            var android=imp.GetPlatformTextureSettings("Android");android.overridden=true;android.maxTextureSize=Size;
            android.format=TextureImporterFormat.ASTC_6x6;imp.SetPlatformTextureSettings(android);imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        public void Dispose(){Object.DestroyImmediate(sandTexture);Object.DestroyImmediate(stoneTexture);}
    }
}
