using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ByteDance.PICO.SpatialAdapter;
using ByteDance.PICO.SpatialAdapter.Component;
using ByteDance.PICO.XR;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;

namespace SwanAtelier.Editor
{
    public static partial class SwanAtelierBuild
    {
        const string Root = "Assets/SwanAtelier";
        const string Generated = Root + "/Generated";
        public const string ScenePath = Root + "/Scenes/SteamAtelierSwan.unity";
        const string Output = "Builds/SwanAtelier";
        static readonly Dictionary<Material, Material> materials = new Dictionary<Material, Material>();
        static int meshNumber, materialNumber;
        static SwanAtelierController game;
        [MenuItem("Steam Atelier/Swan/Generate")]
        public static void Generate()
        {
            Directory.CreateDirectory(Generated); Directory.CreateDirectory(Root + "/Scenes"); Directory.CreateDirectory(Output);
            materials.Clear(); meshNumber = materialNumber = 0; Configure();
            var scene = EditorSceneManager.OpenScene("Assets/Atelier/Scenes/SteamAtelier.unity");
            var engine = UnityEngine.Object.FindObjectOfType<LocomotiveController>();
            var atelier = UnityEngine.Object.FindObjectOfType<AtelierExperience>();
            if (!engine || !atelier) throw new Exception("Saved partner atelier scene is incomplete");
            var free = engine.GetComponent<LocomotiveFreeAssembly>();
            if (free.pieces.Length != 45) throw new Exception("Expected 45 partner assembly groups");
            var atelierRoot = scene.GetRootGameObjects().Single(go=>go.name=="Steam Atelier · Tabletop Workshop").transform;
            var display = engine.display;
            long sourceTriangles = Triangles(display);
            foreach (var b in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true))
            {
                if (b is AtelierExperience || b is LocomotiveFreeAssembly) b.enabled = false;
                else if (b is LocomotiveAR || b is LocomotiveSpatialBridge || b is LocomotiveWorkshop ||
                         b is LocomotiveBrakeSparksV11 || b is TrialAR || b is V9Dashboard || b is LocomotiveXRQuality)
                    UnityEngine.Object.DestroyImmediate(b);
            }
            foreach (var c in UnityEngine.Object.FindObjectsOfType<Camera>(true)) c.enabled = false;
            foreach (var l in UnityEngine.Object.FindObjectsOfType<Light>(true)) l.enabled = false;
            engine.environment = null; engine.workshop = null;
            var thermal = engine.GetComponent<LocomotiveThermalEffects>(); thermal.workshop = null;
            engine.externalView = true; engine.inputEnabled = false; engine.adaptiveDetail = false;
            engine.phaseSlider = engine.speedSlider = null;
            engine.titleText = engine.lessonText = engine.metricsText = engine.partText = engine.statusText = null;
            engine.travel = engine.running = engine.exploded = engine.assemblyLesson = engine.principle = false; engine.travelled = 0;
            free.consolePrefab = null; free.ghostMaterial = null; free.font = null; free.desktopInput = false;
            free.slotOccupants = Enumerable.Range(0,45).ToArray(); free.pieceSlots = Enumerable.Range(0,45).ToArray();
            foreach (var p in free.pieces) p.installed = true;
            engine.ApplyPose();
            var stage = new GameObject("STEAM ATELIER · Swan 新版"); stage.transform.position = new Vector3(0, .95f, 1.45f);
            game = stage.AddComponent<SwanAtelierController>();
            var content = new GameObject("Partner authored composition").transform; content.SetParent(stage.transform, false);
            // Keep the saved authored composition, including its manually removed table and room.
            display.SetParent(content, false); atelierRoot.SetParent(content, false);
            content.localScale = Vector3.one * .12f; content.localRotation = Quaternion.Euler(0,180,0);
            game.content = content; game.engine = engine; game.assembly = free; game.atelier = atelier; game.thermal = thermal;
            game.drive = engine.GetComponent<LocomotiveDriveV9>(); game.presentation = engine.GetComponent<LocomotivePresentationV4>();
            game.doors = engine.GetComponent<LocomotiveDoorsV11>();
            foreach (var root in scene.GetRootGameObjects())
                if (root != stage && root != engine.transform.root.gameObject) UnityEngine.Object.DestroyImmediate(root);
            atelier.assemblyText = atelier.helpText = atelier.hoverText = null;
            if (atelier.resetButton) atelier.resetButton.enabled = true;
            var atmosphere = engine.GetComponent<LocomotiveAtmosphereV5>();
            if (atmosphere) {
                atmosphere.enabled = false;
                foreach (var item in new[] { atmosphere.fireVolume, atmosphere.blastSteam, atmosphere.smokeboxGas, atmosphere.headlightBeam }) if (item) item.SetActive(false);
            }
            int mobileBindings = SelectPartnerMeshes(content);
            ConfigureCutawaySteam(engine,thermal,content);
            var texts = content.GetComponentsInChildren<TMPro.TMP_Text>(true);
            foreach (var r in content.GetComponentsInChildren<Renderer>(true))
                if (!r.GetComponent<TMPro.TMP_Text>()) r.sharedMaterials = r.sharedMaterials.Select(ConvertMaterial).ToArray();
            ConfigureSteam(thermal, display);
            // Hidden source meshes must not be re-exported alongside their optimized replacements.
            RemoveHiddenMeshes(content);
            var protectedTransforms = new HashSet<Transform> { display, engine.locomotive, content, atelierRoot };
            var protectedRenderers = new HashSet<Renderer>();
            foreach (var component in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true)) {
                if (!component) continue;
                var so = new SerializedObject(component); var property = so.GetIterator();
                while (property.Next(true)) if (property.propertyType == SerializedPropertyType.ObjectReference) {
                    var value = property.objectReferenceValue;
                    if (value is Transform t) protectedTransforms.Add(t);
                    if (value is GameObject go) protectedTransforms.Add(go.transform);
                    if (value is Renderer r) { protectedRenderers.Add(r); protectedTransforms.Add(r.transform); }
                    if (value is Component c) protectedTransforms.Add(c.transform);
                }
            }
            foreach (var part in engine.parts) if (part) protectedTransforms.Add(part.transform);
            foreach (var control in content.GetComponentsInChildren<AtelierControl>(true)) protectedTransforms.Add(control.transform);
            foreach (var label in texts) { protectedTransforms.Add(label.transform); var r=label.GetComponent<Renderer>(); if(r) protectedRenderers.Add(r); }
            CombineStatic(content, protectedTransforms, protectedRenderers);
            for (int i=0; i<free.pieces.Length; i++) {
                var clone=UnityEngine.Object.Instantiate(free.pieces[i].prefab); clone.name="SwanPiece_"+free.pieces[i].key;
                clone.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity); clone.transform.localScale=Vector3.one;
                SelectPartnerMeshes(clone.transform); RemoveHiddenMeshes(clone.transform);
                foreach(var r in clone.GetComponentsInChildren<Renderer>(true)) r.sharedMaterials=r.sharedMaterials.Select(ConvertMaterial).ToArray();
                foreach(var c in clone.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
                foreach(var b in clone.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(b);
                CombineStatic(clone.transform,new HashSet<Transform>{clone.transform},new HashSet<Renderer>());
                ConfigureMeshInteraction(clone.GetComponentsInChildren<MeshRenderer>(true));
                if(!clone.GetComponent<SpatialAdapterHoverEffect>())clone.AddComponent<SpatialAdapterHoverEffect>();
                free.pieces[i].prefab=PrefabUtility.SaveAsPrefabAsset(clone,Generated+"/Piece_"+i.ToString("00")+".prefab");
                UnityEngine.Object.DestroyImmediate(clone);
            }
            BuildPlacementGhosts(free);
            foreach(var c in display.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
            // The native hit target must carry its visible mesh and hover effect.
            // A large collider on an empty part parent can intercept a gaze without
            // highlighting the descendant mesh, and can cover neighbouring parts.
            ConfigureMeshInteraction(display.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r=>r.GetComponentInParent<LocomotivePart>()));
            var brass=Material("Swan brass",new Color(.7f,.42f,.16f),.75f,.6f);
            game.socket=Shape("装配接口",stage.transform,PrimitiveType.Sphere,brass,Vector3.zero,Vector3.one*.018f).transform; game.socket.gameObject.SetActive(false);
            game.help=Text(stage.transform,new Vector3(0,.56f,0),"蒸汽工坊·新版 · 45 个部件",14,new Vector2(1.3f,.13f),new Color(1,.88f,.68f));
            for(int button=0;button<3;button++) {
                bool explode=button==0,move=button==2;
                Vector3 position=new Vector3(-.81f+button*.54f,-.15f,-.64f); string label=explode?"一键拆开":move?"捏住移动展台":"全部装好";
                var go=Shape(label,stage.transform,PrimitiveType.Cube,brass,position,new Vector3(.48f,.055f,.025f));
                go.AddComponent<BoxCollider>(); var target=go.AddComponent<SwanAtelierTarget>(); target.moveStage=move; target.explodeAll=explode;
                Text(stage.transform,position+Vector3.back*.016f,label,13,new Vector2(.47f,.05f),Color.white);
            }
            ConvertText(texts);
            ConfigureHover(stage.transform);
            ConfigureConsoleInteractions(stage.transform);
            var cameraGO=new GameObject("Swan Editor Preview Camera",typeof(Camera),typeof(AudioListener));
            var camera=cameraGO.GetComponent<Camera>(); camera.tag="MainCamera";
            camera.transform.position=new Vector3(-1.3f,2.0f,-1.35f); camera.transform.LookAt(stage.transform.position+new Vector3(0,.07f,-.12f));
            camera.nearClipPlane=.02f; camera.farClipPlane=25; camera.fieldOfView=43;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.065f,.083f,.08f);
            game.previewCamera=camera; engine.studioCamera=camera;
            var spatial=new GameObject("PICO Swan Mixed Stage").AddComponent<SpatialCamera>();
            spatial.OutputConfiguration=AssetDatabase.LoadAssetAtPath<SpatialCameraConfiguration>("Packages/com.bytedance.pico.spatialadapter/Resources/Default Unconstrained Spatial Camera Configuration.asset");
            spatial.OpenWindowOnLoad=true; spatial.Dimensions=Vector3.one;
            if (!spatial.GetComponent<SwanSpatialInputMode>())
                spatial.gameObject.AddComponent<SwanSpatialInputMode>();
            var hudRoot=new GameObject("Swan view-following FPS");
            // A safe initial location above the application until the first HMD pose arrives.
            hudRoot.transform.position=new Vector3(-.85f,1.65f,1.15f);
            var hud=hudRoot.AddComponent<SwanFrameRateHud>(); hud.editorPreviewCamera=camera;
            var fpsMaterial=Material("FPS soft grey glyphs",new Color(.72f,.75f,.78f),0,0);
            fpsMaterial.SetColor("_EmissionColor",new Color(.55f,.60f,.62f));fpsMaterial.EnableKeyword("_EMISSION");
            hud.meshText=SwanFpsMeshBuilder.Create(Generated,fpsMaterial);
            hud.meshText.transform.position=hudRoot.transform.position;
            // The HUD has no collider, so it can never intercept a gaze/pinch meant for the model.
            Directional("Warm key",new Vector3(42,-35,0),new Color(1,.88f,.72f),1.25f);
            Directional("Cool fill",new Vector3(32,140,0),new Color(.66f,.8f,1),.6f);
            RenderSettings.ambientMode=AmbientMode.Trilight; RenderSettings.ambientSkyColor=new Color(.6f,.66f,.7f);
            RenderSettings.ambientEquatorColor=new Color(.39f,.40f,.36f); RenderSettings.ambientGroundColor=new Color(.22f,.22f,.19f); RenderSettings.skybox=null;
            foreach(var data in UnityEngine.Object.FindObjectsOfType<UniversalAdditionalCameraData>(true)) UnityEngine.Object.DestroyImmediate(data);
            foreach(var volume in UnityEngine.Object.FindObjectsOfType<Volume>(true)) volume.enabled=false;
            EditorSceneManager.SaveScene(scene,ScenePath); EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)}; AssetDatabase.SaveAssets();
            long triangles=Triangles(content); int renderers=content.GetComponentsInChildren<MeshRenderer>(true).Count(r=>r.enabled&&r.gameObject.activeInHierarchy);
            File.WriteAllText(Output+"/scene-budget.json","{\"sourceModelTriangles\":"+sourceTriangles+",\"visibleTrianglesIncludingControls\":"+triangles+",\"visibleRenderers\":"+renderers+",\"partnerMobileBindings\":"+mobileBindings+",\"pieces\":45,\"scale\":0.12}");
            Debug.Log("ATELIER_SWAN_SCENE_GENERATED triangles="+triangles+" renderers="+renderers+" mobileBindings="+mobileBindings);
            Preview(camera);
        }
        static long Triangles(Transform root) => root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh&&f.gameObject.activeInHierarchy&&f.GetComponent<MeshRenderer>()&&f.GetComponent<MeshRenderer>().enabled).Sum(f=>(long)f.sharedMesh.triangles.Length/3);
        static void ConfigureMeshInteraction(IEnumerable<MeshRenderer> renderers)
        {
            foreach(var renderer in renderers) {
                var filter=renderer.GetComponent<MeshFilter>();
                if(!renderer.enabled||renderer.GetComponent<TMPro.TMP_Text>()||!filter||!filter.sharedMesh||filter.sharedMesh.vertexCount==0)continue;
                var box=renderer.gameObject.AddComponent<BoxCollider>();
                box.center=filter.sharedMesh.bounds.center;
                box.size=Vector3.Max(filter.sharedMesh.bounds.size,Vector3.one*.04f);
                if(!renderer.GetComponent<SpatialAdapterHoverEffect>())renderer.gameObject.AddComponent<SpatialAdapterHoverEffect>();
            }
        }
        static void ConfigureHover(Transform stage)
        {
            var controls=stage.GetComponentsInChildren<AtelierControl>(true);
            var targets=controls.Select(c=>c.gameObject)
                .Concat(stage.GetComponentsInChildren<LocomotivePart>(true).Where(p=>p.GetComponentInChildren<Collider>(true)).Select(p=>p.gameObject))
                .Concat(stage.GetComponentsInChildren<SwanAtelierTarget>(true).Select(t=>t.gameObject)).Distinct().ToArray();
            var buttonMaterial=Material("Hoverable category plate",new Color(.025f,.055f,.067f),.3f,.4f);
            foreach(var target in targets) {
                if(!target.GetComponentInChildren<Collider>(true))throw new Exception("Hover target has no collider: "+target.name);
                bool hasSurface=target.GetComponentsInChildren<MeshRenderer>(true).Any(r=>r.enabled&&!r.GetComponent<TMPro.TMP_Text>());
                // The four category words originally have only text; give each its own
                // small plate so the native mesh highlighter has a concrete surface.
                if(!hasSurface && target.TryGetComponent<AtelierControl>(out var control)) {
                    var box=target.GetComponent<BoxCollider>();
                    if(!box)throw new Exception("Text-only target needs a box: "+target.name);
                    var size=new Vector3(box.size.x*.98f,.008f,box.size.z*.98f);
                    Shape("Category hover surface",target.transform,PrimitiveType.Cube,buttonMaterial,new Vector3(box.center.x,.004f,box.center.z),size);
                }
                if(!target.GetComponent<SpatialAdapterHoverEffect>())target.AddComponent<SpatialAdapterHoverEffect>();
            }
            int meshTargets=stage.GetComponentsInChildren<MeshRenderer>(true).Count(r=>r.GetComponentInParent<LocomotivePart>()&&r.GetComponent<Collider>()&&r.GetComponent<SpatialAdapterHoverEffect>());
            File.WriteAllText(Output+"/hover-coverage.json",JsonUtility.ToJson(new HoverCoverage{sceneTargets=targets.Length,controls=controls.Length,piecePrefabs=45,partMeshTargets=meshTargets,targetNames=targets.Select(t=>t.name).ToArray()},true));
            Debug.Log("ATELIER_SWAN_HOVER_CONFIGURED sceneTargets="+targets.Length+" controls="+controls.Length+" piecePrefabs=45");
        }
        [Serializable] class HoverCoverage { public int sceneTargets,controls,piecePrefabs,partMeshTargets; public string[] targetNames; }
        static int SelectPartnerMeshes(Transform root)
        {
            int count=0;
            foreach(var binding in root.GetComponentsInChildren<LocomotiveXRMesh>(true)) {
                var filter=binding.GetComponent<MeshFilter>();
                if(filter && binding.levels!=null && binding.levels.Length>0) { filter.sharedMesh=binding.levels[Math.Min(1,binding.levels.Length-1)]; count++; }
                UnityEngine.Object.DestroyImmediate(binding);
            }
            foreach(var lod in root.GetComponentsInChildren<LODGroup>(true)) UnityEngine.Object.DestroyImmediate(lod);
            return count;
        }
        static void RemoveHiddenMeshes(Transform root)
        {
            foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true)) if(!r.enabled&&!r.GetComponent<TMPro.TMP_Text>()) {
                var f=r.GetComponent<MeshFilter>(); if(f)UnityEngine.Object.DestroyImmediate(f); UnityEngine.Object.DestroyImmediate(r);
            }
        }
        static void ConvertText(TMPro.TMP_Text[] labels)
        {
            Canvas.ForceUpdateCanvases();
            foreach(var source in labels) {
                if(!source)continue;
                var rect=source.rectTransform; var scale=source.transform.lossyScale;
                var bounds=Vector2.Scale(rect.rect.size,new Vector2(Mathf.Abs(scale.x),Mathf.Abs(scale.y)));
                bounds=Vector2.Max(bounds,new Vector2(.018f,.008f));
                var go=new GameObject("Swan label "+source.name); go.transform.SetParent(source.transform,false);
                go.transform.localPosition=new Vector3(rect.rect.center.x,rect.rect.center.y,-.002f);
                go.transform.localScale=new Vector3(1/Mathf.Abs(scale.x),1/Mathf.Abs(scale.y),1/Mathf.Abs(scale.z));
                var native=go.AddComponent<SpatialAdapterNativeText>(); native.Text=source.text; native.TextColor=source.color;
                native.TextSize=Mathf.Clamp(Mathf.RoundToInt(source is TMPro.TextMeshProUGUI?source.fontSize*.45f:source.fontSize*12),7,18);
                native.BackgroundSize=bounds; native.BackgroundColor=Color.clear; native.Justification=TextJustification.Center;
                var mirror=go.AddComponent<SwanTextMirror>(); mirror.source=source; mirror.output=native;
                source.enabled=false; var renderer=source.GetComponent<MeshRenderer>(); if(renderer)renderer.enabled=false;
            }
            foreach(var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>(true)) canvas.enabled=false;
        }
        static Bounds LocalBounds(Transform root, IEnumerable<MeshRenderer> renderers)
        {
            bool first = true; var bounds = new Bounds();
            foreach (var r in renderers) { var f = r.GetComponent<MeshFilter>(); if (!f || !f.sharedMesh) continue;
                var b = f.sharedMesh.bounds; var matrix = root.worldToLocalMatrix * f.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++) { var point = matrix.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))); if (first) { bounds = new Bounds(point, Vector3.zero); first = false; } else bounds.Encapsulate(point); }
            } return bounds;
        }
        static void CombineStatic(Transform root, HashSet<Transform> protectedTransforms, HashSet<Renderer> protectedRenderers)
        {
            var groups = new Dictionary<Transform, List<MeshFilter>>();
            foreach (var f in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var r = f.GetComponent<MeshRenderer>();
                if (!f.sharedMesh || !r || !r.enabled || !f.gameObject.activeInHierarchy || protectedRenderers.Contains(r) || protectedTransforms.Contains(f.transform)) continue;
                var anchor = f.transform.parent;
                while (anchor && anchor != root && !protectedTransforms.Contains(anchor)) anchor = anchor.parent;
                if (!anchor) anchor = root;
                if (!groups.ContainsKey(anchor)) groups[anchor] = new List<MeshFilter>(); groups[anchor].Add(f);
            }
            foreach (var group in groups)
            {
                if (group.Value.Count < 2) continue;
                var byMaterial = new Dictionary<Material, List<CombineInstance>>();
                foreach (var filter in group.Value)
                {
                    var mats = filter.GetComponent<MeshRenderer>().sharedMaterials;
                    for (int sub = 0; sub < filter.sharedMesh.subMeshCount; sub++)
                    {
                        var mat = mats[Mathf.Min(sub, mats.Length - 1)]; if (!mat) continue;
                        if (!byMaterial.ContainsKey(mat)) byMaterial[mat] = new List<CombineInstance>();
                        byMaterial[mat].Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = sub, transform = group.Key.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                    }
                }
                var subMeshes = new List<CombineInstance>(); var temp = new List<Mesh>();
                foreach (var pair in byMaterial) { var mesh = new Mesh { indexFormat = IndexFormat.UInt32 }; mesh.CombineMeshes(pair.Value.ToArray(), true, true); temp.Add(mesh); subMeshes.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.identity }); }
                if (temp.Count == 0) continue;
                var combined = new Mesh { name = "Swan static group " + meshNumber, indexFormat = IndexFormat.UInt32 };
                combined.CombineMeshes(subMeshes.ToArray(), false, false); combined.RecalculateBounds();
                AssetDatabase.CreateAsset(combined, Generated + "/Mesh_" + (meshNumber++).ToString("0000") + ".asset");
                var go = new GameObject("Swan combined surfaces", typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(group.Key, false);
                go.GetComponent<MeshFilter>().sharedMesh = combined; go.GetComponent<MeshRenderer>().sharedMaterials = byMaterial.Keys.ToArray();
                foreach (var f in group.Value) { UnityEngine.Object.DestroyImmediate(f.GetComponent<MeshRenderer>()); UnityEngine.Object.DestroyImmediate(f); }
                foreach (var mesh in temp) UnityEngine.Object.DestroyImmediate(mesh);
            }
            // Empty source leaves no longer carry geometry or behaviour; prune them to reduce native scene nodes.
            foreach (var t in root.GetComponentsInChildren<Transform>(true).Reverse())
                if (t && t != root && !protectedTransforms.Contains(t) && t.childCount == 0 && t.GetComponents<Component>().Length == 1)
                    UnityEngine.Object.DestroyImmediate(t.gameObject);
        }
        static void BuildPlacementGhosts(LocomotiveFreeAssembly assembly)
        {
            // Bake transparent variants before SpatialAdapter exports the prefabs.
            // Reuse the mobile meshes; a runtime material swap would leave the
            // native prefab dependent on an unexported material.
            var material = new Material(Shader.Find("Standard")) {
                name = "Swan placement silhouette", color = new Color(1f, 1f, 1f, .34f)
            };
            material.SetFloat("_Metallic", 0); material.SetFloat("_Glossiness", 0);
            material.SetOverrideTag("RenderType", "Transparent"); material.SetFloat("_Mode", 3);
            material.SetInt("_SrcBlend", (int)BlendMode.One);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0); material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetColor("_EmissionColor", new Color(.12f, .12f, .12f));
            material.EnableKeyword("_EMISSION"); material.renderQueue = 3000;
            AssetDatabase.CreateAsset(material, Generated + "/PlacementGhost.mat");
            assembly.ghostMaterial = material;
            game.placementGhostPrefabs = new GameObject[assembly.pieces.Length];
            for (int i = 0; i < assembly.pieces.Length; i++) {
                var ghost = UnityEngine.Object.Instantiate(assembly.pieces[i].prefab);
                ghost.name = "Swan placement ghost " + assembly.pieces[i].key;
                foreach (var collider in ghost.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                foreach (var behaviour in ghost.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(behaviour);
                foreach (var t in ghost.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
                foreach (var renderer in ghost.GetComponentsInChildren<Renderer>(true)) {
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
                    renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                }
                game.placementGhostPrefabs[i] = PrefabUtility.SaveAsPrefabAsset(ghost, Generated + "/Ghost_" + i.ToString("00") + ".prefab");
                UnityEngine.Object.DestroyImmediate(ghost);
            }
            Debug.Log("ATELIER_SWAN_PLACEMENT_GHOSTS prefabs=" + game.placementGhostPrefabs.Length + " alpha=" + material.color.a);
        }
        static Material ConvertMaterial(Material source)
        {
            if (!source) return null; if (materials.TryGetValue(source, out var existing)) return existing;
            Color color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.HasProperty("_Color") ? source.color : Color.gray;
            var result = Material("Converted_" + source.name, color, source.HasProperty("_Metallic") ? source.GetFloat("_Metallic") : 0,
                source.HasProperty("_Smoothness") ? source.GetFloat("_Smoothness") : source.HasProperty("_Glossiness") ? source.GetFloat("_Glossiness") : .35f);
            var texture = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
            if (texture) { result.mainTexture = texture; string property=source.HasProperty("_BaseMap")?"_BaseMap":"_MainTex"; result.mainTextureScale=source.GetTextureScale(property); result.mainTextureOffset=source.GetTextureOffset(property); }
            if (source.HasProperty("_BumpMap") && source.GetTexture("_BumpMap")) { result.SetTexture("_BumpMap", source.GetTexture("_BumpMap")); result.EnableKeyword("_NORMALMAP"); }
            if (source.HasProperty("_EmissionColor")) { var emission = source.GetColor("_EmissionColor"); result.SetColor("_EmissionColor", emission); if (emission.maxColorComponent > .01f) result.EnableKeyword("_EMISSION"); }
            if (color.a < .98f || source.GetTag("RenderType",false)=="Transparent") { result.SetOverrideTag("RenderType", "Transparent"); result.SetFloat("_Mode", 3); result.SetInt("_SrcBlend", (int)BlendMode.One); result.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); result.SetInt("_ZWrite", 0); result.EnableKeyword("_ALPHAPREMULTIPLY_ON"); result.renderQueue = 3000; }
            materials[source] = result; return result;
        }
        static Material Material(string name, Color color, float metallic, float smoothness)
        {
            var mat = new Material(Shader.Find("Standard")) { name = name, color = color }; mat.SetFloat("_Metallic", metallic); mat.SetFloat("_Glossiness", smoothness);
            AssetDatabase.CreateAsset(mat, Generated + "/Material_" + (materialNumber++).ToString("000") + ".mat"); return mat;
        }
        static void ConfigureSteam(LocomotiveThermalEffects thermal, Transform model)
        {
            const int size = 128;
            var image = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float u = (x + .5f) / size * 2 - 1, v = (y + .5f) / size * 2 - 1;
                float edge = Mathf.Clamp01(1 - u * u - v * v);
                float alpha = edge * edge * (.65f + .35f * Mathf.PerlinNoise(u * 3 + 10, v * 3 + 10));
                image.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
            image.Apply(); string texturePath = Generated + "/SteamSoftAlpha.png";
            File.WriteAllBytes(texturePath, image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath); importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true; importer.wrapMode = TextureWrapMode.Clamp; importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            var mat = Material("Swan Soft Steam Alpha", new Color(.88f, .92f, .95f, .65f), 0, 0);
            mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            mat.SetOverrideTag("RenderType", "Transparent"); mat.SetFloat("_Mode", 2);
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0); mat.EnableKeyword("_ALPHABLEND_ON"); mat.renderQueue = 3000;
            foreach (var ps in model.GetComponentsInChildren<ParticleSystem>(true))
            {
                // Native particle export does not treat Renderer.enabled as a reliable exclusion.
                bool visible = ps == thermal.smoke || ps == thermal.exhaust;
                var renderer = ps.GetComponent<ParticleSystemRenderer>(); if (renderer) { renderer.sharedMaterial = mat; renderer.enabled = visible; renderer.renderMode = ParticleSystemRenderMode.Billboard; }
                if (!visible) { ps.gameObject.SetActive(false); continue; }
                ps.gameObject.SetActive(true); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main; main.maxParticles = ps == thermal.smoke ? 28 : 18;
                main.startLifetime = ps == thermal.smoke ? 2.0f : .8f; main.startSize = new ParticleSystem.MinMaxCurve(.25f, .55f);
                main.startSpeed = .65f; main.startColor = Color.white; main.gravityModifier = 0;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy; main.simulationSpace = ParticleSystemSimulationSpace.Local;
                var emission = ps.emission; emission.rateOverTime = ps == thermal.smoke ? 12 : 0;
                var velocity = ps.velocityOverLifetime; velocity.enabled = false;
                var colors = ps.colorOverLifetime; colors.enabled = true;
                var fade = new Gradient(); fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                    new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.65f, .18f), new GradientAlphaKey(0, 1) }); colors.color = fade;
                main.playOnAwake = true;
            }
        }
        static GameObject Shape(string name, Transform parent, PrimitiveType type, Material mat, Vector3 position, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); go.GetComponent<Renderer>().sharedMaterial = mat; return go;
        }
        static SpatialAdapterNativeText Text(Transform parent, Vector3 position, string value, int size, Vector2 bounds, Color color)
        {
            var go = new GameObject("Label " + value); go.transform.SetParent(parent, false); go.transform.localPosition = position;
            var text = go.AddComponent<SpatialAdapterNativeText>(); text.Text = value; text.TextSize = size; text.TextColor = color;
            text.Justification = TextJustification.Center; text.BackgroundColor = Color.clear; text.BackgroundSize = bounds; return text;
        }
        static void Directional(string name, Vector3 angles, Color color, float intensity)
        { var light = new GameObject(name).AddComponent<Light>(); light.type = LightType.Directional; light.transform.eulerAngles = angles; light.color = color; light.intensity = intensity; light.shadows = LightShadows.None; }
        static void Configure()
        {
            PlayerSettings.companyName = "PICO Vibe"; PlayerSettings.productName = "蒸汽工坊·新版";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.picovibe.steamatelier"); PlayerSettings.bundleVersion = "0.4.3"; PlayerSettings.Android.bundleVersionCode = 17;
            // Match the SDK's default launch container to the explicit scene camera.
            // The SDK manifest processor derives the launch metadata from this setting.
            var runtimeSettings = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Settings/SpatialAdapterRuntimeSettings.asset"));
            runtimeSettings.FindProperty("m_defaultSpatialCameraConfiguration").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<SpatialCameraConfiguration>("Packages/com.bytedance.pico.spatialadapter/Resources/Default Unconstrained Spatial Camera Configuration.asset");
            runtimeSettings.ApplyModifiedPropertiesWithoutUndo();
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29; PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP); PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false); PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.colorSpace = ColorSpace.Linear; PlayerSettings.runInBackground = true;
            PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android, "PICO_MS_SDK");
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]); settings.FindProperty("activeInputHandler").intValue = 1; settings.ApplyModifiedPropertiesWithoutUndo();
            GraphicsSettings.defaultRenderPipeline = null;
            int current = QualitySettings.GetQualityLevel(); for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = null; } QualitySettings.SetQualityLevel(current, false);
            QualitySettings.shadows = UnityEngine.ShadowQuality.Disable; QualitySettings.shadowDistance = 8; QualitySettings.antiAliasing = 4;
            // PICO_MS_SDK makes this loader initialize Spatial's input subsystem only;
            // SpatialAdapter still owns native rendering. ARCore remains disabled.
            var picoConfig=PXR_ProjectSetting.GetProjectConfig();picoConfig.isSpatialAdapter=true;
            picoConfig.arFoundation=false;picoConfig.openMRC=false;EditorUtility.SetDirty(picoConfig);
            var xr = UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            if (!xr || !xr.Manager) throw new Exception("Android XR manager settings missing");
            foreach(var loader in xr.Manager.activeLoaders.ToArray()) if(!(loader is PXR_Loader))xr.Manager.TryRemoveLoader(loader);
            if(!UnityEditor.XR.Management.Metadata.XRPackageMetadataStore.AssignLoader(xr.Manager,"ByteDance.PICO.XR.PXR_Loader",BuildTargetGroup.Android))
                throw new Exception("Could not configure PICO Spatial input loader");
            xr.InitManagerOnStart=true;xr.Manager.automaticLoading=true;xr.Manager.automaticRunning=true;
            EditorUtility.SetDirty(xr);EditorUtility.SetDirty(xr.Manager);
        }
        static void Preview(Camera camera)
        {
            var rt = RenderTexture.GetTemporary(1600, 1000, 24); var previous = RenderTexture.active;
            try { camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt; var image = new Texture2D(1600, 1000, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); image.Apply(); File.WriteAllBytes(Output + "/editor-preview.png", image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image); }
            finally { camera.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); }
        }
        [MenuItem("Steam Workshop/Swan/2 Build Android APK")]
        public static void BuildAndroid()
        {
            Generate(); string sdk = AndroidExternalToolsSettings.sdkRootPath, jdk = AndroidExternalToolsSettings.jdkRootPath;
            // SpatialAdapter copies its entire export cache, including dependencies removed from the scene.
            // This directory contains only reproducible exports for this project.
            string tempRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp")) + Path.DirectorySeparatorChar;
            string cache = Path.GetFullPath(Path.Combine(tempRoot, "SpatialAdapterExport"));
            if (!cache.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(cache) != "SpatialAdapterExport")
                throw new InvalidOperationException("Refusing to clean an export cache outside this project's Temp folder: " + cache);
            if (Directory.Exists(cache)) { Debug.Log("ATELIER_SWAN_CLEAR_EXPORT_CACHE " + cache); Directory.Delete(cache, true); }
            // Unity's incremental Gradle asset copy retains files that disappeared upstream.
            // Both locations below are generated by this build; never clear their parent directories.
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..")) + Path.DirectorySeparatorChar;
            foreach (string relative in new[] { "Assets/StreamingAssets/SpatialAdapterExport", "Library/Bee/Android/Prj/IL2CPP/Gradle/unityLibrary/src/main/assets/SpatialAdapterExport" })
            {
                string generated = Path.GetFullPath(Path.Combine(projectRoot, relative));
                if (!generated.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(generated) != "SpatialAdapterExport")
                    throw new InvalidOperationException("Unexpected generated asset path: " + generated);
                if (Directory.Exists(generated)) { Debug.Log("ATELIER_SWAN_CLEAR_STAGED_EXPORTS " + generated); Directory.Delete(generated, true); }
            }
            BuildReport report;
            try
            {
                AndroidExternalToolsSettings.sdkRootPath = Path.GetFullPath("../toolchain/android-sdk");
                AndroidExternalToolsSettings.jdkRootPath = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines/AndroidPlayer/OpenJDK");
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, target = BuildTarget.Android, locationPathName = Output + "/SteamAtelier-Swan.apk", options = BuildOptions.Development });
            }
            finally { AndroidExternalToolsSettings.sdkRootPath = sdk; AndroidExternalToolsSettings.jdkRootPath = jdk; }
            File.WriteAllText(Output + "/build-result.json", "{\"result\":\"" + report.summary.result + "\",\"errors\":" + report.summary.totalErrors + ",\"bytes\":" + report.summary.totalSize + "}");
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Swan build failed: " + report.summary.result);
            Debug.Log("ATELIER_SWAN_BUILD_SUCCEEDED");
        }
    }
}
