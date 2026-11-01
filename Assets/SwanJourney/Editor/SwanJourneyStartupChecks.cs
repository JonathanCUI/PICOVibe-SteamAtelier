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
                Debug.Log("SWAN_JOURNEY_STARTUP_CHECKS_PASS freshStep=1 installed=0 resumeRetainsProgress=true");
            }
            finally
            {
                if (hadSave) PlayerPrefs.SetString(key, oldSave); else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }
        static void Invoke(SteamJourney journey, string method, params object[] arguments) =>
            typeof(SteamJourney).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(journey, arguments);
        static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
