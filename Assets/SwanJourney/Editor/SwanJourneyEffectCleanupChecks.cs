using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace SwanJourney.Editor
{
    public static class SwanJourneyEffectCleanupChecks
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static void Need(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(SwanJourneyBuild.ScenePath);
            var j = UnityEngine.Object.FindObjectOfType<SteamJourney>(true);
            var bridge = j.GetComponent<SwanJourneyVisualBridge>();
            var flow = j.GetComponent<SwanJourneyFlowParticles>();
            var arrival = j.GetComponent<JourneySceneArrival>();
            Need(j.SwanInputActive && j.bakedFirebox && j.bakedChimney && flow.UsesArrows, "Visible native replacements retained");
            Need(!j.waterSkin && !j.waterBody && !bridge.scanMaterial && !bridge.ringMesh, "No discarded shader-only material or ring references");
            Need(!arrival.enabled && !arrival.solidShader && !arrival.ghostShader && arrival.contours.Length == 0, "No arrival shader dependencies");
            Need(j.spatialRoot.GetComponentsInChildren<Volume>(true).All(v => !v.enabled && !v.sharedProfile), "No disabled postprocess profile dependencies");
            Need(j.spatialRoot.GetComponentsInChildren<SpatialLightingV20>(true).All(v => !v.enabled && !v.tunnelAmbient), "No unused tunnel ambient field dependency");
            Need(flow.sources.All(s => s.alphaMaterials.Length == 0), "Arrow sources carry no unused wisp palettes");
            Need(bridge.completionBadge && j.modules.Length+j.finishing.Length==17, "Completion feedback and paired lamps retained");
            var tunnel = j.GetComponent<MesaSteamTunnelV17>();
            Need(tunnel && tunnel.freeAir, "Smoke collision field retained for CPU queries");
            float clock = Shader.GetGlobalFloat("_JourneyTime"), field = Shader.GetGlobalFloat("_MesaFieldEnabled");
            Material water = null;
            try
            {
                water = new Material(Shader.Find("SteamJourney/V10WaterSurface"));
                water.SetFloat("_Heat",.732f);water.SetFloat("_FlowTime",123f);j.waterSkin=water;
                Shader.SetGlobalFloat("_JourneyTime",123f);Shader.SetGlobalFloat("_MesaFieldEnabled",.732f);
                j.suppressPersistence=true;
                typeof(SteamJourney).GetMethod("AnimateRefinedEffects",Private).Invoke(j,new object[]{0f});
                tunnel.SendMessage("Awake",SendMessageOptions.RequireReceiver);tunnel.Bind();
                Need(Shader.GetGlobalFloat("_JourneyTime")==123f && Shader.GetGlobalFloat("_MesaFieldEnabled")==.732f, "Swan effects leave desktop globals untouched");
                Need(water.GetFloat("_Heat")==.732f && water.GetFloat("_FlowTime")==123f, "Swan does not update discarded water shader even if legacy references remain");
                Need(!float.IsNaN(tunnel.Air(tunnel.fieldMin+tunnel.fieldSize*.5f)), "CPU smoke clearance still works");
                flow.SendMessage("Awake",SendMessageOptions.RequireReceiver);
                Need(typeof(SwanJourneyFlowParticles).GetField("readBuffer",Private).GetValue(flow)==null, "Arrow mode creates no legacy particle readback buffer");
                j.SwanInputActive=false;
                typeof(SteamJourney).GetMethod("AnimateRefinedEffects",Private).Invoke(j,new object[]{0f});
                Need(water.GetFloat("_Heat")==j.WaterHeat && water.GetFloat("_FlowTime")==j.FlowTime, "Desktop water path remains functional");
            }
            finally
            {
                Shader.SetGlobalFloat("_JourneyTime",clock);Shader.SetGlobalFloat("_MesaFieldEnabled",field);
                if(water)UnityEngine.Object.DestroyImmediate(water);
                EditorSceneManager.OpenScene(SwanJourneyBuild.ScenePath);
            }
            Directory.CreateDirectory(SwanJourneyBuild.Output);
            File.WriteAllText(SwanJourneyBuild.Output+"/effect-cleanup-checks.json","{\"passed\":true,\"nativeRenderingValidated\":false,\"scope\":\"Scene dependency and runtime guard regression\"}");
            Debug.Log("SWAN_EFFECT_CLEANUP_CHECKS_PASS");
        }
    }
}
