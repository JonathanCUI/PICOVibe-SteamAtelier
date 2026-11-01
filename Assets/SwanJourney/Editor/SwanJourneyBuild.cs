using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ByteDance.PICO.SpatialAdapter;
using ByteDance.PICO.SpatialAdapter.Component;
using ByteDance.PICO.XR;
using SwanAtelier;
using SwanAtelier.Editor;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SwanJourney.Editor
{
    public static class SwanJourneyBuild
    {
        public const string PackageId="com.picovibe.steamatelier.v23";
        public const string ScenePath="Assets/SwanJourney/Scenes/SteamJourneySwan.unity";
        public const string EnvironmentScenePath="Assets/SwanJourney/Scenes/SteamJourneyCanyon.unity";
        public const string EnvironmentPrefabPath="Assets/SwanJourney/Resources/SwanJourneyCanyon.prefab";
        public const string Generated="Assets/SwanJourney/Generated";
        public const string Output="Builds/SwanJourney";
        // Recording build: restore this flag to show the existing view-following HUD.
        public const bool ShowFpsHud=false;
        [MenuItem("Steam Atelier/V23 Swan/Generate")]
        public static void Generate()
        {
            var savedLayout = SwanJourneyWorkshopLayout.ReadSaved();
            Directory.CreateDirectory(Generated);Directory.CreateDirectory("Assets/SwanJourney/Scenes");Directory.CreateDirectory(Output);
            Configure();
            var scene=EditorSceneManager.OpenScene("Assets/AtelierJourney/Scenes/SteamJourney.unity");
            var journey=UnityEngine.Object.FindObjectOfType<SteamJourney>(true);
            if(!journey||journey.modules.Length!=7||journey.finishing.Length!=11)throw new Exception("V23 saved scene must contain 7 principle modules and 11 finishing assemblies");
            var roots=scene.GetRootGameObjects();
            var stage=new GameObject("蒸汽工坊·V23 · Swan workbench");
            stage.AddComponent<SwanJourneyTransformSyncBudget>();
            stage.transform.position=new Vector3(0,.90f,1.45f);
            var content=new GameObject("V23 partner composition").transform;content.SetParent(stage.transform,false);
            foreach(var root in roots)root.transform.SetParent(content,false);
            content.localScale=Vector3.one*.12f;content.localRotation=Quaternion.Euler(0,180,0);
            journey.SwanInputActive=true;journey.spatialRoot=content;
            SwanJourneyAssemblySteps.MergeHeadlamps(journey);
            SwanJourneyNodeAlignment.NormalizeContent(content);
            SwanJourneyScenePreparation.Prepare(journey,content);
            // The saved Journey owns simulation and all process state. Only the
            // old desktop input/render setup is disabled in the derived scene.
            foreach(var behaviour in content.GetComponentsInChildren<MonoBehaviour>(true)) {
                if(behaviour is StudioVisualQuality){UnityEngine.Object.DestroyImmediate(behaviour);continue;}
                if(behaviour is StudioVisualQuality||behaviour is LocomotiveXRQuality||behaviour is LocomotiveAR||behaviour is LocomotiveSpatialBridge||behaviour is LocomotiveWorkshop||behaviour is LocomotiveFreeAssembly||behaviour is AtelierExperience||behaviour is TrialAR||behaviour is V9Dashboard)behaviour.enabled=false;
            }
            foreach(var camera in content.GetComponentsInChildren<Camera>(true))camera.enabled=false;
            foreach(var data in content.GetComponentsInChildren<UniversalAdditionalCameraData>(true))UnityEngine.Object.DestroyImmediate(data);
            foreach(var volume in content.GetComponentsInChildren<Volume>(true))volume.enabled=false;
            foreach(var listener in content.GetComponentsInChildren<AudioListener>(true))UnityEngine.Object.DestroyImmediate(listener);
            foreach(var input in content.GetComponentsInChildren<UnityEngine.EventSystems.StandaloneInputModule>(true))UnityEngine.Object.DestroyImmediate(input);
            var previewGo=new GameObject("Swan V23 editor preview",typeof(Camera),typeof(AudioListener));
            var preview=previewGo.GetComponent<Camera>();preview.tag="MainCamera";
            preview.transform.position=new Vector3(-1.45f,1.9f,-.65f);preview.transform.LookAt(stage.transform.position+new Vector3(0,.15f,0));
            preview.nearClipPlane=.02f;preview.farClipPlane=50;preview.fieldOfView=48;
            preview.clearFlags=CameraClearFlags.SolidColor;preview.backgroundColor=new Color(.045f,.055f,.065f);
            journey.view=preview;journey.engine.studioCamera=preview;
            var diagnostics=stage.AddComponent<SwanJourneyRuntimeDiagnostics>();diagnostics.journey=journey;diagnostics.previewCamera=preview;
            if(scene.GetRootGameObjects().Any(SwanJourneyPhysicsBudget.HasDynamics))
                throw new InvalidOperationException("Swan query-only physics budget requires a scene without Unity dynamics");
            var physicsBudget=stage.AddComponent<SwanJourneyPhysicsBudget>();
            physicsBudget.journey=journey;physicsBudget.validatedStaticPhysics=true;
            var placement=stage.AddComponent<SwanTabletopPlacement>();
            var presentation=stage.AddComponent<SwanJourneyPresentation>();presentation.journey=journey;presentation.content=content;presentation.tabletop=placement;
            SwanJourneyScenePreparation.ConfigureSupports(presentation,true);
            presentation.RefreshFootprint();
            var interaction=stage.AddComponent<SwanJourneyInteraction>();interaction.journey=journey;interaction.workbenchRoot=stage.transform;
            var cpuDiagnostics=stage.AddComponent<SwanJourneyCpuDiagnostics>();cpuDiagnostics.journey=journey;cpuDiagnostics.interaction=interaction;
            // Separate horizontal utility row, ahead of both the original
            // instruments and the part-card display footprint.
            var explode=AddUtility(journey,"一键拆开",Vector3.zero,SwanJourneyTargetKind.ExplodeAll);
            var pause=AddUtility(journey,"暂停 / 继续",Vector3.zero,SwanJourneyTargetKind.Control,JourneyAction.Pause);
            var slow=AddUtility(journey,"慢放 / 常速",Vector3.zero,SwanJourneyTargetKind.Control,JourneyAction.Slow);
            var desk=presentation.workshopSupports[1];
            SwanJourneyPresentation.TryBounds(desk,journey.workshopRoot.transform,out var deskBounds);
            var row=new Vector3(deskBounds.center.x,deskBounds.center.y,deskBounds.max.z+.7f);
            explode.localPosition=row+Vector3.left*3.15f;pause.localPosition=row;slow.localPosition=row+Vector3.right*3.15f;
            presentation.workshopMoveHandleAnchor=pause;
            presentation.moveHandle=AddMoveHandle(journey,stage.transform,interaction);
            presentation.RefreshFootprint();
            presentation.sandboxMoveHandleAnchor=AddUtility(journey,"车灯 开 / 关",new Vector3(-5.5f,.6f,15.4f),SwanJourneyTargetKind.Control,JourneyAction.Lights,journey.sandboxRoot.transform);
            savedLayout?.Apply(presentation, interaction);
            SwanJourneyWorkshopLayout.CentreConsole(presentation);
            int targets=ConfigureTargets(journey);
            var nativeVisuals=SwanJourneyMaterials.Configure(journey,content,Generated);
            foreach(var cards in content.GetComponentsInChildren<StudioPartCards>(true))cards.spatialLeaderMaterial=nativeVisuals.rippleMaterial;
            SwanJourneyTextureBudget.Apply(content,Generated);
            SwanJourneyPbrPacking.Apply(content);
            VRFireboxBuild.Configure(journey, nativeVisuals);
            ObservationBuild.ConfigureArrows(journey);
            SwanJourneyEffectCleanup.Apply(journey);
            SwanJourneyFireChecks.Validate(journey);
            SwanJourneySoftParticleChecks.Validate(journey);
            var ui=stage.AddComponent<SwanJourneyNativeUi>();ui.content=stage.transform;
            ui.panelMaterial=SaveMaterial("CardPanel",new Color(.022f,.035f,.045f),0,.25f);
            SwanJourneyTextCoverage.Prepare(journey,content);
            SwanJourneyCardExport.Prepare(ui);
            SwanJourneyMeshAudit.PrepareForExport(content,Generated);
            SwanJourneyLegacyHitBudget.Apply(journey);
            SwanJourneyColliderBudget.Apply(content);
            SwanJourneyStaticBatching.Optimize(journey,Generated);
            SwanJourneyRigidBatching.Apply(journey,nativeVisuals);
            SwanJourneyRigidBatching.CombineWorkshopSleepers(journey);
            var meshAudit=SwanJourneyMeshAudit.Run(content,Output+"/mesh-export-audit.json");
            if(!meshAudit.passed||meshAudit.negativeScaleBoxes.Length>0)throw new Exception("Derived mesh/collider export audit failed; see mesh-export-audit.json");
            var hudRoot=new GameObject("Swan V23 view-following FPS");hudRoot.transform.position=new Vector3(-.85f,1.65f,1.15f);
            var hud=hudRoot.AddComponent<SwanFrameRateHud>();hud.editorPreviewCamera=preview;
            hud.viewOffset=new Vector3(-.14f,.10f,.65f);hud.visualScale=1.05f;
            var fps=SaveMaterial("FpsGlyph",new Color(.65f,1,1),0,0,true);
            fps.color=new Color(.65f,1,1);fps.DisableKeyword("_EMISSION");EditorUtility.SetDirty(fps);
            hud.meshText=SwanFpsMeshBuilder.Create(Generated,fps);hud.meshText.transform.position=hudRoot.transform.position;
            hudRoot.SetActive(ShowFpsHud);hud.meshText.gameObject.SetActive(ShowFpsHud);
            RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.52f,.58f,.65f);RenderSettings.ambientEquatorColor=new Color(.31f,.33f,.35f);RenderSettings.ambientGroundColor=new Color(.16f,.17f,.18f);
            // The partner's baked lighting asset points back to its entire desktop
            // scene. SpatialAdapter follows that dependency and exports it again,
            // including obsolete geometry and unsupported desktop shaders. Native
            // rendering uses the converted PBR materials and dynamic day/night lights.
            Lightmapping.lightingDataAsset=null;
            LightmapSettings.lightmaps=Array.Empty<LightmapData>();
            foreach(var renderer in content.GetComponentsInChildren<Renderer>(true)) {
                renderer.lightmapIndex=-1;renderer.realtimeLightmapIndex=-1;
            }
            SwanJourneySceneSplit.Split(journey,presentation,nativeVisuals,ui.panelMaterial);
            var meshBudget = SwanJourneyWorkshopMeshBudget.Apply(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MeshFilter>(true)));
            // SetMode restores these bindings at startup and when returning from the canyon.
            foreach (var detail in journey.detailMeshes)
            {
                if (!detail.filter || !detail.filter.sharedMesh) continue;
                detail.inspection = detail.filter.sharedMesh;
                if (detail.miniature && SwanJourneyWorkshopMeshBudget.Triangles(detail.miniature) > SwanJourneyWorkshopMeshBudget.Triangles(detail.inspection))
                    detail.miniature = detail.inspection;
            }
            File.WriteAllText(Output+"/workshop-mesh-budget.json",JsonUtility.ToJson(meshBudget,true));
            SwanJourneyPerformanceProbeBuild.Configure(journey,interaction);
            SwanJourneyEmptyNodeBudget.Apply(journey);
            SwanJourneyMaterials.ConfigureCompletionBadge(journey, nativeVisuals);
            SwanJourneySmokeBuild.Configure(journey);
            SwanJourneyBootScene.Generate(journey.font);
            SwanJourneyUrp.UpgradeAndValidateGenerated();
            SwanJourneyTextureBudget.ShareIdenticalCopies(Generated);
            SwanJourneyNodeAlignment.ValidateAndBind(scene,EnvironmentScenePath);
            // The Windows Resources scan passes backslash paths to the SDK's
            // forward-slash-only prefab exporter. A normal AssetDatabase scene
            // dependency reaches the exporter with a canonical Assets/ path.
            // The build processor strips this reference before Player serialization.
            stage.AddComponent<SwanJourneyBuildDependencies>().canyonPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPrefabPath);
            EditorSceneManager.SaveScene(scene,ScenePath);EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(SwanJourneyBootScene.Path,true),new EditorBuildSettingsScene(ScenePath,true)};AssetDatabase.SaveAssets();
            if(AssetDatabase.GetDependencies(ScenePath,true).Contains("Assets/AtelierJourney/Scenes/SteamJourney.unity"))
                throw new Exception("Derived scene still recursively includes the partner desktop scene");
            var report=new SceneReport{package=PackageId,modules=journey.modules.Length,finishing=journey.finishing.Length,hoverMeshes=targets,
                renderers=content.GetComponentsInChildren<MeshRenderer>(true).Length,
                sourceGroups=journey.modules.Concat(journey.finishing).Select(m=>m.title).ToArray(),
                supportNames=presentation.workshopSupports.Concat(presentation.sandboxSupports).Select(t=>t.name).ToArray()};
            File.WriteAllText(Output+"/scene-report.json",JsonUtility.ToJson(report,true));
            Debug.Log("SWAN_JOURNEY_SCENE_GENERATED package="+PackageId+" modules="+(journey.modules.Length+journey.finishing.Length)+" hoverMeshes="+targets+" renderers="+report.renderers);
        }
        static Transform AddUtility(SteamJourney journey,string title,Vector3 position,SwanJourneyTargetKind kind,JourneyAction action=JourneyAction.Assemble,Transform parent=null)
        {
            var root=new GameObject(title);root.transform.SetParent(parent?parent:journey.workshopRoot.transform,false);root.transform.localPosition=position;
            var plate=GameObject.CreatePrimitive(PrimitiveType.Cube);plate.name=title+" touch surface";plate.transform.SetParent(root.transform,false);plate.transform.localScale=new Vector3(2.8f,.46f,.13f);
            plate.GetComponent<MeshRenderer>().sharedMaterial=SaveMaterial("UtilityBrass",new Color(.17f,.135f,.075f),.65f,.45f);
            plate.AddComponent<SpatialAdapterHoverEffect>();var target=plate.AddComponent<SwanJourneyTarget>();target.kind=kind;target.action=action;target.useActionOverride=kind==SwanJourneyTargetKind.Control;
            var text=new GameObject("Caption").AddComponent<TMPro.TextMeshPro>();text.transform.SetParent(root.transform,false);text.transform.localPosition=new Vector3(0,0,.075f);text.transform.localRotation=Quaternion.Euler(0,180,0);
            text.font=journey.font;text.fontSize=1.4f;text.text=title;text.alignment=TMPro.TextAlignmentOptions.Center;text.color=new Color(1,.9f,.69f);text.rectTransform.sizeDelta=new Vector2(2.6f,.4f);
            return root.transform;
        }
        static Transform AddMoveHandle(SteamJourney journey,Transform stage,SwanJourneyInteraction interaction)
        {
            // A shared, metre-sized handle stays available in workshop and sandbox.
            // Presentation places it in front of the current support footprint.
            var root=new GameObject("单手搬台 · 捏住拖动").transform;root.SetParent(stage,false);
            var plate=GameObject.CreatePrimitive(PrimitiveType.Cylinder);plate.name="Move whole scene touch surface";plate.transform.SetParent(root,false);
            // 18 cm long, 1.2 cm thick at the default exhibit scale. Keep the
            // hit collider, visible mesh and hover on the SAME native entity.
            plate.transform.localRotation=Quaternion.Euler(0,0,90);
            plate.transform.localScale=new Vector3(.012f,.09f,.012f);
            UnityEngine.Object.DestroyImmediate(plate.GetComponent<CapsuleCollider>());
            plate.AddComponent<BoxCollider>().size=new Vector3(.04f/.012f,.21f/.09f,.05f/.012f);
            plate.GetComponent<MeshRenderer>().sharedMaterial=SaveMaterial("MoveHandleBrass",new Color(.38f,.25f,.14f),.7f,.45f);
            plate.AddComponent<SpatialAdapterHoverEffect>();plate.AddComponent<SwanJourneyTarget>().kind=SwanJourneyTargetKind.MoveWorkbench;
            var text=new GameObject("Move handle caption").AddComponent<TMPro.TextMeshPro>();text.transform.SetParent(root,false);
            text.transform.localPosition=new Vector3(0,-.025f,-.008f);text.font=journey.font;text.fontSize=.32f;text.enableAutoSizing=true;
            text.fontSizeMin=.18f;text.fontSizeMax=.32f;text.text="捏住拖动";text.alignment=TMPro.TextAlignmentOptions.Center;
            text.color=new Color(1,.94f,.79f);text.rectTransform.sizeDelta=new Vector2(.2f,.035f);
            text.ForceMeshUpdate(true,true);
            var compact=UnityEngine.Object.Instantiate(text,root,false);
            compact.name="Move handle short caption";compact.text="移动";compact.ForceMeshUpdate(true,true);
            interaction.moveLesson=text.gameObject;interaction.moveCaption=compact.gameObject;
            compact.gameObject.SetActive(false);
            return root;
        }
        static Transform[] FindSupports(Transform root,bool sandbox)
        {
            var all=root.GetComponentsInChildren<Transform>(true);
            var selected=all.Where(t=>sandbox?(t.name.IndexOf("flat",StringComparison.OrdinalIgnoreCase)>=0&&t.name.IndexOf("tray",StringComparison.OrdinalIgnoreCase)>=0):
                t.name=="Desktop rail installation"||t.name.IndexOf("Low parts instrument tray",StringComparison.OrdinalIgnoreCase)>=0||t.name.IndexOf("Precision mechanical console",StringComparison.OrdinalIgnoreCase)>=0).ToArray();
            // A single source root is a conservative fallback, reported for review.
            return selected.Length>0?selected:new[]{root};
        }
        static int ConfigureTargets(SteamJourney journey)
        {
            var renderers=new HashSet<MeshRenderer>();
            foreach(var module in journey.modules.Concat(journey.finishing)) {
                foreach(var go in module.objects.Where(g=>g))foreach(var r in go.GetComponentsInChildren<MeshRenderer>(true))renderers.Add(r);
                if(module.preview)foreach(var r in module.preview.GetComponentsInChildren<MeshRenderer>(true))renderers.Add(r);
            }
            foreach(var control in journey.spatialRoot.GetComponentsInChildren<JourneyControl>(true))
                foreach(var r in control.GetComponentsInChildren<MeshRenderer>(true))renderers.Add(r);
            var sky=UnityEngine.Object.FindObjectOfType<MesaDayNightV13>(true);
            if(sky)foreach(var body in new[]{sky.sun,sky.moon})if(body)foreach(var r in body.GetComponentsInChildren<MeshRenderer>(true))renderers.Add(r);
            int count=0;
            foreach(var renderer in renderers) {
                if(renderer.GetComponent<TMPro.TMP_Text>())continue;
                var filter=renderer.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh)continue;
                var bounds=filter.sharedMesh.bounds;if(bounds.size.sqrMagnitude<.0004f)continue;
                SwanJourneyMeshAudit.EnsurePositiveCollider(renderer,bounds);
                if(!renderer.GetComponent<SpatialAdapterHoverEffect>())renderer.gameObject.AddComponent<SpatialAdapterHoverEffect>();count++;
            }
            return count;
        }
        static Material SaveMaterial(string name,Color color,float metal,float smooth,bool unlit=false)
        {
            string path=Generated+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader=SwanJourneyUrp.Require(unlit ? SwanJourneyUrp.Unlit : SwanJourneyUrp.Lit);
            if(!mat){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}else mat.shader=shader;
            mat.color=color;
            if(!unlit){mat.SetFloat("_Metallic",metal);mat.SetFloat("_Smoothness",smooth);}
            EditorUtility.SetDirty(mat);return mat;
        }
        static void Configure()
        {
            PlayerSettings.companyName="PICO Vibe";PlayerSettings.productName="蒸汽工坊·V23";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android,PackageId);PlayerSettings.bundleVersion="0.23.37";PlayerSettings.Android.bundleVersionCode=37;
            SwanJourneyIcon.Configure();
            var runtime=new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Settings/SpatialAdapterRuntimeSettings.asset"));
            runtime.FindProperty("m_enableSpatialAdapter").boolValue=true;
            runtime.FindProperty("m_defaultSpatialCameraConfiguration").objectReferenceValue=AssetDatabase.LoadAssetAtPath<SpatialCameraConfiguration>("Packages/com.bytedance.pico.spatialadapter/Resources/Default Unconstrained Spatial Camera Configuration.asset");runtime.ApplyModifiedPropertiesWithoutUndo();
            PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel29;PlayerSettings.Android.targetSdkVersion=AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android,ScriptingImplementation.IL2CPP);PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.Android,Il2CppCompilerConfiguration.Release);
            PlayerSettings.SetIl2CppCodeGeneration(UnityEditor.Build.NamedBuildTarget.Android,UnityEditor.Build.Il2CppCodeGeneration.OptimizeSpeed);
            Debug.Log("SWAN_JOURNEY_COMPILER configuration="+PlayerSettings.GetIl2CppCompilerConfiguration(BuildTargetGroup.Android)+
                " codeGeneration="+PlayerSettings.GetIl2CppCodeGeneration(UnityEditor.Build.NamedBuildTarget.Android));
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{GraphicsDeviceType.Vulkan});
            PlayerSettings.colorSpace=ColorSpace.Linear;PlayerSettings.runInBackground=true;PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android,"PICO_MS_SDK");
            var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);settings.FindProperty("activeInputHandler").intValue=1;settings.ApplyModifiedPropertiesWithoutUndo();
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/SwanJourneyURP.asset");
            if(!pipeline)throw new InvalidOperationException("Missing V23 URP asset");
            GraphicsSettings.defaultRenderPipeline=pipeline;int current=QualitySettings.GetQualityLevel();
            for(int i=0;i<QualitySettings.names.Length;i++){QualitySettings.SetQualityLevel(i,false);QualitySettings.renderPipeline=null;}QualitySettings.SetQualityLevel(current,false);
            QualitySettings.shadows=UnityEngine.ShadowQuality.Disable;QualitySettings.antiAliasing=4;
            var config=PXR_ProjectSetting.GetProjectConfig();config.isSpatialAdapter=true;config.spatialMesh=true;config.arFoundation=false;config.openMRC=false;EditorUtility.SetDirty(config);
            var xr=UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            if(!xr||!xr.Manager)throw new Exception("Missing Android XR Manager");
            foreach(var loader in xr.Manager.activeLoaders.ToArray())if(!(loader is PXR_Loader))xr.Manager.TryRemoveLoader(loader);
            if(!UnityEditor.XR.Management.Metadata.XRPackageMetadataStore.AssignLoader(xr.Manager,"ByteDance.PICO.XR.PXR_Loader",BuildTargetGroup.Android))throw new Exception("PICO Spatial input loader unavailable");
            xr.InitManagerOnStart=false;xr.Manager.automaticLoading=false;xr.Manager.automaticRunning=false;EditorUtility.SetDirty(xr);EditorUtility.SetDirty(xr.Manager);
        }
        public static void ClearExportCaches()
        {
            string projectRoot=Path.GetFullPath(Path.Combine(Application.dataPath,".."))+Path.DirectorySeparatorChar;
            foreach(string relative in new[]{"Temp/SpatialAdapterExport","Assets/StreamingAssets/SpatialAdapterExport","Library/Bee/Android/Prj/IL2CPP/Gradle/unityLibrary/src/main/assets/SpatialAdapterExport"}) {
                string cache=Path.GetFullPath(Path.Combine(projectRoot,relative));
                if(!cache.StartsWith(projectRoot,StringComparison.OrdinalIgnoreCase)||Path.GetFileName(cache)!="SpatialAdapterExport")throw new InvalidOperationException("Unsafe export cache path");
                if(Directory.Exists(cache))Directory.Delete(cache,true);
            }
        }
        public static void BuildAndroid()
        {
            // Restore the profile used by the last device-validated gesture build.
            // Non-Development/Master is not a verified native-input configuration.
            BuildAndroidInternal(true);
        }
        public static void BuildAndroidDevelopment()
        {
            BuildAndroidInternal(true);
        }
        public static void BuildAndroidWithAssemblyBaseline()=>BuildAndroidInternal(true,true);
        static void BuildAndroidInternal(bool development,bool compareAssembly=false)
        {
            Generate();if(compareAssembly)SwanJourneyAssemblyCostAudit.Compare();SwanJourneyFeedbackChecks.Run();ClearExportCaches();
            // APK incremental packaging can retain gigabytes of freed ZIP gaps
            // after textures shrink. Rebuild only the generated launcher output.
            string launcherBuild=Path.GetFullPath("Library/Bee/Android/Prj/IL2CPP/Gradle/launcher/build");
            string safeRoot=Path.GetFullPath(Path.Combine(Application.dataPath,".."))+Path.DirectorySeparatorChar;
            if(!launcherBuild.StartsWith(safeRoot,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Unsafe launcher cache path");
            if(Directory.Exists(launcherBuild))Directory.Delete(launcherBuild,true);
            string sdk=AndroidExternalToolsSettings.sdkRootPath,jdk=AndroidExternalToolsSettings.jdkRootPath;
            BuildReport report;
            try {
                AndroidExternalToolsSettings.sdkRootPath=Path.GetFullPath("../toolchain/android-sdk");
                AndroidExternalToolsSettings.jdkRootPath=Path.Combine(EditorApplication.applicationContentsPath,"PlaybackEngines/AndroidPlayer/OpenJDK");
                report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{SwanJourneyBootScene.Path,ScenePath},target=BuildTarget.Android,locationPathName=Output+"/SteamAtelier-V23-Swan.apk",options=development?BuildOptions.Development:BuildOptions.None});
            } finally {AndroidExternalToolsSettings.sdkRootPath=sdk;AndroidExternalToolsSettings.jdkRootPath=jdk;}
            long apkBytes=File.Exists(Output+"/SteamAtelier-V23-Swan.apk")?new FileInfo(Output+"/SteamAtelier-V23-Swan.apk").Length:0;
            File.WriteAllText(Output+"/build-result.json","{\"package\":\""+PackageId+"\",\"result\":\""+report.summary.result+"\",\"errors\":"+report.summary.totalErrors+",\"reportedBuildBytes\":"+report.summary.totalSize+",\"apkBytes\":"+apkBytes+"}");
            File.WriteAllText(Output+"/build-profile.json","{\"development\":"+(development?"true":"false")+",\"cppConfiguration\":\"Release\",\"codeGeneration\":\"OptimizeSpeed\",\"lowFrequencyCpuLogs\":true,\"nativePerformanceValidated\":false}");
            if(report.summary.result!=BuildResult.Succeeded||report.summary.totalErrors>0)throw new Exception("V23 build failed: "+report.summary.result+" errors="+report.summary.totalErrors);
            Debug.Log("SWAN_JOURNEY_BUILD_SUCCEEDED");
        }
        [Serializable] sealed class SceneReport { public string package;public int modules,finishing,hoverMeshes,renderers;public string[] sourceGroups,supportNames; }
    }
}
