using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SwanJourney.Editor
{
    public static class SwanJourneyBaselineChecks
    {
        [Serializable] sealed class Report { public bool passed,nativePerformanceValidated; public string[] checks; }
        public static void Run()
        {
            var checks=new List<string>();
            checks.AddRange(SwanJourneyGhostMaterialChecks.Run());
            var scene=EditorSceneManager.OpenScene(SwanJourneyBuild.ScenePath);
            var roots=scene.GetRootGameObjects();
            var journeyScene=UnityEngine.Object.FindObjectOfType<SteamJourney>(true);
            Check(journeyScene.shellRenderers.All(r=>r)&&journeyScene.shellRenderers.Distinct().Count()==journeyScene.shellRenderers.Length,
                "Shell visibility bindings survive batching without null or duplicate renderers");
            Check(journeyScene.detailMeshes.All(d=>d!=null&&d.filter&&d.inspection&&d.miniature)&&
                journeyScene.detailMeshes.Select(d=>d.filter).Distinct().Count()==journeyScene.detailMeshes.Length,
                "Every retained and merged detail has independent, complete workshop and canyon mesh bindings");
            foreach(var detail in journeyScene.detailMeshes.Where(d=>AssetDatabase.GetAssetPath(d.inspection).Contains("/RigidBatches/")))
                Check(AssetDatabase.GetAssetPath(detail.miniature).EndsWith("_Miniature.asset"),
                    "Merged detail retains its authored miniature level: "+detail.filter.name);
            int originalMode=journeyScene.inspectionMode;bool originalCutaway=journeyScene.cutaway;
            for(int mode=0;mode<3;mode++)
            {
                journeyScene.inspectionMode=mode;journeyScene.cutaway=mode!=0;journeyScene.ApplyVisibility();
                Check(journeyScene.shellRenderers.All(r=>r.enabled==(mode==0)),
                    "Actual visibility logic controls every original/merged exterior in inspection mode "+mode);
            }
            journeyScene.inspectionMode=originalMode;journeyScene.cutaway=originalCutaway;journeyScene.ApplyVisibility();
            var pruning=JsonUtility.FromJson<SwanJourneyEmptyNodeBudget.Report>(File.ReadAllText(SwanJourneyBuild.Output+"/empty-node-budget.json"));
            Check(pruning.removed>0&&pruning.nodesBefore-pruning.nodesAfter==pruning.removed&&pruning.geometryUnchanged,"Pruning removes only accounted empty nodes, preserving every mesh and world matrix");
            Check(roots.SelectMany(r=>r.GetComponentsInChildren<MeshFilter>(true)).All(f=>!f.sharedMesh||
                !(AssetDatabase.GetAssetPath(f.sharedMesh).Contains("/WorkshopBudget/")&&
                (AssetDatabase.GetAssetPath(f.sharedMesh).EndsWith("_Surface.asset")||AssetDatabase.GetAssetPath(f.sharedMesh).EndsWith("_Precision.asset")))),
                "Body surfaces and small mechanical details never use the destructive blanket decimation");
            var configured=UnityEngine.Object.FindObjectOfType<SwanJourneyPerformanceProbe>(true);
            Check(configured&&configured.ActiveProfile=="normal"&&configured.surfaces.Length>0,"Diagnostic modes are available but normal graphics remain the default");
            foreach(var pair in configured.surfaces)
                Check(pair.original&&pair.simple&&pair.original!=pair.simple&&pair.simple.GetTexturePropertyNames().All(p=>!pair.simple.GetTexture(p)),
                    "Diagnostic surface is an independent scalar-only material: "+pair.original.name);
            var root=new GameObject("Baseline fixture");
            Material material=null,simple=null;
            try
            {
                var empty=new GameObject("Unreferenced empty").transform;empty.SetParent(root.transform);
                var refs=new HashSet<UnityEngine.Object>();
                Check(SwanJourneyEmptyNodeBudget.CanRemove(empty,refs),"A genuine unreferenced empty leaf is removable");
                refs.Add(empty);Check(!SwanJourneyEmptyNodeBudget.CanRemove(empty,refs),"Serialized Transform references preserve empty anchors");
                refs.Clear();refs.Add(empty.gameObject);Check(!SwanJourneyEmptyNodeBudget.CanRemove(empty,refs),"Serialized GameObject references preserve empty anchors");refs.Clear();
                var rendererNode=GameObject.CreatePrimitive(PrimitiveType.Cube);rendererNode.transform.SetParent(root.transform);
                var renderer=rendererNode.GetComponent<MeshRenderer>();renderer.probeAnchor=empty;
                Check(SwanJourneyBaselineAudit.References(new[]{root}).ContainsKey(empty),"Renderer probe anchors participate in protection, not just MonoBehaviours");
                var animation=root.AddComponent<Animation>();Check(!SwanJourneyEmptyNodeBudget.CanRemove(empty,refs),"Animation path bindings preserve unreferenced descendants");UnityEngine.Object.DestroyImmediate(animation);
                var rect=new GameObject("Layout anchor",typeof(RectTransform));rect.transform.SetParent(root.transform);
                Check(!SwanJourneyEmptyNodeBudget.CanRemove(rect.transform,refs),"Empty UI layout elements are preserved");
                Check(!SwanJourneyEmptyNodeBudget.CanRemove(rendererNode.transform,refs),"Renderable geometry can never be classified as an empty node");
                var journey=root.AddComponent<SteamJourney>();journey.SwanInputActive=true;journey.modules=Array.Empty<JourneyModule>();journey.finishing=Array.Empty<JourneyModule>();
                var probe=root.AddComponent<SwanJourneyPerformanceProbe>();probe.journey=journey;probe.content=root.transform;
                material=new Material(SwanJourneyUrp.Require(SwanJourneyUrp.Lit));simple=new Material(material);
                renderer.sharedMaterial=material;probe.surfaces=new[]{new SwanJourneyPerformanceProbe.Surface{original=material,simple=simple}};
                Check(probe.TryApply("surface-simple")&&renderer.sharedMaterial==simple,"Material-only isolation changes the renderer binding");
                probe.Restore();Check(renderer.sharedMaterial==material&&probe.ActiveProfile=="normal","Ending isolation restores original material identity");
                Check(probe.TryApply("render-off")&&!renderer.enabled,"Drawing-only isolation hides geometry without disabling its transform or collider");
                Check(renderer.gameObject.activeInHierarchy&&renderer.GetComponent<Collider>().enabled,"Diagnostic mask leaves input colliders and gameplay objects active");
                probe.Restore();Check(renderer.enabled,"Drawing-only isolation restores renderer enable state");
                renderer.enabled=false;probe.TryApply("render-off");probe.Restore();Check(!renderer.enabled,"Originally hidden objects remain hidden after restoration");
                Check(!probe.TryApply("unknown"),"Unknown diagnostic commands are rejected");
                journey.inSandbox=true;Check(!probe.TryApply("surface-simple"),"Workshop diagnostics cannot silently affect deferred canyon bindings");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);if(material)UnityEngine.Object.DestroyImmediate(material);if(simple)UnityEngine.Object.DestroyImmediate(simple);}
            var report=new Report{passed=true,checks=checks.ToArray()};
            File.WriteAllText(SwanJourneyBuild.Output+"/baseline-checks.json",JsonUtility.ToJson(report,true));
            Debug.Log("SWAN_BASELINE_CHECKS_PASS checks="+checks.Count+" device90Fps=unverified");
            void Check(bool pass,string message){if(!pass)throw new InvalidOperationException(message);checks.Add(message);}
        }
    }
}
