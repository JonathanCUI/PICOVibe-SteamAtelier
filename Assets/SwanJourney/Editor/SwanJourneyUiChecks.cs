using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ByteDance.PICO.SpatialAdapter.Component;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SwanJourney.Editor
{
    public static class SwanJourneyUiChecks
    {
        [Serializable] sealed class Report { public bool passed; public List<string> checks = new List<string>(); public string error; }

        [MenuItem("Steam Atelier/V23 Swan/Check TMP UI")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(SwanJourneyBuild.ScenePath);
            Run(UnityEngine.Object.FindObjectOfType<SwanJourneyNativeUi>(true));
        }

        public static void Run(SwanJourneyNativeUi ui)
        {
            var report = new Report();
            GameObject fixture = null;
            GameObject initializationFixture = null;
            void Check(bool condition, string message)
            {
                if (!condition) throw new InvalidOperationException(message);
                report.checks.Add(message);
            }
            try
            {
                Check(ui && ui.content, "Derived scene contains the TMP UI bridge");
                var journey = ui.content.GetComponentInChildren<SteamJourney>(true);
                Check(journey && journey.font, "Original Chinese TMP font binding retained");
                var authoredCards = ui.content.GetComponentInChildren<StudioPartCards>(true);
                Check(authoredCards && authoredCards.spatialAnchor && authoredCards.spatialLeaderMaterial,
                    "Part cards retain serialized spatial anchor and native leader material");
                Check(journey.SwanInputActive && authoredCards.spatialLeaderMaterial.shader
                    && authoredCards.spatialLeaderMaterial.shader.name == "Standard", "Swan part-card startup has a retained Standard shader");
                var previousCanvases = new HashSet<Canvas>(authoredCards.spatialAnchor.GetComponentsInChildren<Canvas>(true));
                var originalLeaderColor = authoredCards.spatialLeaderMaterial.color;
                initializationFixture = new GameObject("Temporary actual part-card startup");
                initializationFixture.transform.SetParent(ui.content, false);
                var startedCards = initializationFixture.AddComponent<StudioPartCards>();
                startedCards.journey = journey;
                startedCards.spatialAnchor = authoredCards.spatialAnchor;
                startedCards.spatialLeaderMaterial = authoredCards.spatialLeaderMaterial;
                // Exercise the real partner initialization, including Build and
                // ConfigureSpatial. Synthetic TMP fixtures cannot catch a
                // stripped Shader.Find aborting Start before world anchoring.
                typeof(StudioPartCards).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(startedCards, null);
                Check(startedCards.IsWorldAnchored, "Actual StudioPartCards.Start completes world-space configuration");
                Canvas startedCanvas = null;
                foreach (var candidate in authoredCards.spatialAnchor.GetComponentsInChildren<Canvas>(true))
                    if (!previousCanvases.Contains(candidate) && candidate.transform.parent == authoredCards.spatialAnchor) startedCanvas = candidate;
                Check(startedCanvas && startedCanvas.enabled, "Actual introduction canvas is enabled beneath the authored board anchor");
                var selected = journey.SwanModule(startedCards.SelectedIndex);
                bool hasTitle = false, hasExplanation = false;
                foreach (var label in startedCanvas.GetComponentsInChildren<TMP_Text>(true))
                {
                    hasTitle |= label.text == selected.title && !string.IsNullOrWhiteSpace(label.text);
                    hasExplanation |= label.text == selected.explanation && !string.IsNullOrWhiteSpace(label.text);
                }
                Check(hasTitle && hasExplanation, "Actual Start populates both selected title and explanation");
                var startedLeader = initializationFixture.GetComponentInChildren<LineRenderer>(true);
                Check(startedLeader && startedLeader.sharedMaterial && startedLeader.sharedMaterial.shader.name == "Standard"
                    && startedLeader.sharedMaterial != authoredCards.spatialLeaderMaterial,
                    "Actual Start creates an owned Standard leader clone without URP lookup");
                Check(authoredCards.spatialLeaderMaterial.color == originalLeaderColor,
                    "Part-card line color does not modify the serialized material asset");
                var originals = ui.content.GetComponentsInChildren<TMP_Text>(true);
                var parents = new Dictionary<TMP_Text, Transform>();
                var poses = new Dictionary<TMP_Text, Matrix4x4>();
                foreach (var text in originals) { parents[text] = text.transform.parent; poses[text] = text.transform.localToWorldMatrix; }
                ui.Discover(); ui.RefreshNow();
                Check(ui.ConvertedLabelCount > 10, "Authored world labels remain in TMP upload path");
                Check(ui.content.GetComponentsInChildren<SpatialAdapterNativeText>(true).Length == 0, "No absolute-position NativeText copies exist");
                foreach (var text in originals)
                    Check(text && text.transform.parent == parents[text] && text.transform.localToWorldMatrix == poses[text], "Original label hierarchy and pose retained: " + text.name);

                fixture = new GameObject("Temporary TMP UI checks");
                fixture.transform.SetParent(ui.content, false);
                var text3d = new GameObject("World label", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
                text3d.transform.SetParent(fixture.transform, false);
                text3d.font = journey.font; text3d.fontSize = 1.4f;
                text3d.rectTransform.sizeDelta = new Vector2(5, 1);
                var canvas = new GameObject("World card", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
                canvas.transform.SetParent(fixture.transform, false);
                canvas.renderMode = RenderMode.WorldSpace; canvas.transform.localScale = Vector3.one * .0062f;
                var card = new GameObject("Card body", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                card.transform.SetParent(canvas.transform, false); card.font = journey.font; card.fontSize = 20;
                card.rectTransform.sizeDelta = new Vector2(386, 106);
                TMP_Text[] samples = { text3d, card };
                ui.Discover();
                foreach (var sample in samples)
                {
                    sample.text = ""; sample.enabled = true;
                    ui.RefreshNow();
                    Check(sample.text == "" && sample.enabled && SwanJourneyNativeUi.HasUploadableMesh(sample.mesh), "Blank text retains its binding and has safe geometry: " + sample.name);
                    Check(Array.TrueForAll(sample.mesh.colors32, c => c.a == 0), "Blank placeholder is transparent: " + sample.name);
                    ui.RefreshNow();
                    int signalCount = ui.DirtySignalCount;
                    for (int i = 0; i < 6; i++) { sample.ForceMeshUpdate(true); ui.RefreshNow(); }
                    Check(ui.DirtySignalCount == signalCount && !sample.havePropertiesChanged, "Stable blank text does not repeatedly queue uploads: " + sample.name);

                    sample.text = "锅炉 Boiler 01"; ui.RefreshNow();
                    Check(sample.text == "锅炉 Boiler 01" && sample.mesh.vertexCount > 3 && sample.textInfo.characterCount > 0, "Blank-to-Chinese text rebuilds real glyph geometry: " + sample.name);
                    sample.enabled = false; sample.text = ""; ui.RefreshNow(); ui.RefreshNow();
                    Check(!sample.enabled && !sample.havePropertiesChanged && SwanJourneyNativeUi.HasUploadableMesh(sample.mesh), "Disabled text preserves visibility and stops dirty uploads: " + sample.name);
                    sample.gameObject.SetActive(false); sample.text = ""; ui.RefreshNow();
                    Check(!sample.gameObject.activeSelf && SwanJourneyNativeUi.HasUploadableMesh(sample.mesh), "Inactive text retains safe geometry: " + sample.name);
                    sample.gameObject.SetActive(true); sample.enabled = true; sample.text = "重新显示 02"; ui.RefreshNow();
                    Check(sample.enabled && sample.mesh.vertexCount > 3, "Re-enabled text rebuilds after an inactive interval: " + sample.name);
                }
                var localPosition = card.transform.localPosition;
                var localRotation = card.transform.localRotation;
                var localScale = card.transform.localScale;
                fixture.transform.localPosition = new Vector3(.5f, .2f, -.3f);
                fixture.transform.localRotation = Quaternion.Euler(0, 47, 0);
                fixture.transform.localScale = Vector3.one * .45f;
                ui.RefreshNow();
                Check(card.transform.parent == canvas.transform && card.transform.localPosition == localPosition
                    && card.transform.localRotation == localRotation && card.transform.localScale == localScale,
                    "Move, rotate and scale preserve card-local text layout");
                Check(canvas.enabled && canvas.renderMode == RenderMode.WorldSpace && card.enabled,
                    "World canvas and original TMP remain enabled");

                // A genuine manipulation regression: the full original TMP objects
                // rebuild their local meshes while their ancestor is moved/scaled.
                // Native text consumes position/color/UV0, so those buffers must stay
                // identical and no per-label upload should be queued for a pose edit.
                foreach (var sample in samples) sample.ForceMeshUpdate(true);
                ui.RefreshNow(); ui.RefreshNow();
                int poseSignals = ui.DirtySignalCount;
                var vertices = samples.Select(s => s.mesh.vertices).ToArray();
                var colours = samples.Select(s => s.mesh.colors32).ToArray();
                var uv0 = samples.Select(s => s.mesh.uv).ToArray();
                for (int frame = 0; frame < 48; frame++)
                {
                    fixture.transform.SetPositionAndRotation(new Vector3(frame * .01f, .5f, 1.2f),
                        Quaternion.Euler(0, frame * 7f, 0));
                    fixture.transform.localScale = Vector3.one * Mathf.Lerp(.26f, 1.5f, frame / 47f);
                    foreach (var sample in samples) sample.ForceMeshUpdate(true);
                    ui.RefreshNow();
                }
                Check(ui.DirtySignalCount == poseSignals, "48 root pose/scale changes queue no native text mesh uploads");
                for (int i = 0; i < samples.Length; i++)
                    Check(samples[i].mesh.vertices.SequenceEqual(vertices[i])
                        && samples[i].mesh.colors32.SequenceEqual(colours[i])
                        && samples[i].mesh.uv.SequenceEqual(uv0[i]),
                        "Root manipulation preserves uploaded local glyph buffers: " + samples[i].name);
                card.text = "缩放时依然更新 03";
                ui.RefreshNow();
                Check(ui.DirtySignalCount == poseSignals + 1 && card.havePropertiesChanged
                    && card.textInfo.characterCount > 0, "Actual text edit during manipulation still queues one upload");
                report.passed = true;
                Debug.Log("SWAN_JOURNEY_UI_CHECKS_PASS checks=" + report.checks.Count);
            }
            catch (Exception ex) { report.error = ex.ToString(); throw; }
            finally
            {
                if (fixture) UnityEngine.Object.DestroyImmediate(fixture);
                if (initializationFixture) UnityEngine.Object.DestroyImmediate(initializationFixture);
                Directory.CreateDirectory(SwanJourneyBuild.Output);
                File.WriteAllText(SwanJourneyBuild.Output + "/ui-checks.json", JsonUtility.ToJson(report, true));
            }
        }
    }
}
