using System;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal;
using UnityEngine;

namespace SwanJourney.Editor
{
    internal static class SwanJourneyUrp
    {
        internal const string Lit = "Universal Render Pipeline/Lit";
        internal const string Unlit = "Universal Render Pipeline/Unlit";

        internal static Shader Require(string name)
        {
            var shader = Shader.Find(name);
            if (!shader) throw new InvalidOperationException("Missing V23 shader: " + name);
            return shader;
        }

        // Some old numbered materials are no longer emitted, but remain editable assets.
        // Upgrade those in place to preserve their GUIDs and any external references.
        internal static void UpgradeAndValidateGenerated()
        {
            var upgrader = new StandardUpgrader("Standard");
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { SwanJourneyBuild.Generated }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(path);
                if (font && font.material == material && material && material.shader &&
                    material.shader.name.StartsWith("TextMeshPro/", StringComparison.Ordinal) &&
                    material.mainTexture == font.atlasTexture)
                    continue;
                if (material && material.shader && material.shader.name == "Standard")
                {
                    upgrader.Upgrade(material, MaterialUpgrader.UpgradeFlags.None);
                    EditorUtility.SetDirty(material);
                }
                if (!material || !material.shader || (material.shader.name != Lit && material.shader.name != Unlit))
                    throw new InvalidOperationException("Unexpected shader on generated V23 material: " + path);
            }
            AssetDatabase.SaveAssets();
        }

        internal static void SetSurface(Material material, bool transparent, bool cutout = false)
        {
            material.SetFloat("_Surface", transparent ? 1f : 0f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_BlendModePreserveSpecular", 0f);
            material.SetFloat("_AlphaClip", cutout ? 1f : 0f);
            BaseShaderGUI.SetupMaterialBlendMode(material);
        }
    }
}
