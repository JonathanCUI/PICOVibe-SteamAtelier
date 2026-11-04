using System;
using System.Linq;
using UnityEditor;
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
                var lunarSurface=sky.moon.GetComponent<MeshRenderer>();
                var moonMaterial=SwanXrWorkshopSurfaces.MaterialAsset("Moon disc",lunarSurface.sharedMaterial.shader);
                moonMaterial.CopyPropertiesFromMaterial(lunarSurface.sharedMaterial);
                moonMaterial.SetColor("_BaseColor",new Color(.63f,.70f,.78f));
                moonMaterial.SetColor("_Color",new Color(.63f,.70f,.78f));
                lunarSurface.sharedMaterial=moonMaterial;
                sky.sunHalo=Body(source.sunHalo,parent);sky.moonHalo=Body(source.moonHalo,parent);
                var trial=journey.view.GetComponentInParent<SteamXrTrial>();
                trial.originalSolarHalo=sky.sunHalo.GetComponent<Renderer>().sharedMaterial;
                trial.originalLunarHalo=sky.moonHalo.GetComponent<Renderer>().sharedMaterial;
                ApplyOpticalCard(sky.sunHalo,"Solar optics");
                ApplyOpticalCard(sky.moonHalo,"Lunar optics");
                sky.useOpticalCards=true;
                ApplyCoachWindows(journey);
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
        static void ApplyCoachWindows(SteamJourney journey)
        {
            var pane=journey.GetComponent<StudioSpatialRefineV19>().passengerWindows.First(r=>r).sharedMaterial;
            var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/SwanJourney/XrCoachWindow.shader");
            if(!shader)throw new InvalidOperationException("Missing coach window shader");
            var warm=SwanXrWorkshopSurfaces.MaterialAsset("Night coach windows",shader);
            foreach(var coach in journey.valleyTrain.vehicles.Skip(1))
            {
                int count=0;
                foreach(var renderer in coach.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var slots=renderer.sharedMaterials;
                    for(int i=0;i<slots.Length;i++)
                        if(slots[i]==pane){slots[i]=warm;count++;}
                    renderer.sharedMaterials=slots;
                }
                if(count==0)throw new InvalidOperationException("No window panes on "+coach.name);
            }
        }
        static void ApplyOpticalCard(Transform halo,string name)
        {
            var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/SwanJourney/XrCelestialCard.shader");
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(SwanXrWorkshopSurfaces.Folder+name+".png");
            if(!shader || !texture) throw new InvalidOperationException("Missing celestial optics: "+name);
            var material=SwanXrWorkshopSurfaces.MaterialAsset(name,shader);
            material.shader=shader;
            material.SetTexture("_MainTex",texture);
            if(name=="Solar optics")material.EnableKeyword("SOLAR_RAYS");
            else material.DisableKeyword("SOLAR_RAYS");
            halo.GetComponent<Renderer>().sharedMaterial=material;
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
