using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace SwanJourney.Editor
{
    // Bake rock contact through the actual terrain UVs. The headset samples the same single sand albedo as before.
    internal static class SwanXrCanyonSurfaces
    {
        const int Grid=160;
        const string Native="Assets/SwanJourney/Generated/NativeVisuals/";
        const string Atlas=SwanXrWorkshopSurfaces.Folder+"Ochre sand.png";
        internal static bool Apply(Transform root)
        {
            var original=AssetDatabase.LoadAssetAtPath<Material>(Native+"Material_0198.mat");
            var gravel=AssetDatabase.LoadAssetAtPath<Material>(Native+"Material_0173.mat");
            var sand=SwanXrWorkshopSurfaces.MaterialAsset("Ochre sand",original.shader);
            var bed=SwanXrWorkshopSurfaces.MaterialAsset("Weathered track bed",gravel.shader);
            sand.CopyPropertiesFromMaterial(original);bed.CopyPropertiesFromMaterial(gravel);
            var renderers=root.GetComponentsInChildren<MeshRenderer>(true);
            var contactMaterials=new HashSet<Material>();
            bool rockChanged=ApplyTunnelFinish(renderers,contactMaterials);
            var patches=renderers.Where(r=>r.sharedMaterial==original || r.sharedMaterial==sand).ToArray();
            if(patches.Length==0)throw new InvalidOperationException("Missing canyon sand");
            var source=original.GetTexture("_BaseMap") as Texture2D;
            var albedo=Bake(root,patches,renderers,source,contactMaterials);
            sand.SetTexture("_BaseMap",albedo);sand.SetTexture("_MainTex",albedo);
            var color=new Color(.54f,.50f,.44f);
            bed.SetColor("_BaseColor",color);bed.SetColor("_Color",color);
            bool changed=false;
            foreach(var r in renderers)
            {
                if(r.sharedMaterial==original){r.sharedMaterial=sand;changed=true;}
                else if(r.sharedMaterial==gravel){r.sharedMaterial=bed;changed=true;}
            }
            return changed || rockChanged;
        }

        static bool ApplyTunnelFinish(MeshRenderer[] renderers,HashSet<Material> contactMaterials)
        {
            var replacements=new Dictionary<Material,Material>();
            void Surface(int id, Color multiplier, int albedoId=0)
            {
                var source=AssetDatabase.LoadAssetAtPath<Material>(Native+$"Material_{id:D4}.mat");
                var finish=SwanXrWorkshopSurfaces.MaterialAsset($"Canyon rock {id:D4}",source.shader);
                finish.CopyPropertiesFromMaterial(source);
                var tint=source.GetColor("_BaseColor")*multiplier;
                finish.SetColor("_BaseColor",tint); finish.SetColor("_Color",tint);
                if(albedoId!=0)
                {
                    var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(SwanXrWorkshopSurfaces.Folder+$"Cliff recess {albedoId:D4}.png");
                    if(!texture)throw new InvalidOperationException("Missing canyon recess texture "+albedoId);
                    finish.SetTexture("_BaseMap",texture); finish.SetTexture("_MainTex",texture);
                }
                replacements.Add(source,finish);
                if(source.name.Contains("Rock ambient"))contactMaterials.Add(finish);
            }
            foreach(int id in new[]{166,167,168,169,172,180})
                Surface(id,Color.white,id==180?169:id);
            Surface(182,new Color(.59f,.64f,.73f)); // Inner bore shares its texture with the sunlit outer shell.
            Surface(183,new Color(.82f,.84f,.91f));
            Surface(197,new Color(.92f,.94f,.98f),169);
            bool changed=false;
            foreach(var renderer in renderers)
            {
                var slots=renderer.sharedMaterials; bool swap=false;
                for(int i=0;i<slots.Length;i++)
                    if(slots[i] && replacements.TryGetValue(slots[i],out var finish)){slots[i]=finish;swap=true;}
                if(swap){renderer.sharedMaterials=slots;changed=true;}
            }
            return changed;
        }

        static Texture2D Bake(Transform root,MeshRenderer[] patches,MeshRenderer[] renderers,Texture2D source,HashSet<Material> contactMaterials)
        {
            var bounds=new Bounds();
            bool first=true;
            foreach(var r in patches)
            foreach(var v in r.GetComponent<MeshFilter>().sharedMesh.vertices)
            {
                var p=root.InverseTransformPoint(r.transform.TransformPoint(v));
                if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
            }
            Vector2 Map(Vector3 p)=>new Vector2((p.x-bounds.min.x)/bounds.size.x,(p.z-bounds.min.z)/bounds.size.z);
            var ground=Enumerable.Repeat(float.NegativeInfinity,Grid*Grid).ToArray();
            var footprints=new float[Grid*Grid];var uvContact=new float[Grid*Grid];
            void Faces(MeshRenderer r,bool terrain,bool toUv)
            {
                var mesh=r.GetComponent<MeshFilter>().sharedMesh;
                var tr=root.worldToLocalMatrix*r.localToWorldMatrix;
                var v=mesh.vertices.Select(tr.MultiplyPoint3x4).ToArray();var uv=toUv?mesh.uv:null;var indices=mesh.triangles;
                for(int i=0;i<indices.Length;i+=3)
                {
                    int a=indices[i],b=indices[i+1],c=indices[i+2];
                    var pa=Map(v[a]);var pb=Map(v[b]);var pc=Map(v[c]);
                    var p=toUv?uv[a]:pa;var q=toUv?uv[b]:pb;var z=toUv?uv[c]:pc;
                    var ab=q-p;var ac=z-p;float area=ab.x*ac.y-ab.y*ac.x;
                    if(Mathf.Abs(area)<1e-10f)continue;
                    int x0=Cell(Mathf.Min(p.x,Mathf.Min(q.x,z.x))),x1=Cell(Mathf.Max(p.x,Mathf.Max(q.x,z.x)));
                    int y0=Cell(Mathf.Min(p.y,Mathf.Min(q.y,z.y))),y1=Cell(Mathf.Max(p.y,Mathf.Max(q.y,z.y)));
                    for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                    {
                        float dx=(x+.5f)/Grid-p.x,dy=(y+.5f)/Grid-p.y;
                        float wb=(dx*ac.y-dy*ac.x)/area,wc=(ab.x*dy-ab.y*dx)/area,wa=1-wb-wc;
                        if(wa<0||wb<0||wc<0)continue;
                        int at=y*Grid+x;
                        if(toUv){var pos=wa*pa+wb*pb+wc*pc;uvContact[at]=Sample(footprints,pos.x,pos.y);continue;}
                        float h=wa*v[a].y+wb*v[b].y+wc*v[c].y;
                        if(terrain)ground[at]=Mathf.Max(ground[at],h);
                        else if(h<=ground[at]+.035f)footprints[at]=1;
                    }
                }
            }
            foreach(var r in patches)Faces(r,true,false);
            foreach(var r in renderers)
                if(r.enabled && r.gameObject.activeInHierarchy && r.sharedMaterial &&
                    (r.sharedMaterial.name.Contains("Rock ambient") || contactMaterials.Contains(r.sharedMaterial) ||
                     r.name.StartsWith("Solid sandstone approach",StringComparison.Ordinal)) &&
                    r.bounds.size.magnitude > .075f)Faces(r,false,false);
            if(!footprints.Any(v=>v>0))throw new InvalidOperationException("No rock contact sampled");
            for(int pass=0;pass<4;pass++)
            {
                var blurred=new float[Grid*Grid];
                for(int y=0;y<Grid;y++)for(int x=0;x<Grid;x++)
                {
                    float sum=0;
                    for(int k=-3;k<=3;k++)
                        sum+=footprints[pass%2==0?y*Grid+Mathf.Clamp(x+k,0,Grid-1):Mathf.Clamp(y+k,0,Grid-1)*Grid+x];
                    blurred[y*Grid+x]=sum/7;
                }
                footprints=blurred;
            }
            foreach(var r in patches)Faces(r,true,true);
            var image=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
            try
            {
                if(!image.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(source))))throw new IOException("Invalid sand albedo");
                var pixels=image.GetPixels32();int w=image.width,h=image.height;
                for(int y=0;y<h;y++)for(int x=0;x<w;x++)
                {
                    int i=y*w+x;var c=pixels[i];
                    float u=(x+.5f)/w,v=(y+.5f)/h;
                    float contact=1-.34f*Sample(uvContact,u,v);
                    float sandWeight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(125,202,c.r));
                    float grain=1+.038f*(Mathf.PerlinNoise(u*8,v*8)-.5f);
                    pixels[i]=new Color32(Byte(c.r*Mathf.Lerp(.98f,.82f,sandWeight)*contact*grain),
                        Byte(c.g*Mathf.Lerp(.98f,1.18f,sandWeight)*contact*grain),
                        Byte(c.b*Mathf.Lerp(.98f,1.46f,sandWeight)*contact*grain),255);
                }
                image.SetPixels32(pixels);image.Apply(false);var bytes=image.EncodeToPNG();
                if(!File.Exists(Atlas)||!File.ReadAllBytes(Atlas).SequenceEqual(bytes))File.WriteAllBytes(Atlas,bytes);
            }
            finally{Object.DestroyImmediate(image);}
            AssetDatabase.ImportAsset(Atlas);
            var imp=(TextureImporter)AssetImporter.GetAtPath(Atlas);
            var src=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(source));
            var settings=new TextureImporterSettings();src.ReadTextureSettings(settings);imp.SetTextureSettings(settings);
            imp.maxTextureSize=src.maxTextureSize;imp.SetPlatformTextureSettings(src.GetPlatformTextureSettings("Android"));
            AssetDatabase.WriteImportSettingsIfDirty(Atlas);AssetDatabase.ImportAsset(Atlas);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(Atlas);
        }
        static byte Byte(float value)=>(byte)Mathf.Clamp(Mathf.RoundToInt(value),0,255);
        static int Cell(float n)=>Mathf.Clamp(Mathf.FloorToInt(n*Grid),0,Grid-1);
        static float Sample(float[] map,float u,float v)
        {
            float x=Mathf.Clamp(u*Grid-.5f,0,Grid-1),y=Mathf.Clamp(v*Grid-.5f,0,Grid-1);
            int i=(int)x,j=(int)y,b=Mathf.Min(i+1,Grid-1),c=Mathf.Min(j+1,Grid-1);
            return Mathf.Lerp(Mathf.Lerp(map[j*Grid+i],map[j*Grid+b],x-i),Mathf.Lerp(map[c*Grid+i],map[c*Grid+b],x-i),y-j);
        }
    }
}
