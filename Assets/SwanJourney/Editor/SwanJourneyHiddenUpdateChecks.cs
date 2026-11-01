using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SwanJourney.Editor
{
    public static class SwanJourneyHiddenUpdateChecks
    {
        [Serializable] sealed class Report
        {
            public bool passed, nativeRenderingValidated, devicePerformanceMeasured;
            public string scope = "EditMode real update methods, visibility transitions and source call counters; no Play/P2D or FPS measurement";
            public List<string> checks = new List<string>();
            public int stableHiddenMaterialWrites, stableHiddenPropertyBlockReads, stableVisibleUnchangedMaterialWrites;
            public string failure;
        }

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Cannot replace a dirty editor scene");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var report = new Report();
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                report.checks.AddRange(SwanJourneyCloudChecks.Run());
                report.checks.AddRange(SwanJourneyDrivingDisplayChecks.Run());
                CheckVisualBridge(report);
                report.passed = true;
                Debug.Log("SWAN_JOURNEY_HIDDEN_UPDATE_CHECKS_PASS checks=" + report.checks.Count);
            }
            catch (Exception e) { report.failure = e.ToString(); throw; }
            finally
            {
                try
                {
                    if (setup.Length > 0 && setup.All(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(setup);
                    else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
                finally
                {
                    Directory.CreateDirectory(SwanJourneyBuild.Output);
                    File.WriteAllText(SwanJourneyBuild.Output + "/hidden-update-checks.json", JsonUtility.ToJson(report, true));
                }
            }
        }

        static void CheckVisualBridge(Report report)
        {
            var root = new GameObject("Hidden-update visual fixture");
            SwanJourneyVisualBridge bridge = null;
            var owned = new List<UnityEngine.Object>();
            void Check(bool value, string label)
            {
                if (!value) throw new InvalidOperationException(label);
                report.checks.Add("Native bridge: " + label);
            }
            try
            {
                var shader = SwanJourneyUrp.Require(SwanJourneyUrp.Lit);
                Check(shader, "fixture uses the supported URP Lit material");
                var source = new Material(shader) { color = Color.white };
                source.SetColor("_EmissionColor", Color.black); owned.Add(source);
                var scan = new Material(source); owned.Add(scan);
                var gold = new Material(source); owned.Add(gold);
                var journey = root.AddComponent<SteamJourney>();
                journey.workshopRoot = root; journey.previewRoot = root;
                var displayParent = new GameObject("Hidden display parent");
                displayParent.transform.SetParent(root.transform, false);
                var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
                child.transform.SetParent(displayParent.transform, false);
                var renderer = child.GetComponent<MeshRenderer>(); renderer.sharedMaterial = source;
                var producer = new MaterialPropertyBlock(); var received = new MaterialPropertyBlock();
                void Write(Color color, Color emission)
                {
                    producer.Clear(); producer.SetColor("_Color", color); producer.SetColor("_EmissionColor", emission);
                    renderer.SetPropertyBlock(producer);
                }
                Write(Color.white, Color.black);
                bridge = root.AddComponent<SwanJourneyVisualBridge>();
                bridge.journey = journey; bridge.workbench = root.transform;
                bridge.scanMaterial = scan; bridge.goldMaterial = gold;
                bridge.materials = new[] { new JourneyNativeMaterial { material = source, kind = JourneyNativeMaterialKind.Surface, color = Color.white } };
                Invoke(bridge, "Awake");
                var live = renderer.sharedMaterial;
                Check(live != source && bridge.NativeAnimatedRenderers == 1, "actual Awake creates exactly one owned live binding");
                Invoke(bridge, "LateUpdate");
                Check(bridge.LastMaterialWrites == 2 && bridge.LastPropertyBlockReads == 1, "first visible update applies material parameters");

                foreach (bool hideParent in new[] { false, true })
                {
                    if (hideParent) displayParent.SetActive(false); else renderer.enabled = false;
                    for (int frame = 0; frame < 5; frame++)
                    {
                        Write(new Color(.2f, .3f, .4f), new Color(frame + 1, .4f, .2f));
                        Invoke(bridge, "LateUpdate");
                        Check(bridge.LastHiddenSkipped == 1 && bridge.LastMaterialWrites == 0
                            && bridge.LastPropertyBlockReads == 0 && bridge.LastPropertyBlocksConsumed == 0,
                            (hideParent ? "inactive ancestor" : "disabled renderer") + " frame " + frame + " performs no material/block work");
                    }
                    report.stableHiddenMaterialWrites = bridge.LastMaterialWrites;
                    report.stableHiddenPropertyBlockReads = bridge.LastPropertyBlockReads;
                    renderer.GetPropertyBlock(received);
                    Check(Mathf.Abs(received.GetColor("_EmissionColor").r - 5) < .0001f,
                        "hidden pending parameters retain the latest producer value (actual=" + received.GetColor("_EmissionColor").r.ToString("R") + ")");
                    if (hideParent) displayParent.SetActive(true); else renderer.enabled = true;
                    Invoke(bridge, "LateUpdate");
                    Check(Mathf.Abs(live.GetColor("_EmissionColor").r - 5) < .0001f && Near(live.color, new Color(.2f, .3f, .4f))
                        && bridge.LastMaterialWrites == 2 && bridge.LastPropertyBlocksConsumed == 1,
                        "first visible update consumes latest color/emission and forces refresh");
                    Write(new Color(.2f, .3f, .4f), new Color(5, .4f, .2f));
                    Invoke(bridge, "LateUpdate");
                    report.stableVisibleUnchangedMaterialWrites = bridge.LastMaterialWrites;
                    Check(bridge.LastMaterialWrites == 0 && bridge.LastPropertyBlocksConsumed == 1
                        && Mathf.Abs(live.GetColor("_EmissionColor").r - 5) < .0001f, "unchanged visible producer output skips duplicate material writes");
                }

                foreach (bool arrival in new[] { true, false })
                {
                    if (arrival) Invoke(bridge, "BeginArrival"); else Invoke(bridge, "BeginGold", child.transform);
                    if (!arrival) Invoke(bridge, "GoldProgress", .5f);
                    var temporary = renderer.sharedMaterial;
                    Check(temporary == (arrival ? scan : gold), "transition installs its temporary material");
                    renderer.enabled = false; Invoke(bridge, "LateUpdate");
                    renderer.enabled = true; Write(Color.cyan, Color.yellow); Invoke(bridge, "LateUpdate");
                    Check(renderer.sharedMaterial == temporary, "visible refresh does not overwrite scan/gold material ownership");
                    Invoke(bridge, arrival ? "EndArrival" : "EndGold");
                    Check(renderer.sharedMaterial == live && renderer.enabled, "transition cleanup restores exact live binding");
                }
                Check(source.color == Color.white && source.GetColor("_EmissionColor") == Color.black,
                    "shared source material is unchanged");
            }
            finally
            {
                // Runtime owns Destroy(); this editor fixture disposes its clones
                // immediately before OnDestroy, without changing runtime cleanup.
                if (bridge)
                {
                    var entries = (IList)typeof(SwanJourneyVisualBridge).GetField("animated", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(bridge);
                    foreach (var entry in entries)
                    {
                        var material = (Material)entry.GetType().GetField("instance").GetValue(entry);
                        if (material) UnityEngine.Object.DestroyImmediate(material);
                    }
                    entries.Clear();
                }
                UnityEngine.Object.DestroyImmediate(root);
                foreach (var item in owned) if (item) UnityEngine.Object.DestroyImmediate(item);
            }
        }

        static void Invoke(object target, string method, params object[] args)
        {
            var info = target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
            if (info == null) throw new MissingMethodException(target.GetType().Name, method);
            info.Invoke(target, args);
        }
        static bool Near(Color a, Color b) => Mathf.Abs(a.r-b.r)<.0001f && Mathf.Abs(a.g-b.g)<.0001f
            && Mathf.Abs(a.b-b.b)<.0001f && Mathf.Abs(a.a-b.a)<.0001f;
    }
}
