using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace SwanJourney.Editor
{
    internal static class SwanXrVisualIntegration
    {
        const string Folder="Assets/SwanJourney/Branding/VisualIntegration";
        internal static void Apply(SteamJourney journey)
        {
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/SwanJourney/Branding","VisualIntegration");
            var valley=journey.gameObject.AddComponent<SwanValleyIntegration>();
            valley.journey=journey;valley.sky=journey.GetComponent<MesaDayNightV13>();
            valley.mood=Object.FindObjectOfType<StudioWorkshopMood>(true);
            valley.originalWorkshopReflection=valley.mood.workshopReflection;
            valley.softWorkshopReflection=StudioReflection();
            valley.mood.workshopReflection=valley.softWorkshopReflection;
            valley.tunnelField=AssetDatabase.LoadAssetAtPath<Texture3D>("Assets/AtelierJourney/SpatialPolishV20/Tunnel skylight visibility.asset");
            valley.fieldMin=new Vector3(-2.8193374f,.75f,-7.4599586f);
            valley.fieldSize=new Vector3(9.453632f,3.6999998f,4.9794803f);
            if(!valley.tunnelField || valley.tunnelField.width!=96 || valley.tunnelField.height!=32 || valley.tunnelField.depth!=96)
                throw new InvalidOperationException("Missing tunnel visibility field");
            // The volume is fixed to the authored canyon, then transformed with the whole exhibit.
            var pixels=valley.tunnelField.GetPixels();float minimum=1,maximum=0;
            foreach(var pixel in pixels){minimum=Mathf.Min(minimum,pixel.r);maximum=Mathf.Max(maximum,pixel.r);}
            if(minimum>.4f || maximum<.95f)throw new InvalidOperationException($"Empty tunnel field {minimum}/{maximum}");
            valley.contactMaterial=MaterialAsset("Soft wheel contact",ShaderAsset("XrTrainContact"));
            var shader=ShaderAsset("XrValleySurface");
            var variants=new Dictionary<Material,Material>();
            int slots=0,renderers=0;
            valley.vehicleCentres=new Vector3[4];valley.vehicleSizes=new Vector3[4];
            for(int v=0;v<4;v++)
            {
                var root=journey.valleyTrain.vehicles[v];
                Bounds bounds=default;bool found=false;
                foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var materials=r.sharedMaterials;bool changed=false;
                    for(int i=0;i<materials.Length;i++)
                    {
                        var src=materials[i];
                        if(!src || (src.shader.name!="Universal Render Pipeline/Lit" && src.shader.name!="SwanJourney/XR Structure Surface") || src.renderQueue>=2450)continue;
                        if(!variants.TryGetValue(src,out var mat))
                        {
                            string guid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(src));
                            if(string.IsNullOrEmpty(guid))throw new InvalidOperationException("Transient vehicle material "+src.name);
                            mat=MaterialAsset("Tunnel "+guid,shader);mat.CopyPropertiesFromMaterial(src);mat.shader=shader;
                            if(src.IsKeywordEnabled("_STRUCTURE_AO"))mat.EnableKeyword("_STRUCTURE_AO");
                            mat.renderQueue=-1;EditorUtility.SetDirty(mat);variants.Add(src,mat);
                        }
                        materials[i]=mat;slots++;changed=true;
                    }
                    if(!changed)continue;
                    r.sharedMaterials=materials;renderers++;
                    var b=r.localBounds;var m=root.worldToLocalMatrix*r.localToWorldMatrix;
                    for(int c=0;c<8;c++)
                    {
                        var p=m.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((c&1)==0?-1:1,(c&2)==0?-1:1,(c&4)==0?-1:1)));
                        if(!found){bounds=new Bounds(p,Vector3.zero);found=true;}else bounds.Encapsulate(p);
                    }
                }
                if(!found)throw new InvalidOperationException("No opaque train surface "+root.name);
                valley.vehicleCentres[v]=bounds.center;
                valley.vehicleSizes[v]=Vector3.Scale(bounds.size,new Vector3(.93f,1,.92f));
                Debug.Log($"XR_INTEGRATION_FOOTPRINT vehicle={v} centre={bounds.center} size={valley.vehicleSizes[v]} scale={root.lossyScale}");
            }
            var seat=journey.gameObject.AddComponent<SwanPartSeatFeedback>();
            seat.journey=journey;seat.seatShader=ShaderAsset("XrPartSeat");
            Debug.Log($"XR_INTEGRATION_BUILD surfaces={renderers} slots={slots} sharedMaterials={variants.Count} contactTriangles=8 tunnelRange={minimum:F3}/{maximum:F3} reflection={valley.softWorkshopReflection.width}");
        }
        static Shader ShaderAsset(string name)
        {
            var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/SwanJourney/"+name+".shader");
            if(!shader)throw new InvalidOperationException(name);return shader;
        }
        static Material MaterialAsset(string name,Shader shader)
        {
            string path=Folder+"/"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat){mat=new Material(shader){name=name};AssetDatabase.CreateAsset(mat,path);}
            mat.shader=shader;return mat;
        }
        static Cubemap StudioReflection()
        {
            const string path=Folder+"/Soft workshop reflection.exr";
            if(!File.Exists(path))
            {
                // Offline radiance, not a runtime reflection capture. Broad gradients avoid
                // rectangular white reflections across flat painted water tanks.
                const int width=1024,height=512;
                var tex=new Texture2D(width,height,TextureFormat.RGBAHalf,false,true);
                var pixels=new Color[width*height];
                Vector3 key=new Vector3(-.6f,.72f,.35f).normalized;
                Vector3 fill=new Vector3(.7f,.35f,-.5f).normalized;
                Vector3 rim=new Vector3(-.25f,.38f,-.9f).normalized;
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                {
                    float latitude=((y+.5f)/height-.5f)*Mathf.PI,longitude=((x+.5f)/width)*Mathf.PI*2;
                    var d=new Vector3(Mathf.Cos(latitude)*Mathf.Sin(longitude),Mathf.Sin(latitude),Mathf.Cos(latitude)*Mathf.Cos(longitude));
                    var c=Color.Lerp(new Color(.07f,.055f,.043f),new Color(.28f,.31f,.34f),Mathf.SmoothStep(0,1,d.y*.5f+.5f));
                    c+=new Color(.93f,.88f,.77f)*Mathf.Exp((Vector3.Dot(d,key)-1)*13)*1.65f;
                    c+=new Color(.66f,.75f,.86f)*Mathf.Exp((Vector3.Dot(d,fill)-1)*7)*.65f;
                    c+=new Color(.85f,.90f,1)*Mathf.Exp((Vector3.Dot(d,rim)-1)*24)*1.2f;
                    c.a=1;pixels[y*width+x]=c;
                }
                tex.SetPixels(pixels);tex.Apply();File.WriteAllBytes(path,tex.EncodeToEXR());Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            }
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureShape=TextureImporterShape.TextureCube;
            importer.generateCubemap=TextureImporterGenerateCubemap.AutoCubemap;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.cubemapConvolution=TextureImporterCubemapConvolution.Specular;
            importer.SetTextureSettings(settings);
            importer.sRGBTexture=false;importer.mipmapEnabled=true;importer.isReadable=false;
            importer.filterMode=FilterMode.Trilinear;importer.wrapMode=TextureWrapMode.Clamp;
            importer.maxTextureSize=256;importer.textureCompression=TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
            var cube=AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            if(!cube || cube.mipmapCount<6)throw new InvalidOperationException("Unfiltered studio cubemap");
            return cube;
        }
    }
}
