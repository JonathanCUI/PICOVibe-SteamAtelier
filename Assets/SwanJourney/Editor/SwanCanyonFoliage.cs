using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SwanJourney.Editor
{
    internal static class SwanCanyonFoliage
    {
        internal static void PreparePrefab()
        {
            const string path = "Assets/SwanJourney/Resources/SwanJourneyCanyon.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (RemoveCards(root.transform))
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                    if (!saved) throw new InvalidOperationException("Could not save the canyon vegetation");
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        internal static bool RemoveCards(Transform root)
        {
            int batches = 0, trimmed = 0;
            long triangles = 0;
            var groups = root.GetComponentsInChildren<Transform>(true);
            var grass = groups.Single(t => t.name.StartsWith("Original dry grass and wildflowers", StringComparison.Ordinal));
            var cacti = groups.Single(t => t.name.StartsWith("Original vegetation full meshes", StringComparison.Ordinal));
            foreach (var renderer in grass.GetComponentsInChildren<MeshRenderer>(true).Concat(cacti.GetComponentsInChildren<MeshRenderer>(true)))
            {
                var filter = renderer.GetComponent<MeshFilter>();
                var source = filter.sharedMesh;
                var materials = renderer.sharedMaterials;
                var keep = Enumerable.Range(0, materials.Length)
                    .Where(i => !renderer.transform.IsChildOf(grass) && !IsPaleCard(materials[i].name)).ToArray();
                if (keep.Length == materials.Length) continue;
                if (source.subMeshCount != materials.Length)
                    throw new InvalidOperationException("Unexpected vegetation material layout on " + renderer.name);
                for (int i = 0; i < materials.Length; ++i)
                    if (!keep.Contains(i)) triangles += source.GetIndexCount(i) / 3;
                if (keep.Length == 0)
                {
                    Object.DestroyImmediate(renderer);
                    Object.DestroyImmediate(filter);
                    batches++;
                    continue;
                }
                // Mixed cactus meshes share vertices between material slots. Keep the trunk UVs and normals intact.
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long fileId);
                string path = SwanJourneyBuild.Generated + "/CanyonLean/CactusTrunk_" + guid + "_" + fileId + "_" + string.Join("_", keep) + ".asset";
                var trunk = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (!trunk)
                {
                    trunk = Object.Instantiate(source);
                    trunk.name = source.name + " trunk";
                    trunk.subMeshCount = keep.Length;
                    for (int i = 0; i < keep.Length; ++i)
                        trunk.SetIndices(source.GetIndices(keep[i]), source.GetTopology(keep[i]), i, false);
                    AssetDatabase.CreateAsset(trunk, path);
                }
                filter.sharedMesh = trunk;
                renderer.sharedMaterials = keep.Select(i => materials[i]).ToArray();
                trimmed++;
            }
            Debug.Log($"SWAN_CANYON_FOLIAGE removedRenderers={batches} trimmedMeshes={trimmed} removedTriangles={triangles}");
            return batches + trimmed > 0;
        }

        static bool IsPaleCard(string name) => name.Contains("SaguaroLeaves") || name.Contains("CacactusLeaves")
            || name.Contains("GrocactusLeaves") || name.Contains("Original-Opuntia");
    }
}
