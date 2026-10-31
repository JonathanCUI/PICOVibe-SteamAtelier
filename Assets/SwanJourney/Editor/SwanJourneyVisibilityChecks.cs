using System;
using System.Collections.Generic;
using System.Linq;
using SwanAtelier;
using UnityEngine;

namespace SwanJourney.Editor
{
    // Geometry/layout checks only. They do not establish native headset visibility.
    public static class SwanJourneyVisibilityChecks
    {
        public static List<string> Run()
            => Run(UnityEngine.Object.FindObjectOfType<SwanFrameRateHud>(true),
                UnityEngine.Object.FindObjectOfType<SwanJourneyPresentation>(true));

        public static List<string> Run(SwanFrameRateHud hud, SwanJourneyPresentation presentation)
        {
            var results = new List<string>();
            Check(hud && hud.meshText && presentation && presentation.moveHandle, "FPS and move handle bindings exist", results);
            Check(!hud.meshText.transform.parent && !hud.transform.parent,
                "FPS glyph node and controller remain independent of the movable workbench", results);
            Check(hud.meshText.GetComponentsInChildren<Collider>(true).Length == 0,
                "FPS cannot intercept model or handle input", results);
            var renderers = hud.meshText.GetComponentsInChildren<MeshRenderer>(true);
            Check(renderers.Length == 34 && renderers.All(r => r.sharedMaterial && r.sharedMaterial.shader
                    && r.sharedMaterial.shader.name == "Unlit/Color"),
                "All 34 static FPS glyph renderers use the supported Unlit/Color shader", results);
            var mesh = hud.meshText.GetComponent<MeshFilter>().sharedMesh;
            Check(mesh && mesh.vertexCount > 0 && mesh.isReadable,
                "FPS fixed label uses a readable saved glyph mesh", results);
            var vertices = mesh.vertices; var indices = mesh.triangles;
            Check(indices.Length >= 3 && Vector3.Dot(Vector3.Cross(vertices[indices[1]] - vertices[indices[0]],
                    vertices[indices[2]] - vertices[indices[0]]), Vector3.back) > 0,
                "Unity glyph winding faces the viewer at local negative Z", results);
            float height = mesh.bounds.size.y * hud.visualScale;
            float angularHeight = 2 * Mathf.Atan2(height * .5f, hud.viewOffset.z) * Mathf.Rad2Deg;
            float outsideX = Mathf.Abs(hud.viewOffset.x) + mesh.bounds.extents.x * hud.visualScale;
            Check(hud.viewOffset.z >= .55f && hud.viewOffset.z <= .85f && angularHeight >= 1.5f && angularHeight <= 4f
                    && Mathf.Atan2(outsideX, hud.viewOffset.z) * Mathf.Rad2Deg < 24
                    && Mathf.Atan2(Mathf.Abs(hud.viewOffset.y) + height * .5f, hud.viewOffset.z) * Mathf.Rad2Deg < 20,
                "V23 FPS near layout has readable angular glyph height and stays inside conservative view margins", results);

            var visual = hud.VisualTransform;
            var visualPose = new SavedPose(visual);
            int previousValue = hud.meshText.ShownValue;
            try
            {
                foreach (var pose in new[] { new Pose(new Vector3(.2f, 1.25f, -.3f), Quaternion.identity),
                    new Pose(new Vector3(-.4f, .85f, .3f), Quaternion.Euler(17, 73, -8)) })
                {
                    hud.ApplyHeadPose(pose, Matrix4x4.identity);
                    var relative = Quaternion.Inverse(pose.rotation) * (visual.position - pose.position);
                    Check(Vector3.Distance(relative, hud.viewOffset) < .0001f
                            && Quaternion.Angle(visual.rotation, pose.rotation) < .01f
                            && Vector3.Distance(visual.localScale, Vector3.one * hud.visualScale) < .0001f,
                        "FPS preserves head-relative position, facing and explicit scale across a changed head pose", results);
                }
                hud.meshText.Show(52);
                var active = hud.meshText.variants.Select(v => v.activeSelf).ToArray();
                hud.meshText.Show(52);
                Check(active.Count(v => v) == 2 && hud.meshText.variants.Select(v => v.activeSelf).SequenceEqual(active),
                    "An unchanged FPS value preserves the selected digit variants", results);
                hud.meshText.Show(null);
                Check(hud.meshText.variants.Count(v => v.activeSelf) == 2,
                    "Sampling reset restores the two dash glyphs", results);
            }
            finally
            {
                visualPose.Restore(); hud.meshText.Show(previousValue < 0 ? (double?)null : previousValue);
            }

            Check(presentation.moveHandle.parent == presentation.transform && presentation.workshopMoveHandleAnchor,
                "Common move handle is anchored to the workshop utility row", results);
            Check(presentation.moveHandle.GetComponentsInChildren<SwanJourneyTarget>(true)
                    .Any(t => t.kind == SwanJourneyTargetKind.MoveWorkbench && t.GetComponent<Collider>()),
                "Move handle retains its native MoveWorkbench target and collision surface", results);
            var stagePose = new SavedPose(presentation.transform);
            var handlePose = new SavedPose(presentation.moveHandle);
            var anchorPose = new SavedPose(presentation.workshopMoveHandleAnchor);
            bool mode = presentation.journey.inSandbox;
            try
            {
                presentation.journey.inSandbox = false;
                foreach (float scale in new[] { .26f, .67f, 1.5f })
                {
                    presentation.transform.SetPositionAndRotation(new Vector3(.3f, .8f, 1.4f), Quaternion.Euler(0, 43, 0));
                    presentation.transform.localScale = Vector3.one * scale;
                    presentation.RefreshMoveHandle();
                    Check(SwanJourneyPresentation.TryBounds(presentation.moveHandle, presentation.transform, out var handle)
                            && SwanJourneyPresentation.TryBounds(presentation.workshopMoveHandleAnchor, presentation.transform, out var neighbour),
                        "Handle and utility neighbour expose mesh bounds at scene scale " + scale, results);
                    // Re-read outside the preceding boolean to keep definite assignment explicit.
                    SwanJourneyPresentation.TryBounds(presentation.moveHandle, presentation.transform, out handle);
                    SwanJourneyPresentation.TryBounds(presentation.workshopMoveHandleAnchor, presentation.transform, out neighbour);
                    Check(Mathf.Abs(handle.size.x-.48f)<.001f && Mathf.Abs(handle.size.y-.09f)<.001f
                            && neighbour.min.z-handle.max.z >= .12f,
                        "Handle scales naturally and has a dedicated front row at scene scale " + scale, results);
                    Check(Mathf.Abs(handle.center.y - neighbour.center.y) < .0001f
                            && presentation.moveHandle.parent == presentation.transform,
                        "Handle stays in the familiar control row and moves with the common stage at scene scale " + scale, results);
                }
                int scans = presentation.MoveHandleBoundsScans;
                int layouts = presentation.MoveHandleLayoutUpdates;
                for (int i = 0; i < 32; i++) presentation.RefreshMoveHandle();
                Check(presentation.MoveHandleBoundsScans == scans && presentation.MoveHandleLayoutUpdates == layouts,
                    "32 stable handle refreshes reuse geometry and layout without hierarchy scans", results);
                var beforeMove = presentation.moveHandle.localPosition;
                presentation.workshopMoveHandleAnchor.localPosition += new Vector3(.15f, .02f, 0);
                presentation.RefreshMoveHandle();
                Check(presentation.MoveHandleBoundsScans == scans && presentation.MoveHandleLayoutUpdates == layouts + 1
                        && Vector3.Distance(beforeMove, presentation.moveHandle.localPosition) > .001f,
                    "Moving the utility anchor refreshes layout from cached local bounds", results);
                Check(presentation.sandboxMoveHandleAnchor, "Sandbox utility anchor is bound", results);
                presentation.journey.inSandbox = true;
                presentation.RefreshMoveHandle();
                Check(presentation.MoveHandleBoundsScans == scans + 1,
                    "Switching utility rows caches the sandbox anchor once", results);
                layouts = presentation.MoveHandleLayoutUpdates;
                for (int i = 0; i < 32; i++) presentation.RefreshMoveHandle();
                Check(presentation.MoveHandleBoundsScans == scans + 1 && presentation.MoveHandleLayoutUpdates == layouts,
                    "Stable sandbox handle refreshes also avoid hierarchy scans", results);
            }
            finally
            {
                presentation.journey.inSandbox = mode; anchorPose.Restore(); stagePose.Restore(); handlePose.Restore();
            }
            return results;
        }

        static void Check(bool value, string message, List<string> results)
        {
            if (!value) throw new InvalidOperationException(message);
            results.Add(message);
        }
        sealed class SavedPose
        {
            readonly Transform target;
            readonly Vector3 position, scale;
            readonly Quaternion rotation;
            public SavedPose(Transform target)
            {
                this.target = target; position = target.localPosition; rotation = target.localRotation; scale = target.localScale;
            }
            public void Restore()
            {
                target.localPosition = position; target.localRotation = rotation; target.localScale = scale;
            }
        }
    }
}
