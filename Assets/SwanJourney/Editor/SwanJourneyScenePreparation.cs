using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SwanJourney.Editor
{
    /// <summary>Removes verified retired copies only from the in-memory Swan derivative.</summary>
    public static class SwanJourneyScenePreparation
    {
        const string MesaName = "Mesa original canyon · miniature railway V12";
        static readonly HashSet<string> OldSandboxChildren = new HashSet<string>
        {
            "Previous canyon scene · preserved", "Red Mesa Railway V11", "Terrain and railway",
            "A · Precision mechanical console", "Spatial driving console V19", "Console ground contact pedestal"
        };
        static readonly HashSet<string> OldMesaChildren = new HashSet<string>
        {
            "Full resolution terrain mesh · fixed detail", "Original Mesa terrain · local railway grading",
            "Original terrain cut edge", "Full resolution original rock batches",
            "Closed cut-rock backing and edge thickness", "Original package rock portal collars"
        };
        sealed class Removal
        {
            public Object target;
            public string path;
            public bool lod;
            public readonly HashSet<int> ids = new HashSet<int>();
        }

        public static void Prepare(SteamJourney journey, Transform content)
        {
            RequireDerivative(journey, content);
            // All exhibits can move and scale in MR, including authored scenery.
            // Keep our explicit mesh batches, but do not let Unity turn their
            // transforms into a separate build-time static-batching coordinate space.
            foreach(var t in content.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject,0);
            int before = content.GetComponentsInChildren<MeshRenderer>(true).Length;
            var retired = new HashSet<Transform>();
            AddInactiveChildren(journey.sandboxRoot.transform, OldSandboxChildren, retired);
            var mesa = RequireChild(journey.sandboxRoot.transform, MesaName);
            AddInactiveChildren(mesa, OldMesaChildren, retired);

            // Keep the Charcoal parent: StudioWorkshopMood on it owns current lighting.
            // Keep these referenced background roots, but strip their exact desktop floor leaves below.
            foreach (var name in new[] { "Presentation backdrop", "Surface V2 baked environment" })
                RequireChild(content, name).gameObject.SetActive(false);
            HideBackground(content.Find("Studio ivory backdrop"), retired);
            HideBackground(content.Find("Charcoal workshop setting/Workshop decoration visibility"), retired);

            var removals = retired.Select(t => Subtree(t, false)).ToList();
            // V23 deliberately disables its LODGroups and retains full-detail LOD0.
            // Exporting their inactive LOD1/2/3 siblings triples geometry without changing the view.
            foreach (var group in mesa.GetComponentsInChildren<LODGroup>(true))
            {
                if (group.enabled || IsUnderAny(group.transform, retired)) continue;
                var levels = group.GetLODs();
                if (levels.Length == 0 || !levels[0].renderers.Any(r => r && r.gameObject.activeSelf)) continue;
                var unused = new HashSet<Transform>();
                foreach (Transform child in group.transform)
                    if (!child.gameObject.activeSelf && IsUnusedLod(child.name)) unused.Add(child);
                if (unused.Count == 0) continue;
                // The group and its unused children form one jointly validated removal set.
                var component = new Removal { target = group, path = Path(group.transform) + " [disabled LODGroup]", lod = true };
                component.ids.Add(group.GetInstanceID()); removals.Add(component);
                removals.AddRange(unused.Select(t => Subtree(t, true)));
            }

            var proposedIds = new HashSet<int>(removals.SelectMany(r => r.ids));
            foreach (var refine in content.GetComponentsInChildren<StudioSpatialRefineV19>(true))
            {
                // This exact field only calls SetActive(false); it never restores scenery.
                if (refine.scenery != null)
                    refine.scenery = refine.scenery.Where(o => !o || !proposedIds.Contains(o.GetInstanceID())).ToArray();
                EditorUtility.SetDirty(refine);
            }

            var allComponents = content.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Component>(true)).Where(c => c).ToArray();
            int blockedCount = 0;
            // Check disabled components and currently hidden runtime modes too. If one candidate
            // must survive, its references can protect other candidates on the next pass.
            while (removals.Count > 0)
            {
                var owners = new Dictionary<int, Removal>();
                foreach (var removal in removals) foreach (int id in removal.ids) owners[id] = removal;
                var blocked = new HashSet<Removal>();
                foreach (var component in allComponents)
                {
                    if (component is Transform || owners.ContainsKey(component.GetInstanceID())) continue;
                    using (var serialized = new SerializedObject(component))
                    {
                        var property = serialized.GetIterator();
                        while (property.Next(true))
                        {
                            if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                            var reference = property.objectReferenceValue;
                            if (!reference || !owners.TryGetValue(reference.GetInstanceID(), out var removal)) continue;
                            if (blocked.Add(removal) && blockedCount + blocked.Count <= 20)
                                Debug.LogWarning("SWAN_JOURNEY_PRUNE_KEEP " + removal.path + " referenced by " +
                                    Path(component.transform) + " / " + component.GetType().Name + "." + property.propertyPath);
                        }
                    }
                }
                if (blocked.Count == 0) break;
                blockedCount += blocked.Count;
                removals.RemoveAll(blocked.Contains);
            }
            int legacyRoots = removals.Count(r => !r.lod), lodObjects = removals.Count(r => r.lod);
            foreach (var removal in removals)
            {
                if (!removal.lod) Debug.Log("SWAN_JOURNEY_PRUNE_REMOVE " + removal.path);
                Object.DestroyImmediate(removal.target);
            }
            RemoveDesktopWorktops(journey, content);
            int after = content.GetComponentsInChildren<MeshRenderer>(true).Length;
            Debug.Log("SWAN_JOURNEY_PRUNE before=" + before + " after=" + after + " removed=" + (before - after) +
                " legacyRoots=" + legacyRoots + " unusedLodObjects=" + lodObjects + " referenceProtected=" + blockedCount);
        }

        static void RemoveDesktopWorktops(SteamJourney journey, Transform content)
        {
            var paths = new[]
            {
                "Presentation backdrop/Neutral display floor",
                "Surface V2 baked environment/Baked local tabletop receiver"
            };
            var leaves = paths.Select(p => RequireChild(content, p)).ToArray();
            if (leaves.Any(t => t.childCount != 0))
                throw new InvalidOperationException("Desktop worktop removal only accepts the two verified leaf objects.");
            var supports = new[]
            {
                RequireChild(journey.workshopRoot.transform, "Railway teaching plinth"),
                RequireChild(journey.workshopRoot.transform, "A · 精密仪器浅台"),
                RequireChild(journey.sandboxRoot.transform, MesaName + "/Flush walnut exhibition tray V22"),
                RequireChild(journey.sandboxRoot.transform, "Spatial driving console V20")
            };
            var before = new Bounds[supports.Length];
            for (int i = 0; i < supports.Length; i++)
                if (!SwanJourneyPresentation.TryBounds(supports[i], content, out before[i]))
                    throw new InvalidOperationException("Support has no bounds before worktop removal: " + Path(supports[i]));
            var shell = RequireChild(supports[1], "Rounded walnut plinth");
            var preservedRenderers = new[] { supports[0].GetComponent<MeshRenderer>(), shell.GetComponent<MeshRenderer>() };
            if (preservedRenderers.Any(r => !r || !r.GetComponent<MeshFilter>() || !r.GetComponent<MeshFilter>().sharedMesh))
                throw new InvalidOperationException("The railway wooden plinth and control-console shell must remain renderable.");

            var removedRenderers = new HashSet<Renderer>(leaves.SelectMany(t => t.GetComponents<Renderer>()));
            // These three authored controllers only toggle the desktop floor renderers. Keep their
            // roots and all other references, and avoid saving dangling component references.
            foreach (var mood in content.GetComponentsInChildren<MesaDayNightV13>(true))
                mood.workshopFloor = WithoutWorktops(mood.workshopFloor, removedRenderers);
            foreach (var mood in content.GetComponentsInChildren<MesaMoodV12>(true))
                mood.workshopFloor = WithoutWorktops(mood.workshopFloor, removedRenderers);
            foreach (var mood in content.GetComponentsInChildren<CanyonMoodV11>(true))
                mood.workshopFloor = WithoutWorktops(mood.workshopFloor, removedRenderers);
            var report = new WorktopRemovalReport { removedLeafPaths = paths, retainedSupports = supports.Select(Path).ToArray() };
            foreach (var leaf in leaves)
            {
                foreach (var collider in leaf.GetComponents<Collider>()) { Object.DestroyImmediate(collider); report.removedColliders++; }
                foreach (var renderer in leaf.GetComponents<Renderer>()) { Object.DestroyImmediate(renderer); report.removedRenderers++; }
                foreach (var filter in leaf.GetComponents<MeshFilter>()) { Object.DestroyImmediate(filter); report.removedMeshFilters++; }
                if (leaf.GetComponent<Renderer>() || leaf.GetComponent<MeshFilter>() || leaf.GetComponent<Collider>())
                    throw new InvalidOperationException("Desktop worktop retained exportable geometry or collision: " + Path(leaf));
            }
            for (int i = 0; i < supports.Length; i++)
                if (!SwanJourneyPresentation.TryBounds(supports[i], content, out var after) ||
                    (after.center - before[i].center).sqrMagnitude > 1e-10f ||
                    (after.size - before[i].size).sqrMagnitude > 1e-10f)
                    throw new InvalidOperationException("Desktop worktop removal changed support bounds: " + Path(supports[i]));
            if (preservedRenderers.Any(r => !r || !r.GetComponent<MeshFilter>().sharedMesh))
                throw new InvalidOperationException("Desktop worktop removal damaged the retained wooden supports.");
            report.passed = true;
            report.supportBoundsUnchanged = true;
            Directory.CreateDirectory("Builds/SwanJourney");
            File.WriteAllText("Builds/SwanJourney/desktop-worktop-removal.json", JsonUtility.ToJson(report, true));
            Debug.Log("SWAN_JOURNEY_WORKTOP_REMOVAL renderers=" + report.removedRenderers + " meshFilters=" +
                report.removedMeshFilters + " colliders=" + report.removedColliders + " supportBoundsUnchanged=true");
        }

        static Renderer[] WithoutWorktops(Renderer[] source, HashSet<Renderer> removed) =>
            source == null ? Array.Empty<Renderer>() : source.Where(r => r && !removed.Contains(r)).ToArray();

        [Serializable]
        sealed class WorktopRemovalReport
        {
            public bool passed, supportBoundsUnchanged, nativeRenderingValidated;
            public string scope = "Derived-scene geometry removal and four support bounds; no device rendering validation.";
            public string[] removedLeafPaths, retainedSupports;
            public int removedRenderers, removedMeshFilters, removedColliders;
        }

        public static void ConfigureSupports(SwanJourneyPresentation presentation, bool alignControlSupports = false)
        {
            if (!presentation) throw new ArgumentNullException(nameof(presentation));
            RequireDerivative(presentation.journey, presentation.content);
            var workshop = presentation.journey.workshopRoot.transform;
            var sandbox = presentation.journey.sandboxRoot.transform;
            presentation.workshopSupports = new[]
            {
                RequireChild(workshop, "Railway teaching plinth"),
                RequireChild(workshop, "A · 精密仪器浅台")
            };
            presentation.sandboxSupports = new[]
            {
                RequireChild(sandbox, MesaName + "/Flush walnut exhibition tray V22"),
                RequireChild(sandbox, "Spatial driving console V20")
            };
            if (alignControlSupports)
            {
                // Move each complete freestanding desk, including its attached controls.
                // Neither track/terrain nor any locomotive/simulation transform is moved.
                AlignBottom(presentation.workshopSupports[1], presentation.workshopSupports[0], presentation.content);
                AlignBottom(presentation.sandboxSupports[1], presentation.sandboxSupports[0], presentation.content);
            }
            presentation.RefreshFootprint();
            Debug.Log("SWAN_JOURNEY_SUPPORTS " + string.Join(" | ", presentation.workshopSupports
                .Concat(presentation.sandboxSupports).Select(Path)));
        }

        static void RequireDerivative(SteamJourney journey, Transform content)
        {
            if (!journey || !content || !journey.SwanInputActive || journey.spatialRoot != content ||
                EditorUtility.IsPersistent(content) || !content.gameObject.scene.IsValid() ||
                content.name != "V23 partner composition" || !content.parent)
                throw new InvalidOperationException("Scene preparation requires the builder's in-memory Swan composition.");
            if (!journey.workshopRoot || !journey.sandboxRoot ||
                !journey.workshopRoot.transform.IsChildOf(content) || !journey.sandboxRoot.transform.IsChildOf(content))
                throw new InvalidOperationException("Both V23 runtime modes must remain inside the Swan composition.");
        }
        static Transform RequireChild(Transform parent, string path)
        {
            var child = parent.Find(path);
            if (!child) throw new InvalidOperationException("Missing V23 scene support: " + Path(parent) + "/" + path);
            return child;
        }
        static void AddInactiveChildren(Transform parent, HashSet<string> names, HashSet<Transform> retired)
        {
            // Enumerate all siblings: the saved scene contains three identically named canyon copies.
            foreach (Transform child in parent)
                if (!child.gameObject.activeSelf && names.Contains(child.name)) retired.Add(child);
        }
        static void HideBackground(Transform target, HashSet<Transform> retired)
        {
            if (!target) return;
            target.gameObject.SetActive(false); retired.Add(target);
        }
        static bool IsUnderAny(Transform target, HashSet<Transform> roots)
        {
            for (var current = target; current; current = current.parent) if (roots.Contains(current)) return true;
            return false;
        }
        static bool IsUnusedLod(string name) => name.EndsWith("_LOD1", StringComparison.Ordinal) ||
            name.EndsWith("_LOD2", StringComparison.Ordinal) || name.EndsWith("_LOD3", StringComparison.Ordinal);
        static Removal Subtree(Transform root, bool lod)
        {
            var removal = new Removal { target = root.gameObject, path = Path(root), lod = lod };
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                removal.ids.Add(transform.gameObject.GetInstanceID());
                foreach (var component in transform.GetComponents<Component>()) if (component) removal.ids.Add(component.GetInstanceID());
            }
            return removal;
        }
        static void AlignBottom(Transform moving, Transform fixedBase, Transform relativeTo)
        {
            if (!SwanJourneyPresentation.TryBounds(moving, relativeTo, out var a) ||
                !SwanJourneyPresentation.TryBounds(fixedBase, relativeTo, out var b))
                throw new InvalidOperationException("V23 support has no mesh bounds.");
            float offset = b.min.y - a.min.y;
            moving.position += relativeTo.TransformVector(Vector3.up * offset);
            Debug.Log("SWAN_JOURNEY_SUPPORT_ALIGN " + Path(moving) + " localYOffset=" + offset.ToString("F5"));
        }
        static string Path(Transform target) => target.parent ? Path(target.parent) + "/" + target.name : target.name;
    }
}
