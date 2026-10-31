using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SwanJourney.Editor
{
    // Exercises the partner lifecycle in an isolated, unsaved Edit Mode scene.
    // Counters measure skipped source work, not device performance or native FPS.
    public static class SwanJourneyCloudChecks
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        public static List<string> Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Cloud checks require Edit Mode.");
            var checks = new List<string>();
            string[] globals = { "_MesaAmbientSky", "_MesaAmbientHorizon", "_MesaAmbientGround" };
            var colors = globals.Select(Shader.GetGlobalColor).ToArray();
            try
            {
                using (var fixture = new Fixture(true, true)) CheckSwan(fixture, checks);
                using (var fixture = new Fixture(true, false))
                {
                    fixture.Tick(); fixture.SetMode(true); fixture.Tick();
                    Check(fixture.enhance.CloudWriteCount == 3,
                        "Swan without V16 retains V15 cloud animation in sandbox", checks);
                    fixture.SetMode(false); fixture.Tick(); fixture.Tick();
                    Check(fixture.enhance.CloudWriteCount == 3 && fixture.enhance.SwanHiddenCleanupCount == 2,
                        "Missing-provider fallback still skips stable workshop updates and cleans each exit once", checks);
                }
                using (var fixture = new Fixture(false, true))
                {
                    fixture.Tick(); fixture.Tick(); fixture.Tick();
                    Check(fixture.enhance.CloudWriteCount == 9 && fixture.atmos.CloudWriteCount == 9,
                        "Desktop workshop retains both original cloud update paths", checks);
                    Check(fixture.enhance.SwanHiddenSkipCount == 0 && fixture.atmos.SwanHiddenSkipCount == 0
                        && fixture.enhance.SwanHiddenCleanupCount == 0 && fixture.atmos.SwanHiddenCleanupCount == 0,
                        "Swan hidden gates do not run on the desktop path", checks);
                    fixture.SetMode(true); fixture.Tick();
                    Check(fixture.enhance.CloudWriteCount == 12 && fixture.atmos.CloudWriteCount == 12,
                        "Desktop sandbox retains V15 updates even when V16 is available", checks);
                }
                return checks;
            }
            finally
            {
                for (int i = 0; i < globals.Length; i++) Shader.SetGlobalColor(globals[i], colors[i]);
            }
        }

        static void CheckSwan(Fixture f, List<string> checks)
        {
            var positions = f.sky.clouds.Select(t => t.localPosition).ToArray();
            var scales = f.sky.clouds.Select(t => t.localScale).ToArray();
            Set(f.enhance, "clock", 31f); Set(f.atmos, "clock", 37f);
            float enhanceDelta = Time.unscaledDeltaTime, atmosDelta = Mathf.Min(Time.deltaTime, .06f);
            f.Tick();
            Check(Mathf.Abs(Get<float>(f.enhance, "clock") - (31 + enhanceDelta)) < .0001f
                && Mathf.Abs(f.atmos.Evolution - (37 + atmosDelta) * .40f) < .0001f,
                "Real hidden LateUpdate retains clock accumulation and V16 evolution", checks);
            Check(f.enhance.SwanHiddenCleanupCount == 1 && f.atmos.SwanHiddenCleanupCount == 1,
                "Startup workshop performs exactly one cleanup in each provider", checks);
            Check(f.dust.emission.rateOverTime.constant == 0 && f.plume.emission.rateOverTime.constant == 0
                && !f.dust.isPlaying && !f.plume.isPlaying && f.PuffRenderers.All(r => !r.enabled),
                "First hidden frame stops sandbox particles and disables every V16 puff", checks);
            Check(!f.beam.gameObject.activeSelf && !f.halo.gameObject.activeSelf && !f.flare.gameObject.activeSelf,
                "First hidden frame removes sandbox beam, halo and flare visuals", checks);

            // The native bridge consumes blocks. Visible workshop bulbs must still
            // receive black every frame; hidden bulbs must not be rewritten.
            f.visibleBulb.SetPropertyBlock(null);
            var hiddenBlock = new MaterialPropertyBlock(); hiddenBlock.SetColor("_EmissionColor", Color.magenta);
            f.hiddenBulb.SetPropertyBlock(hiddenBlock);
            f.lamp.intensity = 17;
            f.Tick(); f.Tick(); f.Tick();
            Check(f.enhance.SwanHiddenCleanupCount == 1 && f.atmos.SwanHiddenCleanupCount == 1
                && f.enhance.SwanHiddenSkipCount == 4 && f.atmos.SwanHiddenSkipCount == 4,
                "Stable workshop frames skip without repeating cleanup", checks);
            Check(f.enhance.CloudWriteCount == 0 && f.atmos.CloudWriteCount == 0
                && f.sky.clouds.Select(t => t.localPosition).SequenceEqual(positions)
                && f.sky.clouds.Select(t => t.localScale).SequenceEqual(scales),
                "Hidden clouds retain pose with zero provider writes", checks);
            Check(Emission(f.visibleBulb) == Color.black && Emission(f.hiddenBulb) == Color.magenta && f.lamp.intensity == 17,
                "Visible bulbs stay black after MPB consumption while hidden bulbs and lamps are untouched", checks);
            Check(f.smoke.GetComponent<Renderer>().enabled && f.smoke.emission.rateOverTime.constant == 17
                && f.journey.pressure == 3 && f.journey.brake == .4f && f.journey.running,
                "Hidden optimization preserves legacy workshop smoke and process state", checks);

            f.SetMode(true); f.Tick();
            Check(f.enhance.CloudWriteCount == 0 && f.atmos.CloudWriteCount == 3,
                "Enabled initialized V16 is the sole Swan writer for the three shared clouds", checks);
            Check(!f.smoke.GetComponent<Renderer>().enabled && f.dust.isPlaying && f.plume.isPlaying
                && f.beam.gameObject.activeSelf && f.halo.gameObject.activeSelf,
                "Sandbox entry restores particles and lamp visuals while suppressing legacy smoke", checks);
            Invoke(f.atmos, "Emit"); f.Tick();
            Check(f.atmos.ActivePuffs > 0 && f.PuffRenderers.Any(r => r.enabled),
                "Actual V16 emission and LateUpdate produce active sandbox puffs", checks);
            int v16Writes = f.atmos.CloudWriteCount;
            f.SetMode(false); f.Tick(); f.Tick();
            Check(f.enhance.SwanHiddenCleanupCount == 2 && f.atmos.SwanHiddenCleanupCount == 2
                && f.atmos.CloudWriteCount == v16Writes && f.atmos.ActivePuffs == 0
                && f.PuffRenderers.All(r => !r.enabled),
                "Returning to workshop clears live puffs once and stops cloud writes", checks);
            Check(f.smoke.GetComponent<Renderer>().enabled && !f.dust.isPlaying && !f.plume.isPlaying
                && !f.beam.gameObject.activeSelf && !f.halo.gameObject.activeSelf && !f.flare.gameObject.activeSelf,
                "Return cleanup restores bench smoke and hides all sandbox effects", checks);
            f.SetMode(true); f.Tick(); Invoke(f.atmos, "Emit"); f.Tick();
            Check(f.atmos.ActivePuffs > 0 && f.atmos.CloudWriteCount == v16Writes + 6
                && f.enhance.CloudWriteCount == 0 && f.enhance.SwanHiddenCleanupCount == 2,
                "Re-entry resumes V16 clouds and puffs without a duplicate V15 writer", checks);

            f.atmos.enabled = false;
            int legacyWrites = f.enhance.CloudWriteCount;
            f.Tick();
            Check(f.enhance.CloudWriteCount == legacyWrites + 3,
                "Disabling a cached V16 immediately restores the V15 cloud fallback", checks);
            f.atmos.enabled = true; Invoke(f.atmos, "OnEnable");
            legacyWrites = f.enhance.CloudWriteCount; f.Tick();
            Check(f.enhance.CloudWriteCount == legacyWrites,
                "Re-enabling V16 relinquishes the duplicate V15 writer", checks);
            UnityEngine.Object.DestroyImmediate(f.atmos);
            f.Tick();
            Check(f.enhance.CloudWriteCount == legacyWrites + 3,
                "A destroyed cached provider safely falls back to V15", checks);
        }

        static Color Emission(Renderer renderer)
        {
            var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
            return block.GetColor("_EmissionColor");
        }
        static void Check(bool result, string message, List<string> checks)
        {
            if (!result) throw new InvalidOperationException(message);
            checks.Add(message);
        }
        static void Invoke(object target, string method)
            => target.GetType().GetMethod(method, Private).Invoke(target, null);
        static void Set(object target, string field, object value)
            => target.GetType().GetField(field, Private).SetValue(target, value);
        static T Get<T>(object target, string field)
            => (T)target.GetType().GetField(field, Private).GetValue(target);

        sealed class Fixture : IDisposable
        {
            readonly Scene scene;
            GameObject root;
            Material material;
            Mesh mesh;
            public SteamJourney journey;
            public MesaDayNightV13 sky;
            public MesaEnhanceV15 enhance;
            public MesaAtmosV16 atmos;
            public ParticleSystem smoke, dust, plume;
            public Renderer visibleBulb, hiddenBulb, beam, halo, flare;
            public Light lamp;
            public IEnumerable<MeshRenderer> PuffRenderers => journey.sandboxRoot.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => r.name.StartsWith("Steam puff ", StringComparison.Ordinal));

            public Fixture(bool swan, bool withProvider)
            {
                scene = EditorSceneManager.NewPreviewScene();
                try
                {
                    root = new GameObject("Temporary Swan cloud checks") { hideFlags = HideFlags.HideAndDontSave };
                    SceneManager.MoveGameObjectToScene(root, scene);
                    journey = root.AddComponent<SteamJourney>(); journey.SwanInputActive = swan;
                    journey.spatialRoot = root.transform; journey.pressure = 3; journey.brake = .4f; journey.running = true;
                    journey.workshopRoot = Child("Workshop", root.transform);
                    journey.sandboxRoot = Child("Sandbox", root.transform);
                    journey.trainAnchor = Child("Shared train", journey.workshopRoot.transform).transform;
                    journey.view = Child("View", root.transform).AddComponent<Camera>(); journey.view.enabled = false;
                    mesh = new Mesh { name = "Cloud check triangle", vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }, triangles = new[] { 0, 1, 2 } };
                    var shader = Shader.Find("Standard");
                    if (!shader) throw new InvalidOperationException("Cloud fixture requires the editor Standard shader.");
                    material = new Material(shader);
                    sky = root.AddComponent<MesaDayNightV13>(); sky.journey = journey;
                    sky.clouds = Enumerable.Range(0, 3).Select(i => Render("Cloud " + i, journey.sandboxRoot.transform).transform).ToArray();
                    for (int i = 0; i < sky.clouds.Length; i++) sky.clouds[i].localPosition = new Vector3(i, 2, 3);
                    smoke = Particles("Bench smoke", journey.workshopRoot.transform); journey.smoke = smoke;
                    var smokeEmission = smoke.emission; smokeEmission.rateOverTime = 17;
                    dust = Particles("Sandbox dust", journey.sandboxRoot.transform);
                    plume = Particles("Sandbox plume", journey.sandboxRoot.transform);
                    lamp = Child("Sandbox lamp", journey.sandboxRoot.transform).AddComponent<Light>(); lamp.type = LightType.Spot;
                    visibleBulb = Render("Visible train bulb", journey.workshopRoot.transform);
                    hiddenBulb = Render("Hidden sandbox bulb", journey.sandboxRoot.transform);
                    beam = Render("Beam", journey.sandboxRoot.transform);
                    halo = Render("Halo", journey.sandboxRoot.transform);
                    flare = Render("Flare", journey.sandboxRoot.transform);
                    var old = root.AddComponent<MesaAtmosphereV14>(); old.enabled = false;
                    old.journey = journey; old.sky = sky; old.dust = new[] { dust }; old.lamps = new[] { lamp };
                    old.bulbs = new[] { visibleBulb, hiddenBulb }; old.beams = new[] { beam }; old.halos = new[] { halo };
                    enhance = root.AddComponent<MesaEnhanceV15>(); enhance.journey = journey; enhance.sky = sky;
                    enhance.atmosphere = old; enhance.plume = plume; enhance.flares = new[] { flare };
                    if (withProvider)
                    {
                        atmos = root.AddComponent<MesaAtmosV16>(); atmos.journey = journey; atmos.sky = sky;
                        atmos.previous = enhance; atmos.volumeMesh = mesh; atmos.steamMaterial = material;
                    }
                    Invoke(enhance, "Awake");
                    if (atmos) Invoke(atmos, "Awake");
                    SetMode(false);
                }
                catch { Dispose(); throw; }
            }
            static GameObject Child(string name, Transform parent)
            {
                var value = new GameObject(name); value.transform.SetParent(parent, false); return value;
            }
            Renderer Render(string name, Transform parent)
            {
                var go = Child(name, parent); go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; return renderer;
            }
            static ParticleSystem Particles(string name, Transform parent)
            {
                var value = Child(name, parent).AddComponent<ParticleSystem>();
                var main = value.main; main.playOnAwake = false; main.startLifetime = 1;
                value.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); return value;
            }
            public void SetMode(bool sandbox)
            {
                journey.inSandbox = sandbox; journey.sandboxRoot.SetActive(sandbox);
            }
            public void Tick()
            {
                Invoke(enhance, "LateUpdate");
                if (atmos && atmos.isActiveAndEnabled) Invoke(atmos, "LateUpdate");
            }
            public void Dispose()
            {
                if (root) UnityEngine.Object.DestroyImmediate(root);
                if (material) UnityEngine.Object.DestroyImmediate(material);
                if (mesh) UnityEngine.Object.DestroyImmediate(mesh);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
