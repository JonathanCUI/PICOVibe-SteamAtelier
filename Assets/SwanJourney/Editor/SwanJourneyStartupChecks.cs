using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SwanJourney.Editor
{
    internal static class SwanJourneyStartupChecks
    {
        internal static void Run()
        {
            const string key = "SteamJourney.Progress.v1";
            bool hadSave = PlayerPrefs.HasKey(key);
            string oldSave = PlayerPrefs.GetString(key, "");
            var journey = UnityEngine.Object.FindObjectOfType<SteamJourney>(true);
            journey.suppressPersistence = true;
            try
            {
                // A completed previous session must not bypass the first step.
                string complete = "{\"stage\":7,\"finishIndex\":9,\"installed\":[true,true,true,true,true,true,true],"
                    + "\"verified\":[true,true,true,true,true,true,true],"
                    + "\"finished\":[true,true,true,true,true,true,true,true,true,true]}";
                PlayerPrefs.SetString(key, complete);
                journey.stage = 7; journey.finishIndex = 9;
                foreach (var m in journey.modules.Concat(journey.finishing))
                { m.installed = m.verified = m.parked = true; m.observed = 10; }
                Invoke(journey, "Start");
                Require(journey.stage == 0 && journey.finishIndex == 0 && !journey.Complete && !journey.inSandbox,
                    "Fresh Swan startup must return to the first workshop step.");
                Require(journey.modules.Concat(journey.finishing).All(m => !m.installed && !m.verified && !m.parked && m.observed == 0),
                    "Fresh startup must clear all prior assembly and verification flags.");
                Require(journey.Current == journey.modules[0] && journey.Current.preview.activeInHierarchy
                    && Vector3.Distance(journey.Current.preview.transform.position,
                        journey.previewStand.position + journey.SceneVectorToWorld(journey.Current.previewOffset)) < .0001f,
                    "First part must appear on the relocated preview stand.");
                Require(journey.InstallCurrent(), "First part must remain installable after startup reset.");
                Invoke(journey, "OnApplicationPause", true);
                Invoke(journey, "OnApplicationPause", false);
                journey.SetMode(false);
                Require(journey.modules[0].installed && journey.stage == 1,
                    "Resuming or returning to the workshop must retain the current session's progress.");
                CheckVerificationStart(journey);
                Debug.Log("SWAN_JOURNEY_STARTUP_CHECKS_PASS freshStep=1 installed=0 resumeRetainsProgress=true");
            }
            finally
            {
                if (hadSave) PlayerPrefs.SetString(key, oldSave); else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }
        static void CheckVerificationStart(SteamJourney journey)
        {
            const float frame = 1f / 90f;
            Require(journey.SwanAct(JourneyAction.Verify) && journey.FireLevel > 0 && journey.SteamPressure == 0
                && journey.WheelPower == 0, "A firebox alone starts burning without powering uninstalled mechanisms.");
            journey.SwanAct(JourneyAction.Verify);
            journey.ResetProcess();
            for (int i = 1; i < 5; i++) Require(journey.InstallCurrent(), "Install through the cylinder bench.");
            journey.SwanAct(JourneyAction.Verify);
            Invoke(journey, "UpdateMechanicalPose");
            var side = journey.engine.sides[0];
            var piston = side.piston.localPosition;
            journey.StepProcess(frame);
            Invoke(journey, "UpdateMechanicalPose");
            Require(side.piston.localPosition != piston && journey.WheelPower == 0,
                "An installed bench piston moves on the first tick before the wheelset is fitted.");
            Require(journey.InstallCurrent() && journey.WheelPower > 0,
                "Fitting the wheelset while verification is on immediately couples it to the cylinders.");
            Require(journey.InstallCurrent(), "Install the compressor and brakes.");
            journey.SwanAct(JourneyAction.Verify);
            journey.ResetProcess();
            journey.engine.phase = 45f;
            Invoke(journey, "UpdateMechanicalPose");
            var wheel = side.wheels[0].localRotation;
            piston = side.piston.localPosition;
            var rod = side.mainRod.localPosition;
            journey.SwanAct(JourneyAction.Verify);
            Require(journey.ChimneyExhaustPower > 0 && journey.CompressorRate > 0 && journey.ProcessReached(3),
                "Chimney, steam delivery and compressor have supply at the instant Verify starts.");
            journey.StepProcess(frame);
            Invoke(journey, "UpdateMechanicalPose");
            Require(Quaternion.Angle(wheel, side.wheels[0].localRotation) > .1f && piston != side.piston.localPosition
                && rod != side.mainRod.localPosition && journey.ProcessTime <= frame,
                "Wheels, piston and main rod all change pose in one tick without advancing the lesson clock ahead.");
            Require(journey.modules.All(m => !m.verified && m.observed == 0), "Starting motion does not award observation credit.");
            journey.SwanAct(JourneyAction.Verify);
            Require(journey.DetachModule(journey.modules[3]), "Disconnect the steam delivery pipe.");
            journey.SwanAct(JourneyAction.Verify);
            Require(journey.Delivery == 0 && journey.WheelPower == 0 && journey.CompressorRate == 0,
                "Restarting verification with a disconnected supply cannot energize downstream mechanisms.");
            Debug.Log("SWAN_JOURNEY_VERIFY_START_PASS partial=true fullFirstTick=true disconnected=true");
        }
        static void Invoke(SteamJourney journey, string method, params object[] arguments) =>
            typeof(SteamJourney).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(journey, arguments);
        static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
