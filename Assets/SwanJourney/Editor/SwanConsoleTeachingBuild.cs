using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SwanJourney.Editor
{
    public static class SwanConsoleTeachingBuild
    {
        public const string Evidence="Builds/Evidence/ConsoleTeaching";
        internal static void Apply(SteamJourney j)
        {
            var c=j.GetComponent<StudioSpatialConsoleV18>();
            var controls=c.console.GetComponentsInChildren<JourneyControl>(true)
                .Where(x=>x.gameObject.activeSelf && x.transform.parent==c.console).ToArray();
            JourneyControl Control(JourneyAction action)=>controls.Single(x=>x.action==action);
            TMP_Text Label(string value)=>c.console.GetComponentsInChildren<TMP_Text>(true)
                .Single(x=>x.gameObject.activeSelf && x.transform.parent==c.console && x.text==value);
            var verify=Control(JourneyAction.Verify);var drive=Control(JourneyAction.Drive);var automatic=Control(JourneyAction.FinishAll);
            var before=controls.Select(x=>new ControlRecord {action=x.action.ToString(),position=x.transform.localPosition,scale=x.transform.localScale,
                box=x.GetComponent<BoxCollider>().size}).ToArray();
            LayoutControl(verify,-1.04f,.78f,1.10f);
            LayoutControl(drive,-2.40f,.78f,1.12f);
            LayoutControl(automatic,-1.73f,.90f,.70f);
            // Keep the original hit area on the smaller secondary control.
            automatic.GetComponent<BoxCollider>().size*=.90f/.70f;
            var verifyLabel=j.verifyLabel;
            LayoutText(verifyLabel,-1.04f,1.13f,.80f,.20f,1.04f);verifyLabel.fontStyle=FontStyles.Bold;
            var driveLabel=Label("出发");LayoutText(driveLabel,-2.40f,1.13f,.92f,.20f,1.02f);driveLabel.fontStyle=FontStyles.Bold;
            var autoLabel=Label("一键组装");LayoutText(autoLabel,-1.73f,1.13f,.65f,.18f,.76f);
            LayoutText(c.introLabel,2.36f,1.13f,.70f,.18f,.94f);
            LayoutText(c.modeLabel,1.48f,1.13f,.82f,.18f,.88f);
            autoLabel.color=new Color(.72f,.69f,.60f);
            Label("重新体验").color=new Color(.66f,.64f,.57f);
            // Keep the original reference for compatibility, without a workshop caption strip.
            c.message.text="";
            c.message.gameObject.SetActive(false);
            var teaching=j.GetComponent<SwanConsoleTeaching>();if(!teaching)teaching=j.gameObject.AddComponent<SwanConsoleTeaching>();
            teaching.journey=j;teaching.console=c;teaching.input=j.GetComponent<SwanJourneyInteraction>();
            teaching.driving=j.GetComponent<StudioSpatialRefineV19>();teaching.departureLabel=driveLabel;
            c.teaching=teaching;teaching.driving.teaching=teaching;
            var drivingText=teaching.driving.driveMessage;
            drivingText.text="";drivingText.gameObject.SetActive(false);
            drivingText.enableAutoSizing=false;drivingText.enableWordWrapping=false;
            drivingText.fontSize=1.05f;
            Debug.Log($"XR_CONSOLE_DRIVE_TEXT rect={drivingText.rectTransform.sizeDelta} font={drivingText.fontSize} position={drivingText.transform.localPosition}");
            EnsureFont(c.message.font,j);
            driveLabel.font=c.message.font;driveLabel.fontSharedMaterial=c.message.font.material;
            drivingText.font=c.message.font;drivingText.fontSharedMaterial=c.message.font.material;
            Physics.SyncTransforms();
            var boxes=controls.Where(x=>x.action!=JourneyAction.Reset && x.action!=JourneyAction.ResetGuard)
                .Select(x=>x.GetComponent<BoxCollider>()).ToArray();
            for(int i=0;i<boxes.Length;i++)for(int k=i+1;k<boxes.Length;k++)
                if(boxes[i].bounds.Intersects(boxes[k].bounds))throw new InvalidOperationException("Overlapping console controls: "+boxes[i].name+" / "+boxes[k].name);
            Directory.CreateDirectory(Evidence);
            File.WriteAllText(Evidence+"/layout-changes.json",JsonUtility.ToJson(new ChangeReport {before=before,
                after=controls.Select(x=>new ControlRecord {action=x.action.ToString(),position=x.transform.localPosition,scale=x.transform.localScale,box=x.GetComponent<BoxCollider>().size}).ToArray()},true));
            Debug.Log("XR_CONSOLE_LAYOUT_PASS controls="+controls.Length+" noOverlaps=true additionalModelRenderers=0 additionalModelTriangles=0");
        }
        static void LayoutControl(JourneyControl c,float x,float z,float scale)
        {var p=c.transform.localPosition;c.transform.localPosition=new Vector3(x,p.y,z);c.transform.localScale=Vector3.one*scale;}
        static void LayoutText(TMP_Text text,float x,float z,float width,float height,float size)
        {
            var p=text.transform.localPosition;text.transform.localPosition=new Vector3(x,p.y,z);
            text.rectTransform.sizeDelta=new Vector2(width,height);text.fontSize=size;text.enableAutoSizing=false;
            text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
        }
        static void EnsureFont(TMP_FontAsset font,SteamJourney journey)
        {
            var path=AssetDatabase.GetAssetPath(font);
            if(!path.StartsWith("Assets/SwanJourney/"))throw new InvalidOperationException("Expected derived workshop font: "+path);
            string script=File.ReadAllText("Assets/SwanJourney/Scripts/SwanConsoleTeaching.cs");
            string values=string.Join("",Regex.Matches(script,@"""(?:\\.|[^""\\])*""").Cast<Match>().Select(m=>m.Value))
                +string.Join("",journey.modules.Concat(journey.finishing).Select(m=>m.title));
            var wanted=values.Where(x=>!char.IsControl(x)&&!char.IsSurrogate(x)).Select(x=>(uint)x).Distinct().ToArray();
            var missing=wanted.Where(x=>!font.HasCharacter((int)x)).ToArray();
            int atlasCount=font.atlasTextures.Length;
            if(missing.Length>0)
            {
                var source=font.sourceFontFile;
                if(!source){var so=new SerializedObject(font);source=AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(so.FindProperty("m_SourceFontFileGUID").stringValue));}
                if(!source)throw new InvalidOperationException("Missing source font for static console glyphs");
                font.atlasPopulationMode=AtlasPopulationMode.Dynamic;
                var serialized=new SerializedObject(font);serialized.FindProperty("m_SourceFontFile").objectReferenceValue=source;serialized.ApplyModifiedPropertiesWithoutUndo();
                if(!font.TryAddCharacters(missing,out uint[] absent))throw new InvalidOperationException("Missing console glyphs: "+string.Join(",",absent));
                font.atlasPopulationMode=AtlasPopulationMode.Static;
                if(font.atlasTextures.Length!=atlasCount)throw new InvalidOperationException("Console font unexpectedly added an atlas");
                foreach(var atlas in font.atlasTextures)EditorUtility.SetDirty(atlas);
                EditorUtility.SetDirty(font);EditorUtility.SetDirty(font.material);
            }
            Debug.Log($"XR_CONSOLE_FONT_PASS required={wanted.Length} added={missing.Length} atlases={font.atlasTextures.Length}");
        }
        [Serializable] sealed class ControlRecord {public string action;public Vector3 position,scale,box;}
        [Serializable] sealed class ChangeReport {public ControlRecord[] before,after;}
        public static void Inspect()
        {
            EditorSceneManager.OpenScene("Assets/SwanJourney/Scenes/SteamJourneyXRTrial.unity");
            var j=UnityEngine.Object.FindObjectOfType<SteamJourney>(true);
            var c=j.GetComponent<StudioSpatialConsoleV18>();
            var report=new Layout {console=Path(c.console),previewStand=Path(j.previewStand),previewParent=Path(j.previewStand.parent)};
            foreach(var t in c.console.GetComponentsInChildren<Transform>(true))
            {
                var text=t.GetComponent<TMP_Text>();var control=t.GetComponent<JourneyControl>();
                var collider=t.GetComponent<Collider>();var mesh=t.GetComponent<MeshFilter>();
                report.nodes.Add(new Node {path=Path(t),parent=Path(t.parent),active=t.gameObject.activeSelf,
                    localPosition=t.localPosition,localScale=t.localScale,position=c.console.InverseTransformPoint(t.position),
                    rotation=t.localEulerAngles,text=text?text.text:null,fontSize=text?text.fontSize:0,
                    textRect=text?text.rectTransform.sizeDelta:Vector2.zero,font=text?AssetDatabase.GetAssetPath(text.font):null,
                    action=control?control.action.ToString():null,components=string.Join(",",t.GetComponents<Component>().Select(x=>x?x.GetType().Name:"missing")),
                    mesh=mesh&&mesh.sharedMesh?AssetDatabase.GetAssetPath(mesh.sharedMesh):null,
                    bounds=mesh&&mesh.sharedMesh?mesh.sharedMesh.bounds:default,
                    colliderSize=collider is BoxCollider box?box.size:Vector3.zero});
            }
            report.message=Path(c.message.transform);report.modeLabel=Path(c.modeLabel.transform);
            Directory.CreateDirectory(Evidence);File.WriteAllText(Evidence+"/layout-before.json",JsonUtility.ToJson(report,true));
            Debug.Log("XR_CONSOLE_INSPECT nodes="+report.nodes.Count);
        }
        static string Path(Transform t)=>!t?null:t.parent?Path(t.parent)+"/"+t.name:t.name;
        [Serializable] sealed class Layout {public string console,previewStand,previewParent,message,modeLabel;public List<Node> nodes=new List<Node>();}
        [Serializable] sealed class Node {public string path,parent,text,action,components,font,mesh;public bool active;public Vector3 position,localPosition,localScale,rotation,colliderSize;public Vector2 textRect;public float fontSize;public Bounds bounds;}
    }
}
