using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SwanAtelier.Editor
{
    public static class SwanFpsMeshBuilder
    {
        // Compact texture-free 5x7 glyphs. A glyph is a mesh, not a rectangular plate.
        static readonly Dictionary<char,string[]> Font = new Dictionary<char,string[]> {
            ['0']=new[]{"01110","10001","10011","10101","11001","10001","01110"},
            ['1']=new[]{"00100","01100","00100","00100","00100","00100","01110"},
            ['2']=new[]{"01110","10001","00001","00010","00100","01000","11111"},
            ['3']=new[]{"11110","00001","00001","01110","00001","00001","11110"},
            ['4']=new[]{"00010","00110","01010","10010","11111","00010","00010"},
            ['5']=new[]{"11111","10000","10000","11110","00001","00001","11110"},
            ['6']=new[]{"01110","10000","10000","11110","10001","10001","01110"},
            ['7']=new[]{"11111","00001","00010","00100","01000","01000","01000"},
            ['8']=new[]{"01110","10001","10001","01110","10001","10001","01110"},
            ['9']=new[]{"01110","10001","10001","01111","00001","00001","01110"},
            ['A']=new[]{"01110","10001","10001","11111","10001","10001","10001"},
            ['P']=new[]{"11110","10001","10001","11110","10000","10000","10000"},
            ['F']=new[]{"11111","10000","10000","11110","10000","10000","10000"},
            ['S']=new[]{"01111","10000","10000","01110","00001","00001","11110"},
            ['-']=new[]{"00000","00000","00000","11111","00000","00000","00000"}
        };
        const float Pixel=.0032f, Advance=Pixel*6;

        public static SwanFpsMeshText Create(string assetFolder,Material material)
        {
            var root=new GameObject("FPS visible mesh text");
            var text=root.AddComponent<SwanFpsMeshText>();
            var fixedVertices=new List<Vector3>();var fixedIndices=new List<int>();
            string fixedText="APP     FPS";
            for(int i=0;i<fixedText.Length;i++)
                if(fixedText[i]!=' ')AddGlyph(fixedText[i],new Vector3((i-5)*Advance,0,0),fixedVertices,fixedIndices);
            Render(root,SaveMesh(assetFolder+"/FpsFixed.asset",fixedVertices,fixedIndices),material);
            for(int digit=0;digit<11;digit++) {
                var vertices=new List<Vector3>();var indices=new List<int>();
                AddGlyph(digit==10?'-':(char)('0'+digit),Vector3.zero,vertices,indices);
                var mesh=SaveMesh(assetFolder+"/FpsGlyph"+digit+".asset",vertices,indices);
                for(int slot=0;slot<3;slot++) {
                    var go=new GameObject("FPS digit "+slot+" value "+digit);
                    go.transform.SetParent(root.transform,false);
                    go.transform.localPosition=new Vector3((slot-1)*Advance,0,0);
                    Render(go,mesh,material);text.variants[slot*11+digit]=go;
                }
            }
            text.Show(null);return text;
        }
        static void Render(GameObject go,Mesh mesh,Material material)
        {
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
        }
        static Mesh SaveMesh(string path,List<Vector3> vertices,List<int> indices)
        {
            var mesh=new Mesh{name=System.IO.Path.GetFileNameWithoutExtension(path)};
            mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);return existing;}
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static void AddGlyph(char value,Vector3 offset,List<Vector3> vertices,List<int> indices)
        {
            var rows=Font[value];
            for(int row=0;row<7;row++)for(int x=0;x<5;x++) {
                if(rows[row][x]!='1')continue;
                int end=x+1;while(end<5&&rows[row][end]=='1')end++;
                float left=(x-2.5f)*Pixel,right=(end-2.5f)*Pixel,top=(3.5f-row)*Pixel,bottom=top-Pixel;
                int k=vertices.Count;
                vertices.Add(offset+new Vector3(left,bottom,0));vertices.Add(offset+new Vector3(left,top,0));
                vertices.Add(offset+new Vector3(right,top,0));vertices.Add(offset+new Vector3(right,bottom,0));
                // Face the viewer, toward local -Z.
                indices.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});x=end-1;
            }
        }
    }
}
