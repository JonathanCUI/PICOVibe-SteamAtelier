using System;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEngine.Rendering;
[Serializable] public sealed class JourneyArrivalEdges {public Mesh source,edges;}
[DefaultExecutionOrder(2200)] public sealed class JourneySceneArrival:MonoBehaviour {
 public SteamJourney owner;public Shader solidShader,ghostShader;public JourneyArrivalEdges[] contours;
 public Action nativeBegin,nativeCancel;public Action<float> nativeProgress;bool nativeActive;
 public bool Active=>pending||nativeActive||entries.Count>0;
 public float Progress {get;private set;}
 public float duration=1.8f;
 public bool manual;
 sealed class Entry {public Renderer r;public Material[] original,reveal;public bool enabled,lit;public ShadowCastingMode shadows;public float centre;}
 readonly List<Entry> entries=new List<Entry>();readonly Dictionary<Material,Material> materials=new Dictionary<Material,Material>();
 readonly List<UnityEngine.Object> temporary=new List<UnityEngine.Object>();GameObject visual;bool pending;float elapsed,minimum,span,gridSpan;
 public void Request(){Cancel();pending=true;elapsed=Progress=0;}
 void LateUpdate() { using (SwanJourney.SwanJourneyCpuTimings.Measure(SwanJourney.SwanJourneyCpuTimings.Stage.Arrival)) CpuMeasuredLateUpdate(); }
    void CpuMeasuredLateUpdate(){bool beginning=pending;if(pending){pending=false;Begin();}if(entries.Count==0&&!nativeActive)return;if(!manual){if(!beginning)elapsed+=Time.unscaledDeltaTime;SetProgress(Mathf.Clamp01(elapsed/duration));if(Progress>=1)Cancel();}}
 public void SetProgress(float value){Progress=Mathf.Clamp01(value);if(nativeActive){nativeProgress?.Invoke(Progress);return;}Shader.SetGlobalMatrix("_ArrivalWorldToLocal",owner?owner.SpatialWorldToLocal:Matrix4x4.identity);float sweep=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.06f,.94f,Progress));Shader.SetGlobalVector("_ArrivalRange",new Vector4(minimum,span,sweep,gridSpan));
  foreach(var e in entries)if(e.r){if(e.lit){e.r.enabled=e.enabled;e.r.sharedMaterials=e.reveal;}else e.r.enabled=e.enabled&&sweep>e.centre+.025f;}}
 void Begin(){if(nativeBegin!=null){nativeActive=true;nativeBegin();return;}if(!solidShader||!ghostShader||!owner)return;
  var roots=owner.inSandbox?new[]{owner.sandboxRoot}:new[]{owner.workshopRoot,owner.previewRoot};
  var renderers=roots.Where(o=>o).SelectMany(o=>o.GetComponentsInChildren<Renderer>()).Distinct().Where(r=>r is MeshRenderer&&r.enabled&&r.gameObject.activeInHierarchy&&!r.GetComponentInParent<TMPro.TMP_Text>()&&r.sharedMaterials.All(m=>m&&m.renderQueue<2500)).ToArray();
  var bounds=new Bounds();bool first=true;var triangles=new List<CombineInstance>();var lines=new List<CombineInstance>();var gpuGhosts=new List<Renderer>();var lookup=new Dictionary<Mesh,Mesh>();foreach(var c in contours??Array.Empty<JourneyArrivalEdges>())if(c.source&&!lookup.ContainsKey(c.source))lookup[c.source]=c.edges;
  foreach(var r in renderers){var mf=r.GetComponent<MeshFilter>();var mats=r.sharedMaterials;bool isLit=mf&&mf.sharedMesh&&mats.Length>0&&mats.All(m=>m&&m.renderQueue<2500);
   var e=new Entry{r=r,original=mats,enabled=r.enabled,lit=isLit,shadows=r.shadowCastingMode};entries.Add(e);if(!isLit)continue;
   var localBounds=SceneBounds(r);if(first){bounds=localBounds;first=false;}else bounds.Encapsulate(localBounds);
   bool native=mats.All(m=>m.shader.name=="SteamJourney/StudioMechanical"||m.shader.name=="SteamJourney/MesaGroundV14"||m.shader.name=="SteamJourney/V20LayeredRockAmbient");
   foreach(var m in mats)if(!native&&!materials.ContainsKey(m)){var copy=new Material(m){shader=solidShader,name=m.name+" · arrival"};copy.shaderKeywords=Array.Empty<string>();copy.SetFloat("_HasNormal",m.IsKeywordEnabled("_NORMALMAP")?1:0);copy.SetFloat("_HasMetalMap",m.IsKeywordEnabled("_METALLICSPECGLOSSMAP")?1:0);copy.SetFloat("_HasOcclusion",m.IsKeywordEnabled("_OCCLUSIONMAP")?1:0);copy.SetFloat("_HasEmission",m.IsKeywordEnabled("_EMISSION")?1:0);if(!m.HasProperty("_BaseMap")&&m.HasProperty("_MainTex")){copy.SetTexture("_BaseMap",m.GetTexture("_MainTex"));copy.SetTextureScale("_BaseMap",m.GetTextureScale("_MainTex"));copy.SetColor("_BaseColor",m.HasProperty("_Color")?m.GetColor("_Color"):Color.white);copy.SetFloat("_Metallic",0);copy.SetFloat("_Smoothness",.18f);copy.SetFloat("_HasEmission",0);}materials.Add(m,copy);}
   e.reveal=native?mats:mats.Select(m=>materials[m]).ToArray();
   if(mf.sharedMesh.isReadable){
    for(int s=0;s<mf.sharedMesh.subMeshCount;s++)if(mf.sharedMesh.GetTopology(s)==MeshTopology.Triangles)triangles.Add(new CombineInstance{mesh=mf.sharedMesh,subMeshIndex=s,transform=owner.SpatialWorldToLocal*r.localToWorldMatrix});
   }else gpuGhosts.Add(r);
   if(lookup.TryGetValue(mf.sharedMesh,out var edge)&&edge&&edge.vertexCount>0)lines.Add(new CombineInstance{mesh=edge,transform=owner.SpatialWorldToLocal*r.localToWorldMatrix});
  }
  if(first){Cancel();return;}
  // Scan in authored scene coordinates so moving, rotating or scaling the common root is safe.
  minimum=bounds.min.y-.05f;span=Mathf.Max(.1f,bounds.size.y+.10f);gridSpan=Mathf.Max(.1f,bounds.size.x+.10f);foreach(var e in entries)e.centre=(owner.WorldPointToScene(e.r.bounds.center).y-minimum)/span;
  visual=new GameObject("Temporary scene materialisation");if(owner.spatialRoot)visual.transform.SetParent(owner.spatialRoot,false);MakeGhost("Blue structural shell",triangles,false);MakeGhost("Blue engineering contours",lines,true);MakeGpuGhosts(gpuGhosts);Shader.SetGlobalFloat("_ArrivalNative21",1);SetProgress(0);
 }
 Bounds SceneBounds(Renderer renderer){
  var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;var b=mesh.bounds;var matrix=owner.SpatialWorldToLocal*renderer.localToWorldMatrix;var result=new Bounds(matrix.MultiplyPoint3x4(b.center),Vector3.zero);
  for(int k=0;k<8;k++)result.Encapsulate(matrix.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1))));return result;
 }
 // GPU-only imported meshes can be rendered directly without Read/Write copies.
 void MakeGpuGhosts(List<Renderer> sources){if(sources.Count==0)return;var material=new Material(ghostShader);material.SetFloat("_Wire",0);temporary.Add(material);
  foreach(var source in sources){var go=new GameObject("Shared GPU arrival shell",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(visual.transform,false);go.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);var parentScale=visual.transform.lossyScale;go.transform.localScale=Vector3.Scale(source.transform.lossyScale,new Vector3(1/parentScale.x,1/parentScale.y,1/parentScale.z));
   var mesh=source.GetComponent<MeshFilter>().sharedMesh;go.GetComponent<MeshFilter>().sharedMesh=mesh;var r=go.GetComponent<MeshRenderer>();r.sharedMaterials=Enumerable.Repeat(material,mesh.subMeshCount).ToArray();r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
  }
 }
 void MakeGhost(string name,List<CombineInstance> pieces,bool wire){if(pieces.Count==0)return;var mesh=new Mesh{indexFormat=IndexFormat.UInt32,name=name};
  if(wire){var vertices=new List<Vector3>();var indices=new List<int>();foreach(var piece in pieces){int offset=vertices.Count;foreach(var p in piece.mesh.vertices)vertices.Add(piece.transform.MultiplyPoint3x4(p));foreach(int index in piece.mesh.GetIndices(0))indices.Add(offset+index);}mesh.SetVertices(vertices);mesh.SetIndices(indices,MeshTopology.Lines,0);mesh.RecalculateBounds();}
  else mesh.CombineMeshes(pieces.ToArray(),true,true);temporary.Add(mesh);
  var material=new Material(ghostShader);material.SetFloat("_Wire",wire?1:0);temporary.Add(material);var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(visual.transform,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
 }
 public void Cancel(){if(nativeActive){nativeActive=false;nativeCancel?.Invoke();}pending=false;Shader.SetGlobalFloat("_ArrivalNative21",0);foreach(var e in entries)if(e.r){e.r.sharedMaterials=e.original;e.r.enabled=e.enabled;e.r.shadowCastingMode=e.shadows;}entries.Clear();if(visual){visual.SetActive(false);Destroy(visual);}visual=null;
  foreach(var m in materials.Values)Destroy(m);materials.Clear();foreach(var item in temporary)Destroy(item);temporary.Clear();Shader.SetGlobalVector("_ArrivalRange",new Vector4(0,1,2,0));}
 void OnDisable(){Cancel();}void OnDestroy(){Cancel();}
}
