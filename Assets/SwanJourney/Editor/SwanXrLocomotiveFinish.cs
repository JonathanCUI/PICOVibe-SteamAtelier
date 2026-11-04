using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SwanJourney.Editor
{
    internal static class SwanXrLocomotiveFinish
    {
        const string Native = "Assets/SwanJourney/Generated/NativeVisuals/";

        static void Variant(Dictionary<Material, Material> variants, int id, string finishName, Color tint, float relief,
            float smoothness = 1f, bool keepSourceMask = false, string maskName = null)
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(Native + $"Material_{id:D4}.mat");
            if (!source || !source.IsKeywordEnabled("_METALLICSPECGLOSSMAP"))
                throw new InvalidOperationException($"Missing locomotive {finishName} surface {id}");
            var texture = keepSourceMask ? source.GetTexture("_MetallicGlossMap") :
                AssetDatabase.LoadAssetAtPath<Texture2D>(SwanXrWorkshopSurfaces.Folder + $"Finish {maskName ?? finishName} mask.png");
            if (!texture) throw new InvalidOperationException($"Missing {finishName} packed map");
            var finish = SwanXrWorkshopSurfaces.MaterialAsset($"Finish {finishName} {id:D4}", source.shader);
            finish.CopyPropertiesFromMaterial(source);
            finish.SetTexture("_MetallicGlossMap", texture);
            finish.SetColor("_BaseColor", tint);
            finish.SetFloat("_Smoothness", smoothness);
            finish.SetFloat("_BumpScale", source.GetFloat("_BumpScale") * relief);
            variants.Add(source, finish);
        }

        static int Replace(GameObject root, Dictionary<Material, Material> variants)
        {
            int count = 0;
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var slots = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i] && variants.TryGetValue(slots[i], out var finish))
                    {
                        slots[i] = finish;
                        changed = true;
                        count++;
                    }
                if (changed) renderer.sharedMaterials = slots;
            }
            return count;
        }

        internal static void Apply(SteamJourney journey, SteamXrTrial trial)
        {
            ApplyTankAndDoor(journey, trial);
            var shell = new Dictionary<Material, Material>();
            var rods = new Dictionary<Material, Material>();
            var enamel = new Color(.128f, .146f, .158f);
            var brass = new Color(.68f, .53f, .34f);
            var steel = new Color(.47f, .52f, .55f);
            Variant(shell, 0, "Paint", enamel, .38f);
            Variant(shell, 65, "Paint", enamel, .38f);
            Variant(shell, 11, "Brass", brass, .7f);
            Variant(shell, 40, "Brass", brass, .7f);
            Variant(rods, 30, "Steel", steel, .8f);
            Variant(rods, 36, "Steel", steel, .8f);
            var guard = new Dictionary<Material, Material>();
            Variant(guard, 87, "Paint", new Color(.143f, .155f, .164f), .55f, .84f);
            Variant(guard, 86, "Blackened wire", new Color(.185f, .188f, .19f), .70f, .58f, keepSourceMask: true);
            Variant(guard, 35, "Cast graphite", new Color(.15f, .16f, .162f), .90f, .85f, keepSourceMask: true);
            trial.frontGuard = journey.engine.locomotive.Find("B unified cast front cradle");
            if (!trial.frontGuard) throw new InvalidOperationException("Missing front guard on the workshop locomotive");
            int guardSlots = Replace(trial.frontGuard.gameObject, guard);
            int guardPreviewSlots = 0;
            int boiler = 0, linkage = 0, previews = 0;
            foreach (var module in journey.modules)
            {
                bool hasBarrel = false, hasRod = false;
                foreach (var part in module.objects)
                {
                    if (!part) continue;
                    if (part.transform == trial.frontGuard && module.preview)
                        guardPreviewSlots += Replace(module.preview, guard);
                    if (part.name.StartsWith("P02 ", StringComparison.Ordinal))
                    {
                        boiler += Replace(part, shell);
                        hasBarrel = true;
                    }
                    if (part.name.StartsWith("P11 ", StringComparison.Ordinal) || part.name.StartsWith("P12 ", StringComparison.Ordinal))
                    {
                        linkage += Replace(part, rods);
                        hasRod = true;
                    }
                }
                if (module.preview && (hasBarrel || hasRod))
                    previews += Replace(module.preview, hasBarrel ? shell : rods);
            }
            if (boiler == 0 || linkage == 0 || previews == 0)
                throw new InvalidOperationException("Missing workshop finish receiver");
            if (guardSlots < 4 || guardPreviewSlots < 3)
                throw new InvalidOperationException($"Incomplete front guard bindings: {guardSlots}/{guardPreviewSlots}");
            var originals = new List<Material>();
            var finishes = new List<Material>();
            foreach (var group in new[] { shell, rods, guard })
                foreach (var pair in group) { originals.Add(pair.Key); finishes.Add(pair.Value); }
            trial.finishOriginals = originals.ToArray();
            trial.finishVariants = finishes.ToArray();
            Debug.Log($"XR_FINISH_SLOTS boiler={boiler} rods={linkage} previews={previews}");
            Debug.Log($"XR_GUARD_SLOTS installed={guardSlots} preview={guardPreviewSlots}");
        }

        static void ApplyTankAndDoor(SteamJourney journey, SteamXrTrial trial)
        {
            var tank = new Dictionary<Material, Material>();
            var door = new Dictionary<Material, Material>();
            Variant(tank, 0, "Water tank enamel", new Color(.174f,.173f,.168f), .65f, .92f, keepSourceMask:true);
            Variant(door, 21, "Door seating rim", new Color(.28f,.296f,.30f), .68f, .75f, maskName: "Steel");
            Variant(door, 13, "Door fasteners", new Color(.54f,.58f,.60f), .64f, .94f, maskName: "Steel");
            Variant(door, 11, "Door handwheel", new Color(.74f,.62f,.43f), .72f, .98f, maskName: "Brass");
            int tanks = 0, fittings = 0, previews = 0;
            foreach (var module in System.Linq.Enumerable.Concat(journey.modules, journey.finishing))
            {
                Dictionary<Material, Material> group = null;
                foreach (var part in module.objects)
                {
                    if (!part) continue;
                    if (part.name.StartsWith("P08 ", StringComparison.Ordinal) && part.name.Contains("水箱"))
                    {
                        group = tank;
                        tanks += Replace(part,tank);
                    }
                    else if (part.name.StartsWith("M01 ", StringComparison.Ordinal) ||
                             part.name.StartsWith("V2-SMOKE ", StringComparison.Ordinal))
                    {
                        group = door;
                        fittings += Replace(part,door);
                    }
                }
                if (group != null && module.preview) previews += Replace(module.preview,group);
            }
            if (tanks < 2 || fittings < 3 || previews < 4)
                throw new InvalidOperationException($"Missing tank or door bindings {tanks}/{fittings}/{previews}");
            var sources = new List<Material>(); var variants = new List<Material>();
            foreach (var group in new[]{tank,door})
                foreach (var pair in group) { sources.Add(pair.Key); variants.Add(pair.Value); }
            trial.detailOriginals = sources.ToArray();
            trial.detailVariants = variants.ToArray();
            Debug.Log($"XR_DETAIL_SLOTS tanks={tanks} door={fittings} previews={previews}");
        }
    }
}
