using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SwanJourney.Editor
{
    // Derived canyon only: remove channels which have no visual contribution.
    // No simplification, source mutation, vertex movement or triangle removal.
    public static class SwanJourneyCanyonMeshBudget
    {
        [Serializable] sealed class Report
        {
            public string scope = "Derived canyon mesh channels only; Unity buffer estimates, not measured GLB or native memory";
            public bool passed;
            public int uniqueMeshes, clonedMeshes, filtersRebound, collidersRebound, uv1Removed, whiteColorsRemoved, nonwhiteColorsPreserved;
            public long uniqueBufferBytesBefore, uniqueBufferBytesAfter, referencedTrianglesBefore, referencedTrianglesAfter;
            public float maximumBoundsError;
        }

        public static void Apply(Transform canyon, string generatedDirectory)
        {
            if (Application.isPlaying || !canyon || EditorUtility.IsPersistent(canyon)
                || !canyon.name.StartsWith("Mesa original canyon", StringComparison.Ordinal))
                throw new InvalidOperationException("Canyon mesh budgeting requires the in-memory derived canyon.");
            if (canyon.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length != 0)
                throw new InvalidOperationException("Canyon channel optimization is restricted to static mesh renderers.");
            var filters = canyon.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh).ToArray();
            foreach (var renderer in canyon.GetComponentsInChildren<MeshRenderer>(true)) ValidateMaterialUv(renderer);
            var report = new Report();
            var clones = new Dictionary<Mesh, Mesh>();
            string folder = generatedDirectory + "/CanyonLean";
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            foreach (var source in filters.Select(f => f.sharedMesh).Distinct())
            {
                report.uniqueMeshes++;
                report.uniqueBufferBytesBefore += BufferBytes(source);
                bool uv1 = source.HasVertexAttribute(VertexAttribute.TexCoord1);
                var colors = new List<Color>(); source.GetColors(colors);
                bool white = colors.Count == source.vertexCount && colors.Count > 0
                    && colors.All(c => c.r == 1f && c.g == 1f && c.b == 1f && c.a == 1f);
                if (colors.Count > 0 && !white) report.nonwhiteColorsPreserved++;
                if (!uv1 && !white) { report.uniqueBufferBytesAfter += BufferBytes(source); continue; }
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long fileId))
                    throw new InvalidOperationException("Canyon mesh must be persisted before budgeting: " + source.name);
                var copy = UnityEngine.Object.Instantiate(source);
                copy.name = source.name + " Swan lean channels";
                if (uv1) { copy.SetUVs(1, new List<Vector4>()); report.uv1Removed++; }
                if (white) { copy.SetColors(new List<Color>()); report.whiteColorsRemoved++; }
                ValidateEquivalent(source, copy, white);
                string path = folder + "/Mesh_" + guid + "_" + fileId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing) { EditorUtility.CopySerialized(copy, existing); UnityEngine.Object.DestroyImmediate(copy); copy = existing; EditorUtility.SetDirty(copy); }
                else AssetDatabase.CreateAsset(copy, path);
                ValidateEquivalent(source, copy, white);
                report.uniqueBufferBytesAfter += BufferBytes(copy);
                clones.Add(source, copy); report.clonedMeshes++;
            }
            foreach (var filter in filters)
            {
                var before = filter.sharedMesh;
                report.referencedTrianglesBefore += Triangles(before);
                if (clones.TryGetValue(before, out var after))
                { filter.sharedMesh = after; EditorUtility.SetDirty(filter); report.filtersRebound++; }
                report.referencedTrianglesAfter += Triangles(filter.sharedMesh);
            }
            foreach (var collider in canyon.GetComponentsInChildren<MeshCollider>(true))
                if (collider.sharedMesh && clones.TryGetValue(collider.sharedMesh, out var replacement))
                { collider.sharedMesh = replacement; EditorUtility.SetDirty(collider); report.collidersRebound++; }
            if (report.referencedTrianglesBefore != report.referencedTrianglesAfter
                || report.uniqueBufferBytesAfter > report.uniqueBufferBytesBefore)
                throw new InvalidOperationException("Canyon channel budget changed topology or increased buffer size.");
            AssetDatabase.SaveAssets();
            report.passed = true;
            File.WriteAllText(SwanJourneyBuild.Output + "/canyon-mesh-budget.json", JsonUtility.ToJson(report, true));
            Debug.Log("SWAN_JOURNEY_CANYON_MESH_BUDGET meshes=" + report.clonedMeshes
                + " bytes=" + report.uniqueBufferBytesBefore + "->" + report.uniqueBufferBytesAfter
                + " triangles=" + report.referencedTrianglesAfter + " sourceAssetsChanged=false nativeBenefit=unmeasured");
        }

        static void ValidateMaterialUv(MeshRenderer renderer)
        {
            if ((renderer.lightmapIndex >= 0 && renderer.lightmapIndex < 65534)
                || (renderer.realtimeLightmapIndex >= 0 && renderer.realtimeLightmapIndex < 65534))
                throw new InvalidOperationException("Canyon UV1 is used by a lightmap: " + renderer.name);
            foreach (var material in renderer.sharedMaterials)
            {
                if (!material || !material.shader) throw new InvalidOperationException("Missing canyon material: " + renderer.name);
                string shader = material.shader.name;
                if (shader != SwanJourneyUrp.Lit && shader != SwanJourneyUrp.Unlit)
                    throw new InvalidOperationException("Unverified canyon shader UV semantics: " + shader);
                bool secondary = material.HasProperty("_UVSec") && material.GetFloat("_UVSec") > .5f;
                bool detail = material.HasProperty("_DetailAlbedoMap") && material.GetTexture("_DetailAlbedoMap")
                    || material.HasProperty("_DetailNormalMap") && material.GetTexture("_DetailNormalMap");
                if (secondary && detail) throw new InvalidOperationException("Canyon material uses secondary UVs: " + material.name);
            }
        }

        static void ValidateEquivalent(Mesh source, Mesh copy, bool removedWhite)
        {
            bool same = source.vertexCount == copy.vertexCount && source.subMeshCount == copy.subMeshCount
                && source.indexFormat == copy.indexFormat && source.bounds == copy.bounds
                && source.vertices.SequenceEqual(copy.vertices) && source.normals.SequenceEqual(copy.normals)
                && source.tangents.SequenceEqual(copy.tangents) && source.bindposes.SequenceEqual(copy.bindposes)
                && source.boneWeights.SequenceEqual(copy.boneWeights);
            for (int channel = 0; channel < 8; channel++)
            {
                if (channel == 1) continue;
                var a = new List<Vector4>(); var b = new List<Vector4>();
                source.GetUVs(channel, a); copy.GetUVs(channel, b); same &= a.SequenceEqual(b);
            }
            for (int i = 0; i < source.subMeshCount; i++)
                same &= source.GetTopology(i) == copy.GetTopology(i) && source.GetBaseVertex(i) == copy.GetBaseVertex(i)
                    && source.GetIndices(i, false).SequenceEqual(copy.GetIndices(i, false))
                    && source.GetSubMesh(i).bounds == copy.GetSubMesh(i).bounds;
            if (!removedWhite) same &= source.colors.SequenceEqual(copy.colors);
            if (!same || copy.HasVertexAttribute(VertexAttribute.TexCoord1)
                || removedWhite && copy.HasVertexAttribute(VertexAttribute.Color))
                throw new InvalidOperationException("Canyon lean mesh changed a protected channel or bounds: " + source.name);
        }

        static long BufferBytes(Mesh mesh)
        {
            long result = 0;
            for (int i = 0; i < mesh.vertexBufferCount; i++) result += (long)mesh.GetVertexBufferStride(i) * mesh.vertexCount;
            for (int i = 0; i < mesh.subMeshCount; i++) result += (long)mesh.GetIndexCount(i) * (mesh.indexFormat == IndexFormat.UInt32 ? 4 : 2);
            return result;
        }
        static long Triangles(Mesh mesh)
        {
            long result = 0;
            for (int i = 0; i < mesh.subMeshCount; i++) if (mesh.GetTopology(i) == MeshTopology.Triangles) result += (long)mesh.GetIndexCount(i) / 3;
            return result;
        }
    }
}
