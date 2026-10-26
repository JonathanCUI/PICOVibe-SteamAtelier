using System;
using System.Collections.Generic;
using ByteDance.PICO.XR;
using UnityEngine;

namespace SwanAtelier
{
    // Real scanned table triangles only. No invisible replacement tabletop and no
    // room mesh renderers/colliders: sensing must not intercept the model's input.
    public sealed class SwanTableSurface
    {
        // Spatial reconstruction has small seams and imperfect semantic borders.
        // Bound the tolerance in real metres; never fill a whole table bounding box.
        public const float SupportTolerance=.05f, HeightTolerance=.03f;
        public struct Triangle
        {
            public Vector3 a, b, c;
            public bool HeightAt(Vector3 p, out float height)
            {
                float d = (b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);
                height = 0;
                if (Mathf.Abs(d) < .000001f) return false;
                float u = ((b.z-c.z)*(p.x-c.x)+(c.x-b.x)*(p.z-c.z))/d;
                float v = ((c.z-a.z)*(p.x-c.x)+(a.x-c.x)*(p.z-c.z))/d;
                if (u < -.001f || v < -.001f || u+v > 1.001f) return false;
                height = u*a.y+v*b.y+(1-u-v)*c.y;
                return true;
            }
            public bool SupportAt(Vector3 p,float tolerance,out float height,out float lateralSquared)
            {
                lateralSquared=0;
                if(HeightAt(p,out height)) return true;
                if(tolerance<=0) return false;
                var closest=ClosestOnEdge(p,a,b);
                var candidate=ClosestOnEdge(p,b,c);
                if(HorizontalSquared(p,candidate)<HorizontalSquared(p,closest))closest=candidate;
                candidate=ClosestOnEdge(p,c,a);
                if(HorizontalSquared(p,candidate)<HorizontalSquared(p,closest))closest=candidate;
                lateralSquared=HorizontalSquared(p,closest);height=closest.y;
                return lateralSquared<=tolerance*tolerance;
            }
            static Vector3 ClosestOnEdge(Vector3 p,Vector3 start,Vector3 end)
            {
                var edge=end-start;float length=edge.x*edge.x+edge.z*edge.z;
                float t=length>1e-8f?Mathf.Clamp01(((p.x-start.x)*edge.x+(p.z-start.z)*edge.z)/length):0;
                return start+edge*t;
            }
            static float HorizontalSquared(Vector3 a,Vector3 b) {float x=a.x-b.x,z=a.z-b.z;return x*x+z*z;}
        }
        readonly Dictionary<Guid, List<Triangle>> chunks = new Dictionary<Guid, List<Triangle>>();
        public int TriangleCount { get; private set; }
        public int Revision { get; private set; }
        public void Clear() { chunks.Clear(); TriangleCount=0; Revision++; }
        public void Update(PxrSpatialMeshInfo data)
        {
            if (data.state == UnityEngine.XR.MeshChangeState.Unchanged) return;
            if (chunks.TryGetValue(data.uuid, out var old)) TriangleCount-=old.Count;
            chunks.Remove(data.uuid); Revision++;
            if (data.state == UnityEngine.XR.MeshChangeState.Removed || data.vertices == null ||
                data.indices == null || data.labels == null || data.labels.Length != data.vertices.Length) return;
            var triangles = new List<Triangle>();
            var pose=Matrix4x4.TRS(data.position,data.rotation,Vector3.one);
            for (int n=0;n+2<data.indices.Length;n+=3) {
                int ia=data.indices[n],ib=data.indices[n+1],ic=data.indices[n+2];
                if (ia>=data.vertices.Length||ib>=data.vertices.Length||ic>=data.vertices.Length) continue;
                // Require every vertex to be labelled Table; unknown and floor
                // geometry must not silently turn into a coffee table.
                if (data.labels[ia]!=PxrSemanticLabel.Table || data.labels[ib]!=PxrSemanticLabel.Table || data.labels[ic]!=PxrSemanticLabel.Table) continue;
                Vector3 a=pose.MultiplyPoint3x4(data.vertices[ia]), b=pose.MultiplyPoint3x4(data.vertices[ib]), c=pose.MultiplyPoint3x4(data.vertices[ic]);
                if (!SwanTwoHandManipulation.Finite(a)||!SwanTwoHandManipulation.Finite(b)||!SwanTwoHandManipulation.Finite(c)) continue;
                Vector3 normal=Vector3.Cross(b-a,c-a);
                // The SDK converts winding to Unity coordinates. Only the upward
                // face supports an object: reject the underside as well as legs.
                if (normal.sqrMagnitude<.00000001f || normal.normalized.y<.985f ||
                    Mathf.Max(a.y,b.y,c.y)-Mathf.Min(a.y,b.y,c.y)>.035f) continue;
                triangles.Add(new Triangle {a=a,b=b,c=c});
            }
            if(triangles.Count>0) chunks[data.uuid]=triangles;
            TriangleCount+=triangles.Count;
        }
        public bool TryHeight(Vector3 point, float maxDifference, out float height, float edgeTolerance=0)
        {
            height=0; float nearest=float.MaxValue; bool found=false;
            foreach(var chunk in chunks.Values) foreach(var triangle in chunk)
                if(triangle.SupportAt(point,edgeTolerance,out float y,out float lateral) && Mathf.Abs(y-point.y)<=maxDifference) {
                    float distance=(y-point.y)*(y-point.y)+lateral;
                    if(distance>nearest)continue;
                    nearest=distance;height=y;found=true;
                }
            return found;
        }
        public bool Fits(Vector3[] supports, Vector3 position, Quaternion rotation, Vector3 scale, float height, int samplesPerFootprint=0)
        {
            if(supports==null||supports.Length==0) return false;
            var pose=Matrix4x4.TRS(position,rotation,scale);
            if(samplesPerFootprint<=0 || supports.Length%samplesPerFootprint!=0)samplesPerFootprint=1;
            for(int start=0;start<supports.Length;start+=samplesPerFootprint) {
                int covered=0;Vector3 centre=Vector3.zero;
                for(int i=0;i<samplesPerFootprint;i++) {
                    var p=pose.MultiplyPoint3x4(supports[start+i]);p.y=height;centre+=p;
                    if(TryHeight(p,HeightTolerance,out _,SupportTolerance))covered++;
                }
                // A few missing perimeter triangles must not veto an otherwise
                // supported object. Each separate footprint needs its centre and
                // at least 80% coverage; large overhangs and central holes fail.
                if(covered<Mathf.CeilToInt(samplesPerFootprint*.8f) ||
                    !TryHeight(centre/samplesPerFootprint,HeightTolerance,out _,.035f))return false;
            }
            return true;
        }
        public string Describe(Vector3[] supports,Vector3 position,Quaternion rotation,Vector3 scale,Vector3 contactCentre)
        {
            bool initialized=false;Bounds bounds=default;Vector3 nearest=default;float distance=float.MaxValue;
            var pose=Matrix4x4.TRS(position,rotation,scale);var centre=pose.MultiplyPoint3x4(contactCentre);
            foreach(var chunk in chunks.Values)foreach(var t in chunk) {
                if(!initialized){bounds=new Bounds(t.a,Vector3.zero);initialized=true;}
                bounds.Encapsulate(t.a);bounds.Encapsulate(t.b);bounds.Encapsulate(t.c);
                var mid=(t.a+t.b+t.c)/3;float d=(mid-centre).sqrMagnitude;
                if(d<distance){nearest=mid;distance=d;}
            }
            bool hit=TryHeight(centre,3,out float height,.035f);int covered=0,supported=0;
            if(hit&&supports!=null)foreach(var local in supports){var p=pose.MultiplyPoint3x4(local);p.y=height;if(TryHeight(p,.025f,out _))covered++;if(TryHeight(p,HeightTolerance,out _,SupportTolerance))supported++;}
            return "base="+centre.ToString("F3")+" tableMin="+bounds.min.ToString("F3")+" tableMax="+bounds.max.ToString("F3")+
                " centreHit="+hit+" height="+height.ToString("F3")+" delta="+(centre.y-height).ToString("F3")+
                " covered="+covered+"/"+(supports==null?0:supports.Length)+" supported="+supported+" nearest="+nearest.ToString("F3");
        }
    }
}
