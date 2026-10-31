using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SwanJourney.Editor
{
    public static class SwanJourneyFeedbackProbe
    {
        public static void CheckCanyonGestures()
        {
            var main=EditorSceneManager.OpenScene(SwanJourneyBuild.ScenePath);
            var journey=UnityEngine.Object.FindObjectOfType<SteamJourney>(true);
            var input=UnityEngine.Object.FindObjectOfType<SwanJourneyInteraction>(true);
            var loader=input.GetComponent<SwanJourneySceneLoader>();
            var payload=loader.InstantiateEnvironment(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(SwanJourneyBuild.EnvironmentPrefabPath));
            var checks=new List<string>();
            try
            {
                loader.AttachEnvironment(payload);loader.AdoptEnvironment();journey.inSandbox=true;journey.verificationInputLock=false;
                input.GetComponent<SwanJourneyPresentation>().ApplyModeNow();
                typeof(SwanJourneyInteraction).GetMethod("Start",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(input,null);
                var target=payload.supports.First().gameObject;
                Check(target.GetComponent<Collider>()&&target.GetComponent<Collider>().enabled,"Deferred canyon tray has an enabled gaze/pinch surface");
                var a=new Vector3(-.2f,1,.8f);var b=new Vector3(.2f,1,.8f);
                Send(9501,target,UnityEngine.InputSystem.TouchPhase.Began,a);
                Send(9502,null,UnityEngine.InputSystem.TouchPhase.Began,b);input.ProcessGestureFrame(1,.02f);
                Check(input.TwoHandActive,"A pinch on separately owned canyon plus second hand starts two-hand mode");
                Send(9501,target,UnityEngine.InputSystem.TouchPhase.Moved,a+Vector3.left*.1f);
                Send(9502,null,UnityEngine.InputSystem.TouchPhase.Moved,b+Vector3.right*.1f);input.ProcessGestureFrame(1.02,.02f);
                Check(input.SceneScale>1.05f,"Spreading both hands enlarges the canyon's common stage");
                loader.FollowEnvironment();
                Check(Vector3.Distance(payload.EffectiveScale,loader.environmentAnchor.lossyScale)<.00001f,"Independent canyon follows the newly pinched stage scale including baked metre conversion");
                Send(9501,target,UnityEngine.InputSystem.TouchPhase.Ended,a);input.ProcessGestureFrame(1.04,.02f);
                Check(!input.TwoHandActive,"Releasing either hand exits canyon manipulation");
                File.WriteAllText("Builds/Evidence/session-output-20261030-v2313/canyon-gesture-checks.json",JsonUtility.ToJson(new GestureReport{passed=true,checks=checks.ToArray()},true));
            }
            finally { loader.ReleaseOwnedEnvironment(); }
            void Check(bool value,string name){if(!value)throw new InvalidOperationException(name);checks.Add(name);}
            void Send(long id,GameObject target,UnityEngine.InputSystem.TouchPhase phase,Vector3 hand)
                =>input.QueueGestureSample(new SwanAtelier.SwanGestureSample{id=id,target=target,phase=phase,pinch=true,handPosition=hand,hitPosition=hand});
        }
        [Serializable] sealed class GestureReport { public bool passed;public bool nativeGesturesValidated;public string[] checks; }
        public static void Inspect()
        {
            var lines = new List<string>();
            foreach (var path in new[]{"Assets/AtelierJourney/Scenes/SteamJourney.unity", SwanJourneyBuild.ScenePath, SwanJourneyBuild.EnvironmentScenePath, SwanJourneyBootScene.Path})
            {
                var scene = EditorSceneManager.OpenScene(path);
                lines.Add("SCENE " + path);
                var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
                foreach(var t in all)t.hasChanged=false;
                scene.GetRootGameObjects()[0].transform.localScale*=.9f;
                lines.Add("PARENT_SCALE_DIRTY nodes="+all.Count(t=>t.hasChanged)+" of="+all.Length);
                foreach(var t in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)))
                {
                    if(t.name.IndexOf("rail",StringComparison.OrdinalIgnoreCase)>=0 || t.name.IndexOf("ballast",StringComparison.OrdinalIgnoreCase)>=0 || t.name.Contains("一键") || t.name.Contains("慢放") || t.name.Contains("暂停") || t.name.Contains("精密仪器") || t.name.Contains("Low parts"))
                    {
                        var renderers=t.GetComponentsInChildren<MeshRenderer>(true);
                        SwanJourneyPresentation.TryBounds(t,t,out var b);
                        lines.Add(t.name+" parent="+(t.parent?t.parent.name:"ROOT")+" static="+t.gameObject.isStatic+" active="+t.gameObject.activeInHierarchy+" renderers="+renderers.Length+" enabled="+renderers.Count(r=>r.enabled)+" pos="+t.position+" scale="+t.lossyScale+" bounds="+b);
                    }
                    if(path==SwanJourneyBootScene.Path && t.TryGetComponent<TMP_Text>(out var label))
                    {
                        label.gameObject.SetActive(true);label.ForceMeshUpdate(true,true);
                        lines.Add("TEXT "+label.text+" size="+label.fontSize+" lines="+label.textInfo?.lineCount+" bounds="+(label.mesh?label.mesh.bounds.ToString():"missing")+" rect="+label.rectTransform.rect);
                    }
                }
            }
            File.WriteAllLines("Builds/Evidence/session-output-20261030-v2313/scene-probe.txt",lines);
        }
    }
}
