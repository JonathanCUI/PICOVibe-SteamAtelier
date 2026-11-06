using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SwanJourney.Editor
{
    internal static class SwanDisplayShadowBuild
    {
        internal static void Apply(SteamJourney journey)
        {
            var previous=journey.GetComponent<SwanDisplayShadows>();
            if(previous)
            {
                var old=(previous.workshopCasters??Array.Empty<Renderer>()).Concat(previous.valleyCasters??Array.Empty<Renderer>()).Concat(previous.inspectionCasters??Array.Empty<Renderer>()).Where(r=>r).ToArray();
                foreach(var r in old)UnityEngine.Object.DestroyImmediate(r.gameObject);
                UnityEngine.Object.DestroyImmediate(previous);
            }
            var source=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/SwanJourneyURP.asset");
            const string path="Assets/SwanJourney/Branding/VisualIntegration/Display lighting pipeline.asset";
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if(!pipeline){pipeline=UnityEngine.Object.Instantiate(source);AssetDatabase.CreateAsset(pipeline,path);}
            else EditorUtility.CopySerialized(source,pipeline);
            // This XR-only derived pipeline keeps the shared source and old build paths intact.
            var so=new SerializedObject(pipeline);
            so.FindProperty("m_MainLightShadowsSupported").boolValue=true;
            so.FindProperty("m_MainLightShadowmapResolution").intValue=2048;
            so.FindProperty("m_AdditionalLightShadowsSupported").boolValue=false;
            so.FindProperty("m_SoftShadowsSupported").boolValue=true;
            so.FindProperty("m_SoftShadowQuality").intValue=2;
            so.FindProperty("m_ShadowCascadeCount").intValue=1;
            so.FindProperty("m_ShadowDistance").floatValue=3;
            so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(pipeline);
            GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
            var effect=journey.gameObject.AddComponent<SwanDisplayShadows>();effect.journey=journey;
            effect.mood=UnityEngine.Object.FindObjectOfType<StudioWorkshopMood>(true);
            foreach(var r in UnityEngine.Object.FindObjectsOfType<Renderer>(true))r.shadowCastingMode=ShadowCastingMode.Off;
            bool Eligible(MeshRenderer r)=>r.GetComponent<MeshFilter>() && r.sharedMaterials.Length>0 &&
                r.sharedMaterials.All(m=>m && m.renderQueue<2450 &&
                    (m.shader.name=="Universal Render Pipeline/Lit" || m.shader.name=="SwanJourney/XR Valley Surface" || m.shader.name=="SwanJourney/XR Structure Surface"));
            foreach(var r in journey.engine.locomotive.GetComponentsInChildren<MeshRenderer>(true).Where(Eligible))r.receiveShadows=true;
            var materialPath="Assets/SwanJourney/Branding/VisualIntegration/Display shadow only.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,materialPath);}
            material.SetFloat("_Cull",0);EditorUtility.SetDirty(material);
            var valley=new List<Renderer>();var workshop=new List<Renderer>();
            for(int i=0;i<journey.valleyTrain.vehicles.Length;i++)
            {
                var root=journey.valleyTrain.vehicles[i];
                var renderers=root.GetComponentsInChildren<MeshRenderer>(true).Where(Eligible).ToArray();
                foreach(var r in renderers)r.receiveShadows=true;
                var combines=new List<CombineInstance>();
                foreach(var r in renderers)
                {
                    var mesh=r.GetComponent<MeshFilter>().sharedMesh;
                    for(int s=0;s<mesh.subMeshCount;s++)combines.Add(new CombineInstance{mesh=mesh,subMeshIndex=s,transform=root.worldToLocalMatrix*r.localToWorldMatrix});
                }
                var combined=new Mesh{indexFormat=IndexFormat.UInt32};combined.CombineMeshes(combines.ToArray(),true,true);
                var simplified=ShadowSilhouette(combined,160);int originalTriangles=combined.triangles.Length/3;
                UnityEngine.Object.DestroyImmediate(combined);
                var meshPath=$"Assets/SwanJourney/Branding/VisualIntegration/Vehicle shadow {i}.asset";
                var saved=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if(saved){EditorUtility.CopySerialized(simplified,saved);UnityEngine.Object.DestroyImmediate(simplified);simplified=saved;}
                else AssetDatabase.CreateAsset(simplified,meshPath);
                MeshRenderer Proxy(Transform parent)
                {
                    var go=new GameObject("Lightweight display shadow",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
                    go.GetComponent<MeshFilter>().sharedMesh=simplified;var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;
                    r.shadowCastingMode=ShadowCastingMode.ShadowsOnly;r.receiveShadows=false;r.enabled=false;
                    r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;return r;
                }
                valley.Add(Proxy(root));
                Debug.Log($"DISPLAY_SHADOW_PROXY vehicle={i} originalTriangles={originalTriangles} triangles={simplified.triangles.Length/3} maxVertexShift={simplified.bounds.size.magnitude/160:F4}");
            }
            effect.valleyCasters=valley.ToArray();
            var inspection=new List<Renderer>();
            var allRenderers=UnityEngine.Object.FindObjectsOfType<Renderer>(true);var enabled=allRenderers.Select(r=>r.enabled).ToArray();
            var objects=UnityEngine.Object.FindObjectsOfType<Transform>(true).Select(t=>t.gameObject).ToArray();var active=objects.Select(o=>o.activeSelf).ToArray();
            var parts=journey.modules.Concat(journey.finishing).ToArray();var installed=parts.Select(p=>p.installed).ToArray();
            int oldMode=journey.inspectionMode;bool oldCut=journey.cutaway;
            try
            {
                foreach(var p in parts)p.installed=true;
                for(int mode=0;mode<=2;mode++)
                {
                    journey.cutaway=mode!=0;journey.inspectionMode=mode;journey.ApplyVisibility();
                    var root=journey.engine.locomotive;
                    var candidates=root.GetComponentsInChildren<MeshRenderer>(true).Concat(journey.cutRoot.GetComponentsInChildren<MeshRenderer>(true))
                        .Concat(journey.lessonModels.SelectMany(o=>o.GetComponentsInChildren<MeshRenderer>(true))).Distinct()
                        .Where(r=>r.enabled && r.gameObject.activeInHierarchy && r.shadowCastingMode!=ShadowCastingMode.ShadowsOnly && Eligible(r)).ToArray();
                    var combines=new List<CombineInstance>();
                    foreach(var r in candidates)
                    {
                        var mesh=r.GetComponent<MeshFilter>().sharedMesh;
                        for(int s=0;s<mesh.subMeshCount;s++)combines.Add(new CombineInstance{mesh=mesh,subMeshIndex=s,transform=root.worldToLocalMatrix*r.localToWorldMatrix});
                    }
                    var combined=new Mesh{indexFormat=IndexFormat.UInt32};combined.CombineMeshes(combines.ToArray(),true,true);
                    var simplified=ComponentSilhouette(combined);UnityEngine.Object.DestroyImmediate(combined);
                    string meshPath=mode==0?"Assets/SwanJourney/Branding/VisualIntegration/Workshop shadow.asset":$"Assets/SwanJourney/Branding/VisualIntegration/Inspection shadow {mode}.asset";
                    var saved=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if(saved){EditorUtility.CopySerialized(simplified,saved);UnityEngine.Object.DestroyImmediate(simplified);simplified=saved;}else AssetDatabase.CreateAsset(simplified,meshPath);
                    var go=new GameObject("Inspection shadow "+mode,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);
                    go.GetComponent<MeshFilter>().sharedMesh=simplified;var caster=go.GetComponent<MeshRenderer>();caster.sharedMaterial=material;
                    caster.shadowCastingMode=ShadowCastingMode.ShadowsOnly;caster.receiveShadows=false;caster.enabled=false;
                    caster.lightProbeUsage=LightProbeUsage.Off;caster.reflectionProbeUsage=ReflectionProbeUsage.Off;
                    if(mode==0)workshop.Add(caster);else inspection.Add(caster);
                    Debug.Log($"DISPLAY_INSPECTION_SHADOW mode={mode} sources={candidates.Length} triangles={simplified.triangles.Length/3}");
                }
            }
            finally
            {
                for(int i=0;i<parts.Length;i++)parts[i].installed=installed[i];journey.inspectionMode=oldMode;journey.cutaway=oldCut;
                for(int i=0;i<objects.Length;i++)if(objects[i])objects[i].SetActive(active[i]);
                for(int i=0;i<allRenderers.Length;i++)if(allRenderers[i])allRenderers[i].enabled=enabled[i];
            }
            effect.inspectionCasters=inspection.ToArray();
            effect.workshopCasters=workshop.ToArray();
            if(effect.workshopCasters.Length==0 || effect.valleyCasters.Length==0)throw new Exception("Missing display shadow casters");
            Debug.Log($"DISPLAY_SHADOW_BUILD workshopCandidates={effect.workshopCasters.Length} valleyCandidates={effect.valleyCasters.Length} inspectionCandidates={inspection.Count} maps=1 resolution=2048 filtered=true assemblyShadows=off");
        }
        // The close-up locomotive must not reuse the hollow, distant valley mesh.
        // Weld disconnected triangles for connectivity, then cluster each physical part
        // independently: a long handrail gets a cell sized to its thickness, not train length.
        static Mesh ComponentSilhouette(Mesh source)
        {
            var v=source.vertices;var tri=source.triangles;var parent=Enumerable.Range(0,v.Length).ToArray();
            int Find(int a){while(parent[a]!=a){parent[a]=parent[parent[a]];a=parent[a];}return a;}
            void Join(int a,int b){a=Find(a);b=Find(b);if(a!=b)parent[b]=a;}
            var weld=new Dictionary<Vector3Int,int>();
            for(int i=0;i<v.Length;i++)
            {
                var p=v[i]*100000;var key=new Vector3Int(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.y),Mathf.RoundToInt(p.z));
                if(weld.TryGetValue(key,out int first))Join(first,i);else weld[key]=i;
            }
            for(int i=0;i<tri.Length;i+=3){Join(tri[i],tri[i+1]);Join(tri[i],tri[i+2]);}
            var bounds=new Dictionary<int,Bounds>();
            for(int i=0;i<v.Length;i++)
            {
                int group=Find(i);parent[i]=group;
                if(bounds.TryGetValue(group,out var b)){b.Encapsulate(v[i]);bounds[group]=b;}else bounds[group]=new Bounds(v[i],Vector3.zero);
            }
            float coarseCell=Mathf.Max(source.bounds.size.x,Mathf.Max(source.bounds.size.y,source.bounds.size.z))/80;
            var cells=bounds.ToDictionary(x=>x.Key,x=>
            {
                var s=x.Value.size;float longest=Mathf.Max(s.x,Mathf.Max(s.y,s.z));
                var nonzero=new[]{s.x,s.y,s.z}.Where(t=>t>longest*.001f).ToArray();
                float thin=nonzero.DefaultIfEmpty(.001f).Min();
                // Preserve extended rails, pipes and step plates, not thousands of tiny
                // rivets which cannot resolve in the shadow atlas at this viewing scale.
                bool structural=longest>.45f && thin<.16f && longest>thin*6;
                return structural?Mathf.Max(.0001f,Mathf.Min(coarseCell,thin/2)):coarseCell;
            });
            var lookup=new Dictionary<(int,Vector3Int),int>();var sums=new List<Vector3>();var counts=new List<int>();var remap=new int[v.Length];
            for(int i=0;i<v.Length;i++)
            {
                int group=parent[i];var p=(v[i]-bounds[group].min)/cells[group];
                var key=(group,new Vector3Int(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.y),Mathf.RoundToInt(p.z)));
                if(!lookup.TryGetValue(key,out int n)){n=sums.Count;lookup[key]=n;sums.Add(Vector3.zero);counts.Add(0);}
                sums[n]+=v[i];counts[n]++;remap[i]=n;
            }
            for(int i=0;i<sums.Count;i++)sums[i]/=counts[i];
            var seen=new HashSet<(int,int,int)>();var indices=new List<int>();
            for(int i=0;i<tri.Length;i+=3)
            {
                int a=remap[tri[i]],b=remap[tri[i+1]],c=remap[tri[i+2]];if(a==b||b==c||c==a)continue;
                int lo=Math.Min(a,Math.Min(b,c)),hi=Math.Max(a,Math.Max(b,c));if(!seen.Add((lo,a+b+c-lo-hi,hi)))continue;
                indices.Add(a);indices.Add(b);indices.Add(c);
            }
            var result=new Mesh{indexFormat=IndexFormat.UInt32};result.SetVertices(sums);result.SetTriangles(indices,0);result.RecalculateNormals();result.RecalculateBounds();
            Debug.Log($"DISPLAY_COMPONENT_SHADOW parts={bounds.Count} before={tri.Length/3} after={indices.Count/3}");
            if(indices.Count/3>110000)throw new Exception("Workshop shadow exceeds its triangle budget");
            if(indices.Count==0)throw new Exception("Empty workshop shadow");return result;
        }
        // Offline vertex clustering is only used by invisible shadow silhouettes. The visible
        // model, UVs, animation, colliders and materials are never modified by this reduction.
        static Mesh ShadowSilhouette(Mesh source,int divisions)
        {
            var v=source.vertices;float cell=Mathf.Max(source.bounds.size.x,source.bounds.size.y,source.bounds.size.z)/divisions;
            var map=new Dictionary<Vector3Int,int>();var sums=new List<Vector3>();var counts=new List<int>();var remap=new int[v.Length];
            for(int i=0;i<v.Length;i++)
            {
                var p=v[i]/cell;var key=new Vector3Int(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.y),Mathf.RoundToInt(p.z));
                if(!map.TryGetValue(key,out int n)){n=sums.Count;map.Add(key,n);sums.Add(Vector3.zero);counts.Add(0);}
                sums[n]+=v[i];counts[n]++;remap[i]=n;
            }
            for(int i=0;i<sums.Count;i++)sums[i]/=counts[i];
            var faces=new HashSet<(int,int,int)>();var indices=new List<int>();var tri=source.triangles;
            for(int i=0;i<tri.Length;i+=3)
            {
                int a=remap[tri[i]],b=remap[tri[i+1]],c=remap[tri[i+2]];
                if(a==b || b==c || c==a)continue;
                int lo=Math.Min(a,Math.Min(b,c)),hi=Math.Max(a,Math.Max(b,c));
                if(!faces.Add((lo,a+b+c-lo-hi,hi)))continue;
                indices.AddRange(new[]{a,b,c});
            }
            var result=new Mesh{indexFormat=IndexFormat.UInt32};result.SetVertices(sums);result.SetTriangles(indices,0);result.RecalculateNormals();result.RecalculateBounds();
            if(result.triangles.Length==0 || Vector3.Distance(source.bounds.center,result.bounds.center)>cell*2)throw new Exception("Invalid shadow silhouette");
            return result;
        }
    }
}
