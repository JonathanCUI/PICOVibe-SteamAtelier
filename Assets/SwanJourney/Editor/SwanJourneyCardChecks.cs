using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SwanJourney.Editor
{
    // This verifies the exported-card strategy in EditMode. It does not claim
    // that SpatialAdapter has rendered or positioned the text on a headset.
    public static class SwanJourneyCardChecks
    {
        [Serializable] sealed class Report
        {
            public bool passed;
            public bool nativeRenderingValidated;
            public string scope = "Saved derived TMP subtree and actual Start/content/visibility/lifetime methods in EditMode";
            public List<string> checks = new List<string>();
            public string error;
        }

        [MenuItem("Steam Atelier/V23 Swan/Check Prepared Part Cards")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(SwanJourneyBuild.ScenePath);
            Run(UnityEngine.Object.FindObjectOfType<SwanJourneyNativeUi>(true));
        }

        public static void Run(SwanJourneyNativeUi generatedUi)
        {
            var report = new Report();
            GameObject fixture = null;
            SteamJourney journey = null;
            bool wasSandbox = false;
            void Check(bool condition, string message)
            {
                if (!condition) throw new InvalidOperationException(message);
                report.checks.Add(message);
            }
            try
            {
                Check(generatedUi && generatedUi.content, "Generated UI content exists");
                journey = generatedUi.content.GetComponentInChildren<SteamJourney>(true);
                Check(journey && journey.SwanInputActive && journey.font, "Swan journey retains its original TMP font");
                wasSandbox = journey.inSandbox;
                var savedCards = generatedUi.content.GetComponentsInChildren<StudioPartCards>(true);
                Check(savedCards.Length > 0 && savedCards.All(c => c.HasPreparedSpatialCanvas),
                    "Saved derived scene already contains complete world-space part cards before Start");
                foreach (var cards in savedCards)
                {
                    var serialized = new SerializedObject(cards);
                    foreach (string field in new[] { "canvas", "cardRoot", "title", "body", "observation", "status", "leader", "lineMaterial" })
                        Check(serialized.FindProperty(field)?.objectReferenceValue,
                            "Prepared card reference survives scene serialization: " + field);
                    Check(serialized.FindProperty("labels").arraySize == 6,
                        "Six original card TMP labels are serialized with their controller");
                    var savedCanvas = (Canvas)serialized.FindProperty("canvas").objectReferenceValue;
                    Check(savedCanvas.transform.parent == cards.spatialAnchor,
                        "Saved card canvas is parented to the authored board anchor");
                    Check(savedCanvas.GetComponentsInChildren<TMP_Text>(true).All(t => t.font == journey.font),
                        "Prepared labels keep the authored Chinese font rather than NativeText copies");
                    Check(savedCanvas.GetComponentsInChildren<TMP_Text>(true).All(t => t is TextMeshPro && t.isOrthographic
                        && !t.GetComponent<CanvasRenderer>() && Mathf.Abs(t.transform.localPosition.z + 2) < .0001f),
                        "Six Swan card labels use orthographic 3D TMP with a two-pixel separation from the backdrop");
                    Check(SurfaceCount(savedCanvas.transform) == 14,
                        "Saved card retains two panel meshes and twelve frame meshes");
                    Check(savedCanvas.GetComponentsInChildren<UnityEngine.UI.Image>(true).Length == 0 &&
                        CardSurfaceRenderers(savedCanvas.transform).All(r => !r.transform.parent.GetComponent<CanvasRenderer>()),
                        "Saved card decoration nodes have no duplicate Image or CanvasRenderer native path");
                    Check(CardSurfaceRenderers(savedCanvas.transform).All(r => r.sharedMaterial && EditorUtility.IsPersistent(r.sharedMaterial)),
                        "Saved card surface materials are persistent native assets");
                    Check(HasAuthoredColors(savedCanvas.transform),
                        "Saved card preserves two dark panels and twelve brass edges with authored alpha");
                    Check(HasInitializedLeader((LineRenderer)serialized.FindProperty("leader").objectReferenceValue),
                        "Saved Unity leader has finite, nonzero geometry before LateUpdate; native LineRenderer support is not implied");
                }

                fixture = new GameObject("Temporary prepared card regression");
                fixture.transform.SetParent(generatedUi.content, false);
                var anchor = new GameObject("Temporary board anchor").transform;
                anchor.SetParent(fixture.transform, false);
                anchor.localPosition = new Vector3(2, 3, -4);
                anchor.localRotation = Quaternion.Euler(8, 37, 0);
                var owner = new GameObject("Temporary card controller");
                owner.transform.SetParent(fixture.transform, false);
                var cardsFixture = owner.AddComponent<StudioPartCards>();
                cardsFixture.journey = journey;
                cardsFixture.spatialAnchor = anchor;
                cardsFixture.spatialLeaderMaterial = savedCards[0].spatialLeaderMaterial;
                var ui = fixture.AddComponent<SwanJourneyNativeUi>();
                ui.content = fixture.transform;
                ui.panelMaterial = generatedUi.panelMaterial;

                SwanJourneyCardExport.Prepare(ui, false);
                Check(cardsFixture.HasPreparedSpatialCanvas, "Preparation builds the original card controller's world-space subtree");
                var cardCanvas = anchor.GetComponentInChildren<Canvas>(true);
                var texts = cardCanvas.GetComponentsInChildren<TMP_Text>(true);
                var textIds = texts.Select(t => t.GetInstanceID()).ToArray();
                int canvasId = cardCanvas.GetInstanceID();
                int surfaceCount = SurfaceCount(cardCanvas.transform);
                Check(texts.Length == 6 && surfaceCount > 0, "Fixture contains six original labels and their native mesh backdrops");
                Check(surfaceCount == 14 && cardCanvas.GetComponentsInChildren<UnityEngine.UI.Image>(true).Length == 0 &&
                    CardSurfaceRenderers(cardCanvas.transform).All(r => !r.transform.parent.GetComponent<CanvasRenderer>()),
                    "Export replaces only fourteen Image/CanvasRenderer decorations with their existing mesh surfaces");
                Check(HasAuthoredColors(cardCanvas.transform), "Converted fixture preserves panel and border colors including alpha");
                Check(HasInitializedLeader(owner.GetComponentInChildren<LineRenderer>(true)),
                    "Preparation initializes the Unity leader positions and width without a runtime frame");
                var meshes = texts.Select(t => t.mesh).ToArray();
                Check(meshes.All(SwanJourneyNativeUi.HasUploadableMesh), "Prepared label glyph buffers are uploadable before runtime Start");

                // Exercise the same call graph as generation followed by player
                // Start. Repeating discovery must not duplicate either layer.
                SwanJourneyCardExport.Prepare(ui, false);
                Invoke(cardsFixture, "Start");
                Invoke(cardsFixture, "Start");
                ui.Discover(); ui.RefreshNow();
                Check(anchor.GetComponentsInChildren<Canvas>(true).Length == 1
                    && cardCanvas.GetInstanceID() == canvasId, "Preparation and actual Start reuse one canvas");
                Check(cardCanvas.GetComponentsInChildren<TMP_Text>(true).Select(t => t.GetInstanceID()).SequenceEqual(textIds),
                    "Actual Start keeps the exact exported TMP objects");
                Check(SurfaceCount(cardCanvas.transform) == surfaceCount,
                    "Repeated preparation and discovery do not duplicate panel meshes");
                Check(HasAuthoredColors(cardCanvas.transform) && cardCanvas.GetComponentsInChildren<UnityEngine.UI.Image>(true).Length == 0,
                    "Actual Start keeps persistent mesh colors without recreating Image components");
                Check(owner.GetComponentsInChildren<LineRenderer>(true).Length == 1,
                    "Actual Start reuses one owned part-card leader");

                foreach (int selection in Enumerable.Range(0, journey.modules.Length + journey.finishing.Length))
                {
                    cardsFixture.Select(selection);
                    ui.RefreshNow();
                    var selected = journey.SwanModule(selection);
                    Check(texts.Any(t => t.text == selected.title) && texts.Any(t => t.text == selected.explanation),
                        "Original title and explanation update after selecting group " + selection);
                    Check(texts.All(t => SwanJourneyNativeUi.HasUploadableMesh(t.mesh)),
                        "Changed card content retains uploadable glyph meshes for group " + selection);
                    Check(texts.All(t => !t.isTextOverflowing), "Every instruction fits the card for group " + selection);
                    foreach (var text in texts)
                        Check(text.text.Where(c => !char.IsControl(c)).All(c => text.font.HasCharacter((int)c)),
                            "All instruction characters are baked for group " + selection + ": " + text.name);
                    // Keep the original UGUI comparison samples while checking glyphs and fit on every step.
                    if (selection == 0 || selection == 6 || selection == journey.modules.Length + journey.finishing.Length - 1)
                        foreach (var text in texts) CompareUguiLayout(text, cardCanvas, selection, Check);
                }

                // Compare glyph positions relative to the same anchor as the
                // panel, across parent translation/yaw/tabletop scales.
                var originalLocalGlyphs = texts.Select(t => anchor.InverseTransformPoint(t.transform.TransformPoint(t.mesh.bounds.center))).ToArray();
                Vector3 oldWorldGlyph = texts[0].transform.TransformPoint(texts[0].mesh.bounds.center);
                foreach (float scale in new[] { .12f, .035f, 1.5f })
                {
                    fixture.transform.localPosition += new Vector3(.4f, .2f, -.3f);
                    fixture.transform.localRotation = Quaternion.Euler(0, 49, 0);
                    fixture.transform.localScale = Vector3.one * scale;
                    ui.RefreshNow();
                    Check(texts.All(t => t.transform.IsChildOf(cardCanvas.transform)) && cardCanvas.transform.parent == anchor,
                        "Card text and panel retain common anchor at scale " + scale);
                    for (int i = 0; i < texts.Length; i++)
                    {
                        Vector3 point = anchor.InverseTransformPoint(texts[i].transform.TransformPoint(texts[i].mesh.bounds.center));
                        Check(Vector3.Distance(point, originalLocalGlyphs[i]) < .002f,
                            "Glyph layout stays in panel-local coordinates at scale " + scale + ": " + i);
                    }
                    Check(Vector3.Distance(texts[0].transform.TransformPoint(texts[0].mesh.bounds.center), oldWorldGlyph) > .01f,
                        "Glyph world position follows the changed board transform at scale " + scale);
                    oldWorldGlyph = texts[0].transform.TransformPoint(texts[0].mesh.bounds.center);
                }

                journey.inSandbox = false;
                cardsFixture.visible = false;
                Invoke(cardsFixture, "LateUpdate");
                ui.Discover(); ui.RefreshNow();
                Check(!cardCanvas.gameObject.activeInHierarchy && texts.All(t => !t.gameObject.activeInHierarchy),
                    "Hiding introduction hides the same exported canvas and all labels");
                cardsFixture.visible = true;
                Invoke(cardsFixture, "LateUpdate");
                ui.RefreshNow();
                Check(cardCanvas.gameObject.activeInHierarchy && texts.All(t => t.gameObject.activeInHierarchy),
                    "Showing introduction restores its existing text and background");
                Check(SurfaceCount(cardCanvas.transform) == surfaceCount, "Hide/show does not create leftover backdrops");

                var leader = owner.GetComponentInChildren<LineRenderer>(true);
                var ownedLineMaterial = leader.sharedMaterial;
                var ownedSurfaces = cardCanvas.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.name.StartsWith("Swan card surface ", StringComparison.Ordinal)).ToArray();
                // This non-ExecuteAlways fixture has no Unity player lifecycle in
                // EditMode. As with Start/LateUpdate above, invoke the real hook
                // explicitly rather than assuming DestroyImmediate dispatches it.
                Invoke(cardsFixture, "OnDestroy");
                Check(!cardCanvas && texts.All(t => !t) && ownedSurfaces.All(t => !t),
                    "Actual OnDestroy removes the reparented card canvas, all labels and every native backdrop");
                Check(!ownedLineMaterial && cardsFixture.spatialLeaderMaterial,
                    "Actual OnDestroy releases only its owned leader material and preserves the source material");
                Invoke(cardsFixture, "OnDestroy");
                Check(anchor.childCount == 0,
                    "Repeated actual OnDestroy remains safe and leaves the board anchor empty");
                UnityEngine.Object.DestroyImmediate(owner);
                ui.Discover(); ui.RefreshNow();
                Check(!cardCanvas && texts.All(t => !t) && !leader,
                    "Destroying the owner after its actual cleanup hook removes the child leader without restoring labels");
                Check(anchor.childCount == 0 && SurfaceCount(fixture.transform) == 0,
                    "Destroy leaves no detached card copies beneath the board anchor");
                report.passed = true;
                Debug.Log("SWAN_JOURNEY_CARD_CHECKS_PASS checks=" + report.checks.Count + " nativeRenderingValidated=false");
            }
            catch (Exception ex) { report.error = ex.ToString(); throw; }
            finally
            {
                if (journey) journey.inSandbox = wasSandbox;
                if (fixture) UnityEngine.Object.DestroyImmediate(fixture);
                Directory.CreateDirectory(SwanJourneyBuild.Output);
                File.WriteAllText(SwanJourneyBuild.Output + "/card-checks.json", JsonUtility.ToJson(report, true));
            }
        }

        static void Invoke(StudioPartCards cards, string name)
        {
            typeof(StudioPartCards).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(cards, null);
        }

        static int SurfaceCount(Transform root)
        {
            return root.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("Swan card surface ", StringComparison.Ordinal));
        }

        static MeshRenderer[] CardSurfaceRenderers(Transform root) => root.GetComponentsInChildren<MeshRenderer>(true)
            .Where(r => r.name.StartsWith("Swan card surface ", StringComparison.Ordinal)).ToArray();

        static bool HasAuthoredColors(Transform root)
        {
            int panels = 0, edges = 0;
            foreach (var renderer in CardSurfaceRenderers(root))
            {
                if (!renderer.sharedMaterial) return false;
                bool panel = renderer.transform.parent.name.EndsWith("card", StringComparison.Ordinal);
                Color expected = panel ? new Color(.018f, .028f, .032f, 1) : new Color(.62f, .48f, .27f, .9f);
                Color actual = renderer.sharedMaterial.color;
                if (Mathf.Abs(actual.r - expected.r) > .0001f || Mathf.Abs(actual.g - expected.g) > .0001f ||
                    Mathf.Abs(actual.b - expected.b) > .0001f || Mathf.Abs(actual.a - expected.a) > .0001f) return false;
                if (panel) panels++; else edges++;
            }
            return panels == 2 && edges == 12;
        }

        static void CompareUguiLayout(TMP_Text source, Canvas canvas, int selection, Action<bool, string> check)
        {
            var referenceObject = new GameObject("Temporary original UGUI layout reference", typeof(RectTransform), typeof(TextMeshProUGUI));
            try
            {
                var reference = referenceObject.GetComponent<TextMeshProUGUI>();
                reference.transform.SetParent(canvas.transform, false);
                reference.rectTransform.pivot = source.rectTransform.pivot;
                reference.rectTransform.sizeDelta = source.rectTransform.rect.size;
                reference.font = source.font;
                reference.fontSharedMaterial = source.fontSharedMaterial;
                reference.fontSize = source.fontSize;
                reference.enableAutoSizing = source.enableAutoSizing;
                reference.fontSizeMin = source.fontSizeMin;
                reference.fontSizeMax = source.fontSizeMax;
                reference.fontStyle = source.fontStyle;
                reference.alignment = source.alignment;
                reference.enableWordWrapping = source.enableWordWrapping;
                reference.overflowMode = source.overflowMode;
                reference.margin = source.margin;
                reference.characterSpacing = source.characterSpacing;
                reference.wordSpacing = source.wordSpacing;
                reference.lineSpacing = source.lineSpacing;
                reference.paragraphSpacing = source.paragraphSpacing;
                reference.richText = source.richText;
                reference.extraPadding = source.extraPadding;
                reference.text = source.text;
                source.ForceMeshUpdate(true, true);
                reference.ForceMeshUpdate(true, true);
                string context = " group=" + selection + " label=" + source.name;
                check(source is TextMeshPro && source.isOrthographic && Mathf.Abs(source.fontSize - reference.fontSize) < .05f,
                    "3D TMP matches original UGUI resolved font size" + context + " 3D=" + source.fontSize + " UGUI=" + reference.fontSize);
                var a = source.mesh.bounds;
                var b = reference.mesh.bounds;
                check(Vector3.Distance(a.min, b.min) < .05f && Vector3.Distance(a.max, b.max) < .05f && a.size.y > 1,
                    "3D TMP glyph bounds match independent original UGUI layout within 0.05 pixels" + context + " 3D=" + a + " UGUI=" + b);
                bool sameLines = source.textInfo.characterCount == reference.textInfo.characterCount &&
                    source.textInfo.lineCount == reference.textInfo.lineCount;
                if (sameLines)
                    for (int i = 0; i < source.textInfo.characterCount; i++)
                        sameLines &= source.textInfo.characterInfo[i].lineNumber == reference.textInfo.characterInfo[i].lineNumber;
                check(sameLines, "3D TMP retains original UGUI character count and per-character line wrapping" + context);
            }
            finally { UnityEngine.Object.DestroyImmediate(referenceObject); }
        }

        static bool HasInitializedLeader(LineRenderer leader)
        {
            if (!leader || leader.positionCount != 3 || !Finite(leader.startWidth)
                || !Finite(leader.endWidth) || leader.startWidth <= 0 || leader.endWidth <= 0) return false;
            bool hasSegment = false;
            Vector3 previous = leader.GetPosition(0);
            for (int i = 0; i < leader.positionCount; i++)
            {
                Vector3 point = leader.GetPosition(i);
                if (!Finite(point.x) || !Finite(point.y) || !Finite(point.z)) return false;
                hasSegment |= (point - previous).sqrMagnitude > 1e-10f;
                previous = point;
            }
            return hasSegment;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
