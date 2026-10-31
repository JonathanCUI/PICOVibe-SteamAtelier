using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SwanAtelier;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace SwanJourney.Editor
{
    // Exercises actual application code in Edit Mode; no P2D, save or native claim.
    public static class SwanJourneyQuickInstallChecks
    {
        [Serializable] sealed class Report
        {
            public bool passed, nativeGesturesValidated;
            public string scope = "EditMode queued gestures, flight and original V23 installation state machine";
            public List<string> checks = new List<string>();
            public string failure;
        }
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required");
            var report = new Report();
            SwanJourneyInteraction input = null;
            try
            {
                EditorSceneManager.OpenScene(SwanJourneyBuild.ScenePath);
                input = UnityEngine.Object.FindObjectOfType<SwanJourneyInteraction>(true);
                var journey = input.journey;
                journey.suppressPersistence = true;
                Invoke(input, "Awake"); Invoke(input, "Start");
                Check(input.Ready && journey.SwanInputActive, "Actual V23 input initialized");
                double now = 1000;
                var first = journey.modules[0]; var second = journey.modules[1];
                Reset();
                var trayPose = first.preview.transform.position;
                Tap(first);
                Check(!first.installed && !input.QuickReturning && journey.SwanHeldModule == null,
                    "First tray pinch only arms double pinch");
                Check(Vector3.Distance(first.preview.transform.position, trayPose) < .00001f && !first.parked,
                    "First short pinch leaves untouched tray part in place");
                Tap(first);
                Check(input.QuickReturning && !first.installed, "Second tray pinch starts flight without preliminary pickup");
                Finish(first);
                Check(journey.stage == 1 && journey.Current == second && !second.installed,
                    "Quick installation advances the original teaching order exactly once");

                Reset(); Park(first);
                Tap(first, rayDrift:true, blankRelease:true, handTravel:.03f);
                Tap(first, rayDrift:true, blankRelease:true, handTravel:.03f);
                Check(input.QuickReturning, "Gaze drift and zero release pose do not reject stable hand double pinch");
                Finish(first);
                Reset(); Park(first);
                Tap(first, duration:.34); now += .32; Tap(first, duration:.34);
                Check(input.QuickReturning, "Deliberate short pinches fit relaxed duration and gap");
                Finish(first);

                Reset(); Park(first);
                first.preview.transform.position = journey.trainAnchor.TransformPoint(first.previewCentre);
                Tap(first);
                Check(!first.installed && !input.QuickReturning, "First short pinch near socket cannot silently snap-install");
                Tap(first); Finish(first);
                Reset(); Park(second); Tap(second); Tap(second);
                Check(!second.installed && !input.QuickReturning && journey.stage == 0,
                    "Later module cannot bypass required teaching order");
                Reset(); Park(first); Park(second); Tap(first); Tap(second); Tap(first);
                Check(!input.QuickReturning, "Pinches on different parts cannot combine");
                Reset(); Park(first); Tap(first); now += .6; Tap(first);
                Check(!input.QuickReturning, "Expired first pinch cannot trigger installation");
                Reset(); Park(first); Tap(first); Tap(first, canceled:true); Tap(first);
                Check(!input.QuickReturning && !first.installed, "Canceled gesture clears previous pinch");
                Reset(); Park(first); Tap(first, handTravel:.12f); Tap(first);
                Check(!input.QuickReturning && !first.installed, "Real dragging is not counted as a short pinch");
                Reset(); Park(first); Tap(first, duration:.60); Tap(first);
                Check(!input.QuickReturning, "Long grab cannot be first half of double pinch");

                Reset(); Park(first); Tap(first);
                var hit = first.preview.transform.position; var hand = hit + Vector3.back * .35f;
                Send(8201, Target(first), TouchPhase.Began, hit, hand);
                now += .02; input.ProcessGestureFrame(now, .001f);
                Send(8201, Target(first), TouchPhase.Moved, hit, Vector3.zero);
                now += .05; input.ProcessGestureFrame(now, .001f); Tap(first);
                Check(!input.QuickReturning && journey.SwanHeldModule == null,
                    "Lost hand tracking cancels instead of falling back to gaze movement");

                Reset(); Park(first); Tap(first);
                hit = first.preview.transform.position; hand = hit + Vector3.back * .35f;
                Send(8301, Target(first), TouchPhase.Began, hit, hand);
                Send(8302, null, TouchPhase.Began, hand + Vector3.right * .4f, hand + Vector3.right * .4f);
                now += .03; input.ProcessGestureFrame(now, .01f);
                Check(input.TwoHandActive && !input.QuickReturning && journey.SwanHeldModule == null,
                    "Two hands take ownership and clear pending double pinch");
                var rootPosition = input.workbenchRoot.position;
                for (int i = 0; i < 30; i++)
                {
                    Send(8302, null, TouchPhase.Moved, Vector3.zero, hand + Vector3.right * .48f);
                    Send(8301, Target(first), TouchPhase.Moved, hit, hand + Vector3.right * .08f);
                    now += 1.0 / 90; input.ProcessGestureFrame(now, 1f / 90);
                }
                Check(Vector3.Distance(rootPosition, input.workbenchRoot.position) > .04f,
                    "Two-hand movement survives swapped event order");
                input.ResetSpatialGestures(); Tap(first);
                Check(!input.QuickReturning && !first.installed, "Two-hand release cannot leak pending installation");
                Reset(); Park(first); journey.verificationInputLock = true; Tap(first); Tap(first);
                Check(!input.QuickReturning && !first.installed, "Verification lock blocks quick installation");
                journey.verificationInputLock = false;
                Reset(); Park(first); journey.SwanSceneLoading = true; Tap(first); Tap(first);
                Check(!input.QuickReturning && !first.installed, "Scene loading blocks quick installation");
                journey.SwanSceneLoading = false;
                Reset(); Park(first); Tap(first); Tap(first);
                input.TickQuickReturn(.07f); input.ResetSpatialGestures();
                Check(!input.QuickReturning && !first.installed && first.parked && journey.SwanHeldModule == null,
                    "Pause/reset during flight preserves loose part without installation");
                report.passed = true;
                Debug.Log("SWAN_JOURNEY_QUICK_INSTALL_CHECKS_PASS checks=" + report.checks.Count + " nativeGesturesValidated=false");

                void Reset()
                {
                    input.ResetSpatialGestures(); now += 2;
                    journey.stage = journey.finishIndex = 0;
                    journey.inSandbox = journey.SwanSceneLoading = journey.verificationInputLock = false;
                    foreach (var m in journey.modules.Concat(journey.finishing))
                    { m.installed = m.parked = m.verified = false; m.observed = 0; }
                    journey.RefreshPreview(); journey.ApplyVisibility();
                }
                void Park(JourneyModule m)
                {
                    m.parked = true;
                    m.preview.transform.position = journey.trainAnchor.TransformPoint(m.previewCentre) + Vector3.up * .4f + Vector3.right * .5f;
                    journey.RefreshPreview();
                }
                void Tap(JourneyModule m, double duration = .08, bool canceled = false,
                    bool rayDrift = false, bool blankRelease = false, float handTravel = 0)
                {
                    Vector3 p = m.preview.transform.position, h = p + Vector3.back * .35f;
                    var target = Target(m);
                    Send(8101, target, TouchPhase.Began, p, h); input.ProcessGestureFrame(now, .001f);
                    now += duration * .6;
                    Send(8101, target, TouchPhase.Moved, p + (rayDrift ? Vector3.right * .25f : Vector3.zero), h + Vector3.up * handTravel);
                    input.ProcessGestureFrame(now, .001f);
                    now += duration * .4;
                    Send(8101, blankRelease ? null : target, canceled ? TouchPhase.Canceled : TouchPhase.Ended,
                        blankRelease ? Vector3.zero : p, blankRelease ? Vector3.zero : h + Vector3.up * handTravel);
                    input.ProcessGestureFrame(now, .001f); now += .1;
                }
                void Finish(JourneyModule m)
                {
                    int before = input.QuickReturnCount; input.TickQuickReturn(5);
                    Check(input.QuickReturning && !m.installed, "Stalled frame cannot skip quick flight");
                    for (int i = 0; i < 30; i++) input.TickQuickReturn(1f / 90);
                    Check(m.installed && !input.QuickReturning && input.QuickReturnCount == before + 1,
                        "Flight ends in original socket and installs once");
                }
                void Send(long id, GameObject target, TouchPhase phase, Vector3 hitPosition, Vector3 handPosition)
                    => input.QueueGestureSample(new SwanGestureSample { id=id, target=target, phase=phase,
                        pinch=true, hitPosition=hitPosition, handPosition=handPosition });
            }
            catch (Exception e) { report.failure = e.ToString(); throw; }
            finally
            {
                if (input) input.ResetSpatialGestures();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.OpenScene(SwanJourneyBuild.ScenePath);
                Directory.CreateDirectory(SwanJourneyBuild.Output);
                File.WriteAllText(SwanJourneyBuild.Output + "/quick-install-checks.json", JsonUtility.ToJson(report, true));
            }
            void Check(bool condition, string message)
            { if (!condition) throw new InvalidOperationException(message); report.checks.Add(message); }
        }
        static GameObject Target(JourneyModule module) => module.preview.GetComponentsInChildren<Collider>(true)
            .First(c => c.enabled && c.gameObject.activeInHierarchy).gameObject;
        static void Invoke(object target, string method) => target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
