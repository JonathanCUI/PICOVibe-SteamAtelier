using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
public static partial class AtelierBuild {
 // Thin shells and their mating trim must share the same original analytic sampling.
 // Independent mesh reduction was moving the two surfaces through each other.
 static bool PrecisionMaterial(string id,string name){
  if(id=="P01")return name=="CastSteel"||name=="WarmBrass";
  if(id=="P02")return name=="DeepEnamel"||name=="BrushedBrass"||name=="MachinedNickel";
  return id=="M02-F"||id=="M02-D"||id=="M02-P"||id=="M05-E"||id=="P17";
 }
 static bool PrimarySurface(Transform t){for(;t;t=t.parent)if(t.name.StartsWith("LOD1")||t.name.StartsWith("LOD2")||t.name.StartsWith("LOD3"))return false;return true;}
 static string RelativePath(Transform t,Transform root){var path=t.name;while(t.parent&&t.parent!=root){t=t.parent;path=t.name+"/"+path;}return path;}
 static Transform MatchOriginal(Transform node,Transform originalRoot,Transform targetRoot){
  if(node==originalRoot)return targetRoot;
  var parent=MatchOriginal(node.parent,originalRoot,targetRoot);if(!parent)return null;
  int occurrence=0;for(int i=0;i<node.GetSiblingIndex();i++)if(node.parent.GetChild(i).name==node.name)occurrence++;
  return parent.Cast<Transform>().Where(t=>t.name==node.name).Skip(occurrence).FirstOrDefault();
 }
 static void RestorePrecisionInterfaces(){
  var sourceScene=EditorSceneManager.OpenScene("Assets/Editor/Atelier/PrecisionSource.unity",OpenSceneMode.Additive);
  var log=new List<string>();try{
   var source=sourceScene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<LocomotiveController>(true)).First();
   foreach(var target in engine.parts){
    var original=source.parts.FirstOrDefault(p=>p.partId==target.partId&&Vector3.Distance(p.home,target.home)<.01f);if(!original)continue;
    var originals=original.GetComponentsInChildren<MeshRenderer>(true).Where(r=>PrimarySurface(r.transform)&&r.sharedMaterial&&PrecisionMaterial(target.partId,r.sharedMaterial.name)&&r.GetComponent<MeshFilter>()&&r.GetComponent<MeshFilter>().sharedMesh).ToArray();if(originals.Length==0)continue;
    foreach(var r in target.GetComponentsInChildren<MeshRenderer>(true))if(r.sharedMaterial&&PrecisionMaterial(target.partId,r.sharedMaterial.name))r.enabled=false;
    long triangles=0;var matched=new HashSet<Transform>();
    foreach(var r in originals){var path=RelativePath(r.transform,original.transform);var tr=MatchOriginal(r.transform,original.transform,target.transform);if(!tr)throw new Exception("Missing precision transform "+target.partId+"/"+path);if(!matched.Add(tr))throw new Exception("Duplicate precision target "+path);
     var filter=tr.GetComponent<MeshFilter>();if(!filter)filter=tr.gameObject.AddComponent<MeshFilter>();filter.sharedMesh=r.GetComponent<MeshFilter>().sharedMesh;
     if(r.name=="火箱烟管孔板"){
      // Flat plate faces must not inherit smoothed normals from the bore walls.
      var mesh=filter.sharedMesh;var v=mesh.vertices;var indices=mesh.triangles;var uv=mesh.uv;var points=new Vector3[indices.Length];var tex=new Vector2[indices.Length];var normals=new Vector3[indices.Length];var faces=new int[indices.Length];
      for(int i=0;i<indices.Length;i+=3){var normal=Vector3.Cross(v[indices[i+1]]-v[indices[i]],v[indices[i+2]]-v[indices[i]]).normalized;for(int j=0;j<3;j++){points[i+j]=v[indices[i+j]];if(uv.Length==v.Length)tex[i+j]=uv[indices[i+j]];normals[i+j]=normal;faces[i+j]=i+j;}}
      var flat=new Mesh{name="Machined firebox tube plate",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};flat.vertices=points;flat.normals=normals;flat.uv=tex;flat.triangles=faces;filter.sharedMesh=SaveMesh(flat,"PrecisionTubePlate");tr.position+=engine.locomotive.TransformVector(Vector3.right*.004f);
     }
     var binding=tr.GetComponent<LocomotiveXRMesh>();if(binding)UnityEngine.Object.DestroyImmediate(binding);
     var visual=tr.GetComponent<MeshRenderer>();if(!visual)visual=tr.gameObject.AddComponent<MeshRenderer>();visual.sharedMaterials=r.sharedMaterials;visual.enabled=r.enabled;visual.shadowCastingMode=r.shadowCastingMode;visual.receiveShadows=r.receiveShadows;
     for(int s=0;s<filter.sharedMesh.subMeshCount;s++)triangles+=filter.sharedMesh.GetIndexCount(s)/3;
    }
    log.Add(target.partId+" "+target.title+": "+originals.Length+" original contact surfaces, "+triangles+" triangles");
   }
  }finally{EditorSceneManager.CloseScene(sourceScene,true);}
  File.WriteAllLines(AtelierEditor.Root+"/Reports/precision-interfaces.txt",log);
 }
}
