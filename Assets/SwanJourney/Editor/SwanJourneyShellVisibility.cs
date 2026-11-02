using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace SwanJourney.Editor
{
    // Conservative orthographic depth tests in train space, for the offline shell bake.
    // Mark faces conservatively before the builder expands the retained boundary.
    internal static class SwanJourneyShellVisibility
    {
        internal struct Face {public int a,b,c,id;}
        const int N=512;
        const float Sky=1e20f;
        internal static int[] FindVisible(Vector3[] points,Face[] faces)
        {
            var visible=new int[faces.Length];
            var directions=new List<Vector3>{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
            for(int i=0;i<144;i++)
            {
                double y=1-2*(i+.5)/144, radius=Math.Sqrt(1-y*y),angle=i*2.399963229728653;
                directions.Add(new Vector3((float)(radius*Math.Cos(angle)),(float)y,(float)(radius*Math.Sin(angle))));
            }
            var bounds=new Bounds(points[0],Vector3.zero);foreach(var p in points)bounds.Encapsulate(p);
            Parallel.ForEach(directions,new ParallelOptions{MaxDegreeOfParallelism=Math.Min(8,Environment.ProcessorCount)},dir=>
            {
                var right=Vector3.Cross(Math.Abs(dir.y)>.98?Vector3.forward:Vector3.up,dir).normalized;
                var up=Vector3.Cross(dir,right);var extent=bounds.extents;
                float Radius(Vector3 axis)=>Math.Abs(axis.x)*extent.x+Math.Abs(axis.y)*extent.y+Math.Abs(axis.z)*extent.z;
                float scale=(N-12)/(2*Math.Max(Radius(up),Radius(right)));
                var screen=new Vector3[points.Length];
                for(int i=0;i<points.Length;i++)
                {
                    var d=points[i]-bounds.center;
                    screen[i]=new Vector3(N*.5f+scale*Vector3.Dot(right,d),N*.5f+scale*Vector3.Dot(up,d),-Vector3.Dot(dir,d));
                }
                var z=new float[N*N];for(int i=0;i<z.Length;i++)z[i]=Sky;
                foreach(var f in faces)
                {
                    var a=screen[f.a];var b=screen[f.b];var c=screen[f.c];
                    float bx=b.x-a.x,by=b.y-a.y,cx=c.x-a.x,cy=c.y-a.y,area=bx*cy-by*cx;
                    if(area<1e-6f)continue; // only outward-facing opaque triangles may hide another part
                    int x0=(int)Math.Ceiling(Math.Min(a.x,Math.Min(b.x,c.x))-.5f),x1=(int)Math.Floor(Math.Max(a.x,Math.Max(b.x,c.x))-.5f);
                    int y0=(int)Math.Ceiling(Math.Min(a.y,Math.Min(b.y,c.y))-.5f),y1=(int)Math.Floor(Math.Max(a.y,Math.Max(b.y,c.y))-.5f);
                    float inv=1/area, db=(b.z-a.z)*inv,dc=(c.z-a.z)*inv;
                    for(int y=y0;y<=y1;y++){
                        float py=y+.5f-a.y,px=x0+.5f-a.x,wb=px*cy-py*cx,wc=bx*py-by*px;
                        for(int x=x0;x<=x1;x++,wb+=cy,wc-=by){
                            if(wb<0||wc<0||wb+wc>area)continue;
                            float depth=a.z+wb*db+wc*dc;int j=y*N+x;
                            if(depth<z[j])z[j]=depth;
                        }
                    }
                }
                // Max-depth mip cells deliberately include nearby holes and silhouettes.
                var levels=new List<float[]>{z};
                for(int width=N;width>1;width/=2)
                {
                    var prev=levels[levels.Count-1];int half=width/2;var next=new float[half*half];
                    for(int y=0;y<half;y++)for(int x=0;x<half;x++){int j=2*y*width+2*x;next[y*half+x]=Math.Max(Math.Max(prev[j],prev[j+1]),Math.Max(prev[j+width],prev[j+width+1]));}
                    levels.Add(next);
                }
                foreach(var f in faces)
                {
                    if(System.Threading.Volatile.Read(ref visible[f.id])!=0)continue;
                    var a=screen[f.a];var b=screen[f.b];var c=screen[f.c];
                    int x0=(int)Math.Min(a.x,Math.Min(b.x,c.x))-1,x1=(int)Math.Max(a.x,Math.Max(b.x,c.x))+1;
                    int y0=(int)Math.Min(a.y,Math.Min(b.y,c.y))-1,y1=(int)Math.Max(a.y,Math.Max(b.y,c.y))+1;
                    int level=0;while((1<<level)<Math.Max(x1-x0,y1-y0))level++;
                    int width=N>>level;var mip=levels[level];
                    float near=Math.Min(a.z,Math.Min(b.z,c.z))-1.5f/scale;
                    for(int y=y0>>level;y<=(y1>>level);y++)for(int x=x0>>level;x<=(x1>>level);x++)
                        if(mip[y*width+x]>=near)System.Threading.Volatile.Write(ref visible[f.id],1);
                }
            });
            return visible;
        }
    }
}
