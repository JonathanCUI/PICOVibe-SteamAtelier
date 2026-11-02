using System;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace SwanJourney.Editor
{
    internal static class SwanXrDayNightArt
    {
        internal static void Apply(SteamJourney journey,SwanJourneyInteraction input)
        {
            var atmosphere=journey.GetComponent<MesaAtmosV16>();
            var mood=Object.FindObjectOfType<StudioWorkshopMood>(true);
            var workshop=journey.gameObject.scene;
            var original=EditorSceneManager.OpenScene("Assets/AtelierJourney/Scenes/SteamJourney.unity",OpenSceneMode.Additive);
            try
            {
                var source=original.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MesaDayNightV13>(true)).Single();
                var sky=journey.gameObject.AddComponent<MesaDayNightV13>();
                sky.journey=journey;sky.mood=mood;sky.clouds=atmosphere.cloudTransforms;
                var parent=sky.clouds[0].parent;
                sky.environment=journey.sandboxRoot;sky.skyRoot=parent.gameObject;
                sky.workshopFloor=Array.Empty<Renderer>();
                sky.sun=Body(source.sun,parent);sky.moon=Body(source.moon,parent);
                sky.sunHalo=Body(source.sunHalo,parent);sky.moonHalo=Body(source.moonHalo,parent);
                AddGrabTarget(sky.sun,SwanJourneyTargetKind.Sun);AddGrabTarget(sky.moon,SwanJourneyTargetKind.Moon);
                sky.timeOfDay=source.timeOfDay;sky.cycleSeconds=source.cycleSeconds;sky.automatic=source.automatic;
                atmosphere.sky=sky;input.sky=sky;
                journey.fixedValleyDaylight=false;journey.omitValleyLamps=true;
                SwanXrValleyLocomotiveArt.Apply(journey,source.journey,sky);
            }
            finally
            {
                EditorSceneManager.CloseScene(original,true);
                SceneManager.SetActiveScene(workshop);
            }
        }
        static Transform Body(Transform source,Transform parent)
        {
            var copy=Object.Instantiate(source,parent,false);copy.name=source.name;
            var renderer=copy.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            return copy;
        }
        static void AddGrabTarget(Transform body,SwanJourneyTargetKind kind)
        {
            if(!body.GetComponent<Collider>()) body.gameObject.AddComponent<SphereCollider>();
            var target=body.gameObject.AddComponent<SwanJourneyTarget>();target.kind=kind;
        }
    }
}
