using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SwanJourney.Editor
{
    internal static class SwanXrValleyCloudArt
    {
        const string Folder="Assets/SwanJourney/Branding/ValleyClouds/";
        const string Original="Assets/AtelierJourney/MesaAtmosV16/";
        const int Size=64;

        internal static void Apply(SteamJourney journey,SteamXrTrial trial)
        {
            var atmosphere=journey.GetComponent<MesaAtmosV16>();
            var clouds=atmosphere.cloudTransforms;
            atmosphere.layeredCloudMotion=true;
            if(clouds.Length!=3) throw new InvalidOperationException("Expected the three V16 sky clouds");
            var bake=Shader.Find("Hidden/SwanJourney/BakeValleyCloud");
            var shader=Shader.Find("SwanJourney/XR Valley Cloud");
            if(!bake || !shader || !bake.isSupported || !shader.isSupported)
                throw new InvalidOperationException("Valley cloud shaders unavailable");
            var baker=new Material(bake);
            var cube=Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            trial.cloudControlMeshes=new Mesh[clouds.Length];trial.cloudControlMaterials=new Material[clouds.Length];
            try
            {
                for(int i=0;i<clouds.Length;i++)
                {
                    var cloud=clouds[i];
                    // Fixed authored poses make regeneration idempotent. Main cloud,
                    // distant small cloud, and nearer medium cloud leave open gaps.
                    var positions=new[]{new Vector3(-8.2f,17.8f,-10),new Vector3(2.4f,20.4f,-14.0f),new Vector3(10.8f,16.4f,-8.4f)};
                    var sizes=new[]{new Vector3(11.5f,4.6f,6.0f),new Vector3(4.5f,2.35f,3.4f),new Vector3(7.3f,3.5f,5.0f)};
                    cloud.localPosition=positions[i];cloud.localScale=sizes[i];
                    var original=AssetDatabase.LoadAssetAtPath<Material>(Original+"Cloud "+i+".mat");
                    if(!original || !original.GetTexture("_Noise")) throw new InvalidOperationException("Missing V16 sky density");
                    float seed=original.GetFloat("_Seed");
                    baker.SetTexture("_Noise",original.GetTexture("_Noise"));
                    baker.SetFloat("_Seed",seed);baker.SetFloat("_Phase",seed);baker.SetFloat("_Steam",0);
                    var sunlight=cloud.parent.TransformDirection(new Vector3(-.46f,.87f,.32f));
                    baker.SetVector("_LightOS",cloud.InverseTransformVector(sunlight).normalized);
                    var volume=Bake(baker,i);
                    var path=Folder+"Valley cloud "+i+".mat";
                    var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(!mat){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}
                    mat.SetTexture("_Cloud",volume);mat.SetFloat("_Seed",seed);EditorUtility.SetDirty(mat);
                    Debug.Log($"XR_CLOUD_FINISH index={i} position={cloud.localPosition} size={cloud.localScale} volume={Size} steps=16");
                    var filter=cloud.GetComponent<MeshFilter>();var renderer=cloud.GetComponent<MeshRenderer>();
                    trial.cloudControlMeshes[i]=filter.sharedMesh;trial.cloudControlMaterials[i]=renderer.sharedMaterial;
                    filter.sharedMesh=cube;renderer.sharedMaterial=mat;
                    renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                }
            }
            finally {Object.DestroyImmediate(baker);}
        }

        static Texture3D Bake(Material baker,int index)
        {
            const int side=Size*8;
            var rt=RenderTexture.GetTemporary(side,side,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            var pixels=new Texture2D(side,side,TextureFormat.RGBA32,false,true);
            var previous=RenderTexture.active;
            try
            {
                Graphics.Blit(null,rt,baker);
                RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,side,side),0,0,false);
                var atlas=pixels.GetPixels32();var bytes=new byte[Size*Size*Size*2];
                int next=0,core=0;
                for(int z=0;z<Size;z++) for(int y=0;y<Size;y++) for(int x=0;x<Size;x++)
                {
                    var cell=atlas[(z/8*Size+y)*side+(z%8)*Size+x];
                    bytes[next++]=cell.r;bytes[next++]=cell.g;if(cell.r>128)core++;
                }
                if(core<1000 || core>Size*Size*Size/2) throw new InvalidOperationException("Invalid V16 cloud density "+core);
                string path=Folder+"Valley density "+index+".asset";
                var result=AssetDatabase.LoadAssetAtPath<Texture3D>(path);
                if(!result){result=new Texture3D(Size,Size,Size,TextureFormat.RG16,false);AssetDatabase.CreateAsset(result,path);}
                result.wrapMode=TextureWrapMode.Clamp;result.filterMode=FilterMode.Bilinear;
                result.SetPixelData(bytes,0);result.Apply(false,false);EditorUtility.SetDirty(result);
                return result;
            }
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(pixels);}
        }
    }
}
