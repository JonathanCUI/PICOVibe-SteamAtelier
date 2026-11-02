using System;
using System.IO;
using System.Linq;
using ByteDance.PICO.XR;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;
using SwanJourney;

public static class SteamXrTrialBuild
{
    public static void Build()
    {
        const BuildTargetGroup group = BuildTargetGroup.Android;
        var xr = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(group);
        if (!xr || !xr.Manager || xr.Manager.activeLoaders.Count != 1 || !(xr.Manager.activeLoaders[0] is PXR_Loader))
            throw new InvalidOperationException("Missing PICO-only Android loader");
        xr.InitManagerOnStart = xr.Manager.automaticLoading = xr.Manager.automaticRunning = true;
        EditorUtility.SetDirty(xr);
        EditorUtility.SetDirty(xr.Manager);
        const string source = "Assets/SwanJourney/Scenes/SteamJourneySwan.unity";
        const string scenePath = "Assets/SwanJourney/Scenes/SteamJourneyXRTrial.unity";
        SwanJourney.Editor.SwanCanyonFoliage.PreparePrefab();
        var scene = EditorSceneManager.OpenScene(source);
        var journey = UnityEngine.Object.FindObjectOfType<SteamJourney>(true);
        SwanJourney.Editor.SwanXrBoilerArt.Apply(journey);
        var input = UnityEngine.Object.FindObjectOfType<SwanJourneyInteraction>(true);
        foreach (var c in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true))
        {
            if (!c || c.gameObject.scene != scene) continue;
            switch (c.GetType().Name)
            {
                case "SpatialCamera": case "SwanSpatialInputMode": case "SpatialAdapterHoverEffect":
                case "SwanJourneyNodeAlignmentDiagnostics": case "SwanJourneyRuntimeDiagnostics":
                case "SwanJourneyCpuDiagnostics": case "SwanJourneyPerformanceProbe":
                    c.enabled = false; break;
            }
        }
        foreach (var c in UnityEngine.Object.FindObjectsOfType<Camera>(true)) c.enabled = false;
        foreach (var l in UnityEngine.Object.FindObjectsOfType<AudioListener>(true)) l.enabled = false;
        var rig = new GameObject("PICO XR tracking origin");
        var eye = new GameObject("Tracked stereo camera");
        eye.tag = "MainCamera";
        eye.transform.SetParent(rig.transform, false);
        var camera = eye.AddComponent<Camera>();
        camera.stereoTargetEye = StereoTargetEyeMask.Both;
        camera.nearClipPlane = .025f;
        camera.farClipPlane = 50f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.clear;
        camera.allowHDR = false;
        foreach (var mood in UnityEngine.Object.FindObjectsOfType<StudioWorkshopMood>(true))
            mood.useEnvironmentBackground = true;
        eye.AddComponent<AudioListener>();
        rig.AddComponent<PXR_Manager>();
        input.directXrInput = true;
        journey.view = camera;
        var trial = rig.AddComponent<SteamXrTrial>();
        trial.view = camera; trial.journey = journey; trial.interaction = input;
        SwanJourney.Editor.SwanXrValleyCloudArt.Apply(journey,trial);
        SwanJourney.Editor.SwanXrDayNightArt.Apply(journey,input);
        if (!EditorSceneManager.SaveScene(scene, scenePath, true)) throw new IOException(scenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath,true) };
        PlayerSettings.SetApplicationIdentifier(group, "com.picovibe.steamatelier.xrtrial.room");
        PlayerSettings.productName = "Steam Atelier XR Room";
        PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)34;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.useCustomKeystore = false;
        PlayerSettings.SetScriptingBackend(group,ScriptingImplementation.IL2CPP);
        EditorUserBuildSettings.buildAppBundle = false;
        AssetDatabase.SaveAssets();
        var sdk = AndroidExternalToolsSettings.sdkRootPath;
        var ndk = AndroidExternalToolsSettings.ndkRootPath;
        var jdk = AndroidExternalToolsSettings.jdkRootPath;
        try
        {
            AndroidExternalToolsSettings.sdkRootPath = @"D:/work/6000.0.23f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK";
            AndroidExternalToolsSettings.ndkRootPath = @"D:/work/2022.3.5f1c1/Editor/Data/PlaybackEngines/AndroidPlayer/NDK";
            AndroidExternalToolsSettings.jdkRootPath = @"D:/work/2022.3.5f1c1/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK";
            Directory.CreateDirectory("Builds/XRTrial");
            var r = BuildPipeline.BuildPlayer(new[]{scenePath},"Builds/XRTrial/SteamAtelierXRRoom-TrackLights.apk",BuildTarget.Android,BuildOptions.None);
            if (r.summary.result != BuildResult.Succeeded) throw new Exception("XR build: "+r.summary.result);
        }
        finally
        {
            AndroidExternalToolsSettings.sdkRootPath = RestoreTool(sdk);
            AndroidExternalToolsSettings.ndkRootPath = RestoreTool(ndk);
            AndroidExternalToolsSettings.jdkRootPath = RestoreTool(jdk);
        }
    }
    static string RestoreTool(string oldPath)
    {
        if (!Directory.Exists(oldPath)) return null;
        return oldPath.Replace('\\','/').StartsWith(EditorApplication.applicationContentsPath.Replace('\\','/') +
            "/PlaybackEngines/AndroidPlayer/",StringComparison.OrdinalIgnoreCase) ? null : oldPath;
    }
}
