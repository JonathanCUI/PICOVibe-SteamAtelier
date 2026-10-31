using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SwanJourney.Editor
{
    // Static, separately owned canyon only. Bake the uniform metre conversion
    // into vertices + local translations and compensate root scale. World pose,
    // normals, topology and support geometry remain unchanged.
    public static class SwanJourneyMiniatureGeometry
    {
        [Serializable] sealed class Report { public bool passed,nativeRenderingValidated; public float bakedScale,maxWorldBoundsError;public int meshes,nodes;public long triangles; }
        public static void Apply(SwanJourneyEnvironment payload,float factor)
        {
            if(Application.isPlaying || factor<=0 || factor>=1)throw new InvalidOperationException("Expected a positive miniature factor in derived Edit Mode scene");
            var root=payload.transform; var renderers=root.GetComponentsInChildren<MeshRenderer>(true);
            if(root.GetComponentsInChildren<Animator>(true).Any()||root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any()
                ||root.GetComponentsInChildren<MonoBehaviour>(true).Any(c=>c && !(c is SwanJourneyEnvironment) && !(c is SwanJourneyTransformSyncBudget) && !(c is FlatTraySourceV22) && !(c is MesaRailInfoV12)))
                throw new InvalidOperationException("Only the static canyon may have its units baked");
            var before=renderers.Select(r=>r.bounds).ToArray();
            var copies=new Dictionary<Mesh,Mesh>();string folder=SwanJourneyBuild.Generated+"/CanyonMetres";
            Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            var filters=root.GetComponentsInChildren<MeshFilter>(true);var report=new Report{bakedScale=factor};
            foreach(var mesh in filters.Where(f=>f.sharedMesh).Select(f=>f.sharedMesh)
                .Concat(root.GetComponentsInChildren<MeshCollider>(true).Where(c=>c.sharedMesh).Select(c=>c.sharedMesh)).Distinct())
            {
                var clone=UnityEngine.Object.Instantiate(mesh);var vertices=clone.vertices;
                for(int i=0;i<vertices.Length;i++)vertices[i]*=factor;
                clone.vertices=vertices;clone.RecalculateBounds();
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh,out string guid,out long id);
                var path=folder+"/"+guid+"_"+id+".asset";
                var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(saved){EditorUtility.CopySerialized(clone,saved);UnityEngine.Object.DestroyImmediate(clone);EditorUtility.SetDirty(saved);}
                else {saved=clone;AssetDatabase.CreateAsset(saved,path);}
                copies.Add(mesh,saved);report.meshes++;
            }
            foreach(var f in filters.Where(f=>f.sharedMesh))
            {
                f.sharedMesh=copies[f.sharedMesh];
                for(int i=0;i<f.sharedMesh.subMeshCount;i++)report.triangles+=f.sharedMesh.GetIndexCount(i)/3;
            }
            foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t!=root){t.localPosition*=factor;report.nodes++;}
            foreach(var c in root.GetComponentsInChildren<Collider>(true))
            {
                if(c is MeshCollider mc && mc.sharedMesh)mc.sharedMesh=copies[mc.sharedMesh];
                else if(c is BoxCollider box){box.center*=factor;box.size*=factor;}
                else if(c is SphereCollider sphere){sphere.center*=factor;sphere.radius*=factor;}
                else if(c is CapsuleCollider capsule){capsule.center*=factor;capsule.height*=factor;capsule.radius*=factor;}
            }
            root.localScale/=factor;payload.bakedGeometryScale=factor;
            for(int i=0;i<renderers.Length;i++)
            {
                var b=renderers[i].bounds;float error=Mathf.Max((b.min-before[i].min).magnitude,(b.max-before[i].max).magnitude);
                report.maxWorldBoundsError=Mathf.Max(report.maxWorldBoundsError,error);
            }
            if(report.maxWorldBoundsError>.0001f)throw new InvalidOperationException("Miniature bake changed world geometry: "+report.maxWorldBoundsError);
            AssetDatabase.SaveAssets();report.passed=true;
            File.WriteAllText(SwanJourneyBuild.Output+"/canyon-metres.json",JsonUtility.ToJson(report,true));
            Debug.Log("SWAN_JOURNEY_CANYON_METRES factor="+factor+" meshes="+report.meshes+" boundsError="+report.maxWorldBoundsError+" nativeScale=unverified");
        }
    }
}
