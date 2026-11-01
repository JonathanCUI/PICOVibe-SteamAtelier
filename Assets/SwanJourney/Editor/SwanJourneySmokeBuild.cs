using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SwanJourney.Editor
{
    internal static class SwanJourneySmokeBuild
    {
        const string Art = "Assets/SwanJourney/Branding/ChimneySmoke";
        const int Columns = 8, Rows = 4, Bands = 4;

        internal static void Configure(SteamJourney journey)
        {
            if (journey.bakedChimney) return;
            var material = AssetDatabase.LoadAssetAtPath<Material>(Art + ".mat");
            if (!material)
            {
                var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + ".png");
                if (!atlas) throw new InvalidOperationException("Missing chimney smoke atlas");
                material = new Material(SwanJourneyUrp.Require(SwanJourneyUrp.Unlit)) { name = "ChimneySmoke" };
                material.SetTexture("_BaseMap", atlas);
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Cull", (float)CullMode.Off);
                material.doubleSidedGI = true;
                SwanJourneyUrp.SetSurface(material, true);
                AssetDatabase.CreateAsset(material, Art + ".mat");
            }
            string path = Art + ".asset";
            if (!AssetDatabase.LoadMainAssetAtPath(path))
            {
                for (int i = 0; i < Columns * Rows; i++)
                {
                    var mesh = FrameMesh(i);
                    if (i == 0) AssetDatabase.CreateAsset(mesh, path);
                    else AssetDatabase.AddObjectToAsset(mesh, path);
                }
                AssetDatabase.ImportAsset(path);
            }
            var meshes = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>()
                .OrderBy(m => m.name, StringComparer.Ordinal).ToArray();
            if (meshes.Length != Columns * Rows) throw new InvalidOperationException("Incomplete chimney animation");
            var root = new GameObject("Chimney exhaust");
            var outlet = journey.smoke.transform;
            root.transform.SetParent(outlet.parent, false);
            root.transform.localPosition = outlet.localPosition;
            var effect = root.AddComponent<SwanJourneyChimneySmoke>();
            effect.journey = journey;
            effect.tunnel = journey.GetComponent<MesaSteamTunnelV17>();
            effect.frames = new GameObject[meshes.Length];
            for (int i = 0; i < meshes.Length; i++)
            {
                var frame = new GameObject(meshes[i].name, typeof(MeshFilter), typeof(MeshRenderer));
                frame.transform.SetParent(root.transform, false);
                frame.GetComponent<MeshFilter>().sharedMesh = meshes[i];
                var renderer = frame.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                frame.SetActive(false);
                effect.frames[i] = frame;
            }
            journey.bakedChimney = effect;
            EditorUtility.SetDirty(journey);
        }

        // Two intersecting, narrowing strips share one draw and remain visible around the table.
        static Mesh FrameMesh(int index)
        {
            var positions = new Vector3[(Bands + 1) * 4];
            var uv = new Vector2[positions.Length];
            var triangles = new int[Bands * 12];
            for (int strip = 0; strip < 2; strip++)
            {
                for (int j = 0; j <= Bands; j++)
                {
                    float v = (float)j / Bands;
                    // A slight twist keeps a whole ribbon from collapsing into a bright edge when viewed end-on.
                    var axis = Quaternion.Euler(0f, 26f + 90f * strip + 20f * v, 0f) * Vector3.right;
                    float span = Mathf.Min(1f, .24f + 1.12f * Mathf.Pow(v, .74f));
                    var center = new Vector3(-.36f * v * v, SwanJourneyChimneySmoke.Height * v - .05f, .08f * v * v);
                    int a = strip * (Bands + 1) * 2 + j * 2;
                    for (int side = 0; side < 2; side++)
                    {
                        float offset = (side == 0 ? -1f : 1f) * span;
                        positions[a + side] = center + axis * offset * .91f;
                        // Sample the atlas at texel centers; each end is fully transparent.
                        uv[a + side] = new Vector2((index % Columns + .5f + offset * (.5f - .5f / 128f)) / Columns,
                            (Rows - 1 - index / Columns + Mathf.Lerp(.5f / 256f, 1f - .5f / 256f, v)) / Rows);
                    }
                    if (j == Bands) continue;
                    int k = (strip * Bands + j) * 6;
                    triangles[k] = a; triangles[k + 1] = a + 2; triangles[k + 2] = a + 1;
                    triangles[k + 3] = a + 1; triangles[k + 4] = a + 2; triangles[k + 5] = a + 3;
                }
            }
            var mesh = new Mesh { name = "Chimney billow " + index.ToString("D2"), vertices = positions, uv = uv, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
