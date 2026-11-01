using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SwanJourney.Editor
{
    internal static class SwanJourneyAssemblySteps
    {
        internal static void MergeHeadlamps(SteamJourney journey)
        {
            if (journey.finishing.Length != 11 || journey.socketGhosts.Length != 18)
                throw new InvalidOperationException("Headlamp grouping requires the original 18-step scene.");
            var right = journey.finishing[9];
            var left = journey.finishing[10];
            if (right.title != "右前照灯" || left.title != "左前照灯" || !right.preview || !left.preview)
                throw new InvalidOperationException("The final two source groups must be the right and left headlamps.");

            Vector3 center = (right.previewCentre + left.previewCentre) * .5f;
            var preview = new GameObject("Preview · 双前照灯");
            preview.transform.SetParent(journey.previewRoot.transform, false);
            preview.transform.position = right.preview.transform.position;
            preview.transform.rotation = right.preview.transform.rotation;
            preview.transform.localScale = Vector3.one * right.presentationScale;
            foreach (var lamp in new[] { right, left })
            {
                var part = lamp.preview.transform;
                part.SetParent(preview.transform, false);
                part.localPosition = lamp.previewCentre - center;
                part.localRotation = Quaternion.identity;
                part.localScale = Vector3.one;
                part.gameObject.SetActive(true);
            }
            preview.SetActive(false);

            var ghost = new GameObject("Socket projection · 双前照灯");
            ghost.transform.SetParent(journey.trainAnchor, false);
            ghost.transform.localPosition = center;
            foreach (var old in journey.socketGhosts.Skip(16))
            {
                old.transform.SetParent(ghost.transform, true);
                old.SetActive(true);
            }
            ghost.SetActive(false);
            journey.socketGhosts = journey.socketGhosts.Take(16).Append(ghost).ToArray();
            journey.finishing = journey.finishing.Take(9).Append(new JourneyModule
            {
                title = "双前照灯",
                explanation = "左右前照灯安装在车头两侧，用于照亮前方轨道。拿起这一组灯具，对准两侧安装轮廓，松手即可同时装好。",
                observation = "两盏前照灯同时就位；完成装配后，可在山谷操作台开启车灯。",
                objects = right.objects.Concat(left.objects).Distinct().ToArray(),
                preview = preview, socket = center, previewCentre = center,
                previewOffset = right.previewOffset, presentationScale = right.presentationScale,
                snapRadius = Mathf.Max(right.snapRadius, left.snapRadius),
                installed = right.installed && left.installed
            }).ToArray();
            journey.finishIndex = Mathf.Min(journey.finishIndex, 9);
            EditorUtility.SetDirty(journey);
        }

        internal static void CheckMergedSteps()
        {
            var journey = UnityEngine.Object.FindObjectOfType<SteamJourney>(true);
            if (!journey || journey.modules.Length != 7 || journey.finishing.Length != 10 || journey.socketGhosts.Length != 17)
                throw new InvalidOperationException("Expected 17 assembly steps and matching placement hints.");
            var pair = journey.finishing[9];
            if (pair.objects.Length != 2 || pair.objects.Any(o => !o) || !pair.preview || pair.preview.transform.childCount != 2
                || journey.socketGhosts[16].transform.childCount != 2)
                throw new InvalidOperationException("Both lamps must have assembled objects, preview geometry and placement hints.");
            foreach (var child in pair.preview.GetComponentsInChildren<BoxCollider>(true))
                if (!child.enabled) throw new InvalidOperationException("Both lamp previews must remain grabbable.");
            for (int state = 0; state < 4; state++)
            {
                var old = Enumerable.Range(0, 11).Select(i => i % 2 == 0).ToArray();
                old[9] = (state & 1) != 0; old[10] = (state & 2) != 0;
                var upgraded = SteamJourney.UpgradeFinishedProgress(old, 10);
                if (upgraded.Length != 10 || !old.Take(9).SequenceEqual(upgraded.Take(9)) || upgraded[9] != (state == 3))
                    throw new InvalidOperationException("Legacy progress must retain previous parts and complete the pair only when both lights are installed.");
                if (!SteamJourney.UpgradeFinishedProgress(upgraded, 10).SequenceEqual(upgraded))
                    throw new InvalidOperationException("Repeated progress migration must be stable.");
            }
            if (!SteamJourney.UpgradeFinishedProgress(Enumerable.Repeat(true, 13).ToArray(), 10).All(v => v))
                throw new InvalidOperationException("Older 20-step completed saves must remain complete.");
            journey.suppressPersistence = true;
            journey.stage = journey.finishIndex = 0;
            journey.inSandbox = false;
            foreach (var module in journey.modules.Concat(journey.finishing)) module.installed = false;
            for (int i = 0; i < 16; i++)
                if (!journey.InstallCurrent()) throw new InvalidOperationException("Earlier assembly step could not install: " + i);
            if (journey.Complete || journey.Current != pair || pair.objects.Any(o => o.activeSelf))
                throw new InvalidOperationException("Both lamps must wait for the final step.");
            if (!journey.InstallCurrent() || !journey.Complete || pair.objects.Any(o => !o.activeSelf))
                throw new InvalidOperationException("One final installation must install both lamps and complete the train.");
            if (!journey.DetachModule(pair) || journey.Complete || pair.objects.Any(o => o.activeSelf) || !pair.preview.activeSelf)
                throw new InvalidOperationException("Removing either lamp must reopen the paired step and show both preview lamps.");
            Debug.Log("SWAN_JOURNEY_PAIRED_LAMPS_CHECKS_PASS steps=17 legacyLampStates=4");
        }
    }
}
