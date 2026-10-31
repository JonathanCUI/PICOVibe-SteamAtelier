using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SwanJourney.Editor
{
    /// <summary>Audits derived meshes and prepares bounds-preserving input colliders.</summary>
    public static class SwanJourneyMeshAudit
    {
        const string PositiveColliderName = "Swan positive-scale hit proxy";

        [Serializable]
        public sealed class PreparationReport
        {
            public int convertedFilters, convertedMeshes, contourSegments, removedEmptyFilters;
            public string[] convertedPaths, emptyPaths;
        }

        public static PreparationReport PrepareForExport(Transform content, string generatedFolder)
        {
            if (!content) throw new ArgumentNullException(nameof(content));
            if (string.IsNullOrEmpty(generatedFolder) || !generatedFolder.Replace('\\', '/').StartsWith("Assets/", StringComparison.Ordinal))
                throw new ArgumentException("Generated mesh folder must be inside Assets", nameof(generatedFolder));
            string folder = generatedFolder.Replace('\\', '/').TrimEnd('/') + "/ExportGeometry";
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            var journey = content.GetComponentInChildren<SteamJourney>(true);
            var converted = new Dictionary<Mesh, Mesh>();
            var convertedPaths = new List<string>();
            var emptyPaths = new List<string>();
            var report = new PreparationReport();
            foreach (var filter in content.GetComponentsInChildren<MeshFilter>(true))
            {
                // The SDK temporarily substitutes '_' for empty TMP strings and
                // forces their mesh update. Keep these component/source bindings.
                if (filter.GetComponent<TMP_Text>()) continue;
                var source = filter.sharedMesh;
                if (!source) continue;
                bool empty = source.vertexCount == 0 || source.subMeshCount == 0;
                long indexCount = 0;
                for (int s = 0; s < source.subMeshCount; s++) indexCount += source.GetIndexCount(s);
                empty |= indexCount == 0;
                if (empty)
                {
                    if (!journey || !journey.sandboxRoot || !filter.transform.IsChildOf(journey.sandboxRoot.transform))
                        throw new InvalidOperationException("Unexpected empty non-text mesh outside canyon: " + PathOf(filter.transform));
                    emptyPaths.Add(PathOf(filter.transform) + " | " + AssetDatabase.GetAssetPath(source));
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (renderer) renderer.enabled = false;
                    // A null MeshFilter mesh is replaced with a placeholder by
                    // this exporter. Removing only the filter avoids that fake
                    // geometry while preserving renderer and GameObject bindings.
                    UnityEngine.Object.DestroyImmediate(filter);
                    report.removedEmptyFilters++;
                    continue;
                }
                bool hasLines = false;
                for (int s = 0; s < source.subMeshCount; s++)
                {
                    var topology = source.GetTopology(s);
                    if (topology == MeshTopology.Lines || topology == MeshTopology.LineStrip) hasLines = true;
                    else if (topology != MeshTopology.Triangles)
                        throw new InvalidOperationException("Unsupported contour topology " + topology + ": " + PathOf(filter.transform));
                }
                if (!hasLines) continue;
                if (!converted.TryGetValue(source, out var mesh))
                {
                    mesh = TriangulateContours(source, out int segments);
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long localId);
                    if (string.IsNullOrEmpty(guid))
                        throw new InvalidOperationException("Contour mesh is not a persistent source asset: " + PathOf(filter.transform));
                    string path = folder + "/Contours_" + guid + "_" + localId + ".asset";
                    var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (existing)
                    {
                        EditorUtility.CopySerialized(mesh, existing);
                        UnityEngine.Object.DestroyImmediate(mesh);
                        mesh = existing;
                        EditorUtility.SetDirty(mesh);
                    }
                    else AssetDatabase.CreateAsset(mesh, path);
                    converted.Add(source, mesh);
                    report.convertedMeshes++;
                    report.contourSegments += segments;
                }
                filter.sharedMesh = mesh;
                EditorUtility.SetDirty(filter);
                convertedPaths.Add(PathOf(filter.transform) + " | " + AssetDatabase.GetAssetPath(mesh));
                report.convertedFilters++;
            }
            report.convertedPaths = convertedPaths.ToArray();
            report.emptyPaths = emptyPaths.ToArray();
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Builds/SwanJourney");
            File.WriteAllText("Builds/SwanJourney/mesh-export-preparation.json", JsonUtility.ToJson(report, true));
            Debug.Log("SWAN_JOURNEY_EXPORT_GEOMETRY convertedFilters=" + report.convertedFilters +
                " convertedMeshes=" + report.convertedMeshes + " contourSegments=" + report.contourSegments +
                " removedEmptyFilters=" + report.removedEmptyFilters + " TMP=SDK-deferred");
            return report;
        }

        static Mesh TriangulateContours(Mesh source, out int segmentCount)
        {
            if (!source.isReadable) throw new InvalidOperationException("Contour source must be readable: " + AssetDatabase.GetAssetPath(source));
            var original = source.vertices;
            var vertices = new List<Vector3>(original);
            var originalNormals = source.normals;
            var originalUv = source.uv;
            var originalUv2 = source.uv2;
            var originalColors = source.colors;
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var uv2 = new List<Vector2>();
            var colors = new List<Color>();
            for (int i = 0; i < original.Length; i++)
            {
                normals.Add(originalNormals.Length == original.Length ? originalNormals[i] : Vector3.up);
                uv.Add(originalUv.Length == original.Length ? originalUv[i] : Vector2.zero);
                uv2.Add(originalUv2.Length == original.Length ? originalUv2[i] : Vector2.zero);
                colors.Add(originalColors.Length == original.Length ? originalColors[i] : Color.white);
            }
            var submeshes = new List<int>[source.subMeshCount];
            segmentCount = 0;
            // 0.7 mm diameter at the default 0.12 workshop scale; a narrow
            // three-sided tube stays visible from every angle without billboards.
            float radius = Mathf.Clamp(source.bounds.size.magnitude * .0008f, .0015f, .003f);
            for (int s = 0; s < source.subMeshCount; s++)
            {
                int[] indices = source.GetIndices(s);
                var topology = source.GetTopology(s);
                var triangles = submeshes[s] = new List<int>();
                if (topology == MeshTopology.Triangles) { triangles.AddRange(indices); continue; }
                int step = topology == MeshTopology.Lines ? 2 : 1;
                if (topology == MeshTopology.Lines && indices.Length % 2 != 0)
                    throw new InvalidOperationException("Unpaired line index in " + AssetDatabase.GetAssetPath(source));
                for (int i = 0; i + 1 < indices.Length; i += step)
                {
                    int first = indices[i], second = indices[i + 1];
                    Vector3 a = original[first], b = original[second], direction = b - a;
                    if (direction.sqrMagnitude < 1e-12f) continue;
                    direction.Normalize();
                    Vector3 radial = Vector3.Cross(direction, Mathf.Abs(direction.y) < .9f ? Vector3.up : Vector3.right).normalized;
                    Vector3 other = Vector3.Cross(direction, radial).normalized;
                    int start = vertices.Count;
                    for (int end = 0; end < 2; end++) for (int side = 0; side < 3; side++)
                    {
                        float angle = side * Mathf.PI * 2 / 3;
                        Vector3 normal = radial * Mathf.Cos(angle) + other * Mathf.Sin(angle);
                        int sourceVertex = end == 0 ? first : second;
                        vertices.Add((end == 0 ? a : b) + normal * radius);
                        normals.Add(normal);
                        uv.Add(originalUv.Length == original.Length ? originalUv[sourceVertex] : new Vector2(side / 3f, end));
                        uv2.Add(originalUv2.Length == original.Length ? originalUv2[sourceVertex] : Vector2.zero);
                        colors.Add(originalColors.Length == original.Length ? originalColors[sourceVertex] : Color.white);
                    }
                    for (int side = 0; side < 3; side++)
                    {
                        int next = (side + 1) % 3;
                        triangles.Add(start + side); triangles.Add(start + next); triangles.Add(start + side + 3);
                        triangles.Add(start + next); triangles.Add(start + next + 3); triangles.Add(start + side + 3);
                    }
                    triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
                    triangles.Add(start + 3); triangles.Add(start + 4); triangles.Add(start + 5);
                    segmentCount++;
                }
                if (triangles.Count == 0)
                    throw new InvalidOperationException("Contour submesh has no nonzero segments: " + AssetDatabase.GetAssetPath(source));
            }
            var result = new Mesh { name = source.name + " Swan triangular contours", indexFormat = IndexFormat.UInt32 };
            result.SetVertices(vertices); result.SetNormals(normals);
            result.SetUVs(0, uv); result.SetUVs(1, uv2); result.SetColors(colors);
            result.subMeshCount = source.subMeshCount;
            for (int s = 0; s < submeshes.Length; s++) result.SetIndices(submeshes[s].ToArray(), MeshTopology.Triangles, s, false);
            result.RecalculateBounds();
            return result;
        }

        // A reflected parent and one reflected child have a positive world
        // determinant. Mirroring the centre by the same sign keeps every box
        // corner in precisely the same world position, including off-centre
        // wheel meshes. Parenting also follows the original wheel animation.
        public static BoxCollider EnsurePositiveCollider(MeshRenderer renderer, Bounds bounds)
        {
            if (!renderer) throw new ArgumentNullException(nameof(renderer));
            var original = renderer.GetComponents<BoxCollider>();
            var previous = original.Length > 0 ? original[0] : null;
            Vector3 sourceScale = renderer.transform.lossyScale;
            bool reflected = sourceScale.x < 0 || sourceScale.y < 0 || sourceScale.z < 0;
            var proxy = renderer.transform.Find(PositiveColliderName);
            if (!reflected)
            {
                if (proxy) UnityEngine.Object.DestroyImmediate(proxy.gameObject);
                var box = previous ? previous : renderer.gameObject.AddComponent<BoxCollider>();
                box.center = bounds.center;
                box.size = Vector3.Max(bounds.size, Vector3.one * .025f);
                return box;
            }

            if (!proxy)
            {
                proxy = new GameObject(PositiveColliderName).transform;
                proxy.SetParent(renderer.transform, false);
            }
            proxy.gameObject.layer = renderer.gameObject.layer;
            proxy.localPosition = Vector3.zero;
            proxy.localRotation = Quaternion.identity;
            // Unity may retain the reflection sign on any axis after the
            // wheel's nested rotations. Cancel the actual per-axis signs.
            proxy.localScale = new Vector3(Mathf.Sign(sourceScale.x), Mathf.Sign(sourceScale.y), Mathf.Sign(sourceScale.z));
            var collider = proxy.GetComponent<BoxCollider>();
            if (!collider) collider = proxy.gameObject.AddComponent<BoxCollider>();
            if (previous)
            {
                collider.sharedMaterial = previous.sharedMaterial;
                collider.isTrigger = previous.isTrigger;
                collider.enabled = previous.enabled;
            }
            collider.center = Vector3.Scale(bounds.center, proxy.localScale);
            collider.size = Vector3.Max(bounds.size, Vector3.one * .025f);

            // Fail generation if this hierarchy contains an unsupported scale
            // decomposition. Never silently replace a collider with a bad hit
            // volume or mutate its visible mesh to suppress a PhysX warning.
            Vector3 scale = proxy.lossyScale;
            if (scale.x <= 0 || scale.y <= 0 || scale.z <= 0)
                throw new InvalidOperationException("Positive collider proxy has non-positive scale: " + PathOf(proxy) +
                    " source=" + sourceScale + " proxy=" + scale);
            for (int corner = 0; corner < 8; corner++)
            {
                var sign = new Vector3((corner & 1) == 0 ? -1 : 1,
                    (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1);
                Vector3 source = bounds.center + Vector3.Scale(collider.size * .5f, sign);
                Vector3 expected = renderer.transform.TransformPoint(source);
                Vector3 actual = proxy.TransformPoint(Vector3.Scale(source, proxy.localScale));
                if ((expected - actual).sqrMagnitude > .00000001f)
                    throw new InvalidOperationException("Collider proxy changed a world-space box corner: " + PathOf(proxy));
            }
            foreach (var box in original) UnityEngine.Object.DestroyImmediate(box);
            // Keep the hover component on the original visible renderer. Input
            // target resolution walks parents from this semantic child proxy.
            return collider;
        }

        [Serializable]
        public sealed class MeshIssue
        {
            public string path, mesh, asset, issue, topology;
            public int submesh = -1, vertices;
            public long indices;
            public bool active, rendererEnabled;
        }

        [Serializable]
        public sealed class BoxIssue
        {
            public string path;
            public Vector3 lossyScale, size;
            public bool active, enabled;
        }

        [Serializable]
        public sealed class Report
        {
            public string scene, root;
            public bool passed;
            public int meshFilters, uniqueMeshes, nonTriangleSubmeshes, emptySubmeshes, missingMeshes, deferredNativeText;
            public MeshIssue[] meshIssues;
            public BoxIssue[] negativeScaleBoxes;
        }

        public static Report Run(Transform root, string outputPath = null)
        {
            if (!root) throw new ArgumentNullException(nameof(root));
            var report = new Report { scene = root.gameObject.scene.path, root = PathOf(root) };
            var issues = new List<MeshIssue>();
            var boxes = new List<BoxIssue>();
            var meshes = new HashSet<Mesh>();
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            report.meshFilters = filters.Length;
            foreach (var filter in filters)
            {
                var mesh = filter.sharedMesh;
                if (filter.GetComponent<TMP_Text>() && (!mesh || mesh.vertexCount == 0 || mesh.subMeshCount == 0))
                {
                    report.deferredNativeText++;
                    continue;
                }
                if (!mesh)
                {
                    report.missingMeshes++;
                    issues.Add(Issue(filter, -1, "Missing mesh", "", 0));
                    continue;
                }
                meshes.Add(mesh);
                if (mesh.subMeshCount == 0 || mesh.vertexCount == 0)
                {
                    report.emptySubmeshes++;
                    issues.Add(Issue(filter, -1, mesh.vertexCount == 0 ? "No vertices" : "No submeshes", "", 0));
                    continue;
                }
                // GetTopology/GetIndexCount work for GPU-only imported meshes.
                // Do not call triangles/GetTriangles: those are exactly the
                // exporter calls that reject Lines/Points and unreadable data.
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    var topology = mesh.GetTopology(submesh);
                    long indices = mesh.GetIndexCount(submesh);
                    if (topology != MeshTopology.Triangles)
                    {
                        report.nonTriangleSubmeshes++;
                        issues.Add(Issue(filter, submesh, "Non-triangle topology", topology.ToString(), indices));
                    }
                    if (indices == 0)
                    {
                        report.emptySubmeshes++;
                        issues.Add(Issue(filter, submesh, "Empty index array", topology.ToString(), indices));
                    }
                    else if (topology == MeshTopology.Triangles && indices % 3 != 0)
                        issues.Add(Issue(filter, submesh, "Incomplete triangle index array", topology.ToString(), indices));
                }
            }
            foreach (var box in root.GetComponentsInChildren<BoxCollider>(true))
            {
                Vector3 scale = box.transform.lossyScale, size = box.size;
                if (scale.x >= 0 && scale.y >= 0 && scale.z >= 0 && size.x >= 0 && size.y >= 0 && size.z >= 0) continue;
                boxes.Add(new BoxIssue { path = PathOf(box.transform), lossyScale = scale, size = size,
                    active = box.gameObject.activeInHierarchy, enabled = box.enabled });
            }
            report.uniqueMeshes = meshes.Count;
            report.meshIssues = issues.ToArray();
            report.negativeScaleBoxes = boxes.ToArray();
            report.passed = issues.Count == 0;
            if (!string.IsNullOrEmpty(outputPath))
            {
                string directory = System.IO.Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(outputPath, JsonUtility.ToJson(report, true));
            }
            Debug.Log("SWAN_JOURNEY_MESH_AUDIT passed=" + report.passed + " filters=" + filters.Length +
                " unique=" + meshes.Count + " nonTriangle=" + report.nonTriangleSubmeshes +
                " empty=" + report.emptySubmeshes + " missing=" + report.missingMeshes +
                " negativeBoxes=" + boxes.Count + " deferredNativeText=" + report.deferredNativeText + " report=" + outputPath);
            for (int i = 0; i < Mathf.Min(issues.Count, 20); i++)
            {
                var issue = issues[i];
                Debug.LogWarning("SWAN_JOURNEY_MESH_ISSUE " + issue.issue + " path=" + issue.path +
                    " mesh=" + issue.mesh + " asset=" + issue.asset + " submesh=" + issue.submesh +
                    " topology=" + issue.topology + " indices=" + issue.indices);
            }
            return report;
        }

        static MeshIssue Issue(MeshFilter filter, int submesh, string issue, string topology, long indices)
        {
            var mesh = filter.sharedMesh;
            var renderer = filter.GetComponent<Renderer>();
            return new MeshIssue { path = PathOf(filter.transform), mesh = mesh ? mesh.name : "",
                asset = mesh ? AssetDatabase.GetAssetPath(mesh) : "", issue = issue,
                topology = topology, submesh = submesh, vertices = mesh ? mesh.vertexCount : 0,
                indices = indices, active = filter.gameObject.activeInHierarchy,
                rendererEnabled = renderer && renderer.enabled };
        }

        static string PathOf(Transform value)
        {
            string path = value.name;
            while (value.parent) { value = value.parent; path = value.name + "/" + path; }
            return path;
        }
    }
}
