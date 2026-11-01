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
        readonly Vector3 deskPosition, previewPosition, gripPosition, gripScale;

        SwanJourneyWorkshopLayout(SwanJourneyPresentation table, SwanJourneyInteraction input)
        {
            buttonsVisible = ButtonNames.Select(name => Button(table, name).activeSelf).ToArray();
            deskPosition = table.workshopSupports[1].localPosition;
            previewPosition = table.journey.previewRoot.transform.localPosition;
            var grip = Grip(table);
            gripPosition = grip.localPosition;
            gripScale = grip.localScale;
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
            var grip = Grip(table);
            grip.localPosition = gripPosition;
            grip.localScale = gripScale;
            input.moveLesson.SetActive(lessonVisible);
            table.RefreshFootprint();
        }

        // Two button names contain a literal slash, so Transform.Find(path) would split them.
        static GameObject Button(SwanJourneyPresentation table, string name) =>
            table.journey.workshopRoot.transform.Cast<Transform>().Single(child => child.name == name).gameObject;

        static Transform Grip(SwanJourneyPresentation table) =>
            table.moveHandle.GetComponentsInChildren<SwanJourneyTarget>(true)
                .Single(target => target.kind == SwanJourneyTargetKind.MoveWorkbench).transform;
    }
}
