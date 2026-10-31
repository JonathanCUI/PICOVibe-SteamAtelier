using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SwanJourney.Editor
{
    public static class SwanJourneyProbe
    {
        public static void Inspect()
        {
            var scene=EditorSceneManager.OpenScene("Assets/AtelierJourney/Scenes/SteamJourney.unity");
            var journey=UnityEngine.Object.FindObjectOfType<SteamJourney>(true);
            if(!journey)throw new Exception("Partner SteamJourney component is missing");
            var lines=new System.Collections.Generic.List<string>();
            foreach(var root in scene.GetRootGameObjects()) {
                lines.Add("ROOT "+root.name+" position="+root.transform.position+" scale="+root.transform.localScale);
                foreach(Transform child in root.transform) lines.Add("  "+child.name+" active="+child.gameObject.activeSelf+" local="+child.localPosition);
            }
            lines.Add("JOURNEY "+PathOf(journey.transform));
            foreach(var pair in new[]{("workshop",journey.workshopRoot),("sandbox",journey.sandboxRoot),("preview",journey.previewRoot),("console",journey.consoleRoot)}) {
                lines.Add(pair.Item1+"="+PathOf(pair.Item2.transform));
                foreach(Transform t in pair.Item2.transform)lines.Add("  "+t.name+" local="+t.localPosition+" scale="+t.localScale);
            }
            foreach(var behaviour in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true))
                if(behaviour&&behaviour.enabled)lines.Add("BEHAVIOUR "+behaviour.GetType().Name+" "+PathOf(behaviour.transform));
            foreach(var group in UnityEngine.Object.FindObjectsOfType<Renderer>(true).Where(r=>r.sharedMaterial).GroupBy(r=>r.sharedMaterial.shader.name))
                lines.Add("SHADER "+group.Key+" renderers="+group.Count());
            int missing=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            lines.Add("MODULES "+journey.modules.Length+" FINISHING "+journey.finishing.Length+" MISSING_SCRIPTS "+missing);
            Directory.CreateDirectory("Builds/SwanJourney");File.WriteAllLines("Builds/SwanJourney/source-inventory.txt",lines);
            Debug.Log("SWAN_JOURNEY_SOURCE_INSPECTED modules="+journey.modules.Length+" finishing="+journey.finishing.Length+" missing="+missing);
        }
        static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
    }
}
