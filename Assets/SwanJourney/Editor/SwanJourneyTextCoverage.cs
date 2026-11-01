using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace SwanJourney.Editor
{
    internal static class SwanJourneyTextCoverage
    {
        const string FontPath = SwanJourneyBuild.Generated + "/WorkshopChinese.asset";

        internal static void Prepare(SteamJourney journey, Transform content)
        {
            var source = journey.font;
            var texts = new List<string>();
            var groups = journey.modules.Concat(journey.finishing).ToArray();
            for (int i = 0; i < groups.Length; i++)
            {
                texts.Add(groups[i].title); texts.Add(groups[i].explanation); texts.Add(groups[i].observation);
                texts.Add(StudioPartCards.ObservationText(i, groups.Length));
            }
            foreach (var text in content.GetComponentsInChildren<TMP_Text>(true))
            {
                text.text = text.text.Replace('‹', '<').Replace('›', '>');
                texts.Add(text.text);
            }
            // Include runtime notices and controls, which are absent from the saved scene.
            foreach (string folder in new[] { "Assets/AtelierJourney/Scripts", "Assets/AtelierJourney/StudioVisual", "Assets/SwanJourney/Scripts" })
                foreach (string file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
                    foreach (Match match in Regex.Matches(File.ReadAllText(file), @"""(?:\\.|[^""\\])*"""))
                        if (match.Value.Any(c => c >= '\u4e00' && c <= '\u9fff'))
                            texts.Add(Regex.Unescape(match.Value.Substring(1, match.Value.Length - 2)));
            uint[] required = texts.Where(t => t != null).SelectMany(t => Regex.Replace(t, "<[^>]*>", ""))
                .Where(c => !char.IsControl(c) && !char.IsSurrogate(c)).Select(c => (uint)c)
                .Concat(Enumerable.Range(32, 95).Select(c => (uint)c))
                .Concat(source.characterTable.Select(c => c.unicode)).Distinct().OrderBy(c => c).ToArray();
            uint[] absentBefore = required.Where(c => !source.HasCharacter((int)c)).ToArray();
            if (!AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath) && !AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), FontPath))
                throw new InvalidOperationException("Could not create the derived workshop font.");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            uint[] toAdd = required.Where(c => !font.HasCharacter((int)c)).ToArray();
            if (toAdd.Length > 0)
            {
                font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                font.isMultiAtlasTexturesEnabled = true;
                // Static font assets clear their runtime font reference when saved.
                using (var serialized = new SerializedObject(font))
                {
                    var sourceFile = source.sourceFontFile;
                    if (!sourceFile) throw new InvalidOperationException("The workshop source font file is missing.");
                    serialized.FindProperty("m_SourceFontFile").objectReferenceValue = sourceFile;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                if (!font.TryAddCharacters(toAdd, out uint[] missing))
                    throw new InvalidOperationException("Workshop font lacks characters: " + string.Join(" ", missing.Select(c => "U+" + c.ToString("X4"))));
            }
            // Bake before the native export. Later lesson changes must never depend on a runtime atlas upload.
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            foreach (var atlas in font.atlasTextures)
            {
                if (!AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, font);
                EditorUtility.SetDirty(atlas);
            }
            EditorUtility.SetDirty(font); EditorUtility.SetDirty(font.material);
            journey.font = font;
            foreach (var text in content.GetComponentsInChildren<TMP_Text>(true).Where(t => t.font == source))
            {
                text.font = font;
                text.fontSharedMaterial = font.material;
                EditorUtility.SetDirty(text);
            }
            EditorUtility.SetDirty(journey);
            AssetDatabase.SaveAssets();
            File.WriteAllText(SwanJourneyBuild.Output + "/text-coverage.json", JsonUtility.ToJson(new Report
            {
                passed = required.All(c => font.HasCharacter((int)c)),
                steps = groups.Length, requiredCharacters = required.Length,
                missingBefore = string.Concat(absentBefore.Select(c => char.ConvertFromUtf32((int)c))),
                atlasCount = font.atlasTextures.Length, fontPath = FontPath
            }, true));
            Debug.Log("SWAN_JOURNEY_TEXT_COVERAGE_PASS steps=" + groups.Length + " characters=" + required.Length + " added=" + absentBefore.Length);
        }

        [Serializable] sealed class Report
        {
            public bool passed, nativeRenderingValidated;
            public int steps, requiredCharacters, atlasCount;
            public string missingBefore, fontPath;
        }
    }
}
