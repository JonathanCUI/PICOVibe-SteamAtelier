using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class AtelierPartnerHandoff
{
    static string Reports => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Reports"));
    static double next;
    static AtelierPartnerHandoff() { EditorApplication.update += Poll; }
    [Serializable] class SceneInfo { public string path; public bool dirty; public string[] roots; }
    [Serializable] class ObjectInfo { public string path; public bool active; public int renderers, visibleRenderers; public string[] visibleMeshes; }
    [Serializable] class Report
    {
        public string timestamp, unityVersion, error;
        public bool playing, saved;
        public SceneInfo[] scenes;
        public ObjectInfo[] environment;
        public string[] missingScripts, missingReferences, missingDependencies, dependencies;
        public int assemblyGroups;
    }
    static string ObjectPath(Transform t) => t.parent ? ObjectPath(t.parent) + "/" + t.name : t.name;
    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < next || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        next = EditorApplication.timeSinceStartup + .6;
        string commandPath = Path.Combine(Reports, "handoff-command.txt");
        if (!File.Exists(commandPath)) return;
        string command = File.ReadAllText(commandPath).Trim();
        File.Delete(commandPath);
        var report = new Report { timestamp = DateTime.Now.ToString("s"), unityVersion = Application.unityVersion, playing = EditorApplication.isPlayingOrWillChangePlaymode };
        try
        {
            if (command == "save")
            {
                if (report.playing) throw new InvalidOperationException("Cannot save authoring scenes during Play mode.");
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (scene.isLoaded && scene.isDirty)
                    {
                        if (string.IsNullOrEmpty(scene.path)) throw new InvalidOperationException("Unsaved unnamed scene; select a scene path first.");
                        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed: " + scene.path);
                    }
                }
                AssetDatabase.SaveAssets();
                report.saved = true;
            }
            var sceneInfos = new List<SceneInfo>();
            var objects = new List<GameObject>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                var roots = scene.GetRootGameObjects();
                sceneInfos.Add(new SceneInfo { path = scene.path, dirty = scene.isDirty, roots = roots.Select(g => g.name).ToArray() });
                objects.AddRange(roots.SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject));
            }
            report.scenes = sceneInfos.ToArray();
            report.environment = objects.Where(g => g.name == "Solid oak teaching workbench" || g.name == "Daylit atelier" || g.name.Contains("Window recess") || g.name == "Workshop floor")
                .Select(g => new ObjectInfo { path = ObjectPath(g.transform), active = g.activeInHierarchy,
                    renderers = g.GetComponentsInChildren<MeshRenderer>(true).Length,
                    visibleRenderers = g.GetComponentsInChildren<MeshRenderer>(true).Count(r => r.enabled && r.gameObject.activeInHierarchy),
                    visibleMeshes = g.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy).Select(r => ObjectPath(r.transform)).ToArray() }).ToArray();
            report.missingScripts = objects.Where(g => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(g) > 0).Select(g => ObjectPath(g.transform)).ToArray();
            var missing = new List<string>();
            foreach (var component in objects.SelectMany(g => g.GetComponents<Component>()).Where(c => c))
            {
                var serialized = new SerializedObject(component);
                var p = serialized.GetIterator();
                while (p.Next(true))
                    if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue == null && p.objectReferenceInstanceIDValue != 0)
                        missing.Add(ObjectPath(component.transform) + " | " + component.GetType().Name + "." + p.propertyPath);
                serialized.Dispose();
            }
            report.missingReferences = missing.ToArray();
            report.dependencies = AssetDatabase.GetDependencies(sceneInfos.Select(s => s.path).Where(p => !string.IsNullOrEmpty(p)).ToArray(), true);
            report.missingDependencies = report.dependencies.Where(p => p.StartsWith("Assets/") && !File.Exists(p) && !Directory.Exists(p)).ToArray();
            var assembly = objects.Select(g => g.GetComponent<LocomotiveFreeAssembly>()).FirstOrDefault(w => w);
            report.assemblyGroups = assembly ? assembly.pieces.Length : 0;
        }
        catch (Exception e) { report.error = e.ToString(); }
        File.WriteAllText(Path.Combine(Reports, "partner-handoff-state.json"), JsonUtility.ToJson(report, true));
    }
}
