using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SwanJourney.Editor
{
    // Edit Mode fixtures call the actual presentation methods. They do not call
    // Journey.Start/Update, enter Play, open a scene, save state or contact Swan.
    public static class SwanJourneyDrivingDisplayChecks
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        public static List<string> Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Driving display checks require Edit Mode.");
            var checks = new List<string>();
            using (var f = new Fixture(true))
            {
                f.SetValues(.91f, .8f, -1, 1.7f, 5.3f, 7);
                var position = f.refine.throttleSlide.localPosition;
                var rotation = f.journey.throttleLever.localRotation;
                f.SeedWindowColor(Color.white); f.refine.ToggleReturn(); f.EmitCoachSparks();
                f.Tick();
                Check(f.refine.throttleSlide.localPosition == position && Same(f.journey.throttleLever.localRotation, rotation), "Cold Swan workshop does not position hidden driving handles", checks);
                Check(f.refine.throttleReadout.text == "seed" && f.journey.consoleSpeed.text == "seed", "Cold workshop does not format hidden instrument readouts", checks);
                Check(f.AllWindows(Color.black) && f.refine.WindowGlow == 0, "Cold workshop clears all 30 passenger-window emission blocks", checks);
                Check(!f.refine.cabLight.enabled && !f.headlamp.enabled && f.journey.furnaceLight.enabled, "Cold cleanup disables driving lights and preserves furnace lighting", checks);
                Check(!f.refine.ReturnOpen && Same(f.refine.returnCover.localRotation, Quaternion.identity) && f.coachSparks.particleCount == 0,
                    "Cold cleanup closes return cover and clears coach sparks", checks);

                var sentinel = new Color(.11f, .23f, .37f, 1);
                f.SeedWindowColor(sentinel);
                f.refine.throttleSlide.localPosition = new Vector3(22, 33, 44);
                f.refine.returnCover.localRotation = Quaternion.Euler(17, 18, 19);
                f.journey.throttleLever.localRotation = Quaternion.Euler(23, 34, 45);
                var hiddenRotation = f.journey.throttleLever.localRotation;
                var hiddenCover = f.refine.returnCover.localRotation;
                f.refine.cabLight.intensity = .73f;
                f.refine.throttleReadout.text = "hidden throttle sentinel";
                f.journey.consoleSpeed.text = "hidden speed sentinel";
                for (int i = 0; i < 24; i++) f.Tick();
                Check(f.AllWindows(sentinel) && f.refine.cabLight.intensity == .73f, "Stable hidden frames do not rewrite window MPBs or cab intensity", checks);
                Check(f.refine.throttleSlide.localPosition == new Vector3(22, 33, 44) && Same(f.journey.throttleLever.localRotation, hiddenRotation)
                    && Same(f.refine.returnCover.localRotation, hiddenCover), "Stable hidden frames do not rewrite driving transforms", checks);
                Check(f.refine.throttleReadout.text == "hidden throttle sentinel" && f.journey.consoleSpeed.text == "hidden speed sentinel",
                    "Stable hidden frames do not write text", checks);
                Check(f.journey.throttle == .91f && f.journey.brake == .8f && f.journey.speed == 1.7f && f.journey.pressure == 5.3f && f.journey.direction == -1,
                    "Display gating leaves simulation and control values unchanged", checks);

                f.journey.inSandbox = true; f.SetDaylight(.2f); f.Tick();
                Check(Mathf.Abs(f.refine.throttleSlide.localPosition.z - Mathf.Lerp(.56f, -.48f, .91f)) < .0001f
                    && Same(f.journey.throttleLever.localRotation, Quaternion.Euler(-45*.91f, 0, 0)), "Entering sandbox refreshes hidden handles on the first frame", checks);
                Check(f.refine.throttleReadout.text == "汽门  91 %" && f.refine.driveMessage.text == "后退   ·   制动   ·   第 8 圈"
                    && f.journey.consoleSpeed.text == (1.7f*12).ToString("00.0")+" km/h" && f.journey.consolePressure.text == 5.3f.ToString("0.0")+" bar",
                    "Entering sandbox displays the latest throttle, direction, lap, speed and pressure", checks);
                Check(f.refine.WindowGlow > .08f && f.refine.cabLight.enabled && f.headlamp.enabled && f.journey.furnaceLight.enabled,
                    "Entering sandbox restores current window and driving lighting without changing furnace light", checks);

                // A changed sentinel proves that equal displayed values avoid even a
                // redundant text assignment, rather than relying on TMP's own equality guard.
                f.refine.throttleReadout.text = "cached throttle"; f.refine.driveMessage.text = "cached message";
                f.journey.consoleSpeed.text = "cached speed"; f.journey.consolePressure.text = "cached pressure";
                f.Tick();
                Check(f.refine.throttleReadout.text == "cached throttle" && f.refine.driveMessage.text == "cached message"
                    && f.journey.consoleSpeed.text == "cached speed" && f.journey.consolePressure.text == "cached pressure",
                    "Unchanged visible display values avoid repeated text writes", checks);
                foreach (var window in f.refine.passengerWindows) window.SetPropertyBlock(null);
                f.Tick();
                Check(f.AllWindows(new Color(1,.60f,.25f)*f.refine.WindowGlow),
                    "Visible windows rewrite MPBs every frame after the native bridge consumes them", checks);
                f.SetValues(.36f, 0, 1, .5f, 6.4f, 9); f.Tick();
                Check(f.refine.throttleReadout.text == "汽门  36 %" && f.refine.driveMessage.text == "前进   ·   松开   ·   第 10 圈"
                    && f.journey.consoleSpeed.text == (.5f*12).ToString("00.0")+" km/h" && f.journey.consolePressure.text == 6.4f.ToString("0.0")+" bar",
                    "Changed visible values immediately invalidate text caches", checks);
                f.journey.pressure=1.24f; f.Tick(); string low=f.journey.consolePressure.text;
                f.journey.pressure=1.25f; f.Tick(); string half=f.journey.consolePressure.text;
                f.journey.pressure=1.26f; f.Tick(); string high=f.journey.consolePressure.text;
                f.journey.pressure=1.24f; f.Tick();
                Check(low==1.2f.ToString("0.0")+" bar" && half==low && high==1.3f.ToString("0.0")+" bar" && f.journey.consolePressure.text==low,
                    "Quantized display and cache agree around half-value boundaries and when returning",checks);

                f.refine.ToggleReturn(); f.EmitCoachSparks(); f.journey.inSandbox = false; f.Tick();
                Check(!f.refine.ReturnOpen && f.refine.WindowGlow == 0 && f.AllWindows(Color.black)
                    && !f.refine.cabLight.enabled && !f.headlamp.enabled && f.coachSparks.particleCount == 0,
                    "Leaving sandbox clears windows, lights, return state and coach particles once", checks);
                f.SeedWindowColor(sentinel); f.journey.consolePressure.text = "exit sentinel";
                for (int i = 0; i < 12; i++) f.Tick();
                Check(f.AllWindows(sentinel) && f.journey.consolePressure.text == "exit sentinel", "Later workshop frames do not repeat exit cleanup", checks);

                f.SetValues(.22f, 0, 1, .65f, 7.8f, 12); f.SetDaylight(1); f.journey.inSandbox = true; f.Tick();
                Check(f.refine.throttleReadout.text == "汽门  22 %" && f.refine.driveMessage.text == "前进   ·   松开   ·   第 13 圈"
                    && f.journey.consolePressure.text == 7.8f.ToString("0.0")+" bar" && f.refine.returnLabel.text == "掀盖 · 返回工坊",
                    "Reentering refreshes latest values and a closed return cover", checks);
                Check(Mathf.Abs(f.refine.WindowGlow - .08f) < .0001f && f.AllWindows(new Color(1,.60f,.25f)*.08f),
                    "Reentering refreshes current daylight rather than stale night emission", checks);
            }
            using (var f = new Fixture(false))
            {
                f.SetValues(.64f, .8f, -1, 1.1f, 4.2f, 3); f.refine.ToggleReturn(); f.Tick();
                Check(f.refine.throttleReadout.text == "汽门  64 %" && f.journey.consolePressure.text == 4.2f.ToString("0.0")+" bar"
                    && Same(f.journey.throttleLever.localRotation, Quaternion.Euler(-45*.64f, 0, 0)),
                    "Desktop workshop retains its original instrument and handle updates", checks);
                Check(f.refine.ReturnOpen && f.refine.returnLabel.text == "按下返回工坊" && f.refine.driveMessage.text == "",
                    "Desktop workshop retains original return-state and hidden-message semantics", checks);
                f.SetValues(.18f, 0, 1, 0, 3.7f, 4); f.Tick();
                Check(f.refine.throttleReadout.text == "汽门  18 %" && f.journey.consoleStatus.text == "山谷铁路 · 停车",
                    "Desktop updates continue on subsequent workshop frames", checks);
                f.journey.inSandbox = true; f.Tick();
                Check(f.refine.cabLight.enabled && f.headlamp.enabled && f.refine.driveMessage.text == "前进   ·   松开   ·   第 5 圈",
                    "Desktop driving entry remains compatible", checks);
            }
            return checks;
        }

        sealed class Fixture : IDisposable
        {
            readonly GameObject root;
            public readonly SteamJourney journey;
            public readonly StudioSpatialRefineV19 refine;
            public readonly ParticleSystem coachSparks;
            public readonly Light headlamp;
            readonly MesaDayNightV13 sky;
            public Fixture(bool swan)
            {
                root = new GameObject("Temporary driving display fixture") { hideFlags = HideFlags.HideAndDontSave };
                journey = root.AddComponent<SteamJourney>(); journey.SwanInputActive = swan;
                journey.trainAnchor = Child("Train"); journey.spatialRoot = root.transform;
                journey.throttleLever = Child("Throttle"); journey.brakeLever = Child("Brake"); journey.directionLever = Child("Direction");
                journey.pressureNeedle = Child("Pressure"); journey.speedNeedle = Child("Speed");
                journey.consoleSpeed = Text("Console speed"); journey.consolePressure = Text("Console pressure"); journey.consoleStatus = Text("Console status");
                Set(journey,"throttleRest",Quaternion.identity); Set(journey,"brakeRest",Quaternion.identity); Set(journey,"directionRest",Quaternion.identity);
                journey.throttleLever.localRotation = Quaternion.Euler(4,5,6);
                journey.furnaceLight = Light("Furnace"); journey.furnaceLight.enabled = true;
                headlamp = Light("Headlamp"); headlamp.gameObject.AddComponent<LocomotivePart>().partId = "P25-R"; headlamp.enabled = true;
                refine = root.AddComponent<StudioSpatialRefineV19>(); refine.journey = journey;
                sky = Child("Sky controller").gameObject.AddComponent<MesaDayNightV13>(); refine.sky = sky; SetDaylight(.2f);
                refine.throttleSlide = Child("Throttle slide"); refine.directionPivot = Child("Direction pivot"); refine.brakePivot = Child("Brake pivot");
                refine.returnCover = Child("Return cover"); refine.returnCover.localRotation = Quaternion.Euler(60,0,0); refine.brakeNeedle = Child("Brake needle");
                refine.throttleReadout = Text("Throttle text"); refine.driveMessage = Text("Drive text"); refine.returnLabel = Text("Return text");
                refine.passengerWindows = new Renderer[30];
                for (int i=0;i<30;i++) refine.passengerWindows[i] = Child("Window "+i).gameObject.AddComponent<MeshRenderer>();
                refine.cabLight = Light("Cab"); refine.cabLight.range = 4; refine.cabLight.enabled = true;
                refine.scenery = new[] { Child("Legacy decoration").gameObject };
                coachSparks = Child("Coach sparks").gameObject.AddComponent<ParticleSystem>();
                coachSparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=coachSparks.main; main.playOnAwake=false; main.maxParticles=16; main.startLifetime=10;
                var emission=coachSparks.emission; emission.enabled=false;
                Set(refine,"coachSparks",coachSparks); Invoke(refine,"Awake");
            }
            Transform Child(string name) { var go=new GameObject(name); go.transform.SetParent(root.transform,false); return go.transform; }
            TMP_Text Text(string name) { var text=Child(name).gameObject.AddComponent<TextMeshPro>(); text.text="seed"; return text; }
            Light Light(string name) { var go=new GameObject(name); go.transform.SetParent(journey.trainAnchor,false); return go.AddComponent<Light>(); }
            public void SetValues(float throttle,float brake,int direction,float speed,float pressure,int laps)
            { journey.throttle=throttle; journey.brake=brake; journey.direction=direction; journey.speed=speed; journey.pressure=pressure; journey.laps=laps; }
            public void SetDaylight(float value) => Set(sky,"<Daylight>k__BackingField",value);
            public void Tick() { Invoke(journey,"UpdateDrivingDisplay"); Invoke(refine,"LateUpdate"); }
            public void EmitCoachSparks() { coachSparks.Emit(3); if(coachSparks.particleCount==0) throw new InvalidOperationException("Fixture failed to seed coach sparks."); }
            public void SeedWindowColor(Color value) { var block=new MaterialPropertyBlock(); block.SetColor("_EmissionColor",value); foreach(var window in refine.passengerWindows)window.SetPropertyBlock(block); }
            public bool AllWindows(Color expected)
            {
                var block=new MaterialPropertyBlock();
                foreach(var window in refine.passengerWindows) { window.GetPropertyBlock(block); Color actual=block.GetColor("_EmissionColor");
                    if(Mathf.Abs(actual.r-expected.r)+Mathf.Abs(actual.g-expected.g)+Mathf.Abs(actual.b-expected.b)>.0001f)return false; }
                return true;
            }
            public void Dispose() { if(root)Object.DestroyImmediate(root); }
        }
        static bool Same(Quaternion a,Quaternion b) => Quaternion.Angle(a,b)<.001f;
        static void Check(bool condition,string message,List<string> checks)
        { if(!condition)throw new InvalidOperationException("Driving display check: "+message); checks.Add(message); }
        static void Set(object target,string name,object value)
        { var field=target.GetType().GetField(name,Private); if(field==null)throw new MissingFieldException(target.GetType().Name,name); field.SetValue(target,value); }
        static void Invoke(object target,string method)
        {
            var info=target.GetType().GetMethod(method,Private);
            if(info==null)throw new MissingMethodException(target.GetType().Name,method);
            try { info.Invoke(target,null); }
            catch(TargetInvocationException exception) { throw new InvalidOperationException("Fixture invocation failed: "+method,exception.InnerException??exception); }
        }
    }
}
