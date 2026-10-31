using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SwanJourney.Editor
{
    // Independent, read-only scene inspection. It never enters Play Mode, saves a
    // scene, changes references or touches the source partner scene.
    public static class SwanJourneySceneAudit
    {
        const string ScenePath = "Assets/SwanJourney/Scenes/SteamJourneySwan.unity";
        const string ReportPath = "Builds/SwanJourney/scene-partition-audit.json";

        [Serializable] public sealed class Group
        {
            public string name, rootPath;
            public int transforms, renderers, activeRenderers, meshReferences, missingMeshes, emptyMeshes, uniqueMeshes;
            public int materials, textures;
            public long referencedVertices, referencedTrianglesIncludingInactive, uniqueVertices, uniqueTriangles;
            public long uniqueVertexBufferBytes, uniqueIndexBufferBytes;
        }
        [Serializable] public sealed class Reference
        {
            public string direction, ownerPath, ownerType, propertyPath, targetPath, targetType;
            public bool ownerActive, targetActive;
        }
        [Serializable] public sealed class AssetReference
        {
            public string ownerPath, ownerType, propertyPath, assetPath, assetName, assetType;
            public bool alsoUsedByOutsideRenderer;
        }
        [Serializable] public sealed class Report
        {
            public string scene = ScenePath;
            public string scope = "Saved derived scene including inactive objects; unique Unity mesh vertex/index buffer estimates, not native GPU memory or live visible draw counts. Cross-boundary refs include array elements; Transform.m_Father is excluded.";
            public bool passed, enteredPlayMode, savedScene;
            public string failure, trainPath, trainParentPath;
            public Group sandbox, outsideSandbox, canyon, outsideCanyon;
            public Reference[] sandboxCrossReferences, canyonCrossReferences;
            public AssetReference[] canyonAssetReferencesFromOutside;
            public long geometryBytesSharedAcrossSandboxBoundary, geometryBytesSharedAcrossCanyonBoundary;
        }
        sealed class MeshSets
        {
            public readonly HashSet<Mesh> meshes = new HashSet<Mesh>();
            public readonly HashSet<Material> materials = new HashSet<Material>();
            public readonly HashSet<Texture> textures = new HashSet<Texture>();
            public HashSet<Object> Assets()
            {
                var result = new HashSet<Object>(meshes.Cast<Object>());
                result.UnionWith(materials); result.UnionWith(textures); return result;
            }
        }

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Scene partition audit must run in Edit Mode.");
            var report = new Report();
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = false;
            try
            {
                if (!scene.IsValid() || !scene.isLoaded)
                { scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive); opened = true; }
                var roots = scene.GetRootGameObjects();
                var components = roots.SelectMany(r => r.GetComponentsInChildren<Component>(true)).Where(c => c).Distinct().ToArray();
                var journey = components.OfType<SteamJourney>().Single();
                if (!journey.sandboxRoot) throw new InvalidOperationException("Derived scene has no sandboxRoot binding.");
                var sandboxRoot = journey.sandboxRoot.transform;
                var canyonRoot = sandboxRoot.Find("Mesa original canyon · miniature railway V12");
                if (!canyonRoot) throw new InvalidOperationException("Expected V23 canyon subtree is missing.");
                report.trainPath = Path(journey.trainAnchor);
                report.trainParentPath = Path(journey.trainAnchor ? journey.trainAnchor.parent : null);
                var transforms = components.OfType<Transform>().ToArray();
                var sandboxSet = new HashSet<Transform>(transforms.Where(t => Within(t, sandboxRoot)));
                var canyonSet = new HashSet<Transform>(transforms.Where(t => Within(t, canyonRoot)));
                report.sandbox = Measure("sandboxRoot", Path(sandboxRoot), sandboxSet, components, out var sandboxMeshes);
                report.outsideSandbox = Measure("outside sandboxRoot", "all other derived scene objects", new HashSet<Transform>(transforms.Except(sandboxSet)), components, out var outsideMeshes);
                report.canyon = Measure("static canyon candidate", Path(canyonRoot), canyonSet, components, out var canyonMeshes);
                report.outsideCanyon = Measure("outside static canyon", "all other derived scene objects", new HashSet<Transform>(transforms.Except(canyonSet)), components, out var outsideCanyonMeshes);
                report.geometryBytesSharedAcrossSandboxBoundary = SharedBytes(sandboxMeshes, outsideMeshes);
                report.geometryBytesSharedAcrossCanyonBoundary = SharedBytes(canyonMeshes, outsideCanyonMeshes);
                var sandboxRefs = new List<Reference>();
                var canyonRefs = new List<Reference>();
                var assetRefs = new List<AssetReference>();
                var canyonAssets = canyonMeshes.Assets();
                var outsideAssets = outsideCanyonMeshes.Assets();
                foreach (var component in components)
                {
                    bool ownerSandbox = sandboxSet.Contains(component.transform), ownerCanyon = canyonSet.Contains(component.transform);
                    using (var serialized = new SerializedObject(component))
                    {
                        var property = serialized.GetIterator();
                        while (property.Next(true))
                        {
                            if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                            if (component is Transform && property.propertyPath == "m_Father") continue;
                            var value = property.objectReferenceValue;
                            if (!value) continue;
                            Transform target = value is GameObject go ? go.transform : value is Component targetComponent ? targetComponent.transform : null;
                            if (target && target.gameObject.scene == scene)
                            {
                                bool targetSandbox = sandboxSet.Contains(target), targetCanyon = canyonSet.Contains(target);
                                if (ownerSandbox != targetSandbox)
                                    sandboxRefs.Add(CrossReference(component, property.propertyPath, value, target, ownerSandbox));
                                if (ownerCanyon != targetCanyon)
                                    canyonRefs.Add(CrossReference(component, property.propertyPath, value, target, ownerCanyon));
                            }
                            else if (!ownerCanyon && canyonAssets.Contains(value))
                            {
                                // Scene object bindings alone are insufficient: a persistent
                                // bridge's serialized Material[] can retain every canyon map.
                                assetRefs.Add(new AssetReference {
                                    ownerPath = Path(component.transform), ownerType = component.GetType().FullName,
                                    propertyPath = property.propertyPath, assetPath = AssetDatabase.GetAssetPath(value),
                                    assetName = value.name, assetType = value.GetType().FullName,
                                    alsoUsedByOutsideRenderer = outsideAssets.Contains(value)
                                });
                            }
                        }
                    }
                }
                report.sandboxCrossReferences = Order(sandboxRefs);
                report.canyonCrossReferences = Order(canyonRefs);
                report.canyonAssetReferencesFromOutside = assetRefs.OrderBy(r => r.ownerPath).ThenBy(r => r.ownerType).ThenBy(r => r.propertyPath).ToArray();
                report.passed = true;
                Debug.Log("SWAN_JOURNEY_SCENE_AUDIT sandboxMeshes=" + report.sandbox.uniqueMeshes +
                    " sandboxGeometryBytes=" + (report.sandbox.uniqueVertexBufferBytes + report.sandbox.uniqueIndexBufferBytes) +
                    " sandboxCrossRefs=" + sandboxRefs.Count + " canyonCrossRefs=" + canyonRefs.Count +
                    " outsideCanyonAssetRefs=" + assetRefs.Count + " report=" + ReportPath);
            }
            catch (Exception exception) { report.failure = exception.ToString(); throw; }
            finally
            {
                Directory.CreateDirectory("Builds/SwanJourney");
                File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
                if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static Group Measure(string name, string rootPath, HashSet<Transform> group, Component[] components, out MeshSets sets)
        {
            sets = new MeshSets();
            var result = new Group { name = name, rootPath = rootPath, transforms = group.Count };
            foreach (var renderer in components.OfType<Renderer>().Where(r => group.Contains(r.transform)))
            {
                result.renderers++;
                if (renderer.enabled && renderer.gameObject.activeInHierarchy) result.activeRenderers++;
                foreach (var material in renderer.sharedMaterials)
                {
                    if (!material) continue; sets.materials.Add(material);
                    foreach (string property in material.GetTexturePropertyNames())
                        if (material.GetTexture(property) is Texture texture) sets.textures.Add(texture);
                }
            }
            var references = components.OfType<MeshFilter>().Where(f => group.Contains(f.transform)).Select(f => f.sharedMesh)
                .Concat(components.OfType<SkinnedMeshRenderer>().Where(r => group.Contains(r.transform)).Select(r => r.sharedMesh));
            foreach (var mesh in references)
            {
                result.meshReferences++;
                if (!mesh) { result.missingMeshes++; continue; }
                if (mesh.vertexCount == 0) result.emptyMeshes++;
                result.referencedVertices += mesh.vertexCount;
                result.referencedTrianglesIncludingInactive += Triangles(mesh);
                sets.meshes.Add(mesh);
            }
            foreach (var mesh in sets.meshes)
            {
                result.uniqueVertices += mesh.vertexCount; result.uniqueTriangles += Triangles(mesh);
                result.uniqueVertexBufferBytes += VertexBytes(mesh);
                result.uniqueIndexBufferBytes += IndexBytes(mesh);
            }
            result.uniqueMeshes = sets.meshes.Count; result.materials = sets.materials.Count; result.textures = sets.textures.Count;
            return result;
        }
        static Reference CrossReference(Component owner, string property, Object value, Transform target, bool ownerInside)
            => new Reference { direction = ownerInside ? "inside -> outside" : "outside -> inside",
                ownerPath = Path(owner.transform), ownerType = owner.GetType().FullName, propertyPath = property,
                targetPath = Path(target), targetType = value.GetType().FullName,
                ownerActive = owner.gameObject.activeInHierarchy, targetActive = target.gameObject.activeInHierarchy };
        static Reference[] Order(List<Reference> refs) => refs.OrderBy(r => r.direction).ThenBy(r => r.ownerPath).ThenBy(r => r.ownerType).ThenBy(r => r.propertyPath).ToArray();
        static bool Within(Transform item, Transform root) => item == root || item.IsChildOf(root);
        static long SharedBytes(MeshSets a, MeshSets b) => a.meshes.Intersect(b.meshes).Sum(m => VertexBytes(m) + IndexBytes(m));
        static long Triangles(Mesh mesh)
        {
            long count = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
                if (mesh.GetTopology(i) == MeshTopology.Triangles) count += (long)mesh.GetIndexCount(i) / 3;
            return count;
        }
        static long VertexBytes(Mesh mesh)
        {
            long bytes = 0;
            if (mesh.vertexCount > 0) for (int i = 0; i < mesh.vertexBufferCount; i++) bytes += (long)mesh.vertexCount * mesh.GetVertexBufferStride(i);
            return bytes;
        }
        static long IndexBytes(Mesh mesh)
        {
            long count = 0;
            for (int i = 0; i < mesh.subMeshCount; i++) count += mesh.GetIndexCount(i);
            return count * (mesh.indexFormat == IndexFormat.UInt32 ? 4 : 2);
        }
        static string Path(Transform item)
        {
            if (!item) return "<null>";
            var parts = new List<string>();
            for (var current = item; current; current = current.parent) parts.Add(current.name);
            parts.Reverse(); return string.Join("/", parts);
        }
    }
}
