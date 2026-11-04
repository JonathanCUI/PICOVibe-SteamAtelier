using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SwanJourney.Editor
{
    internal static class SwanXrWorkshopSurfaces
    {
        internal const string Folder = "Assets/SwanJourney/Branding/XrSurfaceArt/";
        internal static Material MaterialAsset(string name, Shader shader)
        {
            string path = Folder + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material ShellMaterial(string name, Color tint, float face, float rim, CullMode cull)
        {
            var m = MaterialAsset(name, Shader.Find("SwanJourney/XR Inspection Surface"));
            m.SetColor("_Tint", tint);
            m.SetFloat("_Opacity",face); m.SetFloat("_Rim",rim); m.SetFloat("_Cull",(int)cull);
            return m;
        }

        internal static void Apply(SteamJourney j)
        {
            var shell = ShellMaterial("Pressure shell rim",new Color(.73f,.83f,.85f),.012f,.18f,CullMode.Back);
            var contour = ShellMaterial("Pressure shell seams",new Color(.67f,.78f,.82f),.13f,.04f,CullMode.Off);
            var bridge=j.GetComponentInParent<SwanJourneyVisualBridge>() ?? UnityEngine.Object.FindObjectOfType<SwanJourneyVisualBridge>(true);
            var changes=new Dictionary<Material,Material>();
            foreach (var def in bridge.materials)
            {
                var original=def.material;
                if(!original)continue;
                Material material;
                switch(original.name)
                {
                    case "Swan V23 Faint complete pressure shell": changes[original]=shell;continue;
                    case "Swan V23 Subtle structural contour": changes[original]=contour;continue;
                    case "Swan V23 Water surface":
                    case "Swan V23 Absorbing boiler water volume":
                    case "Swan V23 Connected firebox water depth":
                    case "Swan V23 Thin reflective cab glass":
                        material=MaterialAsset(original.name.Replace("Swan V23 ",""),original.shader);
                        material.CopyPropertiesFromMaterial(original);break;
                    default:continue;
                }
                bool glass=original.name.EndsWith("cab glass",StringComparison.Ordinal);
                bool surface=original.name.EndsWith("Water surface",StringComparison.Ordinal);
                var color=glass ? new Color(.47f,.55f,.57f,.025f) : surface ? new Color(.055f,.36f,.36f,.72f) : new Color(.045f,.28f,.29f,.24f);
                material.SetColor("_BaseColor",color); material.SetColor("_Color",color);
                material.SetFloat("_Smoothness",glass ? .33f : .42f);
                material.SetFloat("_Metallic",0); material.SetColor("_EmissionColor",Color.black);
                if(glass){material.SetFloat("_SpecularHighlights",0);material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");}
                changes[original]=material;def.material=material;def.color=color;
            }
            if(!changes.ContainsValue(shell) || !changes.ContainsValue(contour))throw new InvalidOperationException("Missing structural inspection surfaces");
            foreach(var r in j.spatialRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                var materials=r.sharedMaterials;bool changed=false;
                for(int i=0;i<materials.Length;i++)
                    if(materials[i] && changes.TryGetValue(materials[i],out var next)){materials[i]=next;changed=true;}
                if(changed)r.sharedMaterials=materials;
            }
        }
    }
}
