using System;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SwanJourney.Editor
{
    // These are hand-authored in the saved Swan scene. Preserve only the layout,
    // so a new partner scene can still supply geometry, materials and references.
    internal sealed class SwanJourneyWorkshopLayout
    {
        static readonly string[] ButtonNames = { "一键拆开", "暂停 / 继续", "慢放 / 常速" };
        readonly bool[] buttonsVisible;
        readonly bool lessonVisible;
        readonly Vector3 deskPosition, previewPosition;

        SwanJourneyWorkshopLayout(SwanJourneyPresentation table, SwanJourneyInteraction input)
        {
            buttonsVisible = ButtonNames.Select(name => Button(table, name).activeSelf).ToArray();
            deskPosition = table.workshopSupports[1].localPosition;
            previewPosition = table.journey.previewRoot.transform.localPosition;
            lessonVisible = input.moveLesson.activeSelf;
        }

        internal static SwanJourneyWorkshopLayout ReadSaved()
        {
            if (!File.Exists(SwanJourneyBuild.ScenePath)) return null;
            var scene = SceneManager.GetSceneByPath(SwanJourneyBuild.ScenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(SwanJourneyBuild.ScenePath, OpenSceneMode.Additive);
            try
            {
                if (scene.isDirty && !openedHere)
                    throw new InvalidOperationException("请先保存火车装配场景，再重新生成 V23。");
                var table = scene.GetRootGameObjects().SelectMany(root =>
                    root.GetComponentsInChildren<SwanJourneyPresentation>(true)).Single();
                return new SwanJourneyWorkshopLayout(table, table.GetComponent<SwanJourneyInteraction>());
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        internal void Apply(SwanJourneyPresentation table, SwanJourneyInteraction input)
        {
            for (int i = 0; i < ButtonNames.Length; i++)
                Button(table, ButtonNames[i]).SetActive(buttonsVisible[i]);
            table.workshopSupports[1].localPosition = deskPosition;
            table.journey.previewRoot.transform.localPosition = previewPosition;
            input.moveLesson.SetActive(lessonVisible);
            table.RefreshFootprint();
        }

        internal static void CentreConsole(SwanJourneyPresentation table)
        {
            var journey = table.journey;
            var workshop = journey.workshopRoot.transform;
            var desk = table.workshopSupports[1];
            // +Z is the player-facing side of the authored workshop (the Swan
            // composition rotates it 180 degrees). Centre across the train's X
            // axis, retaining the authored reach, elevation and button visibility.
            float centreX = workshop.InverseTransformPoint(journey.trainAnchor.position).x;
            float shift = centreX - desk.localPosition.x;
            desk.localPosition += Vector3.right * shift;
            journey.previewRoot.transform.position += workshop.TransformVector(Vector3.right * shift);
            if (!SwanJourneyPresentation.TryBounds(desk, workshop, out var bounds))
                throw new InvalidOperationException("Operation console has no geometry.");
            var row = new Vector3(centreX, bounds.center.y, bounds.max.z + .7f);
            for (int i = 0; i < ButtonNames.Length; i++)
                Button(table, ButtonNames[i]).transform.localPosition = row + Vector3.right * ((i - 1) * 3.15f);
            table.RefreshFootprint();
        }

        // Two button names contain a literal slash, so Transform.Find(path) would split them.
        static GameObject Button(SwanJourneyPresentation table, string name) =>
            table.journey.workshopRoot.transform.Cast<Transform>().Single(child => child.name == name).gameObject;

    }
}
