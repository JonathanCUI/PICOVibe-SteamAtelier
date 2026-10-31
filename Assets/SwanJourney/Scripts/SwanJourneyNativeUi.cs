using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SwanJourney
{
    // Keep TMP's authored layout and hierarchy. SpatialAdapter has its own TMP
    // metadata path; a disabled TMP also disables its native node and children.
    [DefaultExecutionOrder(3300)]
    public sealed class SwanJourneyNativeUi : MonoBehaviour
    {
        public Transform content;
        public Material panelMaterial; // Kept for the existing scene-builder binding.
        readonly HashSet<int> discovered = new HashSet<int>();
        readonly HashSet<int> panelImages = new HashSet<int>();
        readonly List<Label> labels = new List<Label>();
        [SerializeField] List<PanelSurface> surfaces = new List<PanelSurface>();
        float nextScan, nextDiagnostic;
        bool announced;
        bool cardGlyphsAnnounced;
        int worldLabels, repairs, signals, rebuilt;

        [System.Serializable]
        sealed class PanelSurface
        {
            // Retain image for migration of previously serialized bindings.
            public UnityEngine.UI.Image image;
            public RectTransform rect;
            public Transform surface;
        }

        sealed class Label
        {
            public TMP_Text source;
            public bool world, prepared, signalled;
            public string text;
            public Color color;
            public TMP_FontAsset font;
            public Material material;
            public Vector2 size;
            public float fontSize;
            public TextAlignmentOptions alignment;
            public bool active, enabled;
        }

        static readonly Vector3[] EmptyPositions = {
            Vector3.zero, new Vector3(.0001f, 0, 0), new Vector3(0, .0001f, 0)
        };
        static readonly Vector3[] EmptyNormals = { Vector3.back, Vector3.back, Vector3.back };
        static readonly Vector2[] EmptyUvs = { Vector2.zero, Vector2.zero, Vector2.zero };
        static readonly Color32[] EmptyColors = { new Color32(0,0,0,0), new Color32(0,0,0,0), new Color32(0,0,0,0) };
        static readonly int[] EmptyIndices = { 0, 2, 1 };

        void LateUpdate() { using (SwanJourney.SwanJourneyCpuTimings.Measure(SwanJourney.SwanJourneyCpuTimings.Stage.NativeUi)) CpuMeasuredLateUpdate(); }
    void CpuMeasuredLateUpdate()
        {
            if (Time.unscaledTime >= nextScan) { nextScan = Time.unscaledTime + 1; Discover(); }
            RefreshNow();
            if (!cardGlyphsAnnounced && Time.unscaledTime >= 2 && content)
            {
                cardGlyphsAnnounced = true;
                foreach (var cards in content.GetComponentsInChildren<StudioPartCards>(true))
                {
                    if (!cards.journey || !cards.journey.SwanInputActive || !cards.PreparedSpatialCanvas) continue;
                    foreach (var label in cards.PreparedSpatialCanvas.GetComponentsInChildren<TMP_Text>(true))
                        Debug.Log("SWAN_JOURNEY_CARD_GLYPH type=" + label.GetType().Name + " name=" + label.name
                            + " active=" + label.gameObject.activeInHierarchy + " enabled=" + label.enabled
                            + " chars=" + label.textInfo.characterCount + " lines=" + label.textInfo.lineCount
                            + " fontSize=" + label.fontSize + " orthographic=" + label.isOrthographic
                            + " local=" + label.transform.localPosition.ToString("F3")
                            + " glyphBounds=" + (label.mesh ? label.mesh.bounds.ToString() : "missing")
                            + " world=" + label.transform.position.ToString("F3"));
                }
            }
            if (!announced || Time.unscaledTime >= nextDiagnostic)
            {
                nextDiagnostic = Time.unscaledTime + 10;
                int invalid = 0, active = 0;
                foreach (var label in labels)
                {
                    if (!label.source) continue;
                    if (label.world && label.source.isActiveAndEnabled) active++;
                    if (!HasUploadableMesh(label.source.mesh)) invalid++;
                }
                Debug.Log((announced ? "SWAN_JOURNEY_UI_HEALTH" : "SWAN_JOURNEY_UI_READY")
                    + " path=TMP original=" + labels.Count + " world=" + worldLabels + " active=" + active
                    + " nativeText=0 repairedZero=" + repairs + " dirtySignals=" + signals
                    + " rebuilt=" + rebuilt + " invalidMeshes=" + invalid);
                announced = true;
            }
        }

        // Run after the builder binds spatialAnchor / spatialLeaderMaterial,
        // before saving or exporting the derived scene. Use the original TMP
        // subtree so native text and its panel have the same exported ancestry.
        public void PrepareForExport()
        {
            if (Application.isPlaying)
                throw new System.InvalidOperationException("PrepareForExport is a derived-scene generation operation.");
            if (!content) throw new System.InvalidOperationException("Swan UI content is not bound.");
            foreach (var cards in content.GetComponentsInChildren<StudioPartCards>(true))
                if (cards.journey && cards.journey.SwanInputActive && cards.spatialAnchor)
                    cards.PrepareSpatialForExport();
            Canvas.ForceUpdateCanvases();
            Discover();
            RefreshNow();
        }

        // Editor generation supplies persistent native materials. Only authored
        // part-card Images are replaced; their RectTransforms, TMP and Canvas
        // ancestors remain the original objects. Desktop runtime keeps Images.
        public int ConvertPreparedCardImages(System.Func<Color, Material> resolveMaterial)
        {
            if (Application.isPlaying || resolveMaterial == null)
                throw new System.InvalidOperationException("Card Image conversion requires an export material resolver outside Play mode.");
            int converted = 0;
            foreach (var cards in content.GetComponentsInChildren<StudioPartCards>(true))
            {
                if (!cards.journey || !cards.journey.SwanInputActive || !cards.PreparedSpatialCanvas) continue;
                foreach (var image in cards.PreparedSpatialCanvas.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                {
                    var rect = image.rectTransform;
                    if (rect.GetComponent<TMP_Text>())
                        throw new System.InvalidOperationException("A card decoration unexpectedly shares its node with TMP: " + rect.name);
                    PanelSurface binding = null;
                    foreach (var entry in surfaces)
                        if (entry.rect == rect || entry.image == image) { binding = entry; break; }
                    if (binding == null || !binding.surface)
                        throw new System.InvalidOperationException("Card Image has no prepared native mesh: " + rect.name);
                    var renderer = binding.surface.GetComponent<MeshRenderer>();
                    var material = resolveMaterial(image.color);
                    if (!renderer || !material)
                        throw new System.InvalidOperationException("Card decoration requires a native mesh and material: " + rect.name);
                    renderer.sharedMaterial = material;
                    binding.rect = rect;
                    binding.image = null;
                    // CanvasRenderer has its own native visibility path even
                    // without Image.enabled. Remove it only from these non-TMP
                    // decoration nodes so it cannot export a second UI quad.
                    DestroyImmediate(image);
                    foreach (var canvasRenderer in rect.GetComponents<CanvasRenderer>()) DestroyImmediate(canvasRenderer);
                    converted++;
                }
            }
            return converted;
        }

        public void Discover()
        {
            if (!content) return;
            foreach (var source in content.GetComponentsInChildren<TMP_Text>(true))
            {
                if (!discovered.Add(source.GetInstanceID())) continue;
                var canvas = source.GetComponentInParent<Canvas>(true);
                bool world = !canvas || canvas.renderMode == RenderMode.WorldSpace;
                // The desktop overlay is gated by SteamJourney. Do not turn it
                // into another floating label set if a partner extension adds one.
                if (!world) { canvas.enabled = false; source.enabled = false; }
                labels.Add(new Label { source = source, world = world });
                if (world) worldLabels++;
            }
            // Retain the native mesh backdrop for dynamically-created part
            // cards. Keep the Canvas/Image enabled; disabling their native
            // parent node would also hide its text and these child surfaces.
            if (panelMaterial)
            {
                // Serialized bindings survive scene load. Discovery must reuse
                // the exported surfaces rather than adding a second backdrop.
                foreach (var entry in surfaces)
                {
                    if (!entry.rect && entry.image) entry.rect = entry.image.rectTransform;
                    if (entry.rect && entry.surface) panelImages.Add(entry.rect.GetInstanceID());
                }
                foreach (var canvas in content.GetComponentsInChildren<Canvas>(true))
                {
                    if (canvas.renderMode != RenderMode.WorldSpace) continue;
                    foreach (var image in canvas.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                    {
                        var rect = image.rectTransform;
                        if (!panelImages.Add(rect.GetInstanceID())) continue;
                        var surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        surface.name = "Swan card surface " + image.name;
                        var collider = surface.GetComponent<Collider>();
                        if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
                        surface.transform.SetParent(image.transform, false);
                        surface.transform.localPosition = new Vector3(rect.rect.center.x, rect.rect.center.y, .06f);
                        surface.transform.localScale = new Vector3(rect.rect.width, rect.rect.height, .05f);
                        surface.GetComponent<MeshRenderer>().sharedMaterial = panelMaterial;
                        surfaces.Add(new PanelSurface { image = image, rect = rect, surface = surface.transform });
                    }
                }
            }
        }

        // SDK 6.1.0's PostFrameCallbackReceiver runs at order 99999. Prepare a
        // complete mesh before its TMP upload, including for disabled TMPs: the
        // SDK checks activeInHierarchy, not enabled, before updating their mesh.
        public void RefreshNow()
        {
            foreach (var label in labels)
            {
                var source = label.source;
                if (!source) continue;
                var rect = source.rectTransform;
                bool changed = !label.prepared || label.text != source.text || label.color != source.color
                    || label.font != source.font || label.material != source.fontSharedMaterial
                    || label.fontSize != source.fontSize || label.alignment != source.alignment
                    || label.size != rect.rect.size
                    || label.active != source.gameObject.activeInHierarchy || label.enabled != source.enabled;
                // Parent pose/scale is already synchronized by the adapter's transform
                // path. It does not change local glyph positions, colours or UV0.
                // Treating lossyScale as a text edit queued every visible label for a
                // native mesh upload on each manipulation frame (34 labels in V23).
                // TMP handles SDF scale in UV2 separately; native text uploads use
                // position/colour/UV0, not that camera-dependent channel.
                // The flag from our preceding upload signal is not a new edit.
                // Without this distinction a disabled/empty TMP queues forever.
                bool dirty = changed || (source.havePropertiesChanged && !label.signalled);
                if (dirty)
                {
                    source.ForceMeshUpdate(true);
                    rebuilt++;
                }
                if (EnsureUploadableMesh(source.mesh)) repairs++;
                // Font fallback submeshes can also be retained with no glyphs
                // after a text change. Keep their separate mesh buffers valid.
                var meshInfo = source.textInfo?.meshInfo;
                if (meshInfo != null)
                    foreach (var info in meshInfo)
                        if (info.mesh != source.mesh && EnsureUploadableMesh(info.mesh)) repairs++;
                bool signal = dirty && label.world && source.isActiveAndEnabled;
                source.havePropertiesChanged = signal;
                label.signalled = signal;
                if (signal) signals++;
                label.prepared = true;
                label.text = source.text; label.color = source.color; label.font = source.font;
                label.material = source.fontSharedMaterial; label.fontSize = source.fontSize;
                label.alignment = source.alignment; label.size = rect.rect.size;
                label.active = source.gameObject.activeInHierarchy; label.enabled = source.enabled;
            }
        }

        // Empty TMP strings are legitimate state (e.g. the inactive driving
        // readout). Preserve .text and all partner references, while supplying a
        // tiny transparent triangle to the SDK, which rejects zero indices.
        static bool EnsureUploadableMesh(Mesh mesh)
        {
            if (!mesh || HasUploadableMesh(mesh)) return false;
            mesh.Clear();
            mesh.vertices = EmptyPositions;
            mesh.normals = EmptyNormals;
            mesh.uv = EmptyUvs;
            mesh.uv2 = EmptyUvs;
            mesh.colors32 = EmptyColors;
            mesh.triangles = EmptyIndices;
            mesh.RecalculateBounds();
            return true;
        }

        public static bool HasUploadableMesh(Mesh mesh)
        {
            if (!mesh || mesh.vertexCount == 0 || mesh.subMeshCount == 0) return false;
            for (int i = 0; i < mesh.subMeshCount; i++)
                if (mesh.GetIndexCount(i) > 0) return true;
            return false;
        }

        // Compatibility with the existing checker: prepared original TMP labels.
        public int ConvertedLabelCount => worldLabels;
        public int RepairedEmptyMeshCount => repairs;
        public int DirtySignalCount => signals;
    }
}
