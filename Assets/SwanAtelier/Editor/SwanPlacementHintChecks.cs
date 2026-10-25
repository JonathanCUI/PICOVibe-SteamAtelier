using System;
using System.IO;
using System.Linq;
using ByteDance.PICO.SpatialAdapter.Component;
using UnityEngine;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace SwanAtelier.Editor
{
    public static class SwanPlacementHintChecks
    {
        static void Check(bool value, string message) { if (!value) throw new Exception("Placement hint: " + message); }
        public static void CheckVisible(SwanAtelierController game, int slot)
        {
            var ghost = game.PlacementGhost;
            Check(ghost && ghost.activeInHierarchy && game.PlacementSlot == slot, "missing slot " + slot);
            Check(Vector3.Distance(ghost.transform.position, game.assembly.SlotPosition(slot)) < .0001f, "wrong destination");
            Check(Quaternion.Angle(ghost.transform.rotation, game.engine.locomotive.rotation) < .05f, "wrong rotation");
            Check(Vector3.Distance(ghost.transform.lossyScale, game.engine.display.lossyScale) < .0001f, "wrong scale");
            Check(!game.socket.gameObject.activeSelf, "old marker overlaps silhouette");
            Check(ghost.GetComponentsInChildren<Collider>(true).Length == 0 && ghost.GetComponentsInChildren<SpatialAdapterHoverEffect>(true).Length == 0, "hint intercepts input");
            var filters = ghost.GetComponentsInChildren<MeshFilter>(true);
            var source = game.assembly.pieces[slot].prefab.GetComponentsInChildren<MeshFilter>(true);
            Check(filters.Length > 0 && filters.Select(f => f.sharedMesh).SequenceEqual(source.Select(f => f.sharedMesh)), "geometry differs from destination prefab");
            foreach (var renderer in ghost.GetComponentsInChildren<Renderer>(true)) {
                Check(renderer.enabled && renderer.gameObject.layer == 2, "hidden or hit-testable renderer");
                foreach (var material in renderer.sharedMaterials)
                    Check(material && material.color.a > .1f && material.color.a < .35f && material.GetFloat("_ZWrite") == 0 && material.GetTag("RenderType", false) == "Transparent", "opaque/missing material");
                foreach (var material in renderer.sharedMaterials)
                    Check(Mathf.Abs(material.color.r - 1) < .001f && Mathf.Abs(material.color.g - 1) < .001f && Mathf.Abs(material.color.b - 1) < .001f, "silhouette is tinted instead of white");
            }
        }
        static void Send(SwanAtelierController game, GameObject target, TouchPhase phase, Vector3 hit, double now)
        {
            game.QueueGestureSample(new SwanGestureSample { id = 7301, pinch = true, phase = phase, target = target, hitPosition = hit, handPosition = hit });
            game.ProcessGestureFrame(now, .016f);
        }
        public static void Run(SwanAtelierController game)
        {
            Check(game.placementGhostPrefabs.Length == 45 && game.placementGhostPrefabs.All(p => p), "missing exported variants");
            var position = game.transform.position; var rotation = game.transform.rotation; var scale = game.transform.localScale;
            try {
                game.CompleteAssembly(); var loose = game.TakePiece(0); CheckVisible(game, 0);
                var ghost = game.PlacementGhost;
                loose.transform.position += Vector3.one;
                game.UpdatePlacementHint(0);
                Check(game.PlacementGhost == ghost, "rebuilds ghost on every move"); CheckVisible(game, 0);
                foreach (float s in new[] { .26f, 1.5f }) {
                    game.transform.SetPositionAndRotation(position + Vector3.right * .1f, Quaternion.Euler(0, 72, 0));
                    game.transform.localScale = Vector3.one * s;
                    game.UpdatePlacementHint(0); CheckVisible(game, 0);
                }
                game.ReleasePiece(0, true); Check(!game.PlacementGhost, "cancel left hint");
                // Exercise the real single-hand regrab path, including join delay.
                var hit = loose.GetComponentInChildren<MeshRenderer>().gameObject; var p = loose.transform.position;
                Send(game, hit, TouchPhase.Began, p, 10); game.ProcessGestureFrame(10.3, .016f);
                CheckVisible(game, 0);
                Send(game, hit, TouchPhase.Canceled, p, 10.4); Check(!game.PlacementGhost, "input cancel left hint");
                game.UpdatePlacementHint(0); game.enabled = false; Check(!game.PlacementGhost, "disable left hint"); game.enabled = true;
                game.transform.SetPositionAndRotation(position, rotation); game.transform.localScale = scale;
                // Same-fit slots must preview the destination geometry used by snap.
                var pair = Enumerable.Range(0, 45).GroupBy(i => game.assembly.pieces[i].fitKey).FirstOrDefault(g => g.Count() > 1);
                if (pair != null) {
                    game.CompleteAssembly(); int a = pair.First(), b = pair.Skip(1).First();
                    var held = game.TakePiece(a); game.ReleasePiece(a, true); game.TakePiece(b); game.ReleasePiece(b, true);
                    held.transform.position = game.assembly.SlotPosition(b); game.UpdatePlacementHint(a); CheckVisible(game, b);
                    Check(game.ReleasePiece(a) && game.assembly.pieceSlots[a] == b && !game.PlacementGhost, "preview and snap disagree");
                }
                game.CompleteAssembly();
                game.Activate(AtelierAction.ResetGuard); game.Activate(AtelierAction.Assembly); game.HoldReset(1.21f);
                var tray = game.content.GetComponentsInChildren<AtelierControl>(true).First(c => c.action == AtelierAction.TrayPart && c.index >= 0);
                Send(game, tray.gameObject, TouchPhase.Began, tray.transform.position, 20); game.ProcessGestureFrame(20.3, .016f);
                Check(game.PlacementGhost && game.PlacementSlot >= 0, "tray pickup missing hint");
                game.ResetSpatialGestures(); Check(!game.PlacementGhost, "tracking reset left hint");
                game.CompleteAssembly();
                // A repeatable editor view for inspecting silhouette geometry and opacity.
                int shell = Enumerable.Range(0, 45).OrderByDescending(i => game.assembly.pieces[i].prefab.GetComponentsInChildren<MeshFilter>(true).Sum(f => (long)f.sharedMesh.vertexCount)).First();
                var preview = game.TakePiece(shell); preview.transform.position += game.previewCamera.transform.right * .6f;
                game.UpdatePlacementHint(shell); Capture(game.previewCamera);
                Debug.Log("ATELIER_SWAN_PLACEMENT_CHECKS_PASSED prefabs=45 destination,transparent,nonblocking,shared-mesh,reuse,scale-yaw,compatible-slot,regrab,tray,cancel,disable,reset");
            } finally {
                game.ResetSpatialGestures(); game.transform.SetPositionAndRotation(position, rotation); game.transform.localScale = scale; game.CompleteAssembly();
            }
        }
        public static void Capture(Camera camera, string path = "Builds/Evidence/placement-ghost-editor.png")
        {
            var previous = camera.targetTexture; var active = RenderTexture.active;
            var rt = new RenderTexture(1600, 1000, 24); var image = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
            try {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            } finally {
                camera.targetTexture = previous; RenderTexture.active = active; rt.Release();
                UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }
}
