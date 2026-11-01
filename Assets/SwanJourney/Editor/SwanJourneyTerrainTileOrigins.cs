using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace SwanJourney.Editor
{
    internal static class SwanJourneyTerrainTileOrigins
    {
        // The original terrain tiles share a pivot at one corner of the landscape.
        // Give each native drawable a local origin inside its own geometry. Offset
        // the node by the same amount so its world surface and collision still agree.
        internal static int Apply(SwanJourneyEnvironment payload)
        {
            const string folder="Assets/SwanJourney/Generated/CanyonTerrainTiles";
            var group=payload.GetComponentsInChildren<Transform>(true).Single(t=>t.name.StartsWith("Continuous terrain V14",StringComparison.Ordinal));
            var tiles=group.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled && r.sharedMaterial && r.sharedMaterial.name=="Swan V23 Continuous sand").ToArray();
            if(tiles.Length==0)throw new InvalidOperationException("Missing canyon terrain");
            Directory.CreateDirectory(folder);if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.Refresh();
            foreach(var renderer in tiles)
            {
                var node=renderer.transform;var filter=node.GetComponent<MeshFilter>();var original=filter.sharedMesh;
                if(AssetDatabase.GetAssetPath(original).StartsWith(folder+"/",StringComparison.Ordinal))continue;
                var collider=node.GetComponent<MeshCollider>();
                if(node.childCount!=0 || (collider && collider.sharedMesh!=original) || original.subMeshCount!=1)
                    throw new InvalidOperationException("Unexpected terrain leaf or collider: "+node.name);
                var center=original.bounds.center;var positions=original.vertices;var oldMatrix=node.localToWorldMatrix;
                string path=folder+"/"+AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(original))+".asset";
                var copy=Object.Instantiate(original);copy.name=original.name+" · tile origin";
                for(int i=0;i<positions.Length;i++)positions[i]-=center;
                copy.vertices=positions;copy.RecalculateBounds();
                var part=copy.GetSubMesh(0);part.bounds=copy.bounds;copy.SetSubMesh(0,part,MeshUpdateFlags.DontRecalculateBounds);
                var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(saved){EditorUtility.CopySerialized(copy,saved);Object.DestroyImmediate(copy);EditorUtility.SetDirty(saved);}
                else{saved=copy;AssetDatabase.CreateAsset(saved,path);}
                node.localPosition+=node.localRotation*Vector3.Scale(node.localScale,center);filter.sharedMesh=saved;
                if(collider)collider.sharedMesh=saved;
                for(int i=0;i<positions.Length;i++)
                    if((node.TransformPoint(positions[i])-oldMatrix.MultiplyPoint3x4(positions[i]+center)).sqrMagnitude>1e-10f)
                        throw new InvalidOperationException("Terrain surface moved: "+node.name);
                EditorUtility.SetDirty(filter);EditorUtility.SetDirty(node);if(collider)EditorUtility.SetDirty(collider);
            }
            AssetDatabase.SaveAssets();return tiles.Length;
        }
    }
}
