using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityMeshSimplifier;

namespace SwanJourney.Editor
{
    // Runs after the independent canyon payload is detached. Count instances, including inactive
    // alternatives and the complete native marker pool. A shared mesh counts once per placement.
    public static class SwanJourneyWorkshopMeshBudget
    {
        public const int Limit = 1200000;
        const string Folder = "Assets/SwanJourney/Generated/WorkshopBudget";
        [Serializable] public class Report
        {
            public long before, after;
            public int instances, derivedMeshes;
            public long scanBefore, scanAfter, silhouetteBefore, silhouetteAfter;
        }
        public static long Triangles(Mesh mesh)
        {
            long n = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
                if (mesh.GetTopology(i) == MeshTopology.Triangles) n += mesh.GetIndexCount(i) / 3;
            return n;
        }
        enum Role { Surface, Silhouette, Contour, Precision, SocketHull }
        static Role Classify(MeshFilter filter)
        {
            string n = filter.name.ToLowerInvariant();
            if (n.Contains("travelling structural scan")) return Role.Contour;
            if (n.Contains("assembly target silhouette")) return Role.SocketHull;
            if (n.StartsWith("ghost shell")) return Role.Silhouette;
            if (n.Contains("rivet") || n.Contains("knurl") || n.Contains("screw") || n.Contains("bolt") || n.Contains("紧固")) return Role.Precision;
            return Role.Surface;
        }
        public static Report Apply(IEnumerable<MeshFilter> input)
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var report = new Report();
            var filters = input.Where(f => f && f.sharedMesh).ToArray();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var group in filters.GroupBy(f => (source: f.sharedMesh, role: Classify(f))).ToArray())
                {
                    var source = group.Key.source;
                    var role = group.Key.role;
                    long original = Triangles(source);
                    int count = group.Count();
                    string path = AssetDatabase.GetAssetPath(source);
                    Mesh mesh = source;
                    if (original > 96 && source.isReadable && source.blendShapeCount == 0 &&
                        !source.name.ToLowerInvariant().Contains("water") && !path.StartsWith(Folder + "/") &&
                        Enumerable.Range(0, source.subMeshCount).All(i => source.GetTopology(i) == MeshTopology.Triangles))
                    {
                        float ratio = role == Role.Silhouette ? .12f : role == Role.Precision ? .22f :
                            original < 500 ? .46f : original < 4000 ? .34f : .28f;
                        var reduced = role == Role.Contour ? SimplifyStrokes(source) :
                            role == Role.SocketHull && source.subMeshCount == 1 ? BuildSocketHull(source) : Reduce(source, ratio);
                        // Readable GPU geometry with identical material slots and all retained vertex attributes.
                        if (reduced && reduced.subMeshCount == source.subMeshCount && Triangles(reduced) < original &&
                            Enumerable.Range(0, source.subMeshCount).All(i => reduced.GetIndexCount(i) > 0))
                        {
                            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long id);
                            string asset = Folder + "/" + guid + "_" + id + "_" + role + ".asset";
                            reduced.name = source.name + " • tabletop";
                            mesh = AssetDatabase.LoadAssetAtPath<Mesh>(asset);
                            if (mesh) { EditorUtility.CopySerialized(reduced, mesh); UnityEngine.Object.DestroyImmediate(reduced); EditorUtility.SetDirty(mesh); }
                            else { AssetDatabase.CreateAsset(reduced, asset); mesh = reduced; }
                            foreach (var filter in group) filter.sharedMesh = mesh;
                            report.derivedMeshes++;
                        }
                        else if (reduced) UnityEngine.Object.DestroyImmediate(reduced);
                    }
                    long before = count * original, after = count * Triangles(mesh);
                    report.instances += count; report.before += before; report.after += after;
                    if (role == Role.Contour) { report.scanBefore += before; report.scanAfter += after; }
                    if (role == Role.Silhouette || role == Role.SocketHull) { report.silhouetteBefore += before; report.silhouetteAfter += after; }
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.SaveAssets();
            Debug.Log("SWAN_WORKSHOP_MESH_BUDGET " + JsonUtility.ToJson(report));
            if (report.after > Limit) throw new InvalidOperationException("Workshop exceeds " + Limit + " triangles: " + report.after);
            return report;
        }
        static Mesh Reduce(Mesh source, float ratio)
        {
            var simplifier = new MeshSimplifier();
            var options = SimplificationOptions.Default;
            options.PreserveBorderEdges = true;
            // The quadric error metric ranks collapses by distance from the original surface.
            simplifier.SimplificationOptions = options;
            simplifier.Initialize(source);
            simplifier.SimplifyMesh(ratio);
            var mesh = simplifier.ToMesh();
            mesh.RecalculateBounds();
            var a = source.bounds; var b = mesh.bounds;
            // Reject collapses that erase outlying features, or collapse a thin dimension.
            var slack = .001f * a.size.magnitude * Vector3.one + .05f * a.size;
            for (int i=0;i<3;i++)
                if (Mathf.Abs(a.min[i]-b.min[i])>slack[i] || Mathf.Abs(a.max[i]-b.max[i])>slack[i])
                { UnityEngine.Object.DestroyImmediate(mesh); return null; }
            if (source.normals.Length != 0 && mesh.normals.Length == mesh.vertexCount)
            {
                var normals = mesh.normals;
                for (int i=0;i<normals.Length;i++) normals[i].Normalize();
                mesh.normals = normals;
                if (source.tangents.Length != 0 && mesh.uv.Length != 0) mesh.RecalculateTangents();
            }
            return mesh;
        }
        // The positioning guide has one untextured matte material. Weld positions into a grid
        // only for that proxy, retaining winding and the original placement of its large features.
        // Using original vertices instead of voxel cubes preserves flat panels and round profiles.
        static Mesh BuildSocketHull(Mesh source)
        {
            var positions = source.vertices;
            var bounds = source.bounds;
            float cell = Mathf.Max(.000001f, Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) / 60f);
            var cells = new Dictionary<Vector3Int, int>();
            var sums = new List<Vector3>();
            var counts = new List<int>();
            var remap = new int[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                var key = Vector3Int.RoundToInt((positions[i] - bounds.center) / cell);
                if (!cells.TryGetValue(key, out var id))
                {
                    id = sums.Count; cells.Add(key,id); sums.Add(Vector3.zero); counts.Add(0);
                }
                remap[i] = id; sums[id] += positions[i]; counts[id]++;
            }
            for (int i = 0; i < sums.Count; i++) sums[i] /= counts[i];
            var original = source.GetIndices(0);
            var triangles = new List<int>();
            var visited = new HashSet<(int,int,int)>();
            for (int i = 0; i < original.Length; i += 3)
            {
                int a=remap[original[i]], b=remap[original[i+1]], c=remap[original[i+2]];
                if (a==b || b==c || c==a) continue;
                var key = a<b && a<c ? (a,b,c) : b<c ? (b,c,a) : (c,a,b);
                if (!visited.Add(key)) continue;
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
            }
            if (triangles.Count == 0) return null;
            var mesh = new Mesh{indexFormat=sums.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.SetVertices(sums); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        // The authored scan uses separate capped triangular prisms. Ordinary decimation can
        // destroy these isolated thin tubes. Retain complete strokes by location and direction.
        static Mesh SimplifyStrokes(Mesh source)
        {
            const int indicesPerStroke = 24;
            if (source.subMeshCount != 1 || source.GetIndexCount(0) % indicesPerStroke != 0) return null;
            var vertices = source.vertices;
            var indices = source.GetIndices(0);
            var strokes = new Dictionary<(Vector3Int, int), (int offset, float lengthSquared)>();
            float cell = Mathf.Max(.00001f, source.bounds.size.magnitude / 13f);
            for (int i = 0; i < indices.Length; i += indicesPerStroke)
            {
                int first = indices[i];
                if (first + 5 >= vertices.Length || indices[i + 2] != first + 3) return null;
                for (int j = 0; j < indicesPerStroke; j++)
                    if (indices[i + j] < first || indices[i + j] > first + 5) return null;
                var a = (vertices[first] + vertices[first + 1] + vertices[first + 2]) / 3f;
                var b = (vertices[first + 3] + vertices[first + 4] + vertices[first + 5]) / 3f;
                var delta = b - a;
                var dir = delta.normalized;
                int axis = Mathf.Abs(dir.x) > Mathf.Abs(dir.y) ? 0 : 1;
                if (Mathf.Abs(dir.z) > Mathf.Abs(dir[axis])) axis = 2;
                var key = (Vector3Int.FloorToInt(((a + b) * .5f - source.bounds.min) / cell), axis);
                if (!strokes.TryGetValue(key, out var old) || delta.sqrMagnitude > old.lengthSquared)
                    strokes[key] = (i, delta.sqrMagnitude);
            }
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var uv2 = new List<Vector2>();
            var colors = new List<Color32>();
            var triangles = new List<int>();
            var sourceNormals = source.normals;
            var sourceUv = source.uv;
            var sourceUv2 = source.uv2;
            var sourceColors = source.colors32;
            foreach (var entry in strokes.Values.OrderBy(e => e.offset))
            {
                int first = indices[entry.offset], start = positions.Count;
                for (int j = first; j < first + 6; j++)
                {
                    int end = j < first + 3 ? first : first + 3;
                    var center = (vertices[end] + vertices[end + 1] + vertices[end + 2]) / 3f;
                    positions.Add(center + (vertices[j] - center) * 2.5f);
                    if (sourceNormals.Length > 0) normals.Add(sourceNormals[j]);
                    if (sourceUv.Length > 0) uv.Add(sourceUv[j]);
                    if (sourceUv2.Length > 0) uv2.Add(sourceUv2[j]);
                    if (sourceColors.Length > 0) colors.Add(sourceColors[j]);
                }
                for (int j = 0; j < indicesPerStroke; j++)
                    triangles.Add(start + indices[entry.offset + j] - first);
            }
            var mesh = new Mesh { indexFormat = positions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(positions);
            mesh.SetTriangles(triangles, 0);
            if (normals.Count > 0) mesh.SetNormals(normals);
            if (uv.Count > 0) mesh.SetUVs(0, uv);
            if (uv2.Count > 0) mesh.SetUVs(1, uv2);
            if (colors.Count > 0) mesh.SetColors(colors);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
