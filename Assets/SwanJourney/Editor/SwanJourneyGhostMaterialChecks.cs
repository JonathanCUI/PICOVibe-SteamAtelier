using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SwanJourney.Editor
{
    public static class SwanJourneyGhostMaterialChecks
    {
        public static List<string> Run()
        {
            var checks=new List<string>();
            var root=new GameObject("Ghost material lifetime fixture");
            var deferred=new GameObject("Deferred ghost fixture");
            var shader=SwanJourneyUrp.Require(SwanJourneyUrp.Lit);
            var source=new Material(shader){color=new Color(.8f,.9f,1,.25f),renderQueue=3000};
            var other=new Material(source){color=new Color(.9f,.6f,.2f,.3f)};
            var temporary=new Material(shader);
            try
            {
                source.SetColor("_EmissionColor",Color.gray*.15f);
                var journey=root.AddComponent<SteamJourney>();
                var a=Make(root.transform,source);var b=Make(root.transform,source);
                var c=Make(deferred.transform,source);var d=Make(root.transform,other);
                var bridge=root.AddComponent<SwanJourneyVisualBridge>();
                bridge.journey=journey;bridge.workbench=root.transform;bridge.deferredEnvironment=deferred.transform;
                bridge.materials=new[]{new JourneyNativeMaterial{material=source,kind=JourneyNativeMaterialKind.Ghost,color=source.color},
                    new JourneyNativeMaterial{material=other,kind=JourneyNativeMaterialKind.Ghost,color=other.color}};
                Invoke(bridge,"Awake");
                var shared=a.sharedMaterial;var distinct=d.sharedMaterial;
                Check(shared&&shared!=source&&b.sharedMaterial==shared&&c.sharedMaterial==shared,"Matching ghost definitions share one owned live material across roots");
                Check(distinct!=shared&&bridge.NativeAnimatedRenderers==4&&bridge.NativeOwnedMaterialCount==2,"Distinct definitions keep distinct appearance and reduce only duplicate instances");
                Invoke(bridge,"LateUpdate");
                Check(bridge.LastMaterialWrites==4&&shared.color==source.color,"First update writes once per shared definition");
                Invoke(bridge,"LateUpdate");
                Check(bridge.LastMaterialWrites==0,"Stable ghost appearance produces no repeated material writes");
                var block=new MaterialPropertyBlock();block.SetColor("_Color",Color.magenta);a.SetPropertyBlock(block);
                Invoke(bridge,"LateUpdate");
                Check(shared.color==source.color&&!a.HasPropertyBlock(),"Authored ghost policy ignores per-renderer tint and consumes the block as before");
                typeof(SteamJourney).GetField("snapReady",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(journey,true);
                a.enabled=false;Invoke(bridge,"LateUpdate");
                Check(Mathf.Abs(shared.color.a-source.color.a*1.25f)<.00001f&&bridge.LastMaterialWrites==4,"Snap-ready feedback updates shared groups once even when one member is hidden");
                a.enabled=true;Invoke(bridge,"LateUpdate");
                Check(a.sharedMaterial==shared&&bridge.LastMaterialWrites==0,"Re-enabled ghost uses the current shared parameters");
                b.sharedMaterial=temporary;Invoke(bridge,"LateUpdate");
                Check(b.sharedMaterial==temporary,"Updating parameters does not steal a temporary effect binding");
                b.sharedMaterial=shared;
                bridge.SetDeferredEnvironment(null);
                Check(c.sharedMaterial==source&&shared&&a.sharedMaterial==shared&&bridge.NativeSharedGhostCount==2,"Unloading a deferred root restores its source without destroying workshop-shared material");
                bridge.SetDeferredEnvironment(deferred.transform);
                Check(c.sharedMaterial==shared&&bridge.NativeAnimatedRenderers==4,"Reattaching the environment reuses the surviving group without accumulating instances");
                bridge.SetDeferredEnvironment(null);
                // EditMode does not drive MonoBehaviour lifecycle callbacks.
                // Exercise the actual cleanup method before disposing the fixture.
                Invoke(bridge,"OnDestroy");
                UnityEngine.Object.DestroyImmediate(root);root=null;
                Check(!shared&&!distinct&&source&&other,"Owner destruction releases each shared instance and preserves source assets");
            }
            finally
            {
                if(root)UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(deferred);
                UnityEngine.Object.DestroyImmediate(source);UnityEngine.Object.DestroyImmediate(other);UnityEngine.Object.DestroyImmediate(temporary);
            }
            return checks;
            void Check(bool pass,string label){if(!pass)throw new InvalidOperationException(label);checks.Add("Ghost sharing: "+label);}
        }
        static MeshRenderer Make(Transform parent,Material material)
        {
            var go=new GameObject("Independent ghost renderer",typeof(MeshRenderer));go.transform.SetParent(parent,false);
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;return renderer;
        }
        static void Invoke(object target,string method)=>target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);
    }
}
