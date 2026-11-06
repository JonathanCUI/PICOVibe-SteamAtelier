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
    public static void Build()=>BuildRoom("SteamAtelierXRRoom-SketchPage01.apk");
    public static void BuildSketchDragDemo()=>BuildRoom("SteamAtelierXRRoom-WorkshopSketchbook.apk");
    public static void BuildCompletionFeedback()=>BuildRoom("SteamAtelierXRRoom-CompletionCelebration.apk");
    public static void BuildVisualIntegration()=>BuildRoom("SteamAtelierXRRoom-VisualIntegration.apk");
    public static void BuildConsoleTeaching()=>BuildRoom("SteamAtelierXRRoom-ConsoleTeaching.apk");
    public static void BuildLightingFinish()
    {
        SwanJourney.Editor.SwanLightingFinishBake.Bind();
        BuildRoom("SteamAtelierXRRoom-LightingFinish.apk");
    }
    public static void RebakeLightingFinish()
    {
        SwanJourney.Editor.SwanLightingFinishBake.Bake();BuildLightingFinish();
    }
    public static void BuildCanyonLighting()
    {
        SwanJourney.Editor.SwanCanyonLightingStudy.ApplyBindings();
        BuildRoom("SteamAtelierXRRoom-CanyonLighting.apk");
    }
    public static void BuildDisplayLighting()
    {
        SwanJourney.Editor.SwanCanyonPortalStudy.Prepare();
        SwanJourney.Editor.SwanCanyonLightingStudy.ApplyBindings();
        BuildRoom("SteamAtelierXRRoom-DisplayLighting.apk");
    }
    public static void BuildLightingRepair()=>BuildRoom("SteamAtelierXRRoom-LightingRepair.apk");
    public static void BuildCanyonContactRepair()=>BuildRoom("SteamAtelierXRRoom-CanyonContactRepair.apk");
    public static void BuildNightLapDemo()=>BuildRoom("SteamAtelierXRRoom-NightLapDemo.apk");
    public static void BuildNightValleyLighting()=>BuildRoom("SteamAtelierXRRoom-NightValleyLighting.apk");
    public static void PrepareContactFinish(){SwanJourney.Editor.SwanTrackContactBake.Prepare();SwanJourney.Editor.SwanContactFinishBake.Prepare();}
    public static void BuildContactFinish()=>BuildRoom("SteamAtelierXRRoom-ContactFinish.apk");
    public static void BuildVisualPolish234()=>BuildRoom("SteamAtelierXRRoom-VisualPolish234.apk");
    static void BuildRoom(string apkName)
    {
        const BuildTargetGroup group = BuildTargetGroup.Android;
        var xr = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(group);
        if (!xr || !xr.Manager || xr.Manager.activeLoaders.Count != 1 || !(xr.Manager.activeLoaders[0] is PXR_Loader))
            throw new InvalidOperationException("Missing PICO-only Android loader");
        xr.InitManagerOnStart = xr.Manager.automaticLoading = xr.Manager.automaticRunning = true;
        EditorUtility.SetDirty(xr);
        EditorUtility.SetDirty(xr.Manager);
        var pico = PXR_ProjectSetting.GetProjectConfig();
        pico.handTracking = true;
        pico.highFrequencyHand = true;
        pico.eyeTracking = true;
        pico.handTrackingSupportType = HandTrackingSupport.ControllersAndHands;
        EditorUtility.SetDirty(pico);
        const string source = "Assets/SwanJourney/Scenes/SteamJourneySwan.unity";
        const string scenePath = "Assets/SwanJourney/Scenes/SteamJourneyXRTrial.unity";
        if(!AssetDatabase.IsValidFolder("Assets/SwanJourney/Branding/XrSurfaceArt"))
            AssetDatabase.CreateFolder("Assets/SwanJourney/Branding","XrSurfaceArt");
        SwanJourney.Editor.SwanCanyonFoliage.PreparePrefab();
        if(File.Exists(SwanJourney.Editor.SwanLightingFinishBake.Folder+"/Lighting states.json"))
            SwanJourney.Editor.SwanLightingFinishBake.Bind();
        var scene = EditorSceneManager.OpenScene(source);
        var journey = UnityEngine.Object.FindObjectOfType<SteamJourney>(true);
        var scan=journey.gameObject.AddComponent<SwanBlockArrival>();
        scan.journey=journey;scan.arrival=journey.GetComponent<JourneySceneArrival>();
        scan.arrival.enabled=true;scan.arrival.duration=1.8f;
        scan.workshopConsole=journey.previewStand.parent;
        scan.sliceShader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/SwanJourney/XrBlockArrival.shader");
        scan.scanCue=journey.gameObject.AddComponent<AudioSource>();
        scan.scanCue.playOnAwake=false;scan.scanCue.volume=.42f;
        scan.scanCue.clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/AtelierJourney/FlatTrayV22/Audio/zhuanchang.mp3");
        if(!scan.scanCue.clip)throw new InvalidOperationException("Missing zhuanchang scan cue");
        if(!scan.sliceShader)throw new InvalidOperationException("Missing XR block reveal shader");
        scan.loader=UnityEngine.Object.FindObjectOfType<SwanJourneySceneLoader>(true);
        scan.loader.holdForArrival=true;
        SwanJourney.Editor.SwanXrBoilerArt.Apply(journey);
        SwanJourney.Editor.SwanXrWorkshopSurfaces.Apply(journey);
        SwanJourney.Editor.SwanXrConsoleFinish.Apply(journey);
        SwanJourney.Editor.SwanConsoleTeachingBuild.Apply(journey);
        SwanJourney.Editor.SwanXrSketchSheetBuild.Apply(journey.GetComponent<StudioPartCards>());
        var input = UnityEngine.Object.FindObjectOfType<SwanJourneyInteraction>(true);
        var layout = UnityEngine.Object.FindObjectOfType<SwanJourneyPresentation>(true);
        layout.moveHandle.gameObject.SetActive(false);
        layout.moveHandle = null;
        input.moveLesson = input.moveCaption = null;
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
        var hud = UnityEngine.Object.FindObjectOfType<SwanAtelier.SwanFrameRateHud>(true);
        if (!hud || !hud.meshText) throw new InvalidOperationException("XR room is missing its FPS glyphs");
        hud.xrView = camera;
        hud.VisualTransform.SetParent(camera.transform, false);
        hud.VisualTransform.localPosition = hud.viewOffset;
        hud.VisualTransform.localRotation = Quaternion.identity;
        hud.VisualTransform.localScale = Vector3.one * hud.visualScale;
        hud.meshText.gameObject.SetActive(true);
        hud.gameObject.SetActive(true);
        foreach (var mood in UnityEngine.Object.FindObjectsOfType<StudioWorkshopMood>(true))
            mood.useEnvironmentBackground = true;
        eye.AddComponent<AudioListener>();
        rig.AddComponent<PXR_Manager>().eyeTracking = true;
        input.directXrInput = true;
        journey.view = camera;
        var trial = rig.AddComponent<SteamXrTrial>();
        trial.view = camera; trial.journey = journey; trial.interaction = input;
        SwanJourney.Editor.SwanXrLocomotiveFinish.Apply(journey,trial);
        SwanJourney.Editor.SwanSurfaceRepair.Apply(journey);
        trial.focus = rig.AddComponent<SteamXrFocus>();
        trial.focus.journey = journey;
        trial.focus.highlightShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/SwanJourney/XrGazeHighlight.shader");
        trial.handMaskShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/SwanJourney/XrPassthroughHand.shader");
        if (!trial.focus.highlightShader || !trial.handMaskShader) throw new InvalidOperationException("Missing XR interaction shaders");
        trial.handMasks = new[] { AddHandMask(rig.transform, "HandLeft"), AddHandMask(rig.transform, "HandRight") };
        SwanJourney.Editor.SwanXrValleyCloudArt.Apply(journey,trial);
        SwanJourney.Editor.SwanXrDayNightArt.Apply(journey,input);
        var celebration=journey.gameObject.AddComponent<SwanAssemblyCelebration>();
        celebration.journey=journey;celebration.frontReference=trial.frontGuard;
        celebration.scanShader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/SwanJourney/XrCompletionScan.shader");
        celebration.paperShader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/SwanJourney/XrCelebrationPaper.shader");
        if(!celebration.scanShader || !celebration.paperShader)throw new InvalidOperationException("Missing completion shaders");
        ApplyCompletionAudio(journey);
        if(File.Exists("Assets/SwanJourney/Branding/StructureOcclusion/Bindings.json"))
            SwanJourney.Editor.SwanStructureOcclusionBuild.Apply(journey);
        SwanJourney.Editor.SwanXrVisualIntegration.Apply(journey);
        SwanJourney.Editor.SwanTrackContactBake.Apply(journey);
        SwanJourney.Editor.SwanContactFinishBake.Apply(journey);
        SwanJourney.Editor.SwanDisplayShadowBuild.Apply(journey);
        ValidateHandInteraction(scene);
        if (!EditorSceneManager.SaveScene(scene, scenePath, true)) throw new IOException(scenePath);
        SwanJourney.Editor.SwanJourneyQuickInstallChecks.RunXr(scenePath);
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
            var sdkOverride = Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT");
            if (!string.IsNullOrEmpty(sdkOverride)) AndroidExternalToolsSettings.sdkRootPath = sdkOverride;
            var ndkOverride=Environment.GetEnvironmentVariable("ANDROID_NDK_ROOT");
            if(!string.IsNullOrEmpty(ndkOverride))AndroidExternalToolsSettings.ndkRootPath=ndkOverride;
            var jdkOverride=Environment.GetEnvironmentVariable("JAVA_HOME");
            if(!string.IsNullOrEmpty(jdkOverride))AndroidExternalToolsSettings.jdkRootPath=jdkOverride;
            Directory.CreateDirectory("Builds/XRTrial");
            var r = BuildPipeline.BuildPlayer(new[]{scenePath},Path.Combine("Builds/XRTrial",apkName),BuildTarget.Android,BuildOptions.None);
            if (r.summary.result != BuildResult.Succeeded) throw new Exception("XR build: "+r.summary.result);
        }
        finally
        {
            AndroidExternalToolsSettings.sdkRootPath = RestoreTool(sdk);
            AndroidExternalToolsSettings.ndkRootPath = RestoreTool(ndk);
            AndroidExternalToolsSettings.jdkRootPath = RestoreTool(jdk);
        }
    }
    static void ApplyCompletionAudio(SteamJourney journey)
    {
        const string path="Assets/SwanJourney/Branding/Completion/universfield-level-up-08-402152.mp3";
        var importer=AssetImporter.GetAtPath(path) as AudioImporter;
        if(!importer)throw new InvalidOperationException("Missing assembly completion sound");
        var settings=importer.defaultSampleSettings;
        settings.loadType=AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=1;
        settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
        settings.preloadAudioData=true;
        importer.defaultSampleSettings=settings;importer.forceToMono=false;importer.SaveAndReimport();
        var finish=journey.GetComponent<StudioAssemblyFinishV9>();
        finish.sweep=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        finish.nativeSweepPitch=1;finish.nativeSweepGain=1;
        if(!finish.sweep)throw new InvalidOperationException("Completion sound import failed");
        Debug.Log($"XR_COMPLETION_AUDIO clip={finish.sweep.name} length={finish.sweep.length:F3} pitch={finish.nativeSweepPitch} load={finish.sweep.loadType}");
    }
    static void ValidateHandInteraction(UnityEngine.SceneManagement.Scene scene)
    {
        var aim=new HandAimState { aimStatus=HandAimStatus.AimRayValid, aimRayPose=Posef.Identity };
        // PXR joint records are right-handed. One metre forward is raw Z=-1.
        var thumb=new HandJointLocation { locationStatus=HandLocationStatus.PositionValid, pose=new Posef(Vector3.back,Quaternion.identity) };
        var index=thumb;
        foreach (var gap in new[]{.015f,.025f,.030f,.045f})
        {
            index.pose=new Posef(Vector3.back+Vector3.right*gap,Quaternion.identity);
            bool held=gap>.02f;
            if (!SteamXrTrial.ResolvePinch(aim,thumb,index,held,Vector3.zero,out var centre,out _,out bool pressed)
                || pressed!=(gap<.028f) || Vector3.Distance(centre,Vector3.forward+Vector3.right*gap*.5f)>.0001f)
                throw new InvalidOperationException("XR fingertip pinch/hold/release failed at "+gap);
            if (!SteamXrTrial.ResolvePinch(aim,thumb,index,held,Vector3.zero,out _,out _,out bool twoHandPressed,true)
                || twoHandPressed!=(gap<.035f)) throw new InvalidOperationException("XR two-hand hold failed at "+gap);
        }
        aim.aimStatus|=HandAimStatus.AimIndexPinching;
        if (!SteamXrTrial.ResolvePinch(aim,default,default,false,Vector3.zero,out _,out _,out bool sdkPinch) || !sdkPinch
            || SteamXrTrial.ResolvePinch(default,default,default,false,Vector3.zero,out _,out _,out _))
            throw new InvalidOperationException("XR aim-only or untracked hand failed");
        int proxies=0,caps=0;
        foreach (var c in UnityEngine.Object.FindObjectsOfType<Collider>(true))
        {
            if (c.gameObject.scene!=scene) continue;
            var feedback=c.GetComponentInParent<JourneyButtonFeedback>(true);
            bool button=feedback && feedback.cap;
            bool proxy=c.name=="Swan positive-scale hit proxy";
            if (!button && !proxy) continue;
            var root=SteamXrFocus.HighlightRoot(c);
            if (!root || (button && root!=feedback.cap) || !root.GetComponentInChildren<MeshFilter>(true))
                throw new InvalidOperationException("Missing XR focus surface: "+c.name);
            if (button) caps++;
            if (proxy) proxies++;
        }
        if (caps==0 || proxies==0) throw new InvalidOperationException("XR focus fixtures missing: caps="+caps+" proxies="+proxies);
        Debug.Log("XR_HAND_FOCUS_CHECK_PASS independentHands=true pinchHoldRelease=true caps="+caps+" proxies="+proxies);
    }
    static SkinnedMeshRenderer AddHandMask(Transform rig, string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.bytedance.pico.xr/Resources/Prefabs/" + name + ".prefab");
        var hand = UnityEngine.Object.Instantiate(prefab, rig, false);
        if (!hand.GetComponent<PXR_VstModelPosCheck>()) hand.AddComponent<PXR_VstModelPosCheck>();
        var skin = hand.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.bones.Length >= 20);
        foreach (var renderer in hand.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        skin.updateWhenOffscreen = true;
        skin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return skin;
    }

    static string RestoreTool(string oldPath)
    {
        if (!Directory.Exists(oldPath)) return null;
        return oldPath.Replace('\\','/').StartsWith(EditorApplication.applicationContentsPath.Replace('\\','/') +
            "/PlaybackEngines/AndroidPlayer/",StringComparison.OrdinalIgnoreCase) ? null : oldPath;
    }
}
