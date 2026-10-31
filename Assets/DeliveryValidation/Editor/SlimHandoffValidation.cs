using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Portable handoff validation: never modifies or saves the production scene.
public static class SlimHandoffValidation
{
    const string Scene = "Assets/AtelierJourney/Scenes/SteamJourney.unity";
    static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Validation"));
    [MenuItem("Steam Atelier/Validate and Build V23.1")]
    public static void ValidateAndBuild()
    {
        Directory.CreateDirectory(Output);
        var lines = new List<string>();
        var issues = new List<string>();
        var scene = EditorSceneManager.OpenScene(Scene);
        int components = 0, renderers = 0;
        foreach (var root in scene.GetRootGameObjects())
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            var n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            if (n > 0) issues.Add("Missing scripts: " + t.name + " x" + n);
            foreach (var c in t.GetComponents<Component>())
            {
                if (!c) continue;
                components++;
                var obj = new SerializedObject(c);
                var it = obj.GetIterator();
                while (it.Next(true))
                    if (it.propertyType == SerializedPropertyType.ObjectReference &&
                        it.objectReferenceValue == null && it.objectReferenceInstanceIDValue != 0)
                        issues.Add("Missing reference: " + t.name + "/" + c.GetType().Name + "/" + it.propertyPath);
                if (c is Renderer r)
                {
                    renderers++;
                    foreach (var m in r.sharedMaterials)
                    {
                        if (!m) { issues.Add("Missing material: " + t.name); continue; }
                        if (!m.shader || m.shader.name == "Hidden/InternalErrorShader") issues.Add("Missing shader: " + m.name);
                    }
                }
            }
        }
        var dependencies = AssetDatabase.GetDependencies(Scene, true).OrderBy(x => x).ToArray();
        File.WriteAllLines(Path.Combine(Output, "unity-scene-dependencies.txt"), dependencies);
        foreach (var p in dependencies.Where(x => x.StartsWith("Assets/")))
            if (!File.Exists(p)) issues.Add("Missing dependency: " + p);
        foreach (var shader in dependencies.Where(x => x.EndsWith(".shader")).Select(AssetDatabase.LoadAssetAtPath<Shader>).Where(x => x))
            if (ShaderUtil.ShaderHasError(shader)) issues.Add("Shader compilation error: " + shader.name);
        lines.Add("Unity=" + Application.unityVersion);
        lines.Add("Components=" + components + " Renderers=" + renderers);
        lines.Add("Scene dependencies=" + dependencies.Length);
        lines.Add("Reference errors=" + issues.Count);
        lines.AddRange(issues.Distinct());
        File.WriteAllLines(Path.Combine(Output, "editor-validation.txt"), lines);
        if (issues.Count > 0) throw new Exception("Slim handoff reference validation failed; see " + Output);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { Scene },
            locationPathName = Path.Combine(Output, "Windows/SteamAtelier.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        lines.Add("Build=" + report.summary.result);
        lines.Add("Build errors=" + report.summary.totalErrors);
        lines.Add("Build warnings=" + report.summary.totalWarnings);
        lines.Add("Build bytes=" + report.summary.totalSize);
        File.WriteAllLines(Path.Combine(Output, "editor-validation.txt"), lines);
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Slim handoff build failed");
    }
}
