using System.Collections.Generic;
using System.IO;
using System.Linq;
using ByteDance.PICO.SpatialAdapter.Component;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SwanAtelier.Editor
{
    public static partial class SwanAtelierBuild
    {
        static void ConfigureConsoleInteractions(Transform root)
        {
            int targets=0;
            foreach(var control in root.GetComponentsInChildren<AtelierControl>(true)) {
                var renderers=control.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled&&
                    !r.GetComponent<TMPro.TMP_Text>()&&r.GetComponentInParent<AtelierControl>()==control&&
                    r.GetComponent<MeshFilter>()&&r.GetComponent<MeshFilter>().sharedMesh&&
                    r.GetComponent<MeshFilter>().sharedMesh.vertexCount>0).ToArray();
                foreach(var collider in control.GetComponentsInChildren<Collider>(true))
                    if(collider.GetComponentInParent<AtelierControl>()==control)Object.DestroyImmediate(collider);
                foreach(var renderer in renderers) {
                    var bounds=renderer.GetComponent<MeshFilter>().sharedMesh.bounds;
                    var box=renderer.gameObject.AddComponent<BoxCollider>();box.center=bounds.center;box.size=bounds.size;
                    var target=renderer.gameObject.AddComponent<SwanConsoleHitTarget>();target.hitBox=box;target.authoredSize=bounds.size;target.RefreshSize();
                    if(!renderer.GetComponent<SpatialAdapterHoverEffect>())renderer.gameObject.AddComponent<SpatialAdapterHoverEffect>();
                    if(control.action==AtelierAction.Assembly)game.atelier.resetButton=box;
                    targets++;
                }
                if(renderers.Length==0)throw new System.Exception("Console control has no mesh target: "+control.name);
            }
            Debug.Log("ATELIER_SWAN_CONSOLE_MESH_TARGETS count="+targets);
        }
        static void ConfigureCutawaySteam(LocomotiveController engine,LocomotiveThermalEffects thermal,Transform root)
        {
            var converted=new List<MeshRenderer>();var report=new List<string>();
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true)) {
                var source=renderer.sharedMaterial;
                if(!source||!source.shader)continue;
                string shader=source.shader.name;
                bool waterBody=shader=="SteamWorkshop/WaterBodyV8",waterSurface=shader=="SteamWorkshop/WaterSurfaceV8";
                if(shader!="SteamWorkshop/SteamVolume"&&shader!="SteamWorkshop/CylinderSteamV7"&&!waterBody&&!waterSurface)continue;
                var filter=renderer.GetComponent<MeshFilter>();if(!filter)continue;
                Vector3 bounds=waterBody||waterSurface?Vector3.one*.5f:(Vector3)source.GetVector("_Bounds");
                float floor=waterBody||waterSurface?-.647058f:source.GetFloat("_Floor");
                if(!waterSurface) {
                    var mesh=SteamChamberMesh(bounds,floor);mesh.name="Swan rounded cutaway chamber "+converted.Count;
                    if(waterBody) {
                        // The source ray marcher clips water BELOW local Y=.323529.
                        // Rotate the clipped cap to retain the lower cylindrical body.
                        var vertices=mesh.vertices;for(int i=0;i<vertices.Length;i++){vertices[i].y=-vertices[i].y;vertices[i].z=-vertices[i].z;}
                        mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();
                    }
                    AssetDatabase.CreateAsset(mesh,Generated+"/CutawayChamber_"+converted.Count.ToString("00")+".asset");
                    filter.sharedMesh=mesh;
                }
                var tint=waterBody||waterSurface?new Color(.10f,.46f,.58f,waterBody?.14f:.22f):source.GetColor("_BaseColor");
                if(!waterBody&&!waterSurface)tint.a=shader.EndsWith("CylinderSteamV7")?.20f:.13f;
                var material=Material("Swan transparent cutaway "+(waterBody?"water body ":waterSurface?"water surface ":"steam ")+converted.Count,tint,0,.05f);
                material.SetOverrideTag("RenderType","Transparent");material.SetFloat("_Mode",3);
                material.SetInt("_SrcBlend",(int)BlendMode.One);material.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite",0);material.EnableKeyword("_ALPHAPREMULTIPLY_ON");material.renderQueue=3000;
                renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
                report.Add(renderer.name+" | "+shader+" | bounds="+bounds+" floor="+floor+" alpha="+tint.a);
                if(!waterSurface)converted.Add(renderer);
            }
            if(engine.cylinderSteam)engine.cylinderSteam.enabled=false;
            var display=root.gameObject.AddComponent<SwanCutawaySteam>();display.engine=engine;display.thermal=thermal;display.volumes=converted.ToArray();
            File.WriteAllLines("Builds/Evidence/cutaway-volume-conversion.txt",report);
            Debug.Log("ATELIER_SWAN_CUTAWAY_CONVERTED roundedTransparentVolumes="+converted.Count);
        }
        public static Mesh SteamChamberMesh(Vector3 bounds,float floor)
        {
            const int segments=48;
            floor=Mathf.Clamp(floor,-1,.99f);
            var cross=new List<Vector2>();
            bool full=floor<=-.999f;float angle=Mathf.Acos(floor);
            for(int i=0;i<(full?segments:segments+1);i++) {
                float a=full?2*Mathf.PI*i/segments:Mathf.Lerp(-angle,angle,i/(float)segments);
                cross.Add(new Vector2(Mathf.Cos(a)*bounds.y,Mathf.Sin(a)*bounds.z));
            }
            var vertices=new List<Vector3>();var triangles=new List<int>();
            System.Action<Vector3,Vector3,Vector3> tri=(a,b,c)=>{
                int at=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);triangles.Add(at);triangles.Add(at+1);triangles.Add(at+2);
            };
            Vector2 centre=Vector2.zero;foreach(var p in cross)centre+=p;centre/=cross.Count;
            for(int i=0;i<cross.Count;i++) {
                var a=cross[i];var b=cross[(i+1)%cross.Count];
                var la=new Vector3(-bounds.x,a.x,a.y);var lb=new Vector3(-bounds.x,b.x,b.y);
                var ra=new Vector3(bounds.x,a.x,a.y);var rb=new Vector3(bounds.x,b.x,b.y);
                tri(la,lb,rb);tri(la,rb,ra);
                tri(new Vector3(-bounds.x,centre.x,centre.y),lb,la);
                tri(new Vector3(bounds.x,centre.x,centre.y),ra,rb);
            }
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
