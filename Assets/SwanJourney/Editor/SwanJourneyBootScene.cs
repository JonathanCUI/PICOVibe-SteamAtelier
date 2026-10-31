using System.IO;
using System.Linq;
using ByteDance.PICO.SpatialAdapter;
using SwanAtelier;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SwanJourney.Editor
{
    public static class SwanJourneyBootScene
    {
        public const string Path = "Assets/SwanJourney/Scenes/SteamJourneyBoot.unity";
        public static int Generate(TMP_FontAsset font)
        {
            var original = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var root = new GameObject("Swan loading view"); root.transform.position = new Vector3(0,1.35f,1.1f);
                var view = root.AddComponent<SwanJourneyLoadingView>();
                var panel = new GameObject("Loading panel"); panel.transform.SetParent(root.transform,false); view.surface = panel;
                var backdrop = Material("LoadingBackdrop",new Color(.025f,.035f,.045f));
                var ink = Material("LoadingAccent",new Color(.85f,.66f,.32f));
                Cube("Loading background",new Vector3(0,0,.005f),new Vector3(.60f,.22f,.012f),backdrop);
                Cube("Loading track",new Vector3(0,-.045f,-.007f),new Vector3(.48f,.009f,.003f),Material("LoadingTrack",new Color(.13f,.16f,.18f)));
                view.fill = Cube("Resource phase progress",new Vector3(-.2395f,-.045f,-.011f),new Vector3(.001f,.009f,.003f),ink).transform;
                view.workshopTitle = Label("正在开启蒸汽工坊",.045f,.05f);
                view.canyonTitle = Label("正在载入山谷铁路",.045f,.05f);
                view.resourceLabel = Label("1 / 2 · 载入场景资源",.025f,-.015f);
                view.nativeLabel = Label("2 / 2 · 准备空间显示，请稍候",.025f,-.015f);
                view.failedLabel = Label("载入未完成，请重新打开应用",.025f,-.015f);
                view.Show(false);
                // Inactive phase labels also need a valid mesh buffer when the
                // adapter first discovers them, just like workshop TMP labels.
                var textBridge = root.AddComponent<SwanJourneyNativeUi>();
                textBridge.content = root.transform; textBridge.panelMaterial = backdrop;
                textBridge.PrepareForExport();
                var camera = new GameObject("PICO Swan V23 Mixed Stage").AddComponent<SpatialCamera>();
                camera.OutputConfiguration=AssetDatabase.LoadAssetAtPath<SpatialCameraConfiguration>("Packages/com.bytedance.pico.spatialadapter/Resources/Default Unconstrained Spatial Camera Configuration.asset");
                camera.OpenWindowOnLoad=true;camera.Dimensions=Vector3.one;camera.gameObject.AddComponent<SwanSpatialInputMode>();
                var bootstrap = root.AddComponent<SwanJourneyBootstrap>(); bootstrap.loading=view;bootstrap.workshopScene=SwanJourneyBuild.ScenePath;
                EditorSceneManager.SaveScene(scene,Path);
                int count=scene.GetRootGameObjects().Sum(r=>r.GetComponentsInChildren<Transform>(true).Length);
                File.WriteAllText(SwanJourneyBuild.Output+"/boot-scene.json","{\"nodes\":"+count+",\"canyonPreloaded\":false,\"nativeProgressAvailable\":false}");
                return count;

                GameObject Cube(string name,Vector3 pos,Vector3 scale,Material material)
                {
                    var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(panel.transform,false);g.transform.localPosition=pos;g.transform.localScale=scale;
                    Object.DestroyImmediate(g.GetComponent<Collider>());g.GetComponent<MeshRenderer>().sharedMaterial=material;return g;
                }
                GameObject Label(string value,float size,float y)
                {
                    var g=new GameObject(value);g.transform.SetParent(panel.transform,false);g.transform.localPosition=new Vector3(0,y,-.014f);
                    var text=g.AddComponent<TextMeshPro>();text.font=font;text.text=value;text.fontSize=size;text.isOrthographic=true;text.alignment=TextAlignmentOptions.Center;text.color=new Color(.93f,.94f,.96f);text.rectTransform.sizeDelta=new Vector2(.56f,.055f);
                    text.enableWordWrapping=false;text.overflowMode=TextOverflowModes.Overflow;
                    text.ForceMeshUpdate(true,true);
                    // Orthographic TMP uses metre-sized glyphs here, not the
                    // desktop perspective label's 0.1 conversion. Fit the actual
                    // glyph bounds as well, including this project's CJK metrics.
                    var bounds=text.mesh.bounds;
                    float fit=Mathf.Min(1f,Mathf.Min(.52f/Mathf.Max(.0001f,bounds.size.x),
                        (y>0?.030f:.018f)/Mathf.Max(.0001f,bounds.size.y)));
                    text.fontSize*=fit;text.ForceMeshUpdate(true,true);
                    text.havePropertiesChanged=false;return g;
                }
            }
            finally { EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(original); }
        }
        static Material Material(string name,Color color)
        {
            string path=SwanJourneyBuild.Generated+"/"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat){mat=new Material(Shader.Find("Unlit/Color"));AssetDatabase.CreateAsset(mat,path);}
            mat.color=color;EditorUtility.SetDirty(mat);return mat;
        }
    }
}
